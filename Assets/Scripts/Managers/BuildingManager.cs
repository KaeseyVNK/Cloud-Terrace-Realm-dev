using FischlWorks_FogWar;
using UnityEngine; 
using UnityEngine.InputSystem;
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
            
            // Tự động thêm Component UI vào để kích hoạt Plug-and-Play mà không cần kéo thả tay
            if (GetComponent<BuildingSelectionUI>() == null)
            {
                gameObject.AddComponent<BuildingSelectionUI>();
            }

            if (GetComponent<WatchTowerGarrisonUI>() == null)
            {
                gameObject.AddComponent<WatchTowerGarrisonUI>();
            }

            if (GetComponent<MainBuildingUI>() == null)
            {
                gameObject.AddComponent<MainBuildingUI>();
            }
        }
        else
        {
            Destroy(gameObject);
        } 
        _gridSystem = FindAnyObjectByType<GridSystem>();
     
    }

    void Start()
    {
        // Tự động gán constructionFencePrefab trong editor nếu chưa gán
#if UNITY_EDITOR
        if (_constructionFencePrefab == null)
        {
            _constructionFencePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/mongsnhaf.prefab");
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

    // Hàm gọi khi người dùng muốn đổi loại nhà sẽ xây
    public void SelectBuilding(BuildingData buildingData)
    {
        _currentSelectedBuilding = buildingData;
        _currentRotationIndex = 0; // Reset góc xoay về 0 khi chọn công trình mới

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
                Debug.Log("CHẾ ĐỘ XÂY DỰNG: Đã BẬT");
            }
            else
            {
                if (_ghostBuilding != null)
                {
                    _ghostBuilding.SetActive(false);
                }
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
                if (_ghostBuilding != null) _ghostBuilding.SetActive(false);
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
        if (_gridSystem == null || (!IsBuildMode && !IsDeleteMode)) 
        {
            if (_ghostBuilding != null) _ghostBuilding.SetActive(false);
            return;
        }

        // NGĂN CLICK XUYÊN QUA UI (UI Click-Through): Nếu chuột đang di chuyển đè lên thanh UI chọn công trình ở dưới,
        // lập tức ẩn Ghost Building và bỏ qua mọi hành vi chọn ô lưới hoặc đặt nhà để tránh lỗi click chọn công trình là đặt nhà luôn.
        BuildingSelectionUI ui = GetComponent<BuildingSelectionUI>();
        if (ui != null && ui.IsMouseOverUI())
        {
            if (_ghostBuilding != null) _ghostBuilding.SetActive(false);
            return;
        }

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);

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

                    // Trừ đi một nửa kích thước để chuột (centerCell) luôn nằm ở giữa công trình
                    int startX = gridX - size.x / 2;
                    int startZ = gridZ - size.y / 2;

                    bool canBuild = CheckBuildingArea(startX, startZ, size, out List<GridCell> cellsToOccupy);

                    // Đổi màu Ghost để báo hiệu (Xanh = Phù hợp, Đỏ = Trái phép / Có vật cản / Đất không bằng phẳng)
                    SetGhostColor(canBuild ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f));

                    Vector3 centerPos = CalculateBuildingCenter(startX, startZ, centerCell.elevation, size);
                    if (_ghostBuilding != null)
                    {
                        _ghostBuilding.transform.position = centerPos;
                        _ghostBuilding.transform.rotation = Quaternion.Euler(0, _currentRotationIndex * 90f, 0);
                        _ghostBuilding.SetActive(true);
                    }

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        TryBuild(startX, startZ, cellsToOccupy, centerPos, canBuild, CurrentSelectedBuilding);
                    }
                }
                else if (IsDeleteMode)
                {
                    if (Mouse.current.leftButton.wasPressedThisFrame)
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

    private Vector3 CalculateBuildingCenter(int startX, int startZ, int elevation, Vector2Int size)
    {
        // Tính toán độ lệch (Offset) để lấy điểm chính giữa Tâm của công trình (nhiều ô)
        Vector3 startPos = _gridSystem.GetWorldPosition(startX, startZ, elevation);
        float offset_x = (size.x - 1) * _gridSystem.GetCellSize() / 2f;
        float offset_z = (size.y - 1) * _gridSystem.GetCellSize() / 2f;
        
        // Không nâng Y lên nữa vì hệ thống đang dùng Unity Terrain, mặt đất đã chính xác
        return startPos + new Vector3(offset_x, 0f, offset_z);
    }

    private bool CheckBuildingArea(int startX, int startZ, Vector2Int size, out List<GridCell> cells)
    {
        cells = new List<GridCell>();
        float minY = float.MaxValue;
        float maxY = float.MinValue;

        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                GridCell cell = _gridSystem.GetCell(startX + x, startZ + z);
                
                // 1. Ô lưới phải hợp lệ và chưa có vật cản
                if (cell == null || !cell.isBuildable)
                {
                    return false;
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
        
        // KHÔNG CHO XÂY NẾU ĐẤT QUÁ DỐC (Chênh lệch độ cao > 2.5m)
        if (Terrain.activeTerrain != null && (maxY - minY > 2.5f))
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

        VisionSource[] visionSources = ghost.GetComponentsInChildren<VisionSource>(true);
        foreach (VisionSource source in visionSources)
        {
            if (source != null)
            {
                Destroy(source);
            }
        }

        ConstructibleBuilding[] constructibleBuildings = ghost.GetComponentsInChildren<ConstructibleBuilding>(true);
        foreach (ConstructibleBuilding building in constructibleBuildings)
        {
            if (building != null)
            {
                building.IsCompleted = false;
                building.enabled = false;
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
        
        // BƯỚC QUAN TRỌNG: ỦI PHẲNG MẶT ĐẤT!
        _gridSystem.FlattenRectArea(startX, startZ, size.x, size.y);

        // Đợi 1 chút xíu hoặc tính toán trực tiếp CenterPos lại vì mặt đất vừa bị lún xuống/nâng lên
        Vector3 finalCenterPos = CalculateBuildingCenter(startX, startZ, 0, size);
        
        GameObject newBuilding = Instantiate(data.buildingPrefab, finalCenterPos, Quaternion.Euler(0, _currentRotationIndex * 90f, 0));
        _buildingDataMap[newBuilding] = data;

        // Thêm component ConstructibleBuilding để kích hoạt tính năng xây dựng từ từ trồi từ dưới đất lên
        ConstructibleBuilding cb = newBuilding.AddComponent<ConstructibleBuilding>();
        cb.TotalBuildTime = data.buildTime;
        cb.buildingGridSize = data.buildingSize;
        cb.ConstructionFencePrefab = _constructionFencePrefab;

        // Tự động gắn thành phần chiến đấu để công trình có thể nhận sát thương và bị tiêu diệt
        BuildingCombatTarget combatTarget = newBuilding.AddComponent<BuildingCombatTarget>();
        combatTarget.maxHealth = data != null ? data.maxHealth : 100;
        combatTarget.currentHealth = combatTarget.maxHealth;
        combatTarget.unitName = data != null ? data.buildingName : "Building";

        AddShelterComponentIfHouse(newBuilding, data);

        foreach (var cell in cellsToOccupy)
        {
            _builtStructures[cell] = newBuilding;     // Lưu chung 1 ngôi nhà duy nhất cho tất cả các ô nó chiếm
            cell.isBuildable = false;
            cell.isWalkable = false;                 // Nhà che đường 
        }

        // Ghi nhận nhà đã bắt đầu đặt móng (chưa tăng builtBuildingCounts vì chưa hoàn thành)
        Debug.Log($"Đặt móng xây {data.buildingName} thành công tại [{startX}, {startZ}] - Kích thước {size} (Xoay {_currentRotationIndex * 90} độ). Chờ dân làng đến xây dựng!");
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

        foreach (var cell in cellsToClear)
        {
            _builtStructures.Remove(cell);
            cell.isBuildable = true;
            cell.isWalkable = true;
        }

        if (_buildingDataMap.ContainsKey(buildingObj))
        {
            BuildingData dataToDestroy = _buildingDataMap[buildingObj];

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

        // Tự động gắn và cấu hình thành phần chiến đấu cho Nhà chính để kẻ địch có thể tấn công
        MainBuildingCombatTarget combatTarget = newBuilding.GetComponent<MainBuildingCombatTarget>();
        if (combatTarget == null)
        {
            combatTarget = newBuilding.AddComponent<MainBuildingCombatTarget>();
        }
        combatTarget.maxHealth = _mainBuildingData != null ? _mainBuildingData.maxHealth : 500;
        combatTarget.currentHealth = combatTarget.maxHealth;


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

        // Dự phòng: Nếu chưa có kho nào nhận, trả về vị trí gốc của grid hoặc toạ độ giữa bản đồ
        if (!found)
        {
            if (_gridSystem != null)
            {
                bestPos = _gridSystem.GetWorldPosition(_gridSystem.GetWidth() / 2, _gridSystem.GetLength() / 2, 0);
            }
        }

        return bestPos;
    }

    private float GetPathLength(Vector3 start, Vector3 target)
    {
        UnityEngine.AI.NavMeshPath path = new UnityEngine.AI.NavMeshPath();
        if (UnityEngine.AI.NavMesh.CalculatePath(start, target, UnityEngine.AI.NavMesh.AllAreas, path))
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
}
