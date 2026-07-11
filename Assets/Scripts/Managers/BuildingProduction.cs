using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

public class BuildingProduction : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
{
    public static readonly List<BuildingProduction> Registry = new List<BuildingProduction>();

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    [UnityEngine.Serialization.FormerlySerializedAs("buildingData")]
    [SerializeField] private BuildingData _buildingData;
    
    [UnityEngine.Serialization.FormerlySerializedAs("spawnPoint")]
    [Tooltip("Vị trí sinh ra lính/dân")]
    [SerializeField] private Transform _spawnPoint;
    
    [Header("Rally Point")]
    [SerializeField] private GameObject _rallyFlagPrefab;
    [SerializeField] private float _rallyFlagYOffset = 0.05f;

    [Header("Unit Pooling")]
    [SerializeField] private int _combatUnitPoolPrewarmCount = 6;

    // Hàng đợi các unit đang chờ sản xuất
    private Queue<UnitData> _productionQueue = new Queue<UnitData>();
    
    private UnitData _currentProducingUnit;
    private float _currentProductionTimer = 0f;
    private bool _isProducing = false;
    private ConstructibleBuilding _constructibleBuilding;
    private Vector3? _rallyPoint;
    private ResourceNode _rallyResource;
    private BaseCombatUnitController _rallyAttackTarget;
    private GameObject _rallyFlagInstance;
    private bool _populationBlockedLogged;
    private LineRenderer _lineRenderer;

    public BuildingData BuildingData => _buildingData;
    public UnitData CurrentProducingUnit => _currentProducingUnit;
    public float CurrentProductionTimer => _currentProductionTimer;
    public Transform SpawnPoint => _spawnPoint;
    public bool HasRallyPoint => _rallyPoint.HasValue;
    public Vector3 RallyPoint => _rallyPoint ?? (_spawnPoint != null ? _spawnPoint.position : transform.position);
    public IEnumerable<UnitData> ProductionQueue => _productionQueue;
    public void SetBuildingData(BuildingData data)
    {
        _buildingData = data;
        PrewarmProducedCombatUnits();
    }

    public int QueuedVillagerCount
    {
        get
        {
            int count = 0;
            if (_currentProducingUnit != null && PopulationManager.IsVillagerUnit(_currentProducingUnit))
            {
                count++;
            }

            foreach (UnitData queuedUnit in _productionQueue)
            {
                if (PopulationManager.IsVillagerUnit(queuedUnit))
                {
                    count++;
                }
            }

            return count;
        }
    }

    // Được gọi khi khởi tạo công trình (nếu spawnPoint chưa gán, tự tạo)
    void Start()
    {
        _constructibleBuilding = GetComponent<ConstructibleBuilding>();

        if (_spawnPoint == null)
        {
            GameObject sp = new GameObject("SpawnPoint");
            sp.transform.SetParent(transform);
            // Mặc định sinh ra ở cửa (có thể chỉnh lại trong Prefab)
            sp.transform.localPosition = new Vector3(0, 0, -2f); 
            _spawnPoint = sp.transform;
        }

        // Lấy BuildingData từ BuildingManager nếu chưa có (hỗ trợ tra cứu ngược lên các parent của GameObject)
        if (_buildingData == null && BuildingManager.Instance != null)
        {
            Transform curr = transform;
            while (curr != null)
            {
                if (BuildingManager.Instance.BuildingDataMap.TryGetValue(curr.gameObject, out var foundData))
                {
                    _buildingData = foundData;
                    break;
                }
                curr = curr.parent;
            }
        }

        PrewarmProducedCombatUnits();

        // Khởi tạo LineRenderer động phục vụ vẽ điểm tập kết
        _lineRenderer = gameObject.AddComponent<LineRenderer>();
        _lineRenderer.startWidth = 0.06f;
        _lineRenderer.endWidth = 0.06f;
        _lineRenderer.positionCount = 2;
        _lineRenderer.useWorldSpace = true;
        
        Shader lineShader = Shader.Find("Sprites/Default");
        if (lineShader == null) lineShader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        if (lineShader != null)
        {
            _lineRenderer.material = new Material(lineShader);
        }
        _lineRenderer.startColor = new Color(0.15f, 0.85f, 1.0f, 0.85f);
        _lineRenderer.endColor = new Color(0.15f, 0.85f, 1.0f, 0.1f);
        _lineRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (_rallyFlagInstance != null)
        {
            Destroy(_rallyFlagInstance);
        }

        if (_lineRenderer != null && _lineRenderer.material != null)
        {
            Destroy(_lineRenderer.material);
        }
    }

