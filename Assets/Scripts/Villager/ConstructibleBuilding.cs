using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

public class ConstructibleBuilding : MonoBehaviour
{
    public static readonly List<ConstructibleBuilding> Registry = new List<ConstructibleBuilding>();

    [Header("Construction Stats")]
    [UnityEngine.Serialization.FormerlySerializedAs("totalBuildTime")]
    [SerializeField] private float _totalBuildTime = 15f;    // Tổng thời gian cần để hoàn tất (giây)
    public float TotalBuildTime
    {
        get => _totalBuildTime;
        set => _totalBuildTime = value;
    }

    [UnityEngine.Serialization.FormerlySerializedAs("currentProgress")]
    [SerializeField] private float _currentProgress = 0f;    // Tiến độ xây dựng từ 0.0f -> 1.0f
    public float CurrentProgress
    {
        get => _currentProgress;
        set => _currentProgress = value;
    }

    [UnityEngine.Serialization.FormerlySerializedAs("isCompleted")]
    [SerializeField] private bool _isCompleted = false;
    public bool IsCompleted
    {
        get => _isCompleted;
        set => _isCompleted = value;
    }

    [Header("Special Settings")]
    [UnityEngine.Serialization.FormerlySerializedAs("isInstantBuild")]
    [SerializeField] private bool _isInstantBuild = false;
    public bool IsInstantBuild
    {
        get => _isInstantBuild;
        set => _isInstantBuild = value;
    }

    [Header("Visual Effects")]
    [UnityEngine.Serialization.FormerlySerializedAs("initialSunkHeight")]
    [SerializeField] private float _initialSunkHeight = -4f; // Độ sâu lún dưới lòng đất khi bắt đầu xây dựng
    public float InitialSunkHeight
    {
        get => _initialSunkHeight;
        set => _initialSunkHeight = value;
    }

    [Header("Construction Fence")]
    [UnityEngine.Serialization.FormerlySerializedAs("constructionFencePrefab")]
    [Tooltip("Prefab hàng rào công trình đang xây dựng")]
    [SerializeField] private GameObject _constructionFencePrefab;
    public GameObject ConstructionFencePrefab
    {
        get => _constructionFencePrefab;
        set => _constructionFencePrefab = value;
    }

    [HideInInspector] public Vector2Int buildingGridSize = Vector2Int.one;

    [Obsolete("Use TotalBuildTime instead")]
    public float totalBuildTime { get => TotalBuildTime; set => TotalBuildTime = value; }

    [Obsolete("Use CurrentProgress instead")]
    public float currentProgress { get => CurrentProgress; set => CurrentProgress = value; }

    [Obsolete("Use IsCompleted instead")]
    public bool isCompleted { get => IsCompleted; set => IsCompleted = value; }

    [Obsolete("Use InitialSunkHeight instead")]
    public float initialSunkHeight { get => InitialSunkHeight; set => InitialSunkHeight = value; }

    [Obsolete("Use ConstructionFencePrefab instead")]
    public GameObject constructionFencePrefab { get => ConstructionFencePrefab; set => ConstructionFencePrefab = value; }

