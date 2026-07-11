using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class UnitSelectionManager : MonoBehaviour
{
    private const int WalkableNavMeshAreaMask = ~2;
    private const float CommandDestinationSampleRadius = 4f;

    public static UnitSelectionManager Instance;

    public List<SelectableUnit> selectedUnits = new List<SelectableUnit>();
    public LayerMask unitLayerMask; // Gán layer của unit vào đây trong Inspector (VD: Default hoặc Unit)

    public Color selectionBoxColor = new Color(0.2f, 0.8f, 0.2f, 0.3f);
    public Color selectionBoxBorderColor = new Color(0.2f, 0.8f, 0.2f, 1f);

    private bool isDragging = false;
    private Vector2 startMousePos;
    private Texture2D whiteTexture;
    private float _lastClickTime = 0f;
    private const float DoubleClickTimeThreshold = 0.3f; // 300 ms

    private bool _isAttackMode = false;
    private bool _isGatherMode = false;
    private bool _isBuildMode = false;

    private Vector2 _commandScreenPos;
    private static bool _isBoxSelectMode = false;
    public static event System.Action<bool> OnBoxSelectModeChanged;
    public static bool IsBoxSelectMode
    {
        get => _isBoxSelectMode;
        set
        {
            if (_isBoxSelectMode == value) return;
            _isBoxSelectMode = value;
            OnBoxSelectModeChanged?.Invoke(_isBoxSelectMode);
        }
    }

    private float _touchStartTime = 0f;
    private Vector2 _touchStartPos;
    private bool _hasTouchMoved = false;

    public event System.Action OnSelectionChanged;
    private readonly List<SelectableUnit> _prevSelectedUnits = new List<SelectableUnit>();

    [SerializeField] private GridSystem _gridSystem;
    private Camera _mainCamera;
    [SerializeField] private LayerMask _commandLayerMask;
    private LayerMask _effectiveCommandLayerMask;

    private static readonly RaycastHit[] s_sphereCastHits = new RaycastHit[64];
    private static readonly RaycastHit[] s_raycastHits = new RaycastHit[64];

    #if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static float _lastCommandSphereWarningTime = 0f;
    private static float _lastCommandRaycastWarningTime = 0f;
    #endif

    private static readonly Unity.Profiling.ProfilerMarker s_resolveDestinationMarker = new Unity.Profiling.ProfilerMarker("RTS.Command.ResolveDestination");
    private static readonly Unity.Profiling.ProfilerMarker s_dispatchMarker = new Unity.Profiling.ProfilerMarker("RTS.Command.Dispatch");

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }
        _mainCamera = Camera.main;
    }

    private void ResolveCommandLayerMask()
    {
        if (_commandLayerMask.value != 0)
        {
            _effectiveCommandLayerMask = _commandLayerMask;
        }
        else
        {
            _effectiveCommandLayerMask = LayerMask.GetMask("Default", "Unit", "Resource", "Unit Enemy", "Buiding");
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_effectiveCommandLayerMask.value == 0)
            {
                Debug.LogWarning("[RTS] Failed to retrieve layers for command input fallback.");
            }
            #endif
        }
    }

    void Start()
    {
        ResolveCommandLayerMask();
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }

    void Update()
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnitPerformanceMetrics.ReportIfNeeded();
        #endif

        // Bấm Esc để hủy chế độ ra lệnh chủ động
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                ClearTargetingModes();
                GameLog.Log("[RTS] Hủy chế độ ra lệnh chỉ định");
                return;
            }
        }

        // Kiểm tra phím tắt ra lệnh chủ động
        if (selectedUnits.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.T))
            {
                bool hasCombatUnits = false;
                foreach (var unit in selectedUnits)
                {
                    if (unit != null && unit.GetComponent<BaseCombatUnitController>() != null)
                    {
                        hasCombatUnits = true;
                        break;
                    }
                }

                if (hasCombatUnits)
                {
                    ClearTargetingModes();
                    _isAttackMode = true;
                    if (CursorManager.Instance != null) CursorManager.Instance.IsAttackTargetingMode = true;
                    GameLog.Log("[RTS] Vào chế độ chỉ định Tấn công (Attack targeting mode)");
                }
            }
            else if (Input.GetKeyDown(KeyCode.G))
            {
                bool hasVillagers = false;
                foreach (var unit in selectedUnits)
                {
                    if (unit != null && unit.GetComponent<VillagerController>() != null)
                    {
                        hasVillagers = true;
                        break;
                    }
                }

                if (hasVillagers)
                {
                    ClearTargetingModes();
                    _isGatherMode = true;
                    if (CursorManager.Instance != null) CursorManager.Instance.IsGatherTargetingMode = true;
                    GameLog.Log("[RTS] Vào chế độ chỉ định Khai thác (Gather targeting mode)");
                }
            }
            else if (Input.GetKeyDown(KeyCode.B))
            {
                bool hasVillagers = false;
                foreach (var unit in selectedUnits)
                {
                    if (unit != null && unit.GetComponent<VillagerController>() != null)
                    {
                        hasVillagers = true;
                        break;
                    }
                }

                if (hasVillagers)
                {
                    ClearTargetingModes();
                    _isBuildMode = true;
                    if (CursorManager.Instance != null) CursorManager.Instance.IsBuildTargetingMode = true;
                    GameLog.Log("[RTS] Vào chế độ chỉ định Xây dựng (Build targeting mode)");
                }
            }
        }

        // Xử lý Touch Input trên Mobile / Editor Simulation
        bool isTouchSupported = false;
        #if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        isTouchSupported = (Input.touchCount > 0);
        #endif

        if (isTouchSupported)
        {
            HandleTouchInput();
        }
        else
        {
            HandleMouseInput();
        }

        // Kiểm tra xem danh sách các đơn vị được chọn có thay đổi không
        bool selectionChanged = false;
        if (selectedUnits.Count != _prevSelectedUnits.Count)
        {
            selectionChanged = true;
        }
        else
        {
            for (int i = 0; i < selectedUnits.Count; i++)
            {
                if (selectedUnits[i] != _prevSelectedUnits[i])
                {
                    selectionChanged = true;
                    break;
                }
            }
        }

        if (selectionChanged)
        {
            _prevSelectedUnits.Clear();
            _prevSelectedUnits.AddRange(selectedUnits);
            OnSelectionChanged?.Invoke();

            if (selectedUnits.Count > 0 && MyGame.Audio.AudioManager.Instance != null)
            {
                bool hasCombatUnit = false;
                foreach (var unit in selectedUnits)
                {
                    if (unit != null)
                    {
                        var combat = unit.GetComponent<BaseCombatUnitController>();
                        bool isVillager = unit.GetComponent<VillagerController>() != null;
                        if (combat != null && combat.faction == UnitFaction.Player 
                            && !isVillager 
                            && !(combat is MerchantCaravanUnit))
                        {
                            hasCombatUnit = true;
                            break;
                        }
                    }
                }

                if (hasCombatUnit)
                {
                    MyGame.Audio.AudioManager.Instance.PlayUnitMilitiaSelect();
                }
                else
                {
                    MyGame.Audio.AudioManager.Instance.PlayVillagerSelect();
                }
            }
        }
    }

    public void StartBuildMode()
    {
        bool hasVillagers = false;
        foreach (var unit in selectedUnits)
        {
            if (unit != null && unit.GetComponent<VillagerController>() != null)
            {
                hasVillagers = true;
                break;
            }
        }

        if (hasVillagers)
        {
            ClearTargetingModes();
            _isBuildMode = true;
            if (CursorManager.Instance != null) CursorManager.Instance.IsBuildTargetingMode = true;
            GameLog.Log("[RTS] ActionPanel: Vào chế độ chỉ định Xây dựng (Build)");
        }
    }

    public void StartAttackMode()
    {
        bool hasCombatUnits = false;
        foreach (var unit in selectedUnits)
        {
            if (unit != null && unit.GetComponent<BaseCombatUnitController>() != null)
            {
                hasCombatUnits = true;
                break;
            }
        }

        if (hasCombatUnits)
        {
            ClearTargetingModes();
            _isAttackMode = true;
            if (CursorManager.Instance != null) CursorManager.Instance.IsAttackTargetingMode = true;
            GameLog.Log("[RTS] ActionPanel: Vào chế độ chỉ định Tấn công (Attack)");
        }
    }

    public void StartGatherMode()
    {
        bool hasVillagers = false;
        foreach (var unit in selectedUnits)
        {
            if (unit != null && unit.GetComponent<VillagerController>() != null)
            {
                hasVillagers = true;
                break;
            }
        }

        if (hasVillagers)
        {
            ClearTargetingModes();
            _isGatherMode = true;
            if (CursorManager.Instance != null) CursorManager.Instance.IsGatherTargetingMode = true;
            GameLog.Log("[RTS] ActionPanel: Vào chế độ chỉ định Khai thác/Sửa chữa (Gather/Repair)");
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount != 1)
        {
            isDragging = false;
            return;
        }

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            _touchStartedOverUI = IsPointerOverUI(touch.position);
            if (_touchStartedOverUI)
            {
                return;
            }

            _touchStartTime = Time.time;
            _touchStartPos = touch.position;
            _hasTouchMoved = false;

            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                _commandScreenPos = touch.position;
                ExecuteTargetingCommand();
                return;
            }

            if (IsBoxSelectMode)
            {
                startMousePos = touch.position;
                _dragCurrentScreenPos = touch.position;
                isDragging = true;
            }
        }
        
        if (_touchStartedOverUI)
        {
            if (touch.phase == TouchPhase.Ended)
            {
                _touchStartedOverUI = false;
            }
            return;
        }

        if (touch.phase == TouchPhase.Moved)
        {
            _dragCurrentScreenPos = touch.position;
            if (Vector2.Distance(_touchStartPos, touch.position) > 15f)
            {
                _hasTouchMoved = true;
            }
        }
        else if (touch.phase == TouchPhase.Ended)
        {
            _dragCurrentScreenPos = touch.position;

            if (isDragging && IsBoxSelectMode)
            {
                isDragging = false;
                if (Vector2.Distance(startMousePos, touch.position) > 15f)
                {
                    HandleBoxSelection();
                }
                else
                {
                    _commandScreenPos = touch.position;
                    HandleClickSelection();
                }
            }
            else if (!_hasTouchMoved && (Time.time - _touchStartTime < 0.4f))
            {
                _commandScreenPos = touch.position;

                // CLICK OUTSIDE BUILD MENU -> CLOSE IT (but ignore if clicked on UI)
                if (BuildingManager.Instance != null && BuildingManager.Instance.IsBuildMode && BuildingManager.Instance.CurrentSelectedBuilding == null)
                {
                    // Check again at Ended to make absolutely sure we didn't release over UI
                    if (!IsPointerOverUI(touch.position))
                    {
                        BuildingManager.Instance.CancelBuildMode();
                        return;
                    }
                }

                if (selectedUnits.Count > 0)
                {
                    Ray ray = _mainCamera != null ? _mainCamera.ScreenPointToRay(touch.position) : Camera.main.ScreenPointToRay(touch.position);
                    bool tappedFriendlyUnit = false;

                    if (Physics.SphereCast(ray, 1f, out RaycastHit hit, 1000f, unitLayerMask))
                    {
                        SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
                        if (unit != null)
                        {
                            BaseCombatUnitController combatUnit = unit.CombatController;
                            VillagerController villager = unit.VillagerController;
                            if ((combatUnit != null && combatUnit.faction == UnitFaction.Player) || villager != null)
                            {
                                tappedFriendlyUnit = true;
                            }
                        }
                    }

                    if (tappedFriendlyUnit)
                    {
                        HandleClickSelection();
                    }
                    else
                    {
                        HandleRightClickCommand();
                    }
                }
                else
                {
                    HandleClickSelection();
                }
            }

            isDragging = false;
        }
    }

    private void HandleMouseInput()
    {
        // Bắt đầu click trái
        if (Input.GetMouseButtonDown(0))
        {
            // Avoid starting selection logic when clicking on UI elements
            if (IsPointerOverUI(Input.mousePosition))
            {
                return;
            }

            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                _commandScreenPos = Input.mousePosition;
                ExecuteTargetingCommand();
                return;
            }

            // CLICK OUTSIDE BUILD MENU -> CLOSE IT
            if (BuildingManager.Instance != null && BuildingManager.Instance.IsBuildMode && BuildingManager.Instance.CurrentSelectedBuilding == null)
            {
                BuildingManager.Instance.CancelBuildMode();
                return;
            }

            startMousePos = Input.mousePosition;
            _dragCurrentScreenPos = Input.mousePosition;
            isDragging = true;
        }

        if (isDragging)
        {
            _dragCurrentScreenPos = Input.mousePosition;
        }

        // Thả chuột trái
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            _dragCurrentScreenPos = Input.mousePosition;
            isDragging = false;
            if (Vector2.Distance(startMousePos, Input.mousePosition) > 10f)
            {
                HandleBoxSelection();
            }
            else
            {
                _commandScreenPos = Input.mousePosition;
                HandleClickSelection();
            }
        }
        
        // Khi click chuột phải ra lệnh
        if (Input.GetMouseButtonDown(1))
        {
            // Avoid issuing commands when clicking on UI elements
            if (IsPointerOverUI(Input.mousePosition))
            {
                return;
            }

            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                ClearTargetingModes();
                GameLog.Log("[RTS] Hủy chế độ ra lệnh bằng Chuột Phải");
            }
            else
            {
                _commandScreenPos = Input.mousePosition;
                HandleRightClickCommand();
            }
        }
    }

    private void ExecuteTargetingCommand()
    {
        Ray ray = _mainCamera != null ? _mainCamera.ScreenPointToRay(_commandScreenPos) : Camera.main.ScreenPointToRay(_commandScreenPos);
        if (TryGetCommandHit(ray, out RaycastHit hit))
        {
            // Spawn Indicator cho chế độ Chỉ định ra lệnh
            if (_isAttackMode)
            {
                BaseCombatUnitController clickedEnemy = hit.collider.GetComponentInParent<BaseCombatUnitController>();
                if (clickedEnemy != null && clickedEnemy.faction != UnitFaction.Player)
                    MyGame.UI.MoveIndicator.Spawn(clickedEnemy.transform.position, Vector3.up, Color.red, 1.3f, 0.45f);
                else
                    MyGame.UI.MoveIndicator.Spawn(hit.point, hit.normal, Color.red, 1.2f, 0.4f);
            }
            else if (_isGatherMode)
            {
                ResourceNode clickedNode = hit.collider.GetComponentInParent<ResourceNode>();
                ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
                BaseCombatUnitController clickedFriendlyUnit = hit.collider.GetComponentInParent<BaseCombatUnitController>();

                if (clickedNode != null)
                    MyGame.UI.MoveIndicator.Spawn(clickedNode.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);
                else if (clickedBuilding != null)
                    MyGame.UI.MoveIndicator.Spawn(clickedBuilding.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.4f, 0.4f);
                else if (clickedFriendlyUnit != null && clickedFriendlyUnit.faction == UnitFaction.Player)
                    MyGame.UI.MoveIndicator.Spawn(clickedFriendlyUnit.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.4f, 0.4f);
                else
                    MyGame.UI.MoveIndicator.Spawn(hit.point, hit.normal, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);
            }
            else if (_isBuildMode)
            {
                ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
                if (clickedBuilding != null)
                    MyGame.UI.MoveIndicator.Spawn(clickedBuilding.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.4f, 0.4f);
                else
                    MyGame.UI.MoveIndicator.Spawn(hit.point, hit.normal, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);
            }

            if (_isAttackMode)
            {
                BaseCombatUnitController clickedEnemy = hit.collider.GetComponentInParent<BaseCombatUnitController>();
                int moveIndex = 0;
                foreach (var unit in selectedUnits)
                {
                    if (unit != null)
                    {
                        BaseCombatUnitController combatUnit = unit.CombatController;
                        if (combatUnit != null)
                        {
                            if (clickedEnemy != null && clickedEnemy.faction != combatUnit.faction)
                            {
                                combatUnit.CommandAttack(clickedEnemy);
                                GameLog.Log($"[RTS] Chỉ định tấn công mục tiêu: {clickedEnemy.unitName}");
                            }
                            else
                            {
                                Vector3 targetPos = ResolveCommandDestination(hit.point + GetFormationOffset(moveIndex, 1.5f), hit.point);
                                combatUnit.CommandAttackMove(targetPos);
                                moveIndex++;
                            }
                        }
                    }
                }
            }
            else if (_isGatherMode)
            {
                ResourceNode clickedNode = hit.collider.GetComponentInParent<ResourceNode>();
                if (clickedNode != null && clickedNode.GetComponentInParent<RiceField>() != null)
                {
                    clickedNode = null;
                }
                BaseCombatUnitController clickedEnemy = hit.collider.GetComponentInParent<BaseCombatUnitController>();
                WildAnimalController animal = clickedEnemy != null ? clickedEnemy.GetComponent<WildAnimalController>() : null;

                if (animal != null)
                {
                    foreach (var unit in selectedUnits)
                    {
                        if (unit != null)
                        {
                            VillagerController villager = unit.VillagerController;
                            if (villager != null)
                            {
                                villager.CommandHunt(animal);
                                GameLog.Log($"[RTS] Chỉ định dân làng {unit.gameObject.name} săn thú hoang {animal.unitName}");
                            }
                        }
                    }
                }
                else
                {
                    if (clickedNode == null)
                    {
                        GridSystem grid = _gridSystem;
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
                        clickedNode.TriggerBounceEffect();
                        foreach (var unit in selectedUnits)
                        {
                            if (unit != null)
                            {
                                VillagerController villager = unit.VillagerController;
                                if (villager != null)
                                {
                                    villager.CommandGather(clickedNode, null);
                                    GameLog.Log($"[RTS] Chỉ định khai thác tài nguyên: {clickedNode.ResourceType}");
                                }
                            }
                        }
                    }
                    else
                    {
                        // Kiểm tra xem có chỉ định sửa chữa hoặc xây dựng công trình dở dang hay không
                        ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
                        BaseCombatUnitController clickedFriendlyUnit = hit.collider.GetComponentInParent<BaseCombatUnitController>();

                        if (clickedBuilding != null && !clickedBuilding.IsCompleted)
                        {
                            foreach (var unit in selectedUnits)
                            {
                                if (unit != null)
                                {
                                    VillagerController villager = unit.VillagerController;
                                    if (villager != null)
                                    {
                                        villager.CommandBuild(clickedBuilding);
                                        GameLog.Log($"[RTS] Chỉ định xây dựng (từ GatherMode): {clickedBuilding.gameObject.name}");
                                    }
                                }
                            }
                        }
                        else if (clickedFriendlyUnit != null && clickedFriendlyUnit.faction == UnitFaction.Player &&
                                 (clickedFriendlyUnit.GetComponent<BuildingCombatTarget>() != null || clickedFriendlyUnit.GetComponent<MainBuildingCombatTarget>() != null) &&
                                 clickedFriendlyUnit.currentHealth < clickedFriendlyUnit.maxHealth)
                        {
                            foreach (var unit in selectedUnits)
                            {
                                if (unit != null)
                                {
                                    VillagerController villager = unit.VillagerController;
                                    if (villager != null)
                                    {
                                        villager.CommandRepair(clickedFriendlyUnit);
                                        GameLog.Log($"[RTS] Chỉ định sửa chữa (từ GatherMode): {clickedFriendlyUnit.gameObject.name}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            RiceField clickedRiceField = hit.collider.GetComponentInParent<RiceField>();
                            if (clickedRiceField != null)
                            {
                                DistributeVillagersToRiceFields(clickedRiceField);
                            }
                        }
                    }
                }
            }
            else if (_isBuildMode)
            {
                ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
                BaseCombatUnitController clickedFriendlyUnit = hit.collider.GetComponentInParent<BaseCombatUnitController>();

                foreach (var unit in selectedUnits)
                {
                    if (unit != null)
                    {
                        VillagerController villager = unit.GetComponent<VillagerController>();
                        if (villager != null)
                        {
                            if (clickedBuilding != null && !clickedBuilding.IsCompleted)
                            {
                                villager.CommandBuild(clickedBuilding);
                                GameLog.Log($"[RTS] Chỉ định xây dựng: {clickedBuilding.gameObject.name}");
                            }
                            else if (clickedFriendlyUnit != null && clickedFriendlyUnit.faction == UnitFaction.Player &&
                                     (clickedFriendlyUnit.GetComponent<BuildingCombatTarget>() != null || clickedFriendlyUnit.GetComponent<MainBuildingCombatTarget>() != null) &&
                                     clickedFriendlyUnit.currentHealth < clickedFriendlyUnit.maxHealth)
                            {
                                villager.CommandRepair(clickedFriendlyUnit);
                                GameLog.Log($"[RTS] Chỉ định sửa chữa: {clickedFriendlyUnit.gameObject.name}");
                            }
                        }
                    }
                }
            }
        }

        ClearTargetingModes();
    }

    private void ClearTargetingModes()
    {
        _isAttackMode = false;
        _isGatherMode = false;
        _isBuildMode = false;

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.IsAttackTargetingMode = false;
            CursorManager.Instance.IsGatherTargetingMode = false;
            CursorManager.Instance.IsBuildTargetingMode = false;
        }
    }

    private void HandleBoxSelection()
    {
        bool shouldAutoDisableBoxSelect = IsBoxSelectMode;

        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            DeselectAll();
        }

        Vector2 endMousePos = _dragCurrentScreenPos;
        Rect selectionRect = new Rect(
            Mathf.Min(startMousePos.x, endMousePos.x),
            Mathf.Min(startMousePos.y, endMousePos.y),
            Mathf.Abs(startMousePos.x - endMousePos.x),
            Mathf.Abs(startMousePos.y - endMousePos.y)
        );

        foreach (var unit in SelectableUnit.AllUnits)
        {
            if (unit != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(unit.transform.position);
                // Đảm bảo unit nằm ở phía trước camera
                if (screenPos.z > 0 && selectionRect.Contains(new Vector2(screenPos.x, screenPos.y)))
                {
                    if (!selectedUnits.Contains(unit))
                    {
                        selectedUnits.Add(unit);
                        unit.Select();
                    }
                }
            }
        }

        if (shouldAutoDisableBoxSelect)
        {
            IsBoxSelectMode = false;
        }
    }

    void OnGUI()
    {
        if (isDragging && Vector2.Distance(startMousePos, _dragCurrentScreenPos) > 10f)
        {
            float startY = Screen.height - startMousePos.y;
            float currentY = Screen.height - _dragCurrentScreenPos.y;

            Rect rect = new Rect(
                Mathf.Min(startMousePos.x, _dragCurrentScreenPos.x),
                Mathf.Min(startY, currentY),
                Mathf.Abs(startMousePos.x - _dragCurrentScreenPos.x),
                Mathf.Abs(startY - currentY)
            );

            // Vẽ nền
            GUI.color = selectionBoxColor;
            GUI.DrawTexture(rect, whiteTexture);

            // Vẽ viền
            GUI.color = selectionBoxBorderColor;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, 2), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - 2, rect.width, 2), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, 2, rect.height), whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - 2, rect.yMin, 2, rect.height), whiteTexture);
        }
    }

    private void HandleRightClickCommand()
    {
        if (selectedUnits.Count == 0)
        {
            GameLog.Log("[RTS] Chưa chọn unit nào, không thể ra lệnh.");
            return;
        }

        Ray ray = _mainCamera != null ? _mainCamera.ScreenPointToRay(_commandScreenPos) : Camera.main.ScreenPointToRay(_commandScreenPos);
        if (TryGetCommandHit(ray, out RaycastHit hit))
        {
            GameLog.Log($"[RTS] Raycast RightClick trúng: {hit.collider.gameObject.name} tại điểm {hit.point}");

            // Play movement/order sound
            if (MyGame.Audio.AudioManager.Instance != null && selectedUnits.Count > 0)
            {
                MyGame.Audio.AudioManager.Instance.PlayVillagerMove(hit.point);
            }
            
            // 1. Kiểm tra xem click thẳng vào công trình đang cần xây dựng không
            ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
            WatchTowerGarrison clickedWatchTower = hit.collider.GetComponentInParent<WatchTowerGarrison>();

            // 2. Kiểm tra xem click thẳng vào tài nguyên (Collider của cây/đá)
            ResourceNode clickedNode = hit.collider.GetComponentInParent<ResourceNode>();
            if (clickedNode != null && IsHiddenByFog(clickedNode.gameObject))
            {
                clickedNode = null;
            }

            RiceField clickedRiceField = hit.collider.GetComponentInParent<RiceField>();
            AncientRuins clickedRuins = hit.collider.GetComponentInParent<AncientRuins>();
            if (clickedRuins != null && IsHiddenByFog(clickedRuins.gameObject))
            {
                clickedRuins = null;
            }
            
            // 3. Kiểm tra dự phòng xem click vào ô đất có tài nguyên không
            GridSystem grid = _gridSystem;
            if (clickedNode == null && grid != null)
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

            if (clickedNode != null && clickedNode.GetComponentInParent<RiceField>() != null)
            {
                clickedNode = null;
            }

            if (clickedNode != null)
            {
                clickedNode.TriggerBounceEffect();
            }

            // 4. Kiểm tra xem click vào Combat Unit đối thủ không
            BaseCombatUnitController clickedEnemy = hit.collider.GetComponentInParent<BaseCombatUnitController>();
 
            // Spawn Move/Attack Indicator (Chỉ spawn 1 cái duy nhất cho mỗi lượt click chuột phải)
            if (clickedEnemy != null && clickedEnemy.faction != UnitFaction.Player)
            {
                // Click vào kẻ địch hoặc thú rừng trung lập -> Indicator màu đỏ tấn công
                MyGame.UI.MoveIndicator.Spawn(clickedEnemy.transform.position, Vector3.up, Color.red, 1.3f, 0.45f);
            }
            else if (clickedNode != null)
            {
                // Click vào mỏ tài nguyên -> Indicator màu xanh lá cây
                MyGame.UI.MoveIndicator.Spawn(clickedNode.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);
            }
            else if (clickedBuilding != null && !clickedBuilding.IsCompleted)
            {
                // Click vào công trình xây dựng -> Indicator màu xanh lá cây
                MyGame.UI.MoveIndicator.Spawn(clickedBuilding.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.4f, 0.4f);
            }
            else if (clickedRuins != null)
            {
                // Click vào phế tích -> Indicator màu xanh lá cây hoặc đỏ tùy vào trạng thái dọn dẹp
                Color col = clickedRuins.IsCleared ? new Color(0.2f, 0.8f, 0.2f, 1.0f) : Color.red;
                MyGame.UI.MoveIndicator.Spawn(clickedRuins.transform.position, Vector3.up, col, 1.4f, 0.4f);
            }
            else if (clickedRiceField != null)
            {
                // Click vào ruộng lúa -> Indicator màu xanh lá cây tại tâm ruộng lúa
                MyGame.UI.MoveIndicator.Spawn(clickedRiceField.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.4f, 0.4f);
            }
            else
            {
                // Click di chuyển thường hoặc hành động thân thiện khác -> Indicator màu xanh lá cây tại điểm click và góc dốc của địa hình
                MyGame.UI.MoveIndicator.Spawn(hit.point, hit.normal, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);
            }
 
            // Chỉ số dùng để tính toán điểm đội hình di chuyển thường
            int moveIndex = 0;

            if (clickedRiceField != null)
            {
                DistributeVillagersToRiceFields(clickedRiceField);
            }

            using (s_dispatchMarker.Auto())
            {
                foreach (var unit in selectedUnits)
                {
                    if (unit != null)
                    {
                        VillagerController villager = unit.VillagerController;
                        BaseCombatUnitController combatUnit = unit.CombatController;

                        bool shouldGarrison = clickedWatchTower != null;
                        if (clickedWatchTower != null && villager != null)
                        {
                            BaseCombatUnitController towerUnit = clickedWatchTower.GetComponent<BaseCombatUnitController>();
                            if (towerUnit != null && towerUnit.currentHealth < towerUnit.maxHealth)
                            {
                                shouldGarrison = false; // Ưu tiên sửa chữa nếu chòi canh bị hỏng
                            }
                        }

                        if (shouldGarrison && clickedWatchTower.TrySendToGarrison(unit))
                        {
                            GameLog.Log($"[RTS] Da ra lenh {unit.gameObject.name} vao thap canh.");
                            continue;
                        }

                        if (villager != null)
                        {
                            if (clickedRiceField != null)
                            {
                                continue;
                            }

                            // A.0. Ưu tiên 0: Click vào Phế Tích Cổ để khai quật
                            if (clickedRuins != null)
                            {
                                if (!clickedRuins.IsCleared)
                                {
                                    GameLog.LogWarning("[RTS] Phế tích đang bị quái vật canh giữ! Hãy tiêu diệt chúng trước.");
                                }
                                else if (clickedRuins.IsExplored)
                                {
                                    GameLog.Log("[RTS] Phế tích này đã được khai quật xong.");
                                }
                                else
                                {
                                    villager.CommandExplore(clickedRuins);
                                    GameLog.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} đi khai quật phế tích.");
                                }
                                continue;
                            }

                            // A. Ưu tiên 1: Click vào công trình đang xây dựng dở dang -> Đi xây
                            if (clickedBuilding != null && !clickedBuilding.IsCompleted)
                            {
                                villager.CommandBuild(clickedBuilding);
                                GameLog.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} đi xây dựng {clickedBuilding.gameObject.name}");
                            }
                            // A.2. Ưu tiên 1.2: Click vào công trình thân thiện bị thương -> Sửa chữa
                            else if (clickedEnemy != null && clickedEnemy.faction == UnitFaction.Player &&
                                     (clickedEnemy.GetComponent<BuildingCombatTarget>() != null || clickedEnemy.GetComponent<MainBuildingCombatTarget>() != null) &&
                                     clickedEnemy.currentHealth < clickedEnemy.maxHealth)
                            {
                                villager.CommandRepair(clickedEnemy);
                                GameLog.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} đi sửa chữa {clickedEnemy.gameObject.name}");
                            }
                            // B. Ưu tiên 2: Click vào mỏ tài nguyên -> Đi khai thác
                            else if (clickedNode != null)
                            {
                                villager.CommandGather(clickedNode, null);
                                GameLog.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} khai thác {clickedNode.ResourceType}");
                            }
                            // B.2. Ưu tiên 2.2: Click vào thú hoang dã -> Đi săn
                            else if (clickedEnemy != null && clickedEnemy.faction == UnitFaction.Neutral && clickedEnemy.GetComponent<WildAnimalController>() != null)
                            {
                                WildAnimalController animal = clickedEnemy.GetComponent<WildAnimalController>();
                                villager.CommandHunt(animal);
                                GameLog.Log($"[RTS] Đã ra lệnh cho dân làng {unit.gameObject.name} đi săn thú hoang {clickedEnemy.unitName}");
                            }
                            else if (clickedRiceField != null)
                            {
                                villager.CommandFarm(clickedRiceField);
                                GameLog.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} chăm sóc ruộng lúa");
                            }
                            // C. Ưu tiên 3: Click vào đất trống -> Di chuyển
                            else
                            {
                                // Tính vị trí trong đội hình vòng tròn đồng tâm
                                Vector3 targetPos = ResolveCommandDestination(hit.point + GetFormationOffset(moveIndex, 1.2f), hit.point);
                                villager.CommandMoveTo(targetPos);
                                moveIndex++;
                            }
                        }
                        else if (combatUnit != null)
                        {
                            // Nếu click vào một đơn vị đối địch khác phe -> Tiến hành tấn công!
                            if (clickedEnemy != null && clickedEnemy.faction != combatUnit.faction)
                            {
                                combatUnit.CommandAttack(clickedEnemy);
                                GameLog.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} tấn công {clickedEnemy.unitName}");
                            }
                            else
                            {
                                // Tính vị trí trong đội hình vòng tròn đồng tâm
                                Vector3 targetPos = ResolveCommandDestination(hit.point + GetFormationOffset(moveIndex, 1.5f), hit.point);
                                combatUnit.CommandMove(targetPos);
                                moveIndex++;
                            }
                        }
                    }
                }
            }
        }
        else
        {
            GameLog.Log("[RTS] Raycast RightClick không trúng mặt đất hoặc object nào có Collider.");
        }
    }

    private bool TryGetCommandHit(Ray ray, out RaycastHit commandHit)
    {
        // 1. Thử dùng SphereCast để quét diện rộng tìm đối tượng tương tác (Unit, Thú, Tài nguyên, Nhà)
        // Giúp người chơi dễ dàng click trúng các mục tiêu nhỏ hoặc đang di chuyển nhanh (như gà)
        int sphereCount = Physics.SphereCastNonAlloc(ray, 0.75f, s_sphereCastHits, 1000f, _effectiveCommandLayerMask);
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (sphereCount >= s_sphereCastHits.Length)
        {
            UnitPerformanceMetrics.BufferOverflowCount++;
            if (Time.time >= _lastCommandSphereWarningTime + 3.0f)
            {
                Debug.LogWarning("[RTS] SphereCast buffer capacity reached / buffer saturation!");
                _lastCommandSphereWarningTime = Time.time;
            }
        }
        #endif

        int bestSphereIndex = -1;
        float minSphereDistance = float.MaxValue;

        for (int i = 0; i < sphereCount; i++)
        {
            Collider col = s_sphereCastHits[i].collider;
            if (col == null) continue;

            if (IsHiddenByFog(col.gameObject)) continue;

            // Kiểm tra xem đối tượng va chạm có thành phần tương tác được không
            bool isInteractable = col.GetComponentInParent<BaseCombatUnitController>() != null ||
                                  col.GetComponentInParent<ResourceNode>() != null ||
                                  col.GetComponentInParent<ConstructibleBuilding>() != null ||
                                  col.GetComponentInParent<WatchTowerGarrison>() != null ||
                                  col.GetComponentInParent<AncientRuins>() != null;

            if (isInteractable)
            {
                if (s_sphereCastHits[i].distance < minSphereDistance)
                {
                    minSphereDistance = s_sphereCastHits[i].distance;
                    bestSphereIndex = i;
                }
            }
        }

        if (bestSphereIndex != -1)
        {
            commandHit = s_sphereCastHits[bestSphereIndex];
            return true;
        }

        // 2. Dự phòng: Dùng Raycast chính xác thông thường (cho click di chuyển mặt đất, vv.)
        int raycastCount = Physics.RaycastNonAlloc(ray, s_raycastHits, 1000f, _effectiveCommandLayerMask);
        
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (raycastCount >= s_raycastHits.Length)
        {
            UnitPerformanceMetrics.BufferOverflowCount++;
            if (Time.time >= _lastCommandRaycastWarningTime + 3.0f)
            {
                Debug.LogWarning("[RTS] Raycast buffer capacity reached / buffer saturation!");
                _lastCommandRaycastWarningTime = Time.time;
            }
        }
        #endif

        int bestRaycastIndex = -1;
        float minRaycastDistance = float.MaxValue;

        for (int i = 0; i < raycastCount; i++)
        {
            Collider col = s_raycastHits[i].collider;
            if (col == null) continue;

            if (IsHiddenByFog(col.gameObject)) continue;

            if (s_raycastHits[i].distance < minRaycastDistance)
            {
                minRaycastDistance = s_raycastHits[i].distance;
                bestRaycastIndex = i;
            }
        }

        if (bestRaycastIndex != -1)
        {
            commandHit = s_raycastHits[bestRaycastIndex];
            return true;
        }

        commandHit = default;
        return false;
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

    private Vector3 ResolveCommandDestination(Vector3 desiredPosition, Vector3 fallbackCenter)
    {
        using (s_resolveDestinationMarker.Auto())
        {
            if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit desiredHit, CommandDestinationSampleRadius, WalkableNavMeshAreaMask))
            {
                return desiredHit.position;
            }

            if (NavMesh.SamplePosition(fallbackCenter, out NavMeshHit fallbackHit, CommandDestinationSampleRadius * 2f, WalkableNavMeshAreaMask))
            {
                return fallbackHit.position;
            }

            if (Terrain.activeTerrain != null)
            {
                desiredPosition.y = Terrain.activeTerrain.SampleHeight(desiredPosition) + Terrain.activeTerrain.transform.position.y;
            }
            else
            {
                desiredPosition.y = 0f;
            }

            return desiredPosition;
        }
    }

    public Vector3 GetFormationOffset(int index, float spacing)
    {
        if (index == 0) return Vector3.zero;

        // Xếp theo dạng các vòng tròn đồng tâm (Spiral / Concentric Circles Formation)
        // Vòng 1: 6 vị trí. Vòng 2: 12 vị trí. Vòng 3: 18 vị trí...
        int ring = 0;
        int currentCount = 0;
        int maxInRing = 0;

        while (index > currentCount)
        {
            ring++;
            maxInRing = ring * 6;
            currentCount += maxInRing;
        }

        int indexInRing = index - (currentCount - maxInRing);
        float angle = (360f / maxInRing) * indexInRing * Mathf.Deg2Rad;
        float radius = ring * spacing;

        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
    }

    private void HandleClickSelection()
    {
        float timeSinceLastClick = Time.time - _lastClickTime;
        bool isDoubleClick = timeSinceLastClick < DoubleClickTimeThreshold;
        _lastClickTime = Time.time;

        Ray ray = Camera.main.ScreenPointToRay(_commandScreenPos);
        // Sử dụng SphereCast (bán kính 1f) thay vì Raycast để dễ click trúng Unit nhỏ hoặc đang di chuyển
        if (Physics.SphereCast(ray, 1f, out RaycastHit hit, 1000f, unitLayerMask))
        {
            SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
            if (unit != null)
            {
                if (isDoubleClick)
                {
                    GameLog.Log($"[RTS] Đúp chuột trúng unit: {unit.gameObject.name}. Chọn tất cả unit cùng loại trên màn hình.");
                    SelectAllUnitsOfSameTypeOnScreen(unit);
                    return;
                }

                GameLog.Log($"[RTS] Đã click trúng unit: {unit.gameObject.name}");
                // Nếu giữ phím Shift, thêm vào danh sách đang chọn
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (!selectedUnits.Contains(unit))
                    {
                        selectedUnits.Add(unit);
                        unit.Select();
                    }
                    else
                    {
                        selectedUnits.Remove(unit);
                        unit.Deselect();
                    }
                }
                else
                {
                    // Chọn mới hoàn toàn
                    DeselectAll();
                    selectedUnits.Add(unit);
                    unit.Select();
                }
            }
            else
            {
                GameLog.Log("[RTS] Click trúng Collider thuộc Unit LayerMask nhưng không có component SelectableUnit.");
                // Bấm vào đất hoặc object khác không phải unit -> bỏ chọn hết
                DeselectAll();
            }
        }
        else
        {
            GameLog.Log("[RTS] Click chuột trái không trúng Unit nào thuộc LayerMask quy định.");
            // Bấm vào chỗ trống -> bỏ chọn hết
            DeselectAll();
        }
    }

    private void SelectAllUnitsOfSameTypeOnScreen(SelectableUnit targetUnit)
    {
        DeselectAll();

        foreach (var unit in SelectableUnit.AllUnits)
        {
            if (unit != null && IsSameUnitType(targetUnit, unit))
            {
                if (IsUnitOnScreen(unit))
                {
                    selectedUnits.Add(unit);
                    unit.Select();
                }
            }
        }
    }

    private bool IsSameUnitType(SelectableUnit unitA, SelectableUnit unitB)
    {
        if (unitA == null || unitB == null) return false;

        // Check if both are villagers
        bool isVillagerA = unitA.GetComponent<VillagerController>() != null;
        bool isVillagerB = unitB.GetComponent<VillagerController>() != null;
        if (isVillagerA && isVillagerB) return true;
        if (isVillagerA || isVillagerB) return false;

        // Check if both are combat units and match unitName
        var combatA = unitA.GetComponent<BaseCombatUnitController>();
        var combatB = unitB.GetComponent<BaseCombatUnitController>();
        if (combatA != null && combatB != null)
        {
            return combatA.unitName == combatB.unitName;
        }

        return false;
    }

    private bool IsUnitOnScreen(SelectableUnit unit)
    {
        if (unit == null) return false;
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector3 screenPos = cam.WorldToScreenPoint(unit.transform.position);
        return screenPos.z > 0 &&
               screenPos.x >= 0 && screenPos.x <= Screen.width &&
               screenPos.y >= 0 && screenPos.y <= Screen.height;
    }

    public void DeselectAll()
    {
        foreach (var unit in selectedUnits)
        {
            if (unit != null)
            {
                unit.Deselect();
            }
        }
        selectedUnits.Clear();
    }

    public void DeselectUnit(SelectableUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        if (selectedUnits.Remove(unit))
        {
            unit.Deselect();
        }
    }

    private void DistributeVillagersToRiceFields(RiceField clickedRiceField)
    {
        if (clickedRiceField == null) return;

        // 1. Thu thập tất cả các dân làng được chọn
        List<VillagerController> selectedVillagers = new List<VillagerController>();
        foreach (var unit in selectedUnits)
        {
            if (unit != null)
            {
                VillagerController villager = unit.GetComponent<VillagerController>();
                if (villager != null)
                {
                    selectedVillagers.Add(villager);
                }
            }
        }

        if (selectedVillagers.Count == 0) return;

        // 2. Tìm tất cả các ruộng lúa trong cảnh
        RiceField[] allFields = Object.FindObjectsByType<RiceField>(FindObjectsInactive.Exclude);
        List<RiceField> nearbyFields = new List<RiceField>();

        // Lọc các ruộng lúa trong bán kính 15m
        foreach (var field in allFields)
        {
            if (field != null && Vector3.Distance(field.transform.position, clickedRiceField.transform.position) <= 15f)
            {
                nearbyFields.Add(field);
            }
        }

        // 3. Sắp xếp các ruộng lúa theo khoảng cách đến ruộng được click (ruộng được click sẽ ở vị trí đầu tiên)
        nearbyFields.Sort((a, b) => 
            Vector3.Distance(a.transform.position, clickedRiceField.transform.position)
            .CompareTo(Vector3.Distance(b.transform.position, clickedRiceField.transform.position))
        );

        // 4. Phân công từng dân làng cho ruộng lúa
        for (int i = 0; i < selectedVillagers.Count; i++)
        {
            if (i < nearbyFields.Count)
            {
                selectedVillagers[i].CommandFarm(nearbyFields[i]);
                GameLog.Log($"[RTS] Tự động phân công {selectedVillagers[i].gameObject.name} chăm sóc ruộng lúa {nearbyFields[i].gameObject.name}");
            }
            else
            {
                selectedVillagers[i].GoIdle();
                GameLog.Log($"[RTS] Cư dân thừa {selectedVillagers[i].gameObject.name} được đặt về Idle");
            }
        }
    }

    private bool _touchStartedOverUI = false;
    private Vector2 _dragCurrentScreenPos;

    private bool IsPointerOverUIObject(Vector2 screenPosition)
    {
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;
        UnityEngine.EventSystems.PointerEventData eventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
        eventData.position = screenPosition;
        List<UnityEngine.EventSystems.RaycastResult> results = new List<UnityEngine.EventSystems.RaycastResult>();
        UnityEngine.EventSystems.EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    public bool IsPointerOverUI(Vector2 screenPosition)
    {
        bool isOverUGUI = IsPointerOverUIObject(screenPosition);
        bool isOverIMGUI = BuildingSelectionUI.Instance != null && BuildingSelectionUI.Instance.IsMouseOverUI();
        return isOverUGUI || isOverIMGUI;
    }

    private void TriggerTargetBounce(GameObject target)
    {
        if (target == null) return;

        // Nếu là ResourceNode thì dùng luôn hiệu ứng của nó
        var node = target.GetComponentInParent<ResourceNode>();
        if (node != null)
        {
            node.TriggerBounceEffect();
            return;
        }

        // Nếu là Công trình hoặc Phế tích thì dynamic attach TargetBounceEffect
        var building = target.GetComponentInParent<ConstructibleBuilding>();
        var ruins = target.GetComponentInParent<AncientRuins>();
        var tower = target.GetComponentInParent<WatchTowerGarrison>();

        if (building != null || ruins != null || tower != null)
        {
            var parent = target.transform.parent;
            var attachTarget = parent != null ? parent.gameObject : target;
            var bounce = attachTarget.GetComponent<CloudTerraceRealm.UI.TargetBounceEffect>();
            if (bounce == null)
            {
                bounce = attachTarget.AddComponent<CloudTerraceRealm.UI.TargetBounceEffect>();
            }
            if (bounce != null)
            {
                bounce.TriggerBounce();
            }
        }
    }

    private GameObject GetTargetBounceObject(RaycastHit hit)
    {
        ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
        if (node == null)
        {
            GridSystem grid = FindAnyObjectByType<GridSystem>();
            if (grid != null)
            {
                grid.GetXY(hit.point, out int gridX, out int gridZ);
                GridCell cell = grid.GetCell(gridX, gridZ);
                if (cell != null && cell.hasResource && cell.resourceObject != null && !IsHiddenByFog(cell.resourceObject))
                {
                    node = cell.resourceObject.GetComponent<ResourceNode>();
                }
            }
        }
        return node != null ? node.gameObject : hit.collider.gameObject;
    }

    private void RefreshSelectionBoxOverlay()
    {
        SelectionBoxOverlayUI overlay = SelectionBoxOverlayUI.Instance;
        if (overlay == null)
        {
            return;
        }

        if (isDragging && Vector2.Distance(startMousePos, _dragCurrentScreenPos) > 10f)
        {
            overlay.Show(startMousePos, _dragCurrentScreenPos, selectionBoxColor, selectionBoxBorderColor);
        }
        else
        {
            overlay.Hide();
        }
    }

    private bool TryHandleSelectedVillagerResourceClick(Ray ray)
    {
        if (selectedUnits.Count == 0)
        {
            return false;
        }

        bool hasVillager = false;
        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit != null && unit.GetComponent<VillagerController>() != null)
            {
                hasVillager = true;
                break;
            }
        }

        if (!hasVillager || !TryGetCommandHit(ray, out RaycastHit hit))
        {
            return false;
        }

        ResourceNode clickedNode = hit.collider.GetComponentInParent<ResourceNode>();
        if (clickedNode == null)
        {
            GridSystem grid = FindAnyObjectByType<GridSystem>();
            if (grid != null)
            {
                grid.GetXY(hit.point, out int gridX, out int gridZ);
                GridCell cell = grid.GetCell(gridX, gridZ);
                if (cell != null && cell.hasResource && cell.resourceObject != null && !IsHiddenByFog(cell.resourceObject))
                {
                    clickedNode = cell.resourceObject.GetComponent<ResourceNode>();
                }
            }
        }

        if (clickedNode == null || IsHiddenByFog(clickedNode.gameObject) || clickedNode.GetComponentInParent<RiceField>() != null)
        {
            return false;
        }

        clickedNode.TriggerBounceEffect();
        MyGame.UI.MoveIndicator.Spawn(clickedNode.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);

        foreach (SelectableUnit unit in selectedUnits)
        {
            if (unit == null)
            {
                continue;
            }

            VillagerController villager = unit.GetComponent<VillagerController>();
            if (villager != null)
            {
                villager.CommandGather(clickedNode, null);
                GameLog.Log($"[RTS] Left-click chỉ định khai thác tài nguyên: {clickedNode.ResourceType}");
            }
        }

        return true;
    }

}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public static class UnitPerformanceMetrics
{
    public static int SetDestinationCount;
    public static int CombatScanCount;
    public static int KiteScanCount;
    public static int HuntRepathCount;
    public static int BufferOverflowCount;

    private static float _lastReportTime;

    public static void ReportIfNeeded()
    {
        if (UnityEngine.Time.time >= _lastReportTime + 4.0f)
        {
            int pendingCount = 0;
            for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
            {
                var unit = BaseCombatUnitController.Registry[i];
                if (unit != null && unit.enabled && unit.gameObject.activeInHierarchy)
                {
                    var agent = unit.NavAgent;
                    if (agent != null && agent.enabled && agent.pathPending)
                    {
                        pendingCount++;
                    }
                }
            }
            for (int i = 0; i < VillagerController.AllVillagers.Count; i++)
            {
                var villager = VillagerController.AllVillagers[i];
                if (villager != null && villager.enabled && villager.gameObject.activeInHierarchy)
                {
                    var agent = villager.NavAgent;
                    if (agent != null && agent.enabled && agent.pathPending)
                    {
                        pendingCount++;
                    }
                }
            }

            UnityEngine.Debug.Log($"[Performance Metrics] (Last 4s)\n" +
                                  $"- SetDestination calls: {SetDestinationCount}\n" +
                                  $"- Combat scans: {CombatScanCount}\n" +
                                  $"- Kite scans: {KiteScanCount}\n" +
                                  $"- Hunt repaths: {HuntRepathCount}\n" +
                                  $"- Path pending units: {pendingCount}\n" +
                                  $"- Buffer overflows: {BufferOverflowCount}");

            // Reset after report
            SetDestinationCount = 0;
            CombatScanCount = 0;
            KiteScanCount = 0;
            HuntRepathCount = 0;
            BufferOverflowCount = 0;
            _lastReportTime = UnityEngine.Time.time;
        }
    }
}
#endif
