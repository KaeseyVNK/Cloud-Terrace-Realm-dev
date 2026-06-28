using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý vòng đời spawn động vật hoang dã trên bản đồ.
/// Tự động dò tìm khu vực rừng (gần mỏ Gỗ) để sinh động vật.
/// </summary>
public class WildlifeManager : MonoBehaviour
{
    public static WildlifeManager Instance { get; private set; }

    [Header("Prefabs")]
    [Tooltip("Prefab của Gà")]
    [SerializeField] private GameObject _chickenPrefab;

    [Tooltip("Prefab của Hươu")]
    [SerializeField] private GameObject _deerPrefab;

    [Tooltip("Prefab mỏ thức ăn sinh ra khi thú chết")]
    [SerializeField] private GameObject _foodPrefab;

    [Header("Giới Hạn Số Lượng")]
    [Tooltip("Số lượng Gà tối đa")]
    [SerializeField] private int _maxChickens = 16;

    [Tooltip("Số lượng Hươu tối đa")]
    [SerializeField] private int _maxDeers = 12;

    [Tooltip("Tần suất quét spawn (giây)")]
    [SerializeField] private float _spawnInterval = 10f;

    private readonly List<WildAnimalController> _activeAnimals = new List<WildAnimalController>();

    public GameObject FoodPrefab => _foodPrefab;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetPrefabs(GameObject chicken, GameObject deer, GameObject food)
    {
        _chickenPrefab = chicken;
        _deerPrefab = deer;
        _foodPrefab = food;
    }

    private void Start()
    {
        // Tự động tìm nạp các prefab từ Resources nếu chưa gán trong Inspector để chạy mượt
        EnsurePrefabsLoaded();

        // Spawn khởi đầu ngay khi vào game
        SpawnInitialWildlife();

        // Chạy Coroutine spawn bù định kỳ
        StartCoroutine(SpawnRoutine());
    }

    private void EnsurePrefabsLoaded()
    {
        if (_chickenPrefab == null)
        {
            _chickenPrefab = Resources.Load<GameObject>("Chicken_001") ?? 
                             Resources.Load<GameObject>("Prefabs/Chicken_001");
        }
        if (_deerPrefab == null)
        {
            _deerPrefab = Resources.Load<GameObject>("Deer_001") ?? 
                           Resources.Load<GameObject>("Prefabs/Deer_001");
        }
        if (_foodPrefab == null)
        {
            _foodPrefab = Resources.Load<GameObject>("Food") ?? 
                          Resources.Load<GameObject>("Prefabs/ResourceCaple/Food");
        }
    }

    /// <summary>
    /// Đăng ký động vật mới vào danh sách quản lý.
    /// </summary>
    public void RegisterAnimal(WildAnimalController animal)
    {
        if (animal != null && !_activeAnimals.Contains(animal))
        {
            _activeAnimals.Add(animal);
        }
    }

    /// <summary>
    /// Loại bỏ động vật đã chết khỏi danh sách quản lý.
    /// </summary>
    public void RegisterDeath(WildAnimalController animal)
    {
        if (animal != null)
        {
            _activeAnimals.Remove(animal);
        }
    }

    /// <summary>
    /// Sinh toàn bộ thú hoang dã ban đầu khi vào game theo bầy đàn.
    /// </summary>
    private void SpawnInitialWildlife()
    {
        int chickensToSpawn = _maxChickens;
        int deersToSpawn = _maxDeers;

        // Tìm vị trí Nhà Chính (Main House) để spawn bầy đầu tiên ở gần
        Vector3? mainHousePos = null;
        MainBuildingCombatTarget mainHouse = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainHouse != null)
        {
            mainHousePos = mainHouse.transform.position;
        }

        // Bầy gà đầu tiên sẽ được ưu tiên spawn gần nhà chính (cách khoảng 12m - 20m) để người chơi dễ tiếp cận
        if (mainHousePos.HasValue && chickensToSpawn >= 4)
        {
            Vector3? nearMainHousePos = FindSpawnPositionNear(mainHousePos.Value, 12f, 20f);
            if (nearMainHousePos.HasValue)
            {
                SpawnAnimalPackAtPosition(_chickenPrefab, 50, "Chicken_001", 4, nearMainHousePos.Value);
                chickensToSpawn -= 4;
            }
        }

