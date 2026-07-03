using FischlWorks_FogWar;
using UnityEngine; 
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class BuildingManager : MonoBehaviour
{
    private static BuildingManager s_instance;
    public static BuildingManager Instance
    {
        get => s_instance;
        private set => s_instance = value;
    }

    [System.Obsolete("Use Instance instead")]
    public static BuildingManager buildingManager
    {
        get => Instance;
        set => Instance = value;
    }

    private GridSystem _gridSystem;

    [Header("Building Settings")]
    [Tooltip("Danh sách các công trình để test (nhấn 1, 2, 3, 4)")]
    [UnityEngine.Serialization.FormerlySerializedAs("availableBuildings")]
    [SerializeField] private List<BuildingData> _availableBuildings = new List<BuildingData>();
    public List<BuildingData> AvailableBuildings => _availableBuildings;

    [System.Obsolete("Use AvailableBuildings instead")]
    public List<BuildingData> availableBuildings
    {
        get => _availableBuildings;
        set => _availableBuildings = value;
    }

    [Tooltip("Dữ liệu công trình chính lúc bắt đầu game")]
    [UnityEngine.Serialization.FormerlySerializedAs("mainBuildingData")]
    [SerializeField] private BuildingData _mainBuildingData;
    public BuildingData MainBuildingData => _mainBuildingData;

    [System.Obsolete("Use MainBuildingData instead")]
    public BuildingData mainBuildingData
    {
        get => _mainBuildingData;
        set => _mainBuildingData = value;
    }
    
    [Tooltip("Công trình hiện đang được chọn để xây")]
    [UnityEngine.Serialization.FormerlySerializedAs("currentSelectedBuilding")]
    [SerializeField] private BuildingData _currentSelectedBuilding;
    public BuildingData CurrentSelectedBuilding
    {
        get => _currentSelectedBuilding;
        set => _currentSelectedBuilding = value;
    }

    [System.Obsolete("Use CurrentSelectedBuilding instead")]
    public BuildingData currentSelectedBuilding
    {
        get => _currentSelectedBuilding;
        set => _currentSelectedBuilding = value;
    }
    
    [UnityEngine.Serialization.FormerlySerializedAs("ghostMaterial")]
    [SerializeField] private Material _ghostMaterial;

    [Header("Fog Of War")]
    [Tooltip("Only allow placing buildings inside currently visible fog of war areas.")]
    [SerializeField] private bool _requireVisibleAreaToBuild = true;

    [Tooltip("Extra fog visibility radius checked around each occupied build cell.")]
    [SerializeField] private int _buildVisibilityPadding = 0;
    
    [Header("Construction Effects")]
    [Tooltip("Prefab hàng rào móng nhà hiển thị khi đang xây")]
    [UnityEngine.Serialization.FormerlySerializedAs("constructionFencePrefab")]
    [SerializeField] private GameObject _constructionFencePrefab;
    public GameObject ConstructionFencePrefab
    {
        get => _constructionFencePrefab;
        set => _constructionFencePrefab = value;
    }

    [System.Obsolete("Use ConstructionFencePrefab instead")]
    public GameObject constructionFencePrefab
    {
        get => _constructionFencePrefab;
        set => _constructionFencePrefab = value;
    }
    
    [Header("Mode Status")]
    [UnityEngine.Serialization.FormerlySerializedAs("isBuildMode")]
    [SerializeField] private bool _isBuildMode = false;
    public bool IsBuildMode
    {
        get => _isBuildMode;
        set => _isBuildMode = value;
    }

    [System.Obsolete("Use IsBuildMode instead")]
    public bool isBuildMode
    {
        get => _isBuildMode;
        set => _isBuildMode = value;
    }

    [UnityEngine.Serialization.FormerlySerializedAs("isDeleteMode")]
    [SerializeField] private bool _isDeleteMode = false;
    public bool IsDeleteMode
    {
        get => _isDeleteMode;
        set => _isDeleteMode = value;
    }

    [System.Obsolete("Use IsDeleteMode instead")]
    public bool isDeleteMode
    {
        get => _isDeleteMode;
        set => _isDeleteMode = value;
    }
    
    private GameObject _ghostBuilding; 
    private Renderer[] _ghostRenderers;
    private int _currentRotationIndex = 0; // Chỉ số góc xoay công trình: 0 = 0 độ, 1 = 90 độ, 2 = 180 độ, 3 = 270 độ
    private csFogWar _fogWar;
    private bool _blockPlacementThisFrame = false;

    private int _lastPreviewX = -999;
    private int _lastPreviewZ = -999;
    private int _lastPreviewRotation = -1;

    // Dictionary để quản lý nhà nào đang nằm trên ô nào
    private Dictionary<GridCell, GameObject> _builtStructures = new Dictionary<GridCell, GameObject>();

    public GameObject GetBuildingAtCell(GridCell cell)
    {
        if (cell != null && _builtStructures.TryGetValue(cell, out GameObject building))
        {
            return building;
        }
        return null;
    }

    public void CollectOccupiedBuildingCells(HashSet<Vector2Int> cells, int paddingCells = 0)
    {
        if (cells == null)
        {
            return;
        }

        int padding = Mathf.Max(0, paddingCells);
        foreach (var kvp in _builtStructures)
        {
            GridCell cell = kvp.Key;
            if (cell == null || kvp.Value == null)
            {
                continue;
            }

            for (int dx = -padding; dx <= padding; dx++)
            {
                for (int dz = -padding; dz <= padding; dz++)
                {
                    cells.Add(new Vector2Int(cell.x + dx, cell.z + dz));
                }
            }
        }
    }

    public int CollectGrassExclusionZones(Vector4[] zones, int paddingCells = 0)
    {
        if (zones == null || zones.Length == 0)
        {
            return 0;
        }

        float cellSize = _gridSystem != null ? _gridSystem.GetCellSize() : 2f;
        float padding = Mathf.Max(0, paddingCells) * cellSize;
        int count = 0;

        foreach (var kvp in _buildingDataMap)
        {
            GameObject building = kvp.Key;
            BuildingData data = kvp.Value;
            if (building == null || data == null)
            {
                continue;
            }

            Vector2Int size = data.buildingSize;
            float normalizedYaw = Mathf.Repeat(building.transform.eulerAngles.y, 180f);
            if (normalizedYaw > 45f && normalizedYaw < 135f)
            {
                size = new Vector2Int(size.y, size.x);
            }

            float halfX = Mathf.Max(cellSize * 0.5f, size.x * cellSize * 0.5f + padding);
            float halfZ = Mathf.Max(cellSize * 0.5f, size.y * cellSize * 0.5f + padding);
            zones[count] = new Vector4(building.transform.position.x, building.transform.position.z, halfX, halfZ);
            count++;

            if (count >= zones.Length)
            {
                break;
            }
        }

        return count;
    }
    
    // Lưu trữ BuildingData tương ứng của mỗi GameObject
    private Dictionary<GameObject, BuildingData> _buildingDataMap = new Dictionary<GameObject, BuildingData>();
    public Dictionary<GameObject, BuildingData> BuildingDataMap => _buildingDataMap;

    private GameObject _mainBuildingInstance;
    public GameObject MainBuildingInstance => _mainBuildingInstance;

    [System.Obsolete("Use BuildingDataMap instead")]
    public Dictionary<GameObject, BuildingData> buildingDataMap
    {
        get => _buildingDataMap;
        set => _buildingDataMap = value;
    }

    // Đếm số lượng từng loại nhà đã được xây dựng thành công (dùng cho hệ thống yêu cầu công trình)
    private Dictionary<BuildingData, int> _builtBuildingCounts = new Dictionary<BuildingData, int>();
    public Dictionary<BuildingData, int> BuiltBuildingCounts => _builtBuildingCounts;

    [System.Obsolete("Use BuiltBuildingCounts instead")]
    public Dictionary<BuildingData, int> builtBuildingCounts
    {
        get => _builtBuildingCounts;
        set => _builtBuildingCounts = value;
    }

        
    void Awake()
    {
        if(s_instance == null)
        {
            s_instance = this; 
            DontDestroyOnLoad(gameObject);
            EnsureRuntimeComponents();
        }
        else
        {
            Destroy(gameObject);
        } 
        _gridSystem = FindAnyObjectByType<GridSystem>();
     
    }

    void Start()
    {
        EnsureRuntimeComponents();

        // Tự động gán constructionFencePrefab trong editor nếu chưa gán
#if UNITY_EDITOR
        if (_constructionFencePrefab == null)
        {
            _constructionFencePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building/mongsnhaf.prefab");
            if (_constructionFencePrefab != null)
            {
                Debug.Log($"[BuildingManager] Tự động tải thành công prefab hàng rào: {_constructionFencePrefab.name}");
            }
        }
#endif

        // Khởi tạo ghost building nếu đã có currentSelectedBuilding
        if (CurrentSelectedBuilding != null)
        {
            SelectBuilding(CurrentSelectedBuilding);
        }
    }

    private void EnsureRuntimeComponents()
    {
        // Keep build-related helper UI/overlays plug-and-play on persistent managers.
        BuildingSelectionUI legacyBuildUi = GetComponent<BuildingSelectionUI>();
        if (legacyBuildUi != null)
        {
            legacyBuildUi.enabled = false;
        }

        if (GetComponent<WatchTowerGarrisonUI>() == null)
        {
            gameObject.AddComponent<WatchTowerGarrisonUI>();
        }

        if (GetComponent<MainBuildingUI>() == null)
        {
            gameObject.AddComponent<MainBuildingUI>();
        }

        if (GetComponent<BuildGridOverlay>() == null)
        {
            gameObject.AddComponent<BuildGridOverlay>();
        }

        if (GetComponent<MarketUI>() == null)
        {
            gameObject.AddComponent<MarketUI>();
        }

        SetBuildingMenuVisible(IsBuildMode);
    }

    /// <summary>
    /// Registers a building that was spawned directly in the world (e.g., at game start).
    /// </summary>
    public void RegisterSpawnedBuilding(GameObject building, BuildingData data, int startX, int startZ, int rotationIndex = 0)
    {
        if (building == null || data == null || _gridSystem == null) return;

        Vector2Int size = data.buildingSize;
        if (rotationIndex % 2 != 0)
        {
            size = new Vector2Int(size.y, size.x);
        }

        _buildingDataMap[building] = data;

        // Populate cells
        for (int x = startX; x < startX + size.x; x++)
        {
            for (int z = startZ; z < startZ + size.y; z++)
            {
                GridCell cell = _gridSystem.GetCell(x, z);
                if (cell != null)
                {
                    _builtStructures[cell] = building;
                    cell.isBuildable = false;
                    cell.isWalkable = false;
                }
            }
        }

        // Ensure ConstructibleBuilding is completed and registered
        ConstructibleBuilding cb = building.GetComponent<ConstructibleBuilding>();
        if (cb == null)
        {
            cb = building.AddComponent<ConstructibleBuilding>();
        }
        cb.TotalBuildTime = data.buildTime;
        cb.buildingGridSize = data.buildingSize;
        cb.IsInstantBuild = true;
        cb.IsCompleted = true;
        cb.CurrentProgress = 1f;

        OnBuildingCompleted(cb);
    }


    // Hàm gọi khi người dùng muốn đổi loại nhà sẽ xây
    public void SelectBuilding(BuildingData buildingData)
    {
        _blockPlacementThisFrame = true; // Chặn đặt nhà trong frame chọn UI
        _currentSelectedBuilding = buildingData;
        _currentRotationIndex = 0; // Reset góc xoay về 0 khi chọn công trình mới

        _lastPreviewX = -999;
        _lastPreviewZ = -999;
        _lastPreviewRotation = -1;

        if (_ghostBuilding != null)
        {
            Destroy(_ghostBuilding);
        }

        if (CurrentSelectedBuilding != null && CurrentSelectedBuilding.buildingPrefab != null)
        {
            _ghostBuilding = Instantiate(CurrentSelectedBuilding.buildingPrefab);   
            DisableGhostGameplayComponents(_ghostBuilding);
            SetupGhostRenderers();

            Collider[] colliders = _ghostBuilding.GetComponentsInChildren<Collider>();
            foreach (var col in colliders)
            {
                Destroy(col);
            }

            // Tiêu diệt NavMeshObstacle để tránh đục lỗ NavMesh di động làm kẹt dân làng
            UnityEngine.AI.NavMeshObstacle[] obstacles = _ghostBuilding.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>();
            foreach (var obs in obstacles)
            {
                Destroy(obs);
            }

            _ghostBuilding.SetActive(IsBuildMode);
        }
    }

    void Update()
    {
        HandleModeSwitchInput();
        InteractWithGrid();
        _blockPlacementThisFrame = false;
    }

    private void HandleModeSwitchInput()
    {
        // Nhấn B để chuyển đổi Chế độ Xây
        if(Keyboard.current.bKey.wasPressedThisFrame)
        {
            _isBuildMode = !_isBuildMode;
            if(IsBuildMode)
            {
                _isDeleteMode = false;
                if (_ghostBuilding == null && CurrentSelectedBuilding != null)
                {
                    SelectBuilding(CurrentSelectedBuilding);
                }
                SetBuildingMenuVisible(true);
                Debug.Log("CHẾ ĐỘ XÂY DỰNG: Đã BẬT");
            }
            else
            {
                CancelBuildMode();
                Debug.Log("CHẾ ĐỘ XÂY DỰNG: Đã TẮT");
            }
        }    

        // Nhấn C để chuyển đổi Chế độ Xóa
        if(Keyboard.current.cKey.wasPressedThisFrame)
        {
            _isDeleteMode = !_isDeleteMode;
            if(IsDeleteMode)
            {
                _isBuildMode = false;
                if (_ghostBuilding != null)
                {
                    Destroy(_ghostBuilding);
                    _ghostBuilding = null;
                }
                _currentSelectedBuilding = null;
                SetBuildingMenuVisible(false);
                Debug.Log("CHẾ ĐỘ PHÁ HỦY: Đã BẬT");
            }
            else
            {
                Debug.Log("CHẾ ĐỘ PHÁ HỦY: Đã TẮT");
            }
        }

        // Test UI: Phím 1,2,3,4 để đổi nhà khi đang bật chế độ xây
        if (IsBuildMode)
        {
            // Nhấn phím R để xoay công trình 90 độ
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                _currentRotationIndex = (_currentRotationIndex + 1) % 4;
                Debug.Log($"Xoay công trình: {_currentRotationIndex * 90} độ");
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame && AvailableBuildings.Count > 0)
            {
                SelectBuilding(AvailableBuildings[0]);
                Debug.Log($"Đã chọn: {AvailableBuildings[0].buildingName}");
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame && AvailableBuildings.Count > 1)
            {
                SelectBuilding(AvailableBuildings[1]);
                Debug.Log($"Đã chọn: {AvailableBuildings[1].buildingName}");
            }
            if (Keyboard.current.digit3Key.wasPressedThisFrame && AvailableBuildings.Count > 2)
            {
                SelectBuilding(AvailableBuildings[2]);
                Debug.Log($"Đã chọn: {AvailableBuildings[2].buildingName}");
            }
            if (Keyboard.current.digit4Key.wasPressedThisFrame && AvailableBuildings.Count > 3)
            {
                SelectBuilding(AvailableBuildings[3]);
                Debug.Log($"Đã chọn: {AvailableBuildings[3].buildingName}");
            }
        }
    }

    private void InteractWithGrid()
    {
        // Kiểm tra hủy bỏ chế độ đặt/phá (Click chuột phải hoặc ấn Escape)
        bool cancelPressed = Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape);
        #if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            cancelPressed = true;
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame)
            cancelPressed = true;
        #endif

        if (cancelPressed && (IsBuildMode || IsDeleteMode))
        {
            CancelBuildMode();
            return;
        }

        if (_blockPlacementThisFrame)
        {
            if (_ghostBuilding != null) _ghostBuilding.SetActive(false);
            return;
        }

        if (_gridSystem == null || (!IsBuildMode && !IsDeleteMode)) 
        {
            if (_ghostBuilding != null) _ghostBuilding.SetActive(false);
            return;
        }

        // Lấy vị trí input hiện tại (hỗ trợ cả Touch và Mouse)
        Vector2 inputPos = Vector2.zero;
        bool isPointerDown = false;

        bool isTouch = Input.touchCount > 0;
        if (isTouch)
        {
            Touch touch = Input.GetTouch(0);
            inputPos = touch.position;
            isPointerDown = (touch.phase == UnityEngine.TouchPhase.Ended);
        }
        else
        {
            inputPos = Input.mousePosition;
            isPointerDown = Input.GetMouseButtonDown(0);
        }

        // NGĂN CLICK XUYÊN QUA UI (UI Click-Through)
        if (IsPointerOverUI(inputPos))
        {
            if (_ghostBuilding != null)
            {
                _ghostBuilding.SetActive(false);
            }
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(inputPos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            _gridSystem.GetXY(hit.point, out int gridX, out int gridZ);
            GridCell centerCell = _gridSystem.GetCell(gridX, gridZ);

            if (centerCell != null)
            {
                if (IsBuildMode)
                {
                    if (CurrentSelectedBuilding == null) return;
                    
                    Vector2Int size = CurrentSelectedBuilding.buildingSize;
                    if (_currentRotationIndex % 2 != 0) // 90 hoặc 270 độ -> đảo chiều X và Y trên Grid
                    {
                        size = new Vector2Int(size.y, size.x);
                    }

                    // Trừ đi một nửa kích thước để chuột/ngón tay (centerCell) luôn nằm ở giữa công trình
                    int startX = gridX - size.x / 2;
                    int startZ = gridZ - size.y / 2;

                    bool canBuild = CheckBuildingArea(startX, startZ, size, out List<GridCell> cellsToOccupy);

                    // Đổi màu Ghost để báo hiệu (Xanh = Phù hợp, Đỏ = Trái phép / Có vật cản)
                    SetGhostColor(canBuild ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f));

                    Vector3 centerPos = CalculateBuildingCenter(startX, startZ, centerCell.elevation, size);
                    if (_ghostBuilding != null)
                    {
                        _ghostBuilding.transform.position = centerPos;
                        _ghostBuilding.transform.rotation = Quaternion.Euler(0, _currentRotationIndex * 90f, 0);
                        _ghostBuilding.SetActive(true);
                    }

                    if (isPointerDown)
                    {
                        if (isTouch)
                        {
                            // Mobile double-tap/confirmation logic:
                            if (gridX == _lastPreviewX && gridZ == _lastPreviewZ && _currentRotationIndex == _lastPreviewRotation)
                            {
                                TryBuild(startX, startZ, cellsToOccupy, centerPos, canBuild, CurrentSelectedBuilding);
                            }
                            else
                            {
                                _lastPreviewX = gridX;
                                _lastPreviewZ = gridZ;
                                _lastPreviewRotation = _currentRotationIndex;
                                Debug.Log($"[BuildingManager] Touch preview placed at ({gridX}, {gridZ}). Tap again on same tile to construct.");
                            }
                        }
                        else
                        {
                            // Mouse (PC): build instantly
                            TryBuild(startX, startZ, cellsToOccupy, centerPos, canBuild, CurrentSelectedBuilding);
                        }
                    }
                }
                else if (IsDeleteMode)
                {
                    if (isPointerDown)
                    {
                        DeleteBuilding(centerCell);
                    }
                }
            }
        }
        else
        {
            if (_ghostBuilding != null) _ghostBuilding.SetActive(false);
        }
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = screenPos;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        bool isOverUGUI = results.Count > 0;
        bool isOverIMGUI = BuildingSelectionUI.Instance != null && BuildingSelectionUI.Instance.IsMouseOverUI();
        return isOverUGUI || isOverIMGUI;
    }

    private Vector3 CalculateBuildingCenter(int startX, int startZ, int elevation, Vector2Int size)
    {
        // Tính toán độ lệch (Offset) để lấy điểm chính giữa Tâm của công trình (nhiều ô)
        Vector3 startPos = _gridSystem.GetWorldPosition(startX, startZ, elevation);
        float offset_x = size.x * _gridSystem.GetCellSize() / 2f;
        float offset_z = size.y * _gridSystem.GetCellSize() / 2f;
        
        // Không nâng Y lên nữa vì hệ thống đang dùng Unity Terrain, mặt đất đã chính xác
        return startPos + new Vector3(offset_x, 0f, offset_z);
    }

    private bool CheckBuildingArea(int startX, int startZ, Vector2Int size, out List<GridCell> cells)
    {
        cells = new List<GridCell>();
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        bool isBridge = (_currentSelectedBuilding != null && _currentSelectedBuilding.buildingName == "Bridge");

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                GridCell cell = _gridSystem.GetCell(startX + x, startZ + z);
                
                // 1. Ô lưới phải hợp lệ
                if (cell == null)
                {
                    return false;
                }

                if (!cell.isBuildable)
                {
                    // Ngoại lệ đối với cầu: Cho phép xây trên mặt nước sông
                    bool isWater = false;
                    if (Terrain.activeTerrain != null)
                    {
                        float worldX = (startX + x) * _gridSystem.GetCellSize();
                        float worldZ = (startZ + z) * _gridSystem.GetCellSize();
                        float h = Terrain.activeTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ));
                        if (h < _gridSystem.WaterHeight)
                        {
                            isWater = true;
                        }
                    }

                    if (!isBridge || !isWater || cell.hasResource || _builtStructures.ContainsKey(cell))
                    {
                        return false;
                    }
                }
                else
                {
                    if (cell.hasResource || _builtStructures.ContainsKey(cell))
                    {
                        return false;
                    }
                }

                if (!IsBuildCellVisible(startX + x, startZ + z))
                {
                    return false;
                }
                
                // 2. Lấy độ cao thực tế từ Terrain
                if (Terrain.activeTerrain != null)
                {
                    float h = Terrain.activeTerrain.SampleHeight(_gridSystem.GetWorldPosition(startX + x, startZ + z));
                    if (h < minY) minY = h;
                    if (h > maxY) maxY = h;
                }

                cells.Add(cell);
            }
        }
        
        // KHÔNG CHO XÂY NẾU ĐẤT QUÁ DỐC (Chênh lệch độ cao > 2.5m) - Ngoại lệ đối với cầu gỗ
        if (!isBridge && Terrain.activeTerrain != null && (maxY - minY > 2.5f))
        {
            return false; // Báo đỏ, không cho xây trên vách núi dựng đứng
        }

        return true;
    }

    private void DisableGhostGameplayComponents(GameObject ghost)
    {
        if (ghost == null)
        {
            return;
        }

        // Vô hiệu hóa TorchStandController trên ghost để tránh tự động bật đèn
        TorchStandController[] torchControllers = ghost.GetComponentsInChildren<TorchStandController>(true);
        foreach (TorchStandController controller in torchControllers)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        // Tắt toàn bộ đèn Point Light thực tế trên ghost
        Light[] lights = ghost.GetComponentsInChildren<Light>(true);
        foreach (Light l in lights)
        {
            if (l != null)
            {
                l.enabled = false;
            }
        }

        // Tắt toàn bộ đối tượng vòng sáng giả FakeLight trên ghost
        Transform[] allTransforms = ghost.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allTransforms)
        {
            if (t != null && t.gameObject.name == "FakeLight")
            {
                t.gameObject.SetActive(false);
            }
        }

        VisionSource[] visionSources = ghost.GetComponentsInChildren<VisionSource>(true);
        foreach (VisionSource source in visionSources)
        {
            if (source != null)
            {
                DestroyImmediate(source);
            }
        }

        // Vô hiệu hóa WoodGateController trên ghost để tránh việc tự động mở/đóng cửa khi đang xem trước
        WoodGateController[] gateControllers = ghost.GetComponentsInChildren<WoodGateController>(true);
        foreach (WoodGateController controller in gateControllers)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        // Vô hiệu hóa BaseCombatUnitController (ví dụ BuildingCombatTarget) trên ghost để tránh tự đăng ký vào Registry chiến đấu
        BaseCombatUnitController[] combatControllers = ghost.GetComponentsInChildren<BaseCombatUnitController>(true);
        foreach (BaseCombatUnitController controller in combatControllers)
        {
            if (controller != null)
            {
                controller.enabled = false;
            }
        }

        ConstructibleBuilding[] constructibleBuildings = ghost.GetComponentsInChildren<ConstructibleBuilding>(true);
        foreach (ConstructibleBuilding building in constructibleBuildings)
        {
            if (building != null)
            {
                building.IsCompleted = false;
                building.enabled = false;

                // Reset vị trí của _VisualContainer về 0 để mô hình ghost không bị chôn dưới đất khi chưa xây
                Transform visualContainer = building.transform.Find("_VisualContainer");
                if (visualContainer != null)
                {
                    visualContainer.localPosition = Vector3.zero;
                }
            }
        }

        // Vô hiệu hóa RiceField trên ghost và hiển thị ô vuông đất trồng làm footprint xem trước
        RiceField[] riceFields = ghost.GetComponentsInChildren<RiceField>(true);
        foreach (RiceField rf in riceFields)
        {
            if (rf != null)
            {
                rf.enabled = false;

                // Thay vì tắt tất cả child của rf.transform (làm tắt nhầm _VisualContainer), 
                // ta dùng reflection chỉnh trạng thái hiển thị của các Visual con tương ứng
                System.Reflection.FieldInfo emptyField = typeof(RiceField).GetField(
                    "_emptyVisual",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.FieldInfo growingField = typeof(RiceField).GetField(
                    "_growingVisual",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Reflection.FieldInfo ripeField = typeof(RiceField).GetField(
                    "_ripeVisual",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

                if (emptyField != null)
                {
                    GameObject emptyGO = emptyField.GetValue(rf) as GameObject;
                    if (emptyGO != null) emptyGO.SetActive(true);
                }
                if (growingField != null)
                {
                    GameObject growingGO = growingField.GetValue(rf) as GameObject;
                    if (growingGO != null) growingGO.SetActive(false);
                }
                if (ripeField != null)
                {
                    GameObject ripeGO = ripeField.GetValue(rf) as GameObject;
                    if (ripeGO != null) ripeGO.SetActive(false);
                }
            }
        }
    }

    private void SetupGhostRenderers()
    {
        if (_ghostBuilding == null)
        {
            _ghostRenderers = null;
            return;
        }

        _ghostRenderers = _ghostBuilding.GetComponentsInChildren<Renderer>(true);
        if (_ghostMaterial == null)
        {
            return;
        }

        foreach (Renderer ghostRenderer in _ghostRenderers)
        {
            if (ghostRenderer == null)
            {
                continue;
            }

            Material[] materials = ghostRenderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = new Material(_ghostMaterial);
            }
            ghostRenderer.materials = materials;
        }
    }

    private void SetGhostColor(Color color)
    {
        if (_ghostRenderers == null)
        {
            return;
        }

        foreach (Renderer ghostRenderer in _ghostRenderers)
        {
            if (ghostRenderer == null)
            {
                continue;
            }

            foreach (Material material in ghostRenderer.materials)
            {
                if (material != null)
                {
                    material.color = color;
                }
            }
        }
    }

    private bool IsBuildCellVisible(int gridX, int gridZ)
    {
        if (!_requireVisibleAreaToBuild)
        {
            return true;
        }

        csFogWar fogWar = GetFogWar();
        if (fogWar == null || !fogWar.enabled || !fogWar.gameObject.activeInHierarchy)
        {
            return true;
        }

        Vector3 worldPosition = _gridSystem.GetWorldPosition(gridX, gridZ, 0);
        if (!fogWar.CheckWorldGridRange(worldPosition))
        {
            return false;
        }

        return fogWar.CheckVisibility(worldPosition, _buildVisibilityPadding);
    }

    private csFogWar GetFogWar()
    {
        if (_fogWar == null)
        {
            _fogWar = FindAnyObjectByType<csFogWar>(FindObjectsInactive.Include);
        }

        return _fogWar;
    }
    
    private void TryBuild(int startX, int startZ, List<GridCell> cellsToOccupy, Vector3 centerLocation, bool canBuild, BuildingData data)
    {
        if (!canBuild || data == null || data.buildingPrefab == null)
        {
            Debug.LogWarning("Không thể xây ở đây: Không đủ chỗ, vướng vật cản, hoặc địa hình không bằng phẳng!");
            return;
        }

        Vector2Int size = data.buildingSize;
        if (_currentRotationIndex % 2 != 0) // 90 hoặc 270 độ -> đảo chiều X và Y trên Grid
        {
            size = new Vector2Int(size.y, size.x);
        }
        
        // KIỂM TRA ĐIỀU KIỆN CÔNG TRÌNH YÊU CẦU
        if (data.requiredBuildings != null && data.requiredBuildings.Count > 0)
        {
            foreach (var reqBuilding in data.requiredBuildings)
            {
                if (!_builtBuildingCounts.ContainsKey(reqBuilding) || _builtBuildingCounts[reqBuilding] <= 0)
                {
                    Debug.LogWarning($"Không thể xây! Bạn cần phải xây '{reqBuilding.buildingName}' trước.");
                    return; // Bắt buộc phải có công trình yêu cầu
                }
            }
        }

        // KIỂM TRA VÀ TRỪ TÀI NGUYÊN
        if (!ResourceManager.Instance.CanAfford(data.buildCosts))
        {
            Debug.LogWarning("Không thể xây ở đây: Không đủ tài nguyên!");
            return;
        }

        ResourceManager.Instance.ConsumeCosts(data.buildCosts);
        
        // BƯỚC QUAN TRỌNG: ỦI PHẲNG MẶT ĐẤT! (Ngoại trừ cầu gỗ)
        if (data.buildingName != "Bridge")
        {
            _gridSystem.FlattenRectArea(startX, startZ, size.x, size.y);
        }

        // Đợi 1 chút xíu hoặc tính toán trực tiếp CenterPos lại vì mặt đất vừa bị lún xuống/nâng lên
        Vector3 finalCenterPos = CalculateBuildingCenter(startX, startZ, 0, size);
        
        GameObject newBuilding = Instantiate(data.buildingPrefab, finalCenterPos, Quaternion.Euler(0, _currentRotationIndex * 90f, 0));
        _buildingDataMap[newBuilding] = data;

        // Áp dụng BuildingData trực tiếp cho BuildingProduction nếu có để tránh lỗi thứ tự khởi tạo
        BuildingProduction prod = newBuilding.GetComponent<BuildingProduction>();
        if (prod == null) prod = newBuilding.GetComponentInChildren<BuildingProduction>();
        if (prod != null)
        {
            prod.SetBuildingData(data);
        }

        // Thêm component ConstructibleBuilding nếu chưa có để kích hoạt tính năng xây dựng từ từ trồi từ dưới đất lên
        ConstructibleBuilding cb = newBuilding.GetComponent<ConstructibleBuilding>();
        if (cb == null)
        {
            cb = newBuilding.AddComponent<ConstructibleBuilding>();
        }
        cb.TotalBuildTime = data.buildTime;
        cb.buildingGridSize = data.buildingSize;
        cb.ConstructionFencePrefab = _constructionFencePrefab;

        if (newBuilding.GetComponent<FogVisibilityTarget>() == null)
        {
            newBuilding.AddComponent<FogVisibilityTarget>();
        }

        bool isInstant = data.isInstantBuild || 
                         (data.buildingPrefab != null && (
                             data.buildingPrefab.name.ToLower().Contains("torch") || data.buildingPrefab.name.ToLower().Contains("đoốc") || data.buildingPrefab.name.ToLower().Contains("đuốc") || data.buildingPrefab.name.ToLower().Contains("duoc") ||
                             data.buildingPrefab.name.ToLower().Contains("fence") || data.buildingPrefab.name.ToLower().Contains("gate") || data.buildingPrefab.name.ToLower().Contains("rào") || data.buildingPrefab.name.ToLower().Contains("cổng") ||
                             data.buildingPrefab.name.ToLower().Contains("bridge") || data.buildingPrefab.name.ToLower().Contains("cầu")
                         )) ||
                         (data.buildingName != null && (
                             data.buildingName.ToLower().Contains("torch") || data.buildingName.ToLower().Contains("đoốc") || data.buildingName.ToLower().Contains("đuốc") || data.buildingName.ToLower().Contains("duoc") ||
                             data.buildingName.ToLower().Contains("fence") || data.buildingName.ToLower().Contains("gate") || data.buildingName.ToLower().Contains("rào") || data.buildingName.ToLower().Contains("cổng") ||
                             data.buildingName.ToLower().Contains("bridge") || data.buildingName.ToLower().Contains("cầu")
                         ));

        if (isInstant)
        {
            cb.IsInstantBuild = true;
            cb.IsCompleted = true;
            cb.CurrentProgress = 1f;

            // Đảm bảo visual container không bị lún (phòng trường hợp Awake của cb chạy trước khi ta gán cờ)
            Transform visualContainer = newBuilding.transform.Find("_VisualContainer");
            if (visualContainer != null)
            {
                visualContainer.localPosition = Vector3.zero;
            }

            // Đảm bảo NavMeshObstacle được bật ngay lập tức
            UnityEngine.AI.NavMeshObstacle obstacle = newBuilding.GetComponentInChildren<UnityEngine.AI.NavMeshObstacle>();
            if (obstacle != null)
            {
                obstacle.enabled = true;
            }
        }

        // Tự động gắn thành phần chiến đấu nếu chưa có để công trình có thể nhận sát thương và bị tiêu diệt
        BuildingCombatTarget combatTarget = newBuilding.GetComponent<BuildingCombatTarget>();
        if (combatTarget == null)
        {
            combatTarget = newBuilding.AddComponent<BuildingCombatTarget>();
        }
        combatTarget.SetMaxHealth(data != null ? data.maxHealth : 100);
        combatTarget.unitName = data != null ? data.buildingName : "Building";
        combatTarget.SetAttackDamage(0);
        combatTarget.attackRange = 0f;
        combatTarget.attackCooldown = 0f;
        combatTarget.scanRange = 0f;
        combatTarget.autoAggroDuringMove = false;

        AddShelterComponentIfHouse(newBuilding, data);

        bool isBridge = (data.buildingName == "Bridge");
        foreach (var cell in cellsToOccupy)
        {
            _builtStructures[cell] = newBuilding;     // Lưu chung 1 ngôi nhà duy nhất cho tất cả các ô nó chiếm
            cell.isBuildable = false;
            cell.isWalkable = isBridge ? true : false; // Cầu gỗ thì cho phép đi bộ qua
            cell.hasBridge = isBridge;
        }

        if (isBridge)
        {
            int centerX = startX + size.x / 2;
            int centerZ = startZ + size.y / 2;
            bool isVertical = (_currentRotationIndex % 2 == 0);
            _gridSystem.SetWaterObstaclesActive(centerX, centerZ, isVertical, false);
            _gridSystem.BakeNavigationMesh(force: true); // Nướng lại NavMesh để cho phép đi trên cầu gỗ
        }

        if (isInstant)
        {
            OnBuildingCompleted(cb);
            Debug.Log($"[BuildingManager] Xây dựng xong ngay lập tức công trình {data.buildingName} tại [{startX}, {startZ}]!");
        }
        else
        {
            // Ghi nhận nhà đã bắt đầu đặt móng (chưa tăng builtBuildingCounts vì chưa hoàn thành)
            Debug.Log($"Đặt móng xây {data.buildingName} thành công tại [{startX}, {startZ}] - Kích thước {size} (Xoay {_currentRotationIndex * 90} độ). Chờ dân làng đến xây dựng!");
        }

        // Sau khi đặt thành công, dọn dẹp ghost và lựa chọn hiện tại để tránh lưu công trình cũ
        if (_ghostBuilding != null)
        {
            Destroy(_ghostBuilding);
            _ghostBuilding = null;
        }
        _currentSelectedBuilding = null;
        IsBuildMode = false;
        SetBuildingMenuVisible(false);

        _lastPreviewX = -999;
        _lastPreviewZ = -999;
        _lastPreviewRotation = -1;
    }

    /// <summary>
    /// Phá hủy công trình từ bên ngoài (ví dụ khi bị kẻ địch tấn công tiêu diệt).
    /// </summary>
    /// <param name="buildingObj">GameObject của công trình cần phá hủy.</param>
    public void DestroyBuilding(GameObject buildingObj)
    {
        if (buildingObj == null) return;

        // Quét lại toàn bộ Danh sách để nhả ra TẤT CẢ các ô đất mà Nhà này từng ngồi lên
        List<GridCell> cellsToClear = new List<GridCell>();
        foreach (var kvp in _builtStructures)
        {
            if (kvp.Value == buildingObj)
            {
                cellsToClear.Add(kvp.Key);
            }
        }

        BuildingData dataToDestroy = null;
        if (_buildingDataMap.ContainsKey(buildingObj))
        {
            dataToDestroy = _buildingDataMap[buildingObj];
        }

        bool isBridge = (dataToDestroy != null && dataToDestroy.buildingName == "Bridge");

        foreach (var cell in cellsToClear)
        {
            _builtStructures.Remove(cell);
            cell.hasBridge = false;

            if (isBridge)
            {
                // Khôi phục trạng thái ngập nước nguyên bản của dòng sông
                float worldX = cell.x * _gridSystem.GetCellSize();
                float worldZ = cell.z * _gridSystem.GetCellSize();
                float height = Terrain.activeTerrain != null ? Terrain.activeTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) : 0f;
                if (height < _gridSystem.WaterHeight)
                {
                    cell.isWalkable = (height >= _gridSystem.WaterHeight - 0.3f);
                    cell.isBuildable = false;
                }
                else
                {
                    cell.isWalkable = true;
                    cell.isBuildable = true;
                }
            }
            else
            {
                cell.isBuildable = true;
                cell.isWalkable = true;
            }
        }

        if (isBridge)
        {
            int centerX = Mathf.RoundToInt(buildingObj.transform.position.x / _gridSystem.GetCellSize());
            int centerZ = Mathf.RoundToInt(buildingObj.transform.position.z / _gridSystem.GetCellSize());
            float rotY = buildingObj.transform.rotation.eulerAngles.y;
            bool isVertical = (Mathf.Abs(rotY) < 45f || Mathf.Abs(rotY - 180f) < 45f || Mathf.Abs(rotY - 360f) < 45f);
            
            _gridSystem.SetWaterObstaclesActive(centerX, centerZ, isVertical, true);
            _gridSystem.BakeNavigationMesh(force: true); // Nướng lại NavMesh để chặn sông lại
        }

        if (_buildingDataMap.ContainsKey(buildingObj))
        {
            dataToDestroy = _buildingDataMap[buildingObj];

            // Giảm số lượng công trình đã xây
            if (_builtBuildingCounts.ContainsKey(dataToDestroy))
            {
                _builtBuildingCounts[dataToDestroy]--;
            }

            _buildingDataMap.Remove(buildingObj);
        }

        // Nếu là nhà chính bị phá hủy, báo thua hoặc xử lý kết thúc game
        if (buildingObj == _mainBuildingInstance)
        {
            _mainBuildingInstance = null;
            Debug.LogWarning("NHÀ CHÍNH ĐÃ BỊ TIÊU DIỆT! GAME OVER!");
        }

        Destroy(buildingObj);
        Debug.Log($"[BuildingManager] Đã phá hủy công trình {buildingObj.name} chiếm {cellsToClear.Count} ô!");
    }

    private void DeleteBuilding(GridCell hitCell)
    {
        if (_builtStructures.TryGetValue(hitCell, out GameObject buildingToDestroy))
        {
            DestroyBuilding(buildingToDestroy);
        }
        else
        {
             Debug.Log("Không có công trình nào trên ô này để xóa!");
        }
    }   

    public void SpawnMainBuildingAt(int centerGridX, int centerGridZ)
    {
        if (_gridSystem == null || _mainBuildingData == null || _mainBuildingData.buildingPrefab == null) return;

        Vector2Int size = _mainBuildingData.buildingSize;

        // Tính toán vị trí xuất phát để canh giữa (nếu kích thước là số lẻ/chẵn)
        int startX = centerGridX - size.x / 2;
        int startZ = centerGridZ - size.y / 2;

        GridCell centerCell = _gridSystem.GetCell(centerGridX, centerGridZ);
        if (centerCell == null) return;

        int targetElevation = centerCell.elevation;

        List<GridCell> cellsToOccupy = new List<GridCell>();

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                GridCell cell = _gridSystem.GetCell(startX + x, startZ + z);
                if (cell != null)
                {
                    // Dọn dẹp tài nguyên nếu có trên ô để xây nhà chính
                    if (cell.hasResource && cell.resourceObject != null)
                    {
                        Destroy(cell.resourceObject);
                        cell.hasResource = false;
                        cell.resourceObject = null;
                    }
                    cell.isBuildable = true; // Cưỡng ép có thể xây
                    
                    cellsToOccupy.Add(cell);
                }
            }
        }

        Vector3 centerPos = CalculateBuildingCenter(startX, startZ, targetElevation, size);
        
        GameObject newBuilding = Instantiate(_mainBuildingData.buildingPrefab, centerPos, Quaternion.identity);
        _buildingDataMap[newBuilding] = _mainBuildingData;
        _mainBuildingInstance = newBuilding;

        // Áp dụng BuildingData trực tiếp cho BuildingProduction nếu có trên nhà chính
        BuildingProduction prod = newBuilding.GetComponent<BuildingProduction>();
        if (prod == null) prod = newBuilding.GetComponentInChildren<BuildingProduction>();
        if (prod != null)
        {
            prod.SetBuildingData(_mainBuildingData);
        }

        // Tự động gắn và cấu hình thành phần chiến đấu cho Nhà chính để kẻ địch có thể tấn công
        MainBuildingCombatTarget combatTarget = newBuilding.GetComponent<MainBuildingCombatTarget>();
        if (combatTarget == null)
        {
            combatTarget = newBuilding.AddComponent<MainBuildingCombatTarget>();
        }
        combatTarget.SetMaxHealth(_mainBuildingData != null ? _mainBuildingData.maxHealth : 500);


        foreach (var cell in cellsToOccupy)
        {
            _builtStructures[cell] = newBuilding;     
            cell.isBuildable = false;
            cell.isWalkable = false;                 
        }
        
        // Ghi nhận nhà chính đã được tự động xây dựng
        if (!_builtBuildingCounts.ContainsKey(_mainBuildingData))
        {
            _builtBuildingCounts[_mainBuildingData] = 0;
        }
        _builtBuildingCounts[_mainBuildingData]++;
        
        Debug.Log($"Đã tự động xây {_mainBuildingData.buildingName} tại [{centerGridX}, {centerGridZ}]");
    }

    public Vector3 FindNearestDropoff(Vector3 position, ResourceType resourceType)
    {
        Vector3 bestPos = Vector3.zero; 
        float shortestDistance = float.MaxValue;
        bool found = false;

        float cellSize = _gridSystem != null ? _gridSystem.GetCellSize() : 2f;

        foreach (var kvp in _buildingDataMap)
        {
            GameObject buildingObj = kvp.Key;
            BuildingData data = kvp.Value;

            if (buildingObj == null) continue;

            // Bỏ qua công trình nếu nó đang trong quá trình xây dựng chưa hoàn thành
            ConstructibleBuilding cb = buildingObj.GetComponent<ConstructibleBuilding>();
            if (cb != null && !cb.IsCompleted) continue;

            if (data.isStorage)
            {
                // Nếu danh sách rỗng thì nhận mọi loại, nếu không thì phải chứa loại tài nguyên đang vác
                if (data.acceptedResources == null || data.acceptedResources.Count == 0 || data.acceptedResources.Contains(resourceType))
                {
                    Vector3 C = buildingObj.transform.position;
                    float halfWidth = (data.buildingSize.x * cellSize) / 2f;
                    float halfLength = (data.buildingSize.y * cellSize) / 2f;

                    float minX = C.x - halfWidth;
                    float maxX = C.x + halfWidth;
                    float minZ = C.z - halfLength;
                    float maxZ = C.z + halfLength;

                    // Tính điểm mép gần nhất đối với vị trí hiện tại của dân làng
                    float closestX = Mathf.Clamp(position.x, minX, maxX);
                    float closestZ = Mathf.Clamp(position.z, minZ, maxZ);

                    // Nếu điểm này nằm HOÀN TOÀN bên trong, đẩy ra mép gần nhất
                    if (closestX > minX && closestX < maxX && closestZ > minZ && closestZ < maxZ)
                    {
                        float distToMinX = closestX - minX;
                        float distToMaxX = maxX - closestX;
                        float distToMinZ = closestZ - minZ;
                        float distToMaxZ = maxZ - closestZ;

                        float minDist = Mathf.Min(Mathf.Min(distToMinX, distToMaxX), Mathf.Min(distToMinZ, distToMaxZ));

                        if (minDist == distToMinX) closestX = minX;
                        else if (minDist == distToMaxX) closestX = maxX;
                        else if (minDist == distToMinZ) closestZ = minZ;
                        else closestZ = maxZ;
                    }

                    // Đẩy lùi ra ngoài 0.8m để đảm bảo đứng ở vùng NavMesh đi bộ được
                    float margin = 0.8f;
                    if (closestX == minX) closestX -= margin;
                    else if (closestX == maxX) closestX += margin;

                    if (closestZ == minZ) closestZ -= margin;
                    else if (closestZ == maxZ) closestZ += margin;

                    Vector3 candidatePos = new Vector3(closestX, C.y, closestZ);

                    // Tính toán quãng đường thực tế đến điểm mép này
                    float dist = GetPathLength(position, candidatePos);
                    
                    // PHƯƠNG ÁN DỰ PHÒNG CỰC KỲ MẠNH MẼ: Nếu tính toán đường NavMesh thất bại 
                    // (Ví dụ: do NavMeshObstacle của công trình mới xây chưa được nướng lại xong hoặc hơi lệch mép),
                    // chúng ta sẽ dùng khoảng cách hình học 3D (Vector3.Distance) thay thế để đảm bảo kho gần nhất vẫn luôn được chọn!
                    if (dist == float.MaxValue)
                    {
                        dist = Vector3.Distance(position, candidatePos);
                    }

                    if (dist < shortestDistance)
                    {
                        shortestDistance = dist;
                        bestPos = candidatePos;
                        found = true;
                    }
                }
            }
        }

        // Dự phòng: Nếu chưa có kho nào nhận, trả về Vector3.zero để báo hiệu không tìm thấy điểm nộp
        if (!found)
        {
            bestPos = Vector3.zero;
        }

        return bestPos;
    }

    private float GetPathLength(Vector3 start, Vector3 target)
    {
        UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
        if (UnityEngine.AI.NavMesh.CalculatePath(start, target, ~2, path))
        {
            if (path.status == UnityEngine.AI.NavMeshPathStatus.PathComplete || path.status == UnityEngine.AI.NavMeshPathStatus.PathPartial)
            {
                float distance = 0f;
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                return distance;
            }
        }
        return float.MaxValue; // Không có đường đi tới điểm này
    }

    /// <summary>
    /// Được gọi bởi ConstructibleBuilding khi một công trình đã được dân làng hoàn thành xây dựng.
    /// </summary>
    public void OnBuildingCompleted(ConstructibleBuilding cb)
    {
        if (cb == null) return;

        if (_buildingDataMap.TryGetValue(cb.gameObject, out BuildingData data))
        {
            if (!_builtBuildingCounts.ContainsKey(data))
            {
                _builtBuildingCounts[data] = 0;
            }
            _builtBuildingCounts[data]++;

            Debug.Log($"[BuildingManager] '{data.buildingName}' đã hoàn thành xây dựng! Tổng số lượng: {_builtBuildingCounts[data]}. Kích hoạt yêu cầu công nghệ!");
        }
    }

    private void AddShelterComponentIfHouse(GameObject building, BuildingData data)
    {
        if (data != null && (data.buildingName.ToLower().Contains("house") || data.buildingName.ToLower().Contains("home") || building.name.ToLower().Contains("homeblue")))
        {
            HouseShelter shelter = building.GetComponent<HouseShelter>();
            if (shelter == null)
            {
                shelter = building.AddComponent<HouseShelter>();
                
                int capacity = 5;
                // Nếu là nhà lớn hơn thì tăng sức chứa
                if (data.buildingName.ToLower().Contains("large") || data.buildingName.ToLower().Contains("big") || (data.buildingSize.x * data.buildingSize.y > 4))
                {
                    capacity = 10;
                }
                shelter.SetCapacity(capacity);
                Debug.Log($"[BuildingManager] Đã gắn HouseShelter cho {building.name} với sức chứa {capacity} dân.");
            }
        }
    }

    /// <summary>
    /// Đếm số lượng kho chứa Storage đã được xây dựng hoàn tất.
    /// </summary>
    public int GetCompletedStorageCount()
    {
        int count = 0;
        foreach (var kvp in _builtBuildingCounts)
        {
            BuildingData data = kvp.Key;
            int activeCount = kvp.Value;
            if (data != null && activeCount > 0)
            {
                string prefabNameLower = data.buildingPrefab != null ? data.buildingPrefab.name.ToLower() : "";
                if (prefabNameLower == "storage" || (prefabNameLower.Contains("storage") && !prefabNameLower.Contains("food")))
                {
                    count += activeCount;
                }
            }
        }
        return count;
    }

    public void CancelBuildMode()
    {
        IsBuildMode = false;
        IsDeleteMode = false;

        _lastPreviewX = -999;
        _lastPreviewZ = -999;
        _lastPreviewRotation = -1;

        if (_ghostBuilding != null)
        {
            Destroy(_ghostBuilding);
            _ghostBuilding = null;
        }
        _currentSelectedBuilding = null;

        Debug.Log("[BuildingManager] Cancelled build mode.");
        SetBuildingMenuVisible(false);
    }

    private void SetBuildingMenuVisible(bool visible)
    {
        BuildingMenuUI buildingMenu = FindAnyObjectByType<BuildingMenuUI>(FindObjectsInactive.Include);
        if (buildingMenu != null)
        {
            buildingMenu.SetMenuVisible(visible);
        }
    }

    public void ToggleBuildMode()
    {
        _isBuildMode = !_isBuildMode;
        if (_isBuildMode)
        {
            _isDeleteMode = false;
            if (_ghostBuilding == null && CurrentSelectedBuilding != null)
            {
                SelectBuilding(CurrentSelectedBuilding);
            }
            SetBuildingMenuVisible(true);
            Debug.Log("CHẾ ĐỘ XÂY DỰNG: Đã BẬT (từ UI)");
        }
        else
        {
            CancelBuildMode();
            Debug.Log("CHẾ ĐỘ XÂY DỰNG: Đã TẮT (từ UI)");
        }
    }

    public void ToggleDeleteMode()
    {
        _isDeleteMode = !_isDeleteMode;
        if (_isDeleteMode)
        {
            _isBuildMode = false;
            if (_ghostBuilding != null)
            {
                Destroy(_ghostBuilding);
                _ghostBuilding = null;
            }
            _currentSelectedBuilding = null;
            SetBuildingMenuVisible(false);
            Debug.Log("CHẾ ĐỘ PHÁ HỦY: Đã BẬT (từ UI)");
        }
        else
        {
            Debug.Log("CHẾ ĐỘ PHÁ HỦY: Đã TẮT (từ UI)");
        }
    }

    public void RotateGhostBuilding()
    {
        if (IsBuildMode)
        {
            _currentRotationIndex = (_currentRotationIndex + 1) % 4;
            Debug.Log($"Xoay công trình: {_currentRotationIndex * 90} độ (từ UI)");
        }
    }



}
