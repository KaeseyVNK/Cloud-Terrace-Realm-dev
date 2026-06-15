using UnityEngine;
using System.Collections.Generic;

public enum CursorType
{
    Default,
    Move,
    Attack,
    GatherWood,
    GatherStone,
    GatherGold,
    GatherFood,
    Build,
    Garrison
}

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor Textures (Wenrexa Pack)")]
    [SerializeField] private Texture2D _defaultCursor;
    [SerializeField] private Texture2D _moveCursor;
    [SerializeField] private Texture2D _attackCursor;
    [SerializeField] private Texture2D _gatherWoodCursor;
    [SerializeField] private Texture2D _gatherStoneCursor;
    [SerializeField] private Texture2D _gatherGoldCursor;
    [SerializeField] private Texture2D _gatherFoodCursor;
    [SerializeField] private Texture2D _buildCursor;
    [SerializeField] private Texture2D _garrisonCursor;

    [Header("Settings")]
    [SerializeField] private Vector2 _cursorHotspot = Vector2.zero;

    private CursorType _currentType = CursorType.Default;
    private bool _isAttackTargetingMode = false;
    private bool _isGatherTargetingMode = false;
    private bool _isBuildTargetingMode = false;
    private Texture2D _activeTexture;

    // Chế độ debug bàn phím
    private bool _debugMode = false;

    public bool IsAttackTargetingMode
    {
        get => _isAttackTargetingMode;
        set
        {
            _isAttackTargetingMode = value;
            if (value)
            {
                _isGatherTargetingMode = false;
                _isBuildTargetingMode = false;
            }
            UpdateCursorState();
        }
    }

    public bool IsGatherTargetingMode
    {
        get => _isGatherTargetingMode;
        set
        {
            _isGatherTargetingMode = value;
            if (value)
            {
                _isAttackTargetingMode = false;
                _isBuildTargetingMode = false;
            }
            UpdateCursorState();
        }
    }

    public bool IsBuildTargetingMode
    {
        get => _isBuildTargetingMode;
        set
        {
            _isBuildTargetingMode = value;
            if (value)
            {
                _isAttackTargetingMode = false;
                _isGatherTargetingMode = false;
            }
            UpdateCursorState();
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetCursorType(CursorType.Default);
    }

    void Update()
    {
        // Toggle Debug mode bằng phím F12
        if (Input.GetKeyDown(KeyCode.F12))
        {
            _debugMode = !_debugMode;
            Debug.Log($"[CursorManager] Debug mode: {_debugMode}. Dùng phím 1-9 để test trực quan các cursor.");
        }

        if (_debugMode)
        {
            for (int i = 1; i <= 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i))
                {
                    string path = $"Assets/ThirdAssets/StoneCursorWenrexa/PNG/{i:00}.png";
                    Texture2D tex = null;
#if UNITY_EDITOR
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
#endif
                    if (tex != null)
                    {
                        Cursor.SetCursor(tex, _cursorHotspot, CursorMode.Auto);
                        Debug.Log($"[CursorManager] Đã đổi cursor sang ảnh: {path}");
                    }
                }
            }
            return;
        }

        UpdateHoverCursor();
    }

    public void SetCursorType(CursorType type)
    {
        _currentType = type;
        UpdateCursorState();
    }

    private void UpdateCursorState()
    {
        if (_isAttackTargetingMode)
        {
            _activeTexture = _attackCursor;
        }
        else if (_isBuildTargetingMode)
        {
            _activeTexture = _buildCursor;
        }
        // Lưu ý: Đối với _isGatherTargetingMode, hình ảnh cụ thể sẽ do UpdateHoverCursor quyết định động,
        // nếu rê ra ngoài đất trống thì hiển thị _gatherWoodCursor làm mặc định.
        else if (_isGatherTargetingMode && _currentType == CursorType.Default)
        {
            _activeTexture = _gatherWoodCursor;
        }
        else
        {
            switch (_currentType)
            {
                case CursorType.Default:
                    _activeTexture = _defaultCursor;
                    break;
                case CursorType.Move:
                    _activeTexture = _moveCursor;
                    break;
                case CursorType.Attack:
                    _activeTexture = _attackCursor;
                    break;
                case CursorType.GatherWood:
                    _activeTexture = _gatherWoodCursor;
                    break;
                case CursorType.GatherStone:
                    _activeTexture = _gatherStoneCursor;
                    break;
                case CursorType.GatherGold:
                    _activeTexture = _gatherGoldCursor;
                    break;
                case CursorType.GatherFood:
                    _activeTexture = _gatherFoodCursor;
                    break;
                case CursorType.Build:
                    _activeTexture = _buildCursor;
                    break;
                case CursorType.Garrison:
                    _activeTexture = _garrisonCursor;
                    break;
            }
        }

        if (_activeTexture != null)
        {
            Cursor.SetCursor(_activeTexture, _cursorHotspot, CursorMode.Auto);
        }
        else
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }

    private void UpdateHoverCursor()
    {
        if (UnitSelectionManager.Instance == null || Camera.main == null)
        {
            SetCursorType(CursorType.Default);
            return;
        }

        // Chế độ tấn công và xây dựng luôn giữ nguyên hình ảnh
        if (_isAttackTargetingMode)
        {
            SetCursorType(CursorType.Attack);
            return;
        }
        if (_isBuildTargetingMode)
        {
            SetCursorType(CursorType.Build);
            return;
        }

        var selected = UnitSelectionManager.Instance.selectedUnits;
        if (selected == null || selected.Count == 0)
        {
            SetCursorType(CursorType.Default);
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (TryGetHoverHit(ray, out RaycastHit hit))
        {
            // 1. Kiểm tra Tháp Canh
            WatchTowerGarrison clickedWatchTower = hit.collider.GetComponentInParent<WatchTowerGarrison>();
            if (clickedWatchTower != null)
            {
                SetCursorType(CursorType.Garrison);
                return;
            }

            // Phân loại các đơn vị đang chọn
            bool hasVillagers = false;
            bool hasCombatUnits = false;
            foreach (var unit in selected)
            {
                if (unit != null)
                {
                    if (unit.GetComponent<VillagerController>() != null) hasVillagers = true;
                    else if (unit.GetComponent<BaseCombatUnitController>() != null) hasCombatUnits = true;
                }
            }

            // 2. Kiểm tra Kẻ địch hoặc Động vật hoang dã
            BaseCombatUnitController clickedUnit = hit.collider.GetComponentInParent<BaseCombatUnitController>();
            if (clickedUnit != null && clickedUnit.currentState != CombatState.Dead)
            {
                if (clickedUnit.faction == UnitFaction.Enemy || clickedUnit.faction == UnitFaction.Neutral)
                {
                    if (hasCombatUnits || hasVillagers)
                    {
                        SetCursorType(CursorType.Attack);
                        return;
                    }
                }
                else if (clickedUnit.faction == UnitFaction.Player)
                {
                    // Sửa chữa nếu đó là một công trình bị thương và chúng ta chọn dân làng
                    if (hasVillagers && (clickedUnit.GetComponent<BuildingCombatTarget>() != null || clickedUnit.GetComponent<MainBuildingCombatTarget>() != null) && clickedUnit.currentHealth < clickedUnit.maxHealth)
                    {
                        SetCursorType(CursorType.Build);
                        return;
                    }
                }
            }

            // 3. Kiểm tra công trình đang xây dựng dở dang
            ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
            if (clickedBuilding != null && !clickedBuilding.IsCompleted)
            {
                if (hasVillagers)
                {
                    SetCursorType(CursorType.Build);
                    return;
                }
            }

            // 4. Kiểm tra mỏ tài nguyên (cây, đá, vàng, thức ăn)
            ResourceNode clickedNode = hit.collider.GetComponentInParent<ResourceNode>();
            if (clickedNode == null)
            {
                GridSystem grid = FindAnyObjectByType<GridSystem>();
                if (grid != null)
                {
                    grid.GetXY(hit.point, out int gridX, out int gridZ);
                    GridCell cell = grid.GetCell(gridX, gridZ);
                    if (cell != null && cell.hasResource && cell.resourceObject != null)
                    {
                        if (!IsHiddenByFog(cell.resourceObject))
                        {
                            clickedNode = cell.resourceObject.GetComponent<ResourceNode>();
                        }
                    }
                }
            }

            if (clickedNode != null)
            {
                if (hasVillagers || _isGatherTargetingMode)
                {
                    switch (clickedNode.ResourceType)
                    {
                        case ResourceType.Wood:
                            SetCursorType(CursorType.GatherWood);
                            break;
                        case ResourceType.Stone:
                            SetCursorType(CursorType.GatherStone);
                            break;
                        case ResourceType.Gold:
                            SetCursorType(CursorType.GatherGold);
                            break;
                        case ResourceType.Food:
                            SetCursorType(CursorType.GatherFood);
                            break;
                        default:
                            SetCursorType(CursorType.GatherWood);
                            break;
                    }
                    return;
                }
            }

            // 5. Nếu rê chuột vào đất trống/địa hình khi có đơn vị đang chọn -> Hiển thị chuột Di chuyển (Move)
            // Tránh hiển thị chuột di chuyển nếu đang ở chế độ gặt hái chủ động
            if (!_isGatherTargetingMode)
            {
                SetCursorType(CursorType.Move);
                return;
            }
        }

        // Nếu ở chế độ gặt hái chủ động nhưng không hover vào tài nguyên -> Hiện chuột gặt hái mặc định (Wood)
        if (_isGatherTargetingMode)
        {
            SetCursorType(CursorType.GatherWood);
        }
        else
        {
            SetCursorType(CursorType.Default);
        }
    }

    private bool TryGetHoverHit(Ray ray, out RaycastHit hoverHit)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null) continue;
            if (IsHiddenByFog(hits[i].collider.gameObject)) continue;

            hoverHit = hits[i];
            return true;
        }

        hoverHit = default;
        return false;
    }

    private bool IsHiddenByFog(GameObject target)
    {
        if (target == null) return false;
        FogVisibilityTarget visibilityTarget = target.GetComponentInParent<FogVisibilityTarget>();
        return visibilityTarget != null && !visibilityTarget.IsVisible;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_defaultCursor == null)
            _defaultCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/01.png");
        if (_moveCursor == null)
            _moveCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/02.png");
        if (_attackCursor == null)
            _attackCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/13.png");
        if (_gatherWoodCursor == null)
            _gatherWoodCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/05.png");
        if (_gatherStoneCursor == null)
            _gatherStoneCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/15.png");
        if (_gatherGoldCursor == null)
            _gatherGoldCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/14.png");
        if (_gatherFoodCursor == null)
            _gatherFoodCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/08.png");
        if (_buildCursor == null)
            _buildCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/06.png");
        if (_garrisonCursor == null)
            _garrisonCursor = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/07.png");
    }
#endif
}