        // Sinh các bầy gà còn lại ngẫu nhiên trong rừng
        int chickenPacks = Mathf.Max(0, chickensToSpawn / 4);
        for (int i = 0; i < chickenPacks; i++)
        {
            SpawnAnimalPack(_chickenPrefab, 50, "Chicken_001", 4);
        }

        // Sinh hươu theo bầy 6 con ngẫu nhiên trong rừng
        int deerPacks = Mathf.Max(1, deersToSpawn / 6);
        for (int i = 0; i < deerPacks; i++)
        {
            SpawnAnimalPack(_deerPrefab, 150, "Deer_001", 6);
        }
    }

    /// <summary>
    /// Routine quét và spawn bù định kỳ.
    /// </summary>
    private IEnumerator SpawnRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_spawnInterval);

            int currentChickens = 0;
            int currentDeers = 0;

            for (int i = 0; i < _activeAnimals.Count; i++)
            {
                var animal = _activeAnimals[i];
                if (animal == null || animal.currentState == CombatState.Dead) continue;

                if (animal.unitName.Contains("Chicken") || animal.FoodAmount == 50)
                {
                    currentChickens++;
                }
                else if (animal.unitName.Contains("Deer") || animal.FoodAmount == 150)
                {
                    currentDeers++;
                }
            }

            // Spawn bù gà nếu thiếu (theo bầy hoặc con lẻ nếu gần đầy)
            if (currentChickens < _maxChickens)
            {
                int diff = _maxChickens - currentChickens;
                int packSize = Mathf.Min(4, diff);
                SpawnAnimalPack(_chickenPrefab, 50, "Chicken_001", packSize);
            }

            // Spawn bù hươu nếu thiếu (theo bầy hoặc con lẻ nếu gần đầy)
            if (currentDeers < _maxDeers)
            {
                int diff = _maxDeers - currentDeers;
                int packSize = Mathf.Min(6, diff);
                SpawnAnimalPack(_deerPrefab, 150, "Deer_001", packSize);
            }
        }
    }

    /// <summary>
    /// Thử tìm một vị trí spawn hợp lệ trong khoảng bán kính nhất định quanh một điểm.
    /// </summary>
    private Vector3? FindSpawnPositionNear(Vector3 center, float minRadius, float maxRadius)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return null;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float dist = Random.Range(minRadius, maxRadius);
            Vector3 candidatePos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
            candidatePos.y = terrain.SampleHeight(candidatePos) + terrain.transform.position.y;

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit navHit, 5f, ~2))
            {
                // Tránh spawn đè vật cản hoặc công trình
                Collider[] blockCheck = Physics.OverlapSphere(navHit.position, 1.0f);
                bool isBlocked = false;
                foreach (var col in blockCheck)
                {
                    if (col.GetComponentInParent<BaseCombatUnitController>() != null ||
                        col.GetComponentInParent<ConstructibleBuilding>() != null ||
                        col.GetComponentInParent<ResourceNode>() != null)
                    {
                        isBlocked = true;
                        break;
                    }
                }

                if (!isBlocked)
                {
                    return navHit.position;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Thử sinh một bầy động vật tại một khu rừng ngẫu nhiên.
    /// </summary>
    private void SpawnAnimalPack(GameObject prefab, int foodAmount, string defaultName, int packSize)
    {
        if (prefab == null || packSize <= 0) return;

        Vector3? centerPos = FindForestSpawnPosition();
        if (centerPos.HasValue)
        {
            SpawnAnimalPackAtPosition(prefab, foodAmount, defaultName, packSize, centerPos.Value);
        }
    }

    /// <summary>
    /// Sinh một bầy động vật tại tọa độ tâm cho trước.
    /// </summary>
    private void SpawnAnimalPackAtPosition(GameObject prefab, int foodAmount, string defaultName, int packSize, Vector3 centerPos)
    {
        if (prefab == null || packSize <= 0) return;

        int spawnedThisPack = 0;
        // Thử tối đa packSize * 2 lần để tìm điểm trống xung quanh điểm tâm
        for (int i = 0; i < packSize * 2 && spawnedThisPack < packSize; i++)
        {
            // Thêm offset ngẫu nhiên quanh điểm tâm
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(1.2f, 3.5f); // Bầy nhỏ gọn và đẹp
            Vector3 candidatePos = centerPos + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

            // Lấy độ cao địa hình
            if (Terrain.activeTerrain != null)
            {
                candidatePos.y = Terrain.activeTerrain.SampleHeight(candidatePos) + Terrain.activeTerrain.transform.position.y;
            }

            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit navHit, 4f, ~2))
            {
                // Tránh trùng lắp lên các vật cản khác
                Collider[] blockCheck = Physics.OverlapSphere(navHit.position, 0.8f);
                bool isBlocked = false;
                foreach (var col in blockCheck)
                {
                    if (col.GetComponentInParent<BaseCombatUnitController>() != null ||
                        col.GetComponentInParent<ConstructibleBuilding>() != null ||
                        col.GetComponentInParent<ResourceNode>() != null)
                    {
                        isBlocked = true;
                        break;
                    }
                }

                if (!isBlocked)
                {
                    GameObject obj = PoolManager.Instance.Spawn(prefab, navHit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    if (obj != null)
                    {
                        GameObject container = GameObject.Find("Wildlife");
                        if (container == null)
                        {
                            container = new GameObject("Wildlife");
                        }
                        obj.transform.SetParent(container.transform);

                        obj.name = $"{defaultName}_Spawned_{System.Guid.NewGuid().ToString().Substring(0, 4)}";

                        WildAnimalController controller = obj.GetComponent<WildAnimalController>();
                        if (controller == null)
                        {
                            controller = obj.AddComponent<WildAnimalController>();
                        }

                        controller.unitName = defaultName;
                        controller.FoodAmount = foodAmount;

                        RegisterAnimal(controller);
                        spawnedThisPack++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Dò tìm khu rừng ngẫu nhiên dựa vào sự hiện diện của mỏ Gỗ (cây xanh).
    /// </summary>
    private Vector3? FindForestSpawnPosition()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return null;

        float terrainWidth = terrain.terrainData.size.x;
        float terrainLength = terrain.terrainData.size.z;

        // Thử tối đa 30 lần để tìm điểm gần rừng cây
        for (int attempt = 0; attempt < 30; attempt++)
        {
            float rx = Random.Range(20f, terrainWidth - 20f);
            float rz = Random.Range(20f, terrainLength - 20f);
            Vector3 candidatePos = new Vector3(rx, 0f, rz);
            candidatePos.y = terrain.SampleHeight(candidatePos) + terrain.transform.position.y;

            // Kiểm tra NavMesh gần ứng viên
            if (NavMesh.SamplePosition(candidatePos, out NavMeshHit navHit, 6f, ~2))
            {
                // Kiểm tra xem vị trí có gần mỏ gỗ (cây) không
                Collider[] colliders = Physics.OverlapSphere(navHit.position, 20f);
                bool nearForestTree = false;

                foreach (var col in colliders)
                {
                    ResourceNode node = col.GetComponentInParent<ResourceNode>();
                    if (node != null && node.ResourceType == ResourceType.Wood)
                    {
                        nearForestTree = true;
                        break;
                    }
                }

                if (nearForestTree)
                {
                    // Tránh spawn đè lên công trình hoặc các unit khác
                    Collider[] blockCheck = Physics.OverlapSphere(navHit.position, 1.2f);
                    bool isBlocked = false;
                    foreach (var col in blockCheck)
                    {
                        if (col.GetComponentInParent<BaseCombatUnitController>() != null ||
                            col.GetComponentInParent<ConstructibleBuilding>() != null ||
                            col.GetComponentInParent<ResourceNode>() != null)
                        {
                            isBlocked = true;
                            break;
                        }
                    }

                    if (!isBlocked)
                    {
                        return navHit.position;
                    }
                }
            }
        }

        return null;
    }
}