    void Update()
    {
        // Không chạy hàng đợi sản xuất nếu công trình chưa được xây xong hoàn toàn
        if (_constructibleBuilding != null && !_constructibleBuilding.IsCompleted) return;

        if (_isProducing && _currentProducingUnit != null)
        {
            _currentProductionTimer -= Time.deltaTime;
            
            if (_currentProductionTimer <= 0)
            {
                if (PopulationManager.IsVillagerUnit(_currentProducingUnit)
                    && PopulationManager.CurrentVillagers >= PopulationManager.MaxVillagers)
                {
                    _currentProductionTimer = 0.1f;
                    if (!_populationBlockedLogged)
                    {
                        GameLog.LogWarning("Dân làng đã chạm giới hạn nhà dân. Sản xuất sẽ tiếp tục khi có thêm chỗ ở.");
                        _populationBlockedLogged = true;
                    }
                    return;
                }

                FinishProduction();
            }
        }
    }

    private void LateUpdate()
    {
        bool isSelected = (TestProductionUI.Instance != null && TestProductionUI.Instance.SelectedProduction == this);
        if (isSelected && HasRallyPoint)
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.SetPosition(0, _spawnPoint != null ? _spawnPoint.position : transform.position);
                _lineRenderer.SetPosition(1, RallyPoint);
            }
        }
        else
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = false;
            }
        }
    }

    // UI sẽ gọi hàm này khi người chơi bấm nút "Mua Lính"
    public void RequestProduceUnit(UnitData unit)
    {
        // Không cho phép sản xuất nếu công trình chưa xây xong
        ConstructibleBuilding cb = GetComponent<ConstructibleBuilding>();
        if (cb != null && !cb.IsCompleted)
        {
            GameLog.LogWarning("Không thể sản xuất: Công trình này đang được xây dựng!");
            return;
        }

        // 1. Kiểm tra xem công trình này có quyền sản xuất loại Unit này không
        if (_buildingData == null || !_buildingData.producibleUnits.Contains(unit))
        {
            GameLog.LogWarning("Công trình này không thể sản xuất " + unit.unitName);
            return;
        }

        // 2. Kiểm tra tài nguyên
        if (!unit.AreTechnologyRequirementsMet())
        {
            GameLog.LogWarning("Chưa mở khóa công nghệ để sản xuất " + unit.unitName + ": " + unit.GetMissingTechnologyNames());
            return;
        }

        if (!ResourceManager.Instance.CanAfford(unit.productionCosts))
        {
            GameLog.LogWarning("Không đủ tài nguyên để sản xuất " + unit.unitName);
            return;
        }

        if (PopulationManager.IsVillagerUnit(unit) && !PopulationManager.CanQueueVillager(out string populationReason))
        {
            GameLog.LogWarning(populationReason);
            return;
        }

        // 3. Trừ tài nguyên
        ResourceManager.Instance.ConsumeCosts(unit.productionCosts);

        // 4. Thêm vào hàng đợi
        _productionQueue.Enqueue(unit);

        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayTrainingStart();
        }

        GameLog.Log("Đã thêm " + unit.unitName + " vào hàng đợi sản xuất.");

        // 5. Nếu đang không bận rộn thì bắt đầu sản xuất ngay
        if (!_isProducing)
        {
            StartNextProduction();
        }
    }

    public int ProductionQueueCount()
    {
        return _productionQueue.Count;
    }

    public void EnqueueUnitWithoutCost(UnitData unit)
    {
        _productionQueue.Enqueue(unit);
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayTrainingStart();
        }
        GameLog.Log("Đã thêm " + unit.unitName + " vào hàng đợi sản xuất của " + gameObject.name);
        if (!_isProducing)
        {
            StartNextProduction();
        }
    }

    /// <summary>
    /// Hủy một lượt sản xuất lính trong hàng đợi theo index và hoàn tiền.
    /// </summary>
    public bool CancelQueueItem(int index)
    {
        if (index < 0 || index >= _productionQueue.Count) return false;

        var list = new List<UnitData>(_productionQueue);
        var unitToCancel = list[index];

        // Hoàn trả tài nguyên
        if (ResourceManager.Instance != null && unitToCancel != null && unitToCancel.productionCosts != null)
        {
            foreach (var cost in unitToCancel.productionCosts)
            {
                ResourceManager.Instance.AddResource(cost.resourceType, cost.amount);
            }
        }

        list.RemoveAt(index);

        _productionQueue.Clear();
        foreach (var unit in list)
        {
            _productionQueue.Enqueue(unit);
        }

        GameLog.Log($"Đã hủy {unitToCancel.unitName} ở vị trí hàng đợi {index} và hoàn trả tài nguyên.");
        return true;
    }

    private void StartNextProduction()
    {
        if (_productionQueue.Count > 0)
        {
            _currentProducingUnit = _productionQueue.Dequeue();
            _currentProductionTimer = _currentProducingUnit.productionTime;
            _isProducing = true;
            _populationBlockedLogged = false;
            GameLog.Log("Đang sản xuất: " + _currentProducingUnit.unitName + "...");
        }
        else
        {
            _isProducing = false;
            _currentProducingUnit = null;
            _populationBlockedLogged = false;
        }
    }

    private void PrewarmProducedCombatUnits()
    {
        if (_buildingData == null || _buildingData.producibleUnits == null || _combatUnitPoolPrewarmCount <= 0)
        {
            return;
        }

        for (int i = 0; i < _buildingData.producibleUnits.Count; i++)
        {
            UnitData unit = _buildingData.producibleUnits[i];
            if (unit == null || unit.unitPrefab == null)
            {
                continue;
            }

            bool isVillager = unit.unitPrefab.GetComponent<VillagerController>() != null ||
                              unit.unitPrefab.GetComponentInChildren<VillagerController>(true) != null;
            bool isCombatUnit = unit.unitPrefab.GetComponent<BaseCombatUnitController>() != null ||
                                unit.unitPrefab.GetComponentInChildren<BaseCombatUnitController>(true) != null;

            if (isCombatUnit && !isVillager && PoolManager.Instance != null)
            {
                PoolManager.Instance.Prewarm(unit.unitPrefab, _combatUnitPoolPrewarmCount);
            }
        }
    }

    private void FinishProduction()
    {
        GameLog.Log("Sản xuất hoàn tất: " + _currentProducingUnit.unitName);
        
        // Sinh ra lính / dân
        if (_currentProducingUnit.unitPrefab != null)
        {
            GameObject spawnedUnit = ShouldUsePoolForUnit(_currentProducingUnit.unitPrefab)
                ? PoolManager.Instance.Spawn(_currentProducingUnit.unitPrefab, _spawnPoint.position, _spawnPoint.rotation)
                : Instantiate(_currentProducingUnit.unitPrefab, _spawnPoint.position, _spawnPoint.rotation);
            
            if (spawnedUnit != null)
            {
                bool isVillager = spawnedUnit.GetComponent<VillagerController>() != null ||
                                  spawnedUnit.GetComponentInChildren<VillagerController>(true) != null;
                if (isVillager)
                {
                    spawnedUnit.transform.SetParent(GameManager.VillagersContainer);
                }
                else
                {
                    spawnedUnit.transform.SetParent(GameManager.CombatUnitsContainer);
                }
            }

            ApplyRallyOrder(spawnedUnit);
        }

        // Chuyển sang con tiếp theo trong hàng đợi
        StartNextProduction();
    }

    public void SetRallyPoint(Vector3 point)
    {
        // Điều chỉnh tọa độ Y về mặt đất để tránh đặt cờ lên đá hoặc ngọn cây
        if (Terrain.activeTerrain != null)
        {
            point.y = Terrain.activeTerrain.SampleHeight(point) + Terrain.activeTerrain.transform.position.y;
        }
        else
        {
            point.y = 0f;
        }

        Vector3 rallyPoint = GetNearestNavMeshPoint(point);
        _rallyPoint = rallyPoint;
        _rallyResource = null;
        _rallyAttackTarget = null;
        MoveRallyFlag(rallyPoint);
        GameLog.Log($"[Rally] {gameObject.name} đã đặt điểm tập kết tại {rallyPoint}.");
    }

    public void SetRallyResource(ResourceNode resource)
    {
        if (resource == null || !resource.CanHarvest)
        {
            SetRallyPoint(resource != null ? resource.transform.position : RallyPoint);
            return;
        }

        _rallyPoint = resource.transform.position;
        _rallyResource = resource;
        _rallyAttackTarget = null;
        MoveRallyFlag(resource.transform.position);
        resource.TriggerBounceEffect();
        GameLog.Log($"[Rally] {gameObject.name} đã đặt việc khai thác {resource.ResourceType}.");
    }

    public void SetRallyAttackTarget(BaseCombatUnitController target)
    {
        if (target == null)
        {
            return;
        }

        _rallyPoint = target.transform.position;
        _rallyResource = null;
        _rallyAttackTarget = target;
        MoveRallyFlag(target.transform.position);
        GameLog.Log($"[Rally] {gameObject.name} đã đặt mục tiêu tấn công {target.unitName}.");
    }

    public void SetRallyFromHit(RaycastHit hit)
    {
        ResourceNode resource = GetVisibleResourceFromHit(hit);
        if (resource != null)
        {
            SetRallyResource(resource);
            return;
        }

        BaseCombatUnitController combatTarget = hit.collider.GetComponentInParent<BaseCombatUnitController>();
        if (combatTarget != null && !IsHiddenByFog(combatTarget.gameObject))
        {
            SetRallyAttackTarget(combatTarget);
            return;
        }

        SetRallyPoint(hit.point);
    }

    public void SetRallyFlagVisible(bool visible)
    {
        if (_rallyFlagInstance != null)
        {
            _rallyFlagInstance.SetActive(visible && HasRallyPoint);
        }
    }

    private ResourceNode GetVisibleResourceFromHit(RaycastHit hit)
    {
        ResourceNode resource = hit.collider.GetComponentInParent<ResourceNode>();
        if (resource != null && !IsHiddenByFog(resource.gameObject))
        {
            return resource;
        }

        GridSystem grid = FindAnyObjectByType<GridSystem>();
        if (grid == null)
        {
            return null;
        }

        grid.GetXY(hit.point, out int gridX, out int gridZ);
        GridCell cell = grid.GetCell(gridX, gridZ);
        if (cell == null || !cell.hasResource || cell.resourceObject == null)
        {
            return null;
        }

        if (IsHiddenByFog(cell.resourceObject))
        {
            return null;
        }

        return cell.resourceObject.GetComponent<ResourceNode>();
    }

    private bool IsHiddenByFog(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        FogVisibilityTarget visibilityTarget = target.GetComponentInParent<FogVisibilityTarget>();
        return visibilityTarget != null && !visibilityTarget.IsVisible;
    }

    private void ApplyRallyOrder(GameObject spawnedUnit)
    {
        if (spawnedUnit == null || !_rallyPoint.HasValue)
        {
            return;
        }

        VillagerController villager = spawnedUnit.GetComponent<VillagerController>();
        BaseCombatUnitController combatUnit = spawnedUnit.GetComponent<BaseCombatUnitController>();

        if (_rallyResource != null && _rallyResource.CanHarvest)
        {
            if (villager != null)
            {
                villager.CommandGather(_rallyResource, null);
                return;
            }

            MoveSpawnedCombatUnit(combatUnit, _rallyResource.transform.position);
            return;
        }

        if (_rallyAttackTarget != null)
        {
            if (combatUnit != null && _rallyAttackTarget.faction != combatUnit.faction)
            {
                combatUnit.CommandAttack(_rallyAttackTarget);
                return;
            }

            MoveSpawnedUnit(villager, combatUnit, _rallyAttackTarget.transform.position);
            return;
        }

        MoveSpawnedUnit(villager, combatUnit, _rallyPoint.Value);
    }

    private bool ShouldUsePoolForUnit(GameObject unitPrefab)
    {
        if (unitPrefab == null)
        {
            return false;
        }

        bool isVillager = unitPrefab.GetComponent<VillagerController>() != null ||
                          unitPrefab.GetComponentInChildren<VillagerController>(true) != null;
        bool isCombatUnit = unitPrefab.GetComponent<BaseCombatUnitController>() != null ||
                            unitPrefab.GetComponentInChildren<BaseCombatUnitController>(true) != null;

        return isCombatUnit && !isVillager;
    }

    private void MoveSpawnedUnit(VillagerController villager, BaseCombatUnitController combatUnit, Vector3 destination)
    {
        destination = GetNearestNavMeshPoint(destination);

        if (villager != null)
        {
            villager.CommandMoveTo(destination);
            return;
        }

        MoveSpawnedCombatUnit(combatUnit, destination);
    }

    private void MoveSpawnedCombatUnit(BaseCombatUnitController combatUnit, Vector3 destination)
    {
        if (combatUnit != null)
        {
            combatUnit.CommandMove(GetNearestNavMeshPoint(destination));
        }
    }

    private Vector3 GetNearestNavMeshPoint(Vector3 point)
    {
        if (NavMesh.SamplePosition(point, out NavMeshHit hit, 8f, ~2))
        {
            return hit.position;
        }

        return point;
    }

    private void MoveRallyFlag(Vector3 position)
    {
        if (_rallyFlagPrefab == null)
        {
            return;
        }

        Vector3 markerPosition = position + Vector3.up * _rallyFlagYOffset;

        if (_rallyFlagInstance == null)
        {
            _rallyFlagInstance = Instantiate(_rallyFlagPrefab, markerPosition, Quaternion.identity);
            _rallyFlagInstance.name = $"{gameObject.name}_RallyFlag";
        }
        else
        {
            _rallyFlagInstance.transform.position = markerPosition;
            _rallyFlagInstance.SetActive(true);
        }
    }

    [System.Serializable]
    private class ProductionSaveState
    {
        public string currentProducingUnitName = "";
        public float currentProductionTimer = 0f;
        public List<string> queuedUnitNames = new List<string>();
        public bool hasRallyPoint = false;
        public Vector3 rallyPoint = Vector3.zero;
    }

    public string CaptureState()
    {
        var state = new ProductionSaveState
        {
            currentProducingUnitName = _currentProducingUnit != null ? (_currentProducingUnit.unitName ?? _currentProducingUnit.name) : "",
            currentProductionTimer = _currentProductionTimer,
            hasRallyPoint = _rallyPoint.HasValue,
            rallyPoint = _rallyPoint ?? Vector3.zero
        };

        foreach (var unit in _productionQueue)
        {
            if (unit != null)
            {
                state.queuedUnitNames.Add(unit.unitName ?? unit.name);
            }
        }

        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        var state = JsonUtility.FromJson<ProductionSaveState>(stateJson);
        if (state == null) return;

        _currentProducingUnit = FindUnitData(state.currentProducingUnitName);
        _currentProductionTimer = state.currentProductionTimer;
        _isProducing = _currentProducingUnit != null;

        _productionQueue.Clear();
        if (state.queuedUnitNames != null)
        {
            foreach (var name in state.queuedUnitNames)
            {
                var unit = FindUnitData(name);
                if (unit != null)
                {
                    _productionQueue.Enqueue(unit);
                }
            }
        }

        if (state.hasRallyPoint)
        {
            SetRallyPoint(state.rallyPoint);
        }
        else
        {
            _rallyPoint = null;
            _rallyResource = null;
            _rallyAttackTarget = null;
            if (_rallyFlagInstance != null)
            {
                _rallyFlagInstance.SetActive(false);
            }
        }
    }

    private UnitData FindUnitData(string unitName)
    {
        if (string.IsNullOrEmpty(unitName) || BuildingManager.Instance == null) return null;

        // Tìm trong Main Building
        var result = FindUnitDataInBuilding(BuildingManager.Instance.MainBuildingData, unitName);
        if (result != null) return result;

        // Tìm trong các building khác
        foreach (var building in BuildingManager.Instance.AvailableBuildings)
        {
            result = FindUnitDataInBuilding(building, unitName);
            if (result != null) return result;
        }

        return null;
    }

    private UnitData FindUnitDataInBuilding(BuildingData building, string unitName)
    {
        if (building == null || building.producibleUnits == null) return null;
        foreach (var unit in building.producibleUnits)
        {
            if (unit == null) continue;
            if (unit.unitName == unitName || unit.name == unitName)
            {
                return unit;
            }
        }
        return null;
    }
}
