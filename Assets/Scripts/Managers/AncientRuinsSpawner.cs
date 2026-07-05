using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Quản lý việc tự động sinh (spawn) các Phế Tích Cổ (Ancient Ruins) trên bản đồ.
/// Hỗ trợ sinh khởi tạo (initial spawn) khi bắt đầu game và sinh bù định kỳ (periodic spawn) sau mỗi X ngày.
/// </summary>
public class AncientRuinsSpawner : MonoBehaviour
{
    private static AncientRuinsSpawner _instance;
    public static AncientRuinsSpawner Instance => _instance;

    [Header("Ancient Ruins Prefabs")]
    [Tooltip("Danh sách các prefab phế tích cổ dùng để spawn ngẫu nhiên")]
    [SerializeField] private GameObject[] _ruinsPrefabs;

    [Header("Spawner Settings")]
    [Tooltip("Số lượng phế tích được sinh lúc khởi đầu game")]
    [SerializeField] private int _initialRuinsCount = 3;

    [Tooltip("Số lượng phế tích tối đa đồng thời trên bản đồ")]
    [SerializeField] private int _maxActiveRuins = 5;

    [Tooltip("Tần suất quét sinh thêm phế tích mới (số ngày)")]
    [SerializeField] private int _spawnIntervalDays = 3;

    [Tooltip("Khoảng cách tối thiểu từ phế tích tới Nhà Chính (số ô lưới)")]
    [SerializeField] private int _minDistanceFromCenter = 18;

    private GridSystem _gridSystem;
    private readonly List<AncientRuins> _activeRuins = new List<AncientRuins>();
    private int _daysSinceLastSpawn = 0;

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

        FilterPrefabsToOnlySword();