    private Transform _visualContainer;
    private NavMeshObstacle _navObstacle;
    private GameObject _constructionFenceInstance;

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    void Awake()
    {
        // Fallback: Tự động nhận dạng qua tên đối tượng hoặc component MarketController trung lập
        if (!_isInstantBuild)
        {
            string nameLower = gameObject.name.ToLower();
            if (nameLower.Contains("torch") || nameLower.Contains("đoốc") || nameLower.Contains("đuốc") || nameLower.Contains("duoc") || nameLower.Contains("neutral") ||
                nameLower.Contains("fence") || nameLower.Contains("gate") || nameLower.Contains("rào") || nameLower.Contains("cổng"))
            {
                _isInstantBuild = true;
            }
            else
            {
                MarketController mc = GetComponent<MarketController>();
                if (mc == null) mc = GetComponentInChildren<MarketController>();
                if (mc != null && mc.isNeutral)
                {
                    _isInstantBuild = true;
                }
            }
        }

        if (_isInstantBuild)
        {
            _isCompleted = true;
            _currentProgress = 1f;
        }

        Debug.Log($"[ConstructibleBuilding] Khởi tạo trên {gameObject.name}. Số lượng đối tượng con gốc: {transform.childCount}");

        // 1. Kiểm tra và trích xuất Mesh ở ROOT (nếu có) sang đối tượng con phụ
        // Điều này cực kỳ quan trọng cho các Prefab phẳng có MeshFilter và MeshRenderer nằm ngay trên Parent thay vì ở các con.
        MeshFilter rootMeshFilter = GetComponent<MeshFilter>();
        MeshRenderer rootMeshRenderer = GetComponent<MeshRenderer>();
        if (rootMeshFilter != null && rootMeshRenderer != null)
        {
            Debug.Log($"   -> Phát hiện Mesh trực tiếp trên ROOT của {gameObject.name}. Đang trích xuất xuống con...");
            
            GameObject rootVisualCopy = new GameObject("_RootVisualCopy");
            rootVisualCopy.transform.SetParent(transform);
            rootVisualCopy.transform.localPosition = Vector3.zero;
            rootVisualCopy.transform.localRotation = Quaternion.identity;
            rootVisualCopy.transform.localScale = Vector3.one;

            // Di chuyển Mesh Filter
            MeshFilter copyFilter = rootVisualCopy.AddComponent<MeshFilter>();
            copyFilter.sharedMesh = rootMeshFilter.sharedMesh;

            // Di chuyển Mesh Renderer
            MeshRenderer copyRenderer = rootVisualCopy.AddComponent<MeshRenderer>();
            copyRenderer.sharedMaterials = rootMeshRenderer.sharedMaterials;

            // Xóa Mesh components trên root để root chỉ làm nhiệm vụ Anchor và giữ Collider click chuột
            Destroy(rootMeshRenderer);
            Destroy(rootMeshFilter);

            Debug.Log("   -> Đã trích xuất xong Mesh từ ROOT xuống con.");
        }

        // 2. Thu thập danh sách con nguyên bản TRƯỚC KHI tạo bất kỳ con mới nào
        List<Transform> childrenToMove = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            
            // Tắt cờ Static để Unity cho phép di chuyển mô hình ở Runtime (Tránh lỗi Static Batching chặn di chuyển)
            child.gameObject.isStatic = false;
            foreach (var grandChild in child.GetComponentsInChildren<Transform>(true))
            {
                grandChild.gameObject.isStatic = false;
            }

            childrenToMove.Add(child);
            Debug.Log($"   -> Đã thu thập đối tượng con: {child.name} (Static đã tắt)");
        }

