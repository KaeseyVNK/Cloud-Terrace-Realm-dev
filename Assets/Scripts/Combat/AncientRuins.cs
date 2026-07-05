using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý Phế Tích Cổ (Ancient Ruins) cần lính canh bảo vệ.
/// Sau khi tiêu diệt hết lính canh, dân làng có thể tới nghiên cứu/khai quật để kích hoạt quay thẻ.
/// </summary>
public class AncientRuins : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
{
    [System.Serializable]
    public struct GuardConfig
    {
        public GameObject guardPrefab;
        public int count;
    }

    [Header("Lính Canh (Guards)")]
    [Tooltip("Danh sách các loại quái vật và số lượng canh giữ phế tích")]
    [SerializeField] private List<GuardConfig> _guards = new List<GuardConfig>();

    [Tooltip("Bán kính phân bổ lính canh quanh phế tích")]
    [SerializeField] private float _guardSpawnRadius = 5f;

    [Header("Khai Quật (Exploration)")]
    [Tooltip("Thời gian cần thiết để khai quật xong phế tích (giây)")]
    [SerializeField] private float _explorationDuration = 8f;

    [Tooltip("Hệ số nhân sức mạnh thêm của lính canh phế tích")]
    [SerializeField] private float _guardStrengthMultiplier = 1.2f;

    private List<BaseCombatUnitController> _spawnedGuards = new List<BaseCombatUnitController>();
    private List<GridCell> _occupiedCells = new List<GridCell>();

    public void SetOccupiedCells(List<GridCell> cells)
    {
        _occupiedCells = cells;
    }
    private List<VillagerController> _assignedVillagers = new List<VillagerController>();
    private bool _isCleared = false;
    private bool _isExplored = false;
    private float _explorationProgress = 0f;

    public bool IsCleared => _isCleared;
    public bool IsExplored => _isExplored;
    public float ExplorationDuration => _explorationDuration;
    public float ExplorationProgress => _explorationProgress;
    public bool BypassSpawnGuards { get; set; }

    private void Start()
    {
        // Kiểm tra xem phế tích này có nằm trong một Phế Tích lớn hơn không (trường hợp dùng prefab con làm trang trí cho RuinsSword)
        AncientRuins parentRuin = null;
        Transform curr = transform.parent;
        while (curr != null)
        {
            parentRuin = curr.GetComponent<AncientRuins>();
            if (parentRuin != null) break;
            curr = curr.parent;
        }

        if (parentRuin != null)
        {
            // Đây chỉ là đối tượng con dùng để trang trí/tăng độ đồ sộ cho phế tích cha
            // Hủy script AncientRuins để tránh chạy trùng lặp logic lính canh và khai quật.
            // Giữ lại NavMeshObstacle để đối tượng con vẫn cản đường AI di chuyển hợp lý.
            FogVisibilityTarget fog = GetComponent<FogVisibilityTarget>();
            if (fog != null) Destroy(fog);

            Destroy(this);
            return;
        }

        if (CloudTerraceRealm.SaveSystem.SaveManager.Instance != null && CloudTerraceRealm.SaveSystem.SaveManager.Instance.IsLoadingSave)
        {
            BypassSpawnGuards = true;
        }

        if (!BypassSpawnGuards)
        {
            SpawnGuards();
        }
        
        // Tự động gắn FogVisibilityTarget vào phế tích nếu chưa có
        // để đảm bảo nó được che/hiện theo sương mù chiến trận (Fog of War)
        if (GetComponent<FogVisibilityTarget>() == null)
        {
            gameObject.AddComponent<FogVisibilityTarget>();
        }
    }

    private void SpawnGuards()
    {
        if (_guards == null || _guards.Count == 0)
        {
            GameLog.LogWarning($"[AncientRuins] {gameObject.name} không có danh sách lính canh! Tự động đánh dấu là Cleared.");
            _isCleared = true;
            return;
        }

        // Tạo container con "Guards" để gom nhóm quái vật
        GameObject guardsContainer = new GameObject("Guards");
        guardsContainer.transform.SetParent(transform);
        guardsContainer.transform.localPosition = Vector3.zero;

        bool hasAnyPrefab = false;
        foreach (var config in _guards)
        {
            if (config.guardPrefab != null && config.count > 0)
            {
                hasAnyPrefab = true;
                for (int i = 0; i < config.count; i++)
                {
                    Vector2 randCircle = Random.insideUnitCircle * _guardSpawnRadius;
                    Vector3 spawnPos = transform.position + new Vector3(randCircle.x, 0f, randCircle.y);

                    // Cân chỉnh chiều cao theo địa hình Terrain nếu có
                    if (Terrain.activeTerrain != null)
                    {
                        spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos) + Terrain.activeTerrain.transform.position.y;
                    }
                    else
                    {
                        spawnPos.y = transform.position.y;
                    }

                    GameObject guardObj = Instantiate(config.guardPrefab, spawnPos, Quaternion.identity);
                    guardObj.transform.SetParent(guardsContainer.transform); // Gom vào parent object
                    
                    BaseCombatUnitController guardUnit = guardObj.GetComponent<BaseCombatUnitController>();
                    
                    if (guardUnit != null)
                    {
                        guardUnit.faction = UnitFaction.Enemy;
                        if (guardUnit is EnemyUnitController enemyUnit)
                        {
                            enemyUnit.CanRetreat = false;
                            enemyUnit.IsGuard = true;
                        }

                        // Áp dụng hệ số tăng sức mạnh lính canh theo tiến trình game
                        if (EnemyManager.Instance != null)
                        {
                            int currentNight = EnemyManager.Instance.CurrentNightNumber;
                            if (currentNight <= 0 && TimeManager.Instance != null)
                            {
                                currentNight = TimeManager.Instance.dayCount;
                            }

                            EnemyManager.Instance.GetEnemyStatMultipliers(currentNight, out float healthMult, out float damageMult, out float speedMult);

                            // Cộng thêm hệ số nhân đặc biệt cho guard phế tích
                            healthMult *= _guardStrengthMultiplier;
                            damageMult *= _guardStrengthMultiplier;

                            guardUnit.ApplyStatMultipliers(healthMult, damageMult, speedMult);
                        }

                        _spawnedGuards.Add(guardUnit);
                    }
                }
            }
        }