        // Đăng ký lắng nghe sự kiện qua ngày mới từ TimeManager
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
        }
    }

    /// <summary>
    /// Gán danh sách prefabs phế tích từ bên ngoài (ví dụ GameManager)
    /// </summary>
    public void SetPrefabs(GameObject[] prefabs)
    {
        _ruinsPrefabs = prefabs;
        FilterPrefabsToOnlySword();
    }

    /// <summary>
    /// Lọc danh sách prefabs để chỉ giữ lại các prefab liên quan đến RuinsSword
    /// </summary>
    private void FilterPrefabsToOnlySword()
    {
        if (_ruinsPrefabs == null || _ruinsPrefabs.Length == 0) return;

        var filteredList = new List<GameObject>();
        foreach (var p in _ruinsPrefabs)
        {
            if (p != null && (p.name.Contains("Sword") || p.name.Contains("RuinsSword")))
            {
                filteredList.Add(p);
            }
        }
        _ruinsPrefabs = filteredList.ToArray();
    }

    /// <summary>
    /// Sinh các phế tích cổ khởi tạo khi bắt đầu game.
    /// </summary>
    public void SpawnInitialRuins()
    {
        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }

        if (_ruinsPrefabs == null || _ruinsPrefabs.Length == 0)
        {
            GameLog.LogWarning("[AncientRuinsSpawner] Không có prefab phế tích nào để spawn khởi tạo!");
            return;
        }

        // Sử dụng bộ sinh ngẫu nhiên đơn định theo Seed của GridSystem (salt = 99)
        System.Random prng = _gridSystem != null ? _gridSystem.CreateDeterministicRandom(99) : new System.Random();

        int spawnedCount = 0;
        int maxAttempts = 100;

        for (int i = 0; i < maxAttempts && spawnedCount < _initialRuinsCount; i++)
        {
            if (TrySpawnSingleRuin(prng))
            {
                spawnedCount++;
            }
        }

        GameLog.Log($"[AncientRuinsSpawner] Đã spawn thành công {spawnedCount}/{_initialRuinsCount} phế tích cổ khởi tạo.");
    }

    /// <summary>
    /// Thử tìm vị trí và spawn 1 phế tích cổ ngẫu nhiên.
    /// </summary>
    private bool TrySpawnSingleRuin(System.Random prng = null)
    {
        if (_activeRuins.Count >= _maxActiveRuins)
        {
            return false;
        }

        if (FindValidSpawnPosition(out int startX, out int startZ, out int elevation, out List<GridCell> cellsToOccupy, prng))
        {
            // Chọn ngẫu nhiên một prefab phế tích
            int prefabIdx = prng != null ? prng.Next(0, _ruinsPrefabs.Length) : Random.Range(0, _ruinsPrefabs.Length);
            GameObject prefab = _ruinsPrefabs[prefabIdx];
            if (prefab == null) return false;

            // Tính vị trí tâm cho khu vực 2x2 ô lưới
            float cellSize = _gridSystem.GetCellSize();
            Vector3 startPos = _gridSystem.GetWorldPosition(startX, startZ, elevation);
            float offsetX = cellSize / 2f;
            float offsetZ = cellSize / 2f;
            Vector3 spawnPos = startPos + new Vector3(offsetX, 0f, offsetZ);

            // Cân chỉnh chiều cao theo địa hình Terrain thực tế
            if (Terrain.activeTerrain != null)
            {
                spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
            }

            // San phẳng khu vực xây dựng phế tích
            _gridSystem.FlattenRectArea(startX, startZ, 2, 2);
            Quaternion spawnRot;
            if (prefab.name.Contains("Sword") || prefab.name.Contains("RuinsSword"))
            {
                // Đối với prefab kiếm khổng lồ: X = -90, Y = 0, Z = random
                float randRot = prng != null ? (float)(prng.NextDouble() * 360f) : Random.Range(0f, 360f);
                spawnRot = Quaternion.Euler(-90f, 0f, randRot);
            }
            else
            {
                // Đối với các phế tích đá thông thường: X = 0, Y = random, Z = 0
                float randRot = prng != null ? (float)(prng.NextDouble() * 360f) : Random.Range(0f, 360f);
                spawnRot = Quaternion.Euler(0f, randRot, 0f);
            }

            GameObject ruinObj = Instantiate(prefab, spawnPos, spawnRot);
            ruinObj.name = $"AncientRuin_{startX}_{startZ}";

            // Đăng ký các ô lưới đã bị chiếm dụng
            foreach (var cell in cellsToOccupy)
            {
                cell.isBuildable = false;
                cell.isWalkable = false;
                cell.hasResource = false;
                cell.resourceObject = ruinObj;
            }

            // Gán NavMeshObstacle động để AI tự tránh đường đi
            NavMeshObstacle obstacle = ruinObj.GetComponent<NavMeshObstacle>();
            if (obstacle == null)
            {
                obstacle = ruinObj.AddComponent<NavMeshObstacle>();
            }
            obstacle.carving = true;
            obstacle.size = new Vector3(4f, 4f, 4f); // 2x2 ô tương ứng kích thước khoảng 4mx4m

            // Tự động gán FogVisibilityTarget nếu chưa có
            if (ruinObj.GetComponent<FogVisibilityTarget>() == null)
            {
                ruinObj.AddComponent<FogVisibilityTarget>();
            }

            AncientRuins ruinsComponent = ruinObj.GetComponent<AncientRuins>();
            if (ruinsComponent != null)
            {
                ruinsComponent.SetOccupiedCells(cellsToOccupy);
                _activeRuins.Add(ruinsComponent);
            }
            else
            {
                GameLog.LogWarning($"[AncientRuinsSpawner] Prefab {prefab.name} thiếu script AncientRuins!");
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Tìm vị trí ô lưới 2x2 trống, phẳng và cách xa Nhà Chính để spawn phế tích.
    /// </summary>
    private bool FindValidSpawnPosition(out int startX, out int startZ, out int elevation, out List<GridCell> cellsToOccupy, System.Random prng = null)
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

        int maxAttempts = 200;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            int rX = prng != null ? prng.Next(10, mapWidth - 10) : Random.Range(10, mapWidth - 10);
            int rZ = prng != null ? prng.Next(10, mapLength - 10) : Random.Range(10, mapLength - 10);

            // Bỏ qua nếu thuộc vùng an toàn khởi đầu của người chơi
            if (_gridSystem.IsInsideStartingSafeZonePublic(rX, rZ) || _gridSystem.IsInsideStartingSafeZonePublic(rX + 1, rZ + 1))
            {
                continue;
            }

            // Đảm bảo cách xa Nhà Chính (tâm bản đồ)
            float distToCenter = Vector2.Distance(new Vector2(rX + 0.5f, rZ + 0.5f), new Vector2(centerX, centerZ));
            if (distToCenter < _minDistanceFromCenter)
            {
                continue;
            }

            // Đảm bảo không quá sát phế tích cổ khác
            Vector3 candidateWorldPos = _gridSystem.GetWorldPosition(rX, rZ);
            bool tooCloseToOtherRuin = false;
            for (int i = 0; i < _activeRuins.Count; i++)
            {
                var ruin = _activeRuins[i];
                if (ruin != null && Vector3.Distance(ruin.transform.position, candidateWorldPos) < 25f)
                {
                    tooCloseToOtherRuin = true;
                    break;
                }
            }
            if (tooCloseToOtherRuin)
            {
                continue;
            }

            // Kiểm tra các ô lưới 2x2 xem có thể đặt được không
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

    /// <summary>
    /// Xử lý khi ngày mới bắt đầu để spawn định kỳ.
    /// </summary>
    private void HandleDayChanged(int dayCount)
    {
        _daysSinceLastSpawn++;

        if (_daysSinceLastSpawn >= _spawnIntervalDays)
        {
            _daysSinceLastSpawn = 0;

            if (_activeRuins.Count < _maxActiveRuins)
            {
                if (TrySpawnSingleRuin())
                {
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.ShowBloodMoonAlert(
                            "PHẾ TÍCH CỔ XUẤT HIỆN",
                            "Một phế tích cổ đại mới đã xuất hiện trên bản đồ! Hãy tìm kiếm và khai quật. 🔍",
                            4.5f
                        );
                    }
                }
            }
        }
    }

    /// <summary>
    /// Xóa phế tích khỏi danh sách active khi bị phá hủy hoặc khai quật xong.
    /// </summary>
    public void OnRuinDestroyed(AncientRuins ruin)
    {
        if (_activeRuins.Contains(ruin))
        {
            _activeRuins.Remove(ruin);
            GameLog.Log($"[AncientRuinsSpawner] Đã xóa {ruin.gameObject.name} khỏi danh sách phế tích hoạt động.");
        }
    }
}