        // 3. Tính toán chiều cao thực tế của công trình dựa vào Bounding Box của toàn bộ Mesh Renderer
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        float boundsHeight = 4.0f; // Giá trị dự phòng nếu không tìm thấy Renderer
        if (renderers.Length > 0)
        {
            Bounds combinedBounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                // Chỉ gộp các Renderer hợp lệ và có kích thước lớn hơn 0
                if (r.bounds.size.sqrMagnitude > 0.01f)
                {
                    combinedBounds.Encapsulate(r.bounds);
                }
            }
            boundsHeight = combinedBounds.size.y;
        }

        // Chiều cao chóp nhà được phép nhô lên trên mặt đất (ví dụ: 0.6 mét)
        float tipOffset = 0.6f;
        // Độ lún = Chiều cao nhà trừ đi chiều cao chóp (đảm bảo không lún ngược lên trên nếu nhà quá thấp)
        _initialSunkHeight = -Mathf.Max(0.5f, boundsHeight - tipOffset);
        Debug.Log($"[ConstructibleBuilding] Chiều cao ngôi nhà: {boundsHeight}m. Lún xuống: {_initialSunkHeight}m để chừa lại chóp nhô lên: {tipOffset}m");

        // 4. Tạo một Visual Container động bằng code để gom toàn bộ mesh và vật thể con
        GameObject containerObj = new GameObject("_VisualContainer");
        _visualContainer = containerObj.transform;
        _visualContainer.SetParent(transform);
        _visualContainer.localPosition = Vector3.zero;
        _visualContainer.localRotation = Quaternion.identity;
        _visualContainer.localScale = Vector3.one;

        // 5. Chuyển đổi toàn bộ GameObject con vào trong Container này
        foreach (var child in childrenToMove)
        {
            child.SetParent(_visualContainer, true);
        }

        // 6. Tìm và tạm thời tắt NavMeshObstacle để dân làng có thể đứng sát vào gõ búa
        _navObstacle = GetComponentInChildren<NavMeshObstacle>();
        if (_navObstacle != null)
        {
            if (_isInstantBuild)
            {
                _navObstacle.enabled = true;
                Debug.Log($"   -> Giữ nguyên NavMeshObstacle bật cho công trình xây ngay trên {gameObject.name}");
            }
            else
            {
                _navObstacle.enabled = false;
                Debug.Log($"   -> Đã tạm thời tắt NavMeshObstacle trên {gameObject.name}");
            }
        }

        // 7. Lún toàn bộ phần mô hình xuống đất để chuẩn bị hiệu ứng trồi lên
        if (_isInstantBuild)
        {
            _visualContainer.localPosition = Vector3.zero;
        }
        else
        {
            _visualContainer.localPosition = new Vector3(0f, _initialSunkHeight, 0f);
            Debug.Log($"   -> Đã lún _VisualContainer xuống Y = {_initialSunkHeight}. Vị trí cục bộ mới: {_visualContainer.localPosition}");
        }
    }

    void Start()
    {
        if (_isInstantBuild || _isCompleted)
        {
            return;
        }
        // 1. Tự động tải prefab hàng rào nếu chưa được gán
        if (_constructionFencePrefab == null)
        {
            #if UNITY_EDITOR
            _constructionFencePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building/mongsnhaf.prefab");
            if (_constructionFencePrefab != null)
            {
                Debug.Log($"[ConstructibleBuilding] Tự động tải thành công prefab hàng rào: {_constructionFencePrefab.name}");
            }
            #endif
        }

        // 2. Tạo hàng rào công trình tại gốc tọa độ của nhà (nằm trên mặt đất, không bị lún theo visualContainer)
        if (_constructionFencePrefab != null)
        {
            _constructionFenceInstance = Instantiate(_constructionFencePrefab, transform);
            _constructionFenceInstance.transform.localPosition = Vector3.zero;
            _constructionFenceInstance.transform.localRotation = Quaternion.identity;

            // 3. Tính toán kích thước và tỷ lệ scale hàng rào bao quanh móng nhà
            float cellSize = 2f;
            GridSystem gridSystem = FindAnyObjectByType<GridSystem>();
            if (gridSystem != null)
            {
                cellSize = gridSystem.GetCellSize();
            }

            float targetSizeX = buildingGridSize.x * cellSize;
            float targetSizeZ = buildingGridSize.y * cellSize;

            // Tính toán bounds thực tế của hàng rào trong KHÔNG GIAN CỤC BỘ (Local Space) của hàng rào
            // Điều này tránh hoàn toàn các lỗi sai lệch do xoay/tỉ lệ (scale) của công trình cha
            MeshFilter[] fenceMeshFilters = _constructionFenceInstance.GetComponentsInChildren<MeshFilter>(true);
            float fenceSizeX = 4f; // Kích thước dự phòng mặc định
            float fenceSizeZ = 4f;

            if (fenceMeshFilters.Length > 0)
            {
                Bounds localFenceBounds = new Bounds();
                bool foundValidBounds = false;
                foreach (var mf in fenceMeshFilters)
                {
                    if (mf.sharedMesh != null && mf.sharedMesh.bounds.size.sqrMagnitude > 0.01f)
                    {
                        // Chuyển đổi tâm bounds của Mesh từ không gian con về không gian cục bộ của hàng rào cha
                        Vector3 localCenter = _constructionFenceInstance.transform.InverseTransformPoint(mf.transform.TransformPoint(mf.sharedMesh.bounds.center));
                        // Chuyển đổi kích thước bounds về không gian cục bộ của hàng rào cha
                        Vector3 localSize = _constructionFenceInstance.transform.InverseTransformVector(mf.transform.TransformVector(mf.sharedMesh.bounds.size));
                        localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));

                        Bounds boundsInFenceSpace = new Bounds(localCenter, localSize);
                        if (!foundValidBounds)
                        {
                            localFenceBounds = boundsInFenceSpace;
                            foundValidBounds = true;
                        }
                        else
                        {
                            localFenceBounds.Encapsulate(boundsInFenceSpace);
                        }
                    }
                }

                if (foundValidBounds)
                {
                    fenceSizeX = localFenceBounds.size.x;
                    fenceSizeZ = localFenceBounds.size.z;
                }
            }

            // Lấy scale của công trình cha để chia tỉ lệ (bù trừ tỉ lệ kế thừa từ cha)
            float parentScaleX = Mathf.Max(0.01f, Mathf.Abs(transform.localScale.x));

            // Tính toán tỷ lệ scale cục bộ cho hàng rào (chia cho parentScaleX để triệt tiêu ảnh hưởng từ scale của nhà cha)
            float scaleX = ((targetSizeX / fenceSizeX) * 1.5f) / parentScaleX;

            _constructionFenceInstance.transform.Rotate(-90, 0, 0);
            _constructionFenceInstance.transform.localScale = new Vector3(scaleX, scaleX, 17);
            
            Debug.Log($"[ConstructibleBuilding] Khởi tạo hàng rào xây dựng cho {gameObject.name}. ParentScale: {parentScaleX}, GridSize: {buildingGridSize}, CellSize: {cellSize}, TargetSize: {targetSizeX}x{targetSizeZ}, LocalFenceSize: {fenceSizeX:F2}x{fenceSizeZ:F2}, FinalLocalScale: {scaleX:F2}");
        }
        else
        {
            Debug.LogWarning("[ConstructibleBuilding] Không tìm thấy prefab hàng rào xây dựng (mongsnhaf.prefab)!");
        }
    }

    /// <summary>
    /// Tiến hành xây dựng công trình (được gọi mỗi frame bởi Dân Làng trong trạng thái Building)
    /// </summary>
    /// <param name="buildAmount">Lượng tiến độ tăng thêm (tỉ lệ phần trăm từ 0 đến 1)</param>
    public void Construct(float buildAmount)
    {
        if (IsCompleted) return;

        CurrentProgress += buildAmount;
        CurrentProgress = Mathf.Clamp01(CurrentProgress);

        // Hiệu ứng trồi từ dưới đất lên: Interpolate độ cao Y từ lún sâu lên bằng 0.0f (mặt đất chuẩn)
        float currentY = Mathf.Lerp(_initialSunkHeight, 0f, CurrentProgress);
        _visualContainer.localPosition = new Vector3(0f, currentY, 0f);

        if (CurrentProgress >= 1f)
        {
            CompleteConstruction();
        }
    }

    private void CompleteConstruction()
    {
        IsCompleted = true;
        _visualContainer.localPosition = Vector3.zero; // Trả về tọa độ chuẩn tuyệt đối

        // Hủy hàng rào xây dựng khi công trình đã hoàn thành
        if (_constructionFenceInstance != null)
        {
            Destroy(_constructionFenceInstance);
            _constructionFenceInstance = null;
            Debug.Log($"[ConstructibleBuilding] Đã hủy hàng rào xây dựng của {gameObject.name}");
        }

        // Bật lại đục lưới NavMesh để các Unit khác đi vòng tránh công trình đã hoàn thành
        if (_navObstacle != null)
        {
            _navObstacle.enabled = true;
        }

        // Thông báo cho BuildingManager ghi nhận hoàn tất để cập nhật điều kiện công nghệ
        if (BuildingManager.Instance != null)
        {
            BuildingManager.Instance.OnBuildingCompleted(this);
        }

        Debug.Log($"[ConstructibleBuilding] '{gameObject.name}' đã hoàn tất xây dựng và đi vào hoạt động!");
    }
}