        if (!hasAnyPrefab)
        {
            GameLog.LogWarning($"[AncientRuins] {gameObject.name} không có lính canh hợp lệ! Tự động đánh dấu là Cleared.");
            _isCleared = true;
        }
    }

    private void Update()
    {
        if (!_isCleared)
        {
            // Kiểm tra xem lính canh còn sống không
            _spawnedGuards.RemoveAll(g => g == null || g.currentState == CombatState.Dead);
            if (_spawnedGuards.Count == 0)
            {
                _isCleared = true;
                OnRuinsCleared();
            }
        }

        if (_isCleared && !_isExplored && _assignedVillagers.Count > 0)
        {
            // Đếm số lượng dân làng thực sự đang đứng gần phế tích để khai quật
            int activeExplorers = 0;
            for (int i = 0; i < _assignedVillagers.Count; i++)
            {
                var villager = _assignedVillagers[i];
                if (villager != null && Vector3.Distance(villager.transform.position, transform.position) <= 4.5f)
                {
                    activeExplorers++;
                }
            }

            if (activeExplorers > 0)
            {
                _explorationProgress += Time.deltaTime * activeExplorers;
                if (_explorationProgress >= _explorationDuration)
                {
                    _isExplored = true;
                    OnExplorationCompleted();
                }
            }
        }
    }

    private void OnRuinsCleared()
    {
        GameLog.Log($"[AncientRuins] Lính canh tại {gameObject.name} đã bị tiêu diệt! Sẵn sàng khai quật.");
        
        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                "PHẾ TÍCH CỔ GIẢI PHÓNG",
                "Quái vật canh giữ đã bị tiêu diệt! Hãy cử Dân Làng đến khai quật phế tích cổ. 🔍",
                4f
            );
        }
    }

    private void OnExplorationCompleted()
    {
        GameLog.Log($"[AncientRuins] Khai quật thành công phế tích {gameObject.name}!");

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                "KHAI QUẬT PHẾ TÍCH THÀNH CÔNG",
                "Dân làng của bạn đã phát hiện ra công nghệ cổ đại! Chọn 1 thẻ nâng cấp. 🏆",
                5f
            );
        }

        if (CardManager.Instance != null)
        {
            CardManager.Instance.TriggerCardDraft();
        }

        // Giải phóng các ô bị phế tích chiếm dụng trên bản đồ để có thể xây dựng lại
        if (_occupiedCells != null && _occupiedCells.Count > 0)
        {
            for (int i = 0; i < _occupiedCells.Count; i++)
            {
                var cell = _occupiedCells[i];
                if (cell != null)
                {
                    cell.isBuildable = true;
                    cell.isWalkable = true;
                    cell.hasResource = false;
                    cell.resourceObject = null;
                }
            }
        }

        // Thông báo cho AncientRuinsSpawner giải phóng
        if (AncientRuinsSpawner.Instance != null)
        {
            AncientRuinsSpawner.Instance.OnRuinDestroyed(this);
        }

        // Giải tán tất cả dân làng đang được gán cho phế tích này
        for (int i = 0; i < _assignedVillagers.Count; i++)
        {
            var villager = _assignedVillagers[i];
            if (villager != null)
            {
                villager.ClearAssignedRuins();
            }
        }
        _assignedVillagers.Clear();

        // Biến mất phế tích sau khi bị khai quật xong
        Destroy(gameObject);
    }

    public void AssignVillager(VillagerController villager)
    {
        if (villager != null && !_assignedVillagers.Contains(villager))
        {
            _assignedVillagers.Add(villager);
        }
    }

    public void UnassignVillager(VillagerController villager)
    {
        if (villager != null)
        {
            _assignedVillagers.Remove(villager);
        }
    }

    [System.Serializable]
    private class RuinSaveState
    {
        public bool isCleared;
        public bool isExplored;
        public float explorationProgress;
    }

    public string CaptureState()
    {
        var state = new RuinSaveState
        {
            isCleared = this._isCleared,
            isExplored = this._isExplored,
            explorationProgress = this._explorationProgress
        };
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        var state = JsonUtility.FromJson<RuinSaveState>(stateJson);
        if (state == null) return;

        this._isCleared = state.isCleared;
        this._isExplored = state.isExplored;
        this._explorationProgress = state.explorationProgress;

        if (_isCleared)
        {
            // Hủy toàn bộ lính canh hiện hữu nếu phế tích đã bị dọn
            foreach (var guard in _spawnedGuards)
            {
                if (guard != null) Destroy(guard.gameObject);
            }
            _spawnedGuards.Clear();
        }
    }
}
