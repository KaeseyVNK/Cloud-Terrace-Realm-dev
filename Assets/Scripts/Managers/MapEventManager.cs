using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Quản lý việc tự động sinh các Sự kiện ngẫu nhiên trên bản đồ (Cổng Hư Không và Hộ Tống Thương Nhân)
/// nhằm gia tăng tính chất Roguelike cho game.
/// </summary>
public class MapEventManager : MonoBehaviour
{
    private static MapEventManager _instance;
    public static MapEventManager Instance => _instance;

    [Header("Event Prefabs")]
    [Tooltip("Prefab Cổng Hư Không")]
    [SerializeField] private GameObject _voidPortalPrefab;
    [Tooltip("Prefab Ngựa thồ Thương nhân")]
    [SerializeField] private GameObject _merchantCaravanPrefab;

    [Header("Settings")]
    [Tooltip("Tần suất quét sinh sự kiện (số ngày)")]
    [SerializeField] private int _spawnIntervalDays = 2;
    [Tooltip("Số lượng sự kiện tối đa đồng thời trên bản đồ")]
    [SerializeField] private int _maxActiveEvents = 3;
    [Tooltip("Khoảng cách tối thiểu từ sự kiện tới Nhà Chính (số ô lưới)")]
    [SerializeField] private int _minDistanceFromCenter = 15;

    private GridSystem _gridSystem;
    private int _daysSinceLastEvent = 0;

    // Danh sách lưu trữ các sự kiện đang hoạt động
    private readonly List<GameObject> _activeEvents = new List<GameObject>();
    private readonly List<VoidPortal> _activePortals = new List<VoidPortal>();
    private readonly List<MerchantCaravanGroup> _activeCaravans = new List<MerchantCaravanGroup>();

    // Trình quản lý tiến độ đoàn xe thồ
    private class MerchantCaravanGroup
    {
        public List<MerchantCaravanUnit> units = new List<MerchantCaravanUnit>();
        public Vector3 startPos;
        public Vector3 destPos;
        public int size; // 1 = Small, 2 = Medium, 3 = Large
        public bool isFinished = false;
        
        // Quản lý các mốc phục kích (ambush check)
        public bool ambush30Triggered = false;
        public bool ambush60Triggered = false;
        public bool ambush90Triggered = false;

        public Vector3 GetAveragePosition()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].currentState != CombatState.Dead)
                {
                    sum += units[i].transform.position;
                    count++;
                }
            }
            return count > 0 ? sum / count : Vector3.zero;
        }

        public bool IsAlive()
        {
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] != null && units[i].currentState != CombatState.Dead)
                {
                    return true;
                }
            }
            return false;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }

        // Đăng ký nhận sự kiện qua ngày mới từ TimeManager
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
        }

        // Bắt đầu chu kỳ sinh quái tấn công từ Cổng Hư Không và kiểm tra phục kích đoàn xe
        StartCoroutine(EventUpdateCycleRoutine());
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
        }
    }

    public void SetupPrefabs(GameObject voidPortal, GameObject merchantCaravan)
    {
        _voidPortalPrefab = voidPortal;
        _merchantCaravanPrefab = merchantCaravan;
    }

    private void HandleDayChanged(int dayCount)
    {
        _daysSinceLastEvent++;

        if (_daysSinceLastEvent >= _spawnIntervalDays)
        {
            _daysSinceLastEvent = 0;

            if (_activeEvents.Count < _maxActiveEvents)
            {
                // Chọn ngẫu nhiên loại sự kiện: 0 = Cổng Hư Không, 1 = Hộ Tống Thương Nhân
                int eventType = Random.Range(0, 2);
                if (eventType == 0)
                {
                    SpawnVoidPortalEvent();
                }
                else
                {
                    SpawnMerchantEscortEvent();
                }
            }
        }
    }

    /// <summary>
    /// Chu kỳ cập nhật sự kiện định kỳ (quét phục kích và sinh lính tấn công từ Cổng)
    /// </summary>
    private IEnumerator EventUpdateCycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(3.0f);

            // 1. Quản lý lính gác tấn công từ các Cổng Hư Không (Mỗi 30s sinh 2 Skeleton và đi công nhà chính)
            UpdatePortalSpawns();

            // 2. Quản lý đoàn thương nhân di chuyển và phục kích
            UpdateCaravansProgress();
        }
    }

    private float _nextPortalSpawnTime = 0f;
    private void UpdatePortalSpawns()
    {
        if (Time.time < _nextPortalSpawnTime) return;
        _nextPortalSpawnTime = Time.time + 35f;

        // Dọn dẹp các cổng đã chết
        _activePortals.RemoveAll(p => p == null || p.currentState == CombatState.Dead);

        if (_activePortals.Count == 0) return;

        var mainHouse = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainHouse == null) return;

        foreach (var portal in _activePortals)
        {
            if (portal == null || portal.currentState == CombatState.Dead) continue;

            // Spawn 2 Skeleton Warriors tại cổng hư không để hành quân về Nhà Chính
            GameObject skeletonPrefab = null;
            if (EnemyManager.Instance != null)
            {
                skeletonPrefab = EnemyManager.Instance.GetEnemyPrefabByName("Skeleton_Warrior");
            }

            if (skeletonPrefab != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    Vector2 randOffset = Random.insideUnitCircle * 2f;
                    Vector3 spawnPos = portal.transform.position + new Vector3(randOffset.x, 0f, randOffset.y);
                    if (Terrain.activeTerrain != null)
                    {
                        spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
                    }

                    GameObject enemyObj = Instantiate(skeletonPrefab, spawnPos, Quaternion.identity);
                    
                    // Gán lệnh đi công nhà chính
                    NavMeshAgent agent = enemyObj.GetComponent<NavMeshAgent>();
                    if (agent != null)
                    {
                        agent.enabled = true;
                        agent.SetDestination(mainHouse.transform.position);
                    }
                }
            }
        }
    }

    private void UpdateCaravansProgress()
    {
        // Loại bỏ các đoàn xe đã bị hủy hoàn toàn hoặc hoàn thành
        _activeCaravans.RemoveAll(c => c == null || c.isFinished);

        for (int i = 0; i < _activeCaravans.Count; i++)
        {
            var caravan = _activeCaravans[i];
            if (caravan == null || caravan.isFinished) continue;

            // Kiểm tra xem đoàn xe còn sống không
            if (!caravan.IsAlive())
            {
                // Thất bại!
                OnCaravanFailed(null); // Sẽ xử lý dọn dẹp đoàn xe
                caravan.isFinished = true;
                continue;
            }

            // Tính tiến độ di chuyển dựa trên khoảng cách
            Vector3 avgPos = caravan.GetAveragePosition();
            float totalDist = Vector3.Distance(caravan.startPos, caravan.destPos);
            float currentDistFromStart = Vector3.Distance(avgPos, caravan.startPos);
            float progressRatio = Mathf.Clamp01(currentDistFromStart / totalDist);

            // Kiểm tra mốc phục kích
            if (progressRatio >= 0.3f && !caravan.ambush30Triggered)
            {
                caravan.ambush30Triggered = true;
                TriggerCaravanAmbush(caravan, 3);
            }
            else if (progressRatio >= 0.6f && !caravan.ambush60Triggered)
            {
                caravan.ambush60Triggered = true;
                TriggerCaravanAmbush(caravan, 4);
            }
            else if (progressRatio >= 0.85f && !caravan.ambush90Triggered)
            {
                caravan.ambush90Triggered = true;
                TriggerCaravanAmbush(caravan, caravan.size == 3 ? 5 : 4);
            }
        }
    }

    private void TriggerCaravanAmbush(MerchantCaravanGroup caravanGroup, int count)
    {
        Vector3 caravanPos = caravanGroup.GetAveragePosition();

        GameObject warriorPrefab = null;
        GameObject archerPrefab = null;
        if (EnemyManager.Instance != null)
        {
            warriorPrefab = EnemyManager.Instance.GetEnemyPrefabByName("Skeleton_Warrior");
            archerPrefab = EnemyManager.Instance.GetEnemyPrefabByName("EnemyArcher");
        }

        if (warriorPrefab == null) return;

        MerchantCaravanUnit targetUnit = null;
        for (int j = 0; j < caravanGroup.units.Count; j++)
        {
            if (caravanGroup.units[j] != null && caravanGroup.units[j].currentState != CombatState.Dead)
            {
                targetUnit = caravanGroup.units[j];
                break;
            }
        }

        Debug.Log($"[MapEventManager] Đoàn thương nhân bị phục kích! Đang sinh {count} quái gác tấn công.");

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                "ĐOÀN THƯƠNG NHÂN BỊ PHỤC KÍCH",
                "Quái vật đang xuất hiện để tấn công đoàn xe thồ thương nhân! Hãy bảo vệ họ! ⚔️",
                4f
            );
        }

        for (int i = 0; i < count; i++)
        {
            // Sinh quái cách đoàn xe khoảng 9-12m trong sương mù
            Vector2 randDir = Random.insideUnitCircle.normalized * Random.Range(9f, 12f);
            Vector3 spawnPos = caravanPos + new Vector3(randDir.x, 0f, randDir.y);
            if (Terrain.activeTerrain != null)
            {
                spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
            }

            GameObject prefab = (i % 3 == 0 && archerPrefab != null) ? archerPrefab : warriorPrefab;
            GameObject enemyObj = Instantiate(prefab, spawnPos, Quaternion.identity);

            // Gán lệnh đi tấn công đoàn xe
            var enemyController = enemyObj.GetComponent<BaseCombatUnitController>();
            if (enemyController != null && targetUnit != null)
            {
                enemyController.CommandAttack(targetUnit);
            }
            else
            {
                NavMeshAgent agent = enemyObj.GetComponent<NavMeshAgent>();
                if (agent != null)
                {
                    agent.enabled = true;
                    agent.SetDestination(caravanPos);
                }
            }
        }
    }

    /// <summary>
    /// Spawn sự kiện Cổng Hư Không
    /// </summary>
    private void SpawnVoidPortalEvent()
    {
        if (_voidPortalPrefab == null) return;

        if (FindValidSpawnPosition(out int startX, out int startZ, out int elevation, out List<GridCell> cellsToOccupy))
        {
            float cellSize = _gridSystem.GetCellSize();
            Vector3 startPos = _gridSystem.GetWorldPosition(startX, startZ, elevation);
            Vector3 spawnPos = startPos + new Vector3(cellSize / 2f, 0f, cellSize / 2f);

            if (Terrain.activeTerrain != null)
            {
                spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
            }

            // San phẳng khu vực
            _gridSystem.FlattenRectArea(startX, startZ, 2, 2);

            GameObject portalObj = Instantiate(_voidPortalPrefab, spawnPos, Quaternion.identity);
            portalObj.name = $"VoidPortalEvent_{startX}_{startZ}";

            // Đăng ký ô lưới bị chiếm dụng
            foreach (var cell in cellsToOccupy)
            {
                cell.isBuildable = false;
                cell.isWalkable = false;
                cell.hasResource = false;
                cell.resourceObject = portalObj;
            }

            VoidPortal portalComponent = portalObj.GetComponent<VoidPortal>();
            if (portalComponent != null)
            {
                portalComponent.SetOccupiedCells(cellsToOccupy);
                _activePortals.Add(portalComponent);
            }

            // Spawn 3 quái gác cổng hư không
            SpawnAmbushGuards(spawnPos, 3);

            _activeEvents.Add(portalObj);

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowBloodMoonAlert(
                    "CỔNG HƯ VÔ XUẤT HIỆN",
                    "Một Cổng Hư Vô mới đã mở ra ngoài sương mù hoang dã! Hãy tiêu diệt trước khi quái xâm chiếm. 😈",
                    5f
                );
            }
        }
    }

    /// <summary>
    /// Spawn sự kiện Hộ Tống Thương Nhân Xuyên Bản Đồ
    /// </summary>
    private void SpawnMerchantEscortEvent()
    {
        if (_merchantCaravanPrefab == null) return;

        int mapWidth = _gridSystem.GetWidth();
        int mapLength = _gridSystem.GetLength();

        // 1. Xác định Start Edge và Dest Edge ngẫu nhiên đối diện nhau
        int edgeIndex = Random.Range(0, 4); // 0: Tây, 1: Đông, 2: Nam, 3: Bắc
        int startCellX = 0, startCellZ = 0;
        int destCellX = 0, destCellZ = 0;

        switch (edgeIndex)
        {
            case 0: // Từ Tây (startX=12) sang Đông (destX=width-12)
                startCellX = 12;
                startCellZ = Random.Range(15, mapLength - 15);
                destCellX = mapWidth - 12;
                destCellZ = Random.Range(15, mapLength - 15);
                break;
            case 1: // Từ Đông sang Tây
                startCellX = mapWidth - 12;
                startCellZ = Random.Range(15, mapLength - 15);
                destCellX = 12;
                destCellZ = Random.Range(15, mapLength - 15);
                break;
            case 2: // Từ Nam (startZ=12) sang Bắc (destZ=length-12)
                startCellX = Random.Range(15, mapWidth - 15);
                startCellZ = 12;
                destCellX = Random.Range(15, mapWidth - 15);
                destCellZ = mapLength - 12;
                break;
            case 3: // Từ Bắc sang Nam
                startCellX = Random.Range(15, mapWidth - 15);
                startCellZ = mapLength - 12;
                destCellX = Random.Range(15, mapWidth - 15);
                destCellZ = 12;
                break;
        }

        Vector3 startPos = _gridSystem.GetWorldPosition(startCellX, startCellZ);
        Vector3 destPos = _gridSystem.GetWorldPosition(destCellX, destCellZ);

        if (Terrain.activeTerrain != null)
        {
            startPos.y = Terrain.activeTerrain.SampleHeight(startPos) + Terrain.activeTerrain.transform.position.y;
            destPos.y = Terrain.activeTerrain.SampleHeight(destPos) + Terrain.activeTerrain.transform.position.y;
        }

        // 2. Xác định quy mô đoàn xe (1 = Nhỏ, 2 = Vừa, 3 = Lớn)
        int sizeRoll = Random.Range(1, 4); // 1, 2, hoặc 3

        var group = new MerchantCaravanGroup
        {
            startPos = startPos,
            destPos = destPos,
            size = sizeRoll
        };

        // Spawn các đơn vị ngựa thồ thương nhân nối đuôi nhau
        for (int i = 0; i < sizeRoll; i++)
        {
            // Các ngựa thồ spawn cách nhau 2.5m
            Vector3 offset = (startPos - destPos).normalized * (i * 2.5f);
            Vector3 individualSpawnPos = startPos + offset;
            
            if (Terrain.activeTerrain != null)
            {
                individualSpawnPos.y = Terrain.activeTerrain.SampleHeight(individualSpawnPos) + Terrain.activeTerrain.transform.position.y;
            }

            GameObject caravanObj = Instantiate(_merchantCaravanPrefab, individualSpawnPos, Quaternion.identity);
            caravanObj.name = $"MerchantCaravan_{sizeRoll}_{i}";

            MerchantCaravanUnit caravanUnit = caravanObj.GetComponent<MerchantCaravanUnit>();
            if (caravanUnit == null)
            {
                caravanUnit = caravanObj.AddComponent<MerchantCaravanUnit>();
            }

            caravanUnit.SetDestination(destPos);
            group.units.Add(caravanUnit);
            _activeEvents.Add(caravanObj);
        }

        _activeCaravans.Add(group);

        string sizeName = sizeRoll == 3 ? "LỚN" : (sizeRoll == 2 ? "VỪA" : "NHỎ");
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                $"ĐOÀN THƯƠNG NHÂN {sizeName} CẦN HỘ TỐNG",
                $"Đoàn thương nhân đang di chuyển cắt ngang bản đồ! Hãy hộ tống họ đến đích an toàn. 🐎",
                5.5f
            );
        }
    }

    private void SpawnAmbushGuards(Vector3 centerPos, int count)
    {
        GameObject warriorPrefab = null;
        if (EnemyManager.Instance != null)
        {
            warriorPrefab = EnemyManager.Instance.GetEnemyPrefabByName("Skeleton_Warrior");
        }

        if (warriorPrefab == null) return;

        for (int i = 0; i < count; i++)
        {
            Vector2 randCircle = Random.insideUnitCircle * 3.5f;
            Vector3 spawnPos = centerPos + new Vector3(randCircle.x, 0f, randCircle.y);
            if (Terrain.activeTerrain != null)
            {
                spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
            }

            GameObject enemy = Instantiate(warriorPrefab, spawnPos, Quaternion.identity);
            
            // Set lính canh tĩnh đứng tại cổng
            var guard = enemy.GetComponent<EnemyUnitController>();
            if (guard != null)
            {
                guard.IsGuard = true;
            }
        }
    }

    /// <summary>
    /// Callback khi một ngựa thồ thương nhân hoàn thành lộ trình hộ tống
    /// </summary>
    public void OnCaravanEscorted(MerchantCaravanUnit unit)
    {
        // Tìm caravan group chứa unit này
        MerchantCaravanGroup targetGroup = null;
        foreach (var group in _activeCaravans)
        {
            if (group.units.Contains(unit))
            {
                targetGroup = group;
                break;
            }
        }

        if (targetGroup == null || targetGroup.isFinished) return;
        targetGroup.isFinished = true;

        // Xóa các ngựa thồ khác trong group (nếu còn) để hoàn tất sự kiện
        foreach (var u in targetGroup.units)
        {
            if (u != null && u != unit && u.gameObject != null)
            {
                Destroy(u.gameObject);
            }
        }

        // Phát thưởng dựa trên kích thước đoàn
        int goldReward = 0;
        int woodReward = 0;
        bool getCard = false;
        bool getSoldiers = false;

        switch (targetGroup.size)
        {
            case 1: // Nhỏ
                goldReward = Random.Range(150, 250);
                woodReward = Random.Range(150, 250);
                break;
            case 2: // Vừa
                goldReward = Random.Range(300, 450);
                woodReward = Random.Range(300, 450);
                getCard = true;
                break;
            case 3: // Lớn
                goldReward = Random.Range(600, 800);
                woodReward = Random.Range(600, 800);
                getCard = true;
                getSoldiers = true;
                break;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.AddResource(ResourceType.Gold, goldReward);
            ResourceManager.Instance.AddResource(ResourceType.Wood, woodReward);
        }

        if (getCard && CardManager.Instance != null)
        {
            CardManager.Instance.TriggerCardDraft();
        }

        if (getSoldiers && GameManager.Instance != null)
        {
            // Spawn 1 Knight và 1 PlayerArcher tại nhà chính của người chơi
            var mainHouse = FindAnyObjectByType<MainBuildingCombatTarget>();
            Vector3 spawnPos = mainHouse != null ? mainHouse.transform.position + Vector3.back * 3f : Vector3.zero;
            
            if (Terrain.activeTerrain != null)
            {
                spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
            }

            if (GameManager.Instance.RewardKnightPrefab != null)
            {
                GameObject kObj = Instantiate(GameManager.Instance.RewardKnightPrefab, spawnPos, Quaternion.identity);
                kObj.transform.SetParent(GameManager.CombatUnitsContainer);
            }
            if (GameManager.Instance.RewardArcherPrefab != null)
            {
                GameObject aObj = Instantiate(GameManager.Instance.RewardArcherPrefab, spawnPos + Vector3.right * 1.5f, Quaternion.identity);
                aObj.transform.SetParent(GameManager.CombatUnitsContainer);
            }
        }

        if (HUDManager.Instance != null)
        {
            string bonusText = getSoldiers ? " và nhận thêm 2 lính phòng thủ!" : "";
            HUDManager.Instance.ShowBloodMoonAlert(
                "HỘ TỐNG THÀNH CÔNG",
                $"Đoàn xe thồ đã đến đích an toàn! Bạn nhận được +{goldReward} vàng, +{woodReward} gỗ{bonusText} 🏆",
                5.5f
            );
        }
    }

    /// <summary>
    /// Callback khi một ngựa thồ bị tiêu diệt
    /// </summary>
    public void OnCaravanFailed(MerchantCaravanUnit unit)
    {
        // Kiểm tra xem đây có phải là đoàn xe thồ cuối cùng bị chết không
        MerchantCaravanGroup targetGroup = null;
        if (unit != null)
        {
            foreach (var group in _activeCaravans)
            {
                if (group.units.Contains(unit))
                {
                    targetGroup = group;
                    break;
                }
            }
        }

        if (targetGroup != null)
        {
            if (!targetGroup.IsAlive() && !targetGroup.isFinished)
            {
                targetGroup.isFinished = true;
                TriggerFailureHUD();
            }
        }
        else
        {
            TriggerFailureHUD();
        }
    }

    private void TriggerFailureHUD()
    {
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                "ĐOÀN XE THƯƠNG NHÂN BỊ TIÊU DIỆT",
                "Bạn đã thất bại trong việc bảo vệ đoàn xe thồ thương nhân! 💀",
                4.5f
            );
        }
    }

    /// <summary>
    /// Tìm vị trí ô lưới 2x2 trống, phẳng để spawn cổng hư không.
    /// </summary>
    private bool FindValidSpawnPosition(out int startX, out int startZ, out int elevation, out List<GridCell> cellsToOccupy)
    {
        startX = 0;
        startZ = 0;
        elevation = -1;
        cellsToOccupy = null;

        if (_gridSystem == null) return false;

        int mapWidth = _gridSystem.GetWidth();
        int mapLength = _gridSystem.GetLength();
        int centerX = mapWidth / 2;
        int centerZ = mapLength / 2;

        int maxAttempts = 150;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int rX = Random.Range(12, mapWidth - 12);
            int rZ = Random.Range(12, mapLength - 12);

            // Tránh vùng an toàn người chơi
            if (_gridSystem.IsInsideStartingSafeZonePublic(rX, rZ) || _gridSystem.IsInsideStartingSafeZonePublic(rX + 1, rZ + 1))
            {
                continue;
            }

            // Đảm bảo khoảng cách đến tâm Nhà Chính
            float distToCenter = Vector2.Distance(new Vector2(rX + 0.5f, rZ + 0.5f), new Vector2(centerX, centerZ));
            if (distToCenter < _minDistanceFromCenter)
            {
                continue;
            }

            // Kiểm tra xem ô 2x2 có trống không
            bool canPlace = true;
            int targetElev = -1;
            List<GridCell> tempCells = new List<GridCell>();

            for (int dx = 0; dx < 2; dx++)
            {
                for (int dz = 0; dz < 2; dz++)
                {
                    GridCell cell = _gridSystem.GetCell(rX + dx, rZ + dz);
                    if (cell == null || !cell.isBuildable || cell.hasResource || cell.elevation < 0)
                    {
                        canPlace = false;
                        break;
                    }

                    if (targetElev == -1)
                    {
                        targetElev = cell.elevation;
                    }
                }
                if (!canPlace) break;
            }

            if (canPlace)
            {
                for (int dx = 0; dx < 2; dx++)
                {
                    for (int dz = 0; dz < 2; dz++)
                    {
                        tempCells.Add(_gridSystem.GetCell(rX + dx, rZ + dz));
                    }
                }

                startX = rX;
                startZ = rZ;
                elevation = targetElev;
                cellsToOccupy = tempCells;
                return true;
            }
        }

        return false;
    }
}
