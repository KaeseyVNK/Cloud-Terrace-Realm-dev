using UnityEngine;
using System.Collections.Generic;

public class UnitSelectionManager : MonoBehaviour
{
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
    public static bool IsBoxSelectMode { get; set; } = false;

    private float _touchStartTime = 0f;
    private Vector2 _touchStartPos;
    private bool _hasTouchMoved = false;

    public event System.Action OnSelectionChanged;
    private readonly List<SelectableUnit> _prevSelectedUnits = new List<SelectableUnit>();

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void Start()
    {
        whiteTexture = new Texture2D(1, 1);
        whiteTexture.SetPixel(0, 0, Color.white);
        whiteTexture.Apply();
    }

    void Update()
    {
        // Bấm Esc để hủy chế độ ra lệnh chủ động
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                ClearTargetingModes();
                Debug.Log("[RTS] Hủy chế độ ra lệnh chỉ định");
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
                    Debug.Log("[RTS] Vào chế độ chỉ định Tấn công (Attack targeting mode)");
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
                    Debug.Log("[RTS] Vào chế độ chỉ định Khai thác (Gather targeting mode)");
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
                    Debug.Log("[RTS] Vào chế độ chỉ định Xây dựng (Build targeting mode)");
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
            Debug.Log("[RTS] ActionPanel: Vào chế độ chỉ định Xây dựng (Build)");
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
            Debug.Log("[RTS] ActionPanel: Vào chế độ chỉ định Tấn công (Attack)");
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
            Debug.Log("[RTS] ActionPanel: Vào chế độ chỉ định Khai thác/Sửa chữa (Gather/Repair)");
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
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
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
                isDragging = true;
            }
        }
        else if (touch.phase == TouchPhase.Moved)
        {
            if (Vector2.Distance(_touchStartPos, touch.position) > 15f)
            {
                _hasTouchMoved = true;
            }
        }
        else if (touch.phase == TouchPhase.Ended)
        {
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

                if (selectedUnits.Count > 0)
                {
                    Ray ray = Camera.main.ScreenPointToRay(touch.position);
                    bool tappedFriendlyUnit = false;

                    if (Physics.SphereCast(ray, 1f, out RaycastHit hit, 1000f, unitLayerMask))
                    {
                        SelectableUnit unit = hit.collider.GetComponentInParent<SelectableUnit>();
                        if (unit != null)
                        {
                            BaseCombatUnitController combatUnit = unit.GetComponent<BaseCombatUnitController>();
                            VillagerController villager = unit.GetComponent<VillagerController>();
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
            // Avoid starting selection logic when clicking on UGUI elements
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                _commandScreenPos = Input.mousePosition;
                ExecuteTargetingCommand();
                return;
            }

            startMousePos = Input.mousePosition;
            isDragging = true;
        }

        // Thả chuột trái
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
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
            // Avoid issuing commands when clicking on UGUI elements
            if (UnityEngine.EventSystems.EventSystem.current != null && 
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (_isAttackMode || _isGatherMode || _isBuildMode)
            {
                ClearTargetingModes();
                Debug.Log("[RTS] Hủy chế độ ra lệnh bằng Chuột Phải");
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
        Ray ray = Camera.main.ScreenPointToRay(_commandScreenPos);
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
                if (clickedNode != null)
                    MyGame.UI.MoveIndicator.Spawn(clickedNode.transform.position, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);
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
                        BaseCombatUnitController combatUnit = unit.GetComponent<BaseCombatUnitController>();
                        if (combatUnit != null)
                        {
                            if (clickedEnemy != null && clickedEnemy.faction != combatUnit.faction)
                            {
                                combatUnit.CommandAttack(clickedEnemy);
                                Debug.Log($"[RTS] Chỉ định tấn công mục tiêu: {clickedEnemy.unitName}");
                            }
                            else
                            {
                                Vector3 targetPos = hit.point + GetFormationOffset(moveIndex, 1.5f);
                                if (Terrain.activeTerrain != null)
                                {
                                    targetPos.y = Terrain.activeTerrain.SampleHeight(targetPos) + Terrain.activeTerrain.transform.position.y;
                                }
                                else
                                {
                                    targetPos.y = 0f;
                                }
                                combatUnit.CommandAttackMove(targetPos);
                                moveIndex++;
                                Debug.Log($"[RTS] Chỉ định di chuyển tấn công tới: {targetPos}");
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
                            VillagerController villager = unit.GetComponent<VillagerController>();
                            if (villager != null)
                            {
                                villager.CommandHunt(animal);
                                Debug.Log($"[RTS] Chỉ định dân làng {unit.gameObject.name} săn thú hoang {animal.unitName}");
                            }
                        }
                    }
                }
                else
                {
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
                        clickedNode.TriggerBounceEffect();
                        foreach (var unit in selectedUnits)
                        {
                            if (unit != null)
                            {
                                VillagerController villager = unit.GetComponent<VillagerController>();
                                if (villager != null)
                                {
                                    villager.CommandGather(clickedNode, null);
                                    Debug.Log($"[RTS] Chỉ định khai thác tài nguyên: {clickedNode.ResourceType}");
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
                                Debug.Log($"[RTS] Chỉ định xây dựng: {clickedBuilding.gameObject.name}");
                            }
                            else if (clickedFriendlyUnit != null && clickedFriendlyUnit.faction == UnitFaction.Player &&
                                     (clickedFriendlyUnit.GetComponent<BuildingCombatTarget>() != null || clickedFriendlyUnit.GetComponent<MainBuildingCombatTarget>() != null) &&
                                     clickedFriendlyUnit.currentHealth < clickedFriendlyUnit.maxHealth)
                            {
                                villager.CommandRepair(clickedFriendlyUnit);
                                Debug.Log($"[RTS] Chỉ định sửa chữa: {clickedFriendlyUnit.gameObject.name}");
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
        if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
        {
            DeselectAll();
        }

        Vector2 endMousePos = Input.mousePosition;
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
    }

    void OnGUI()
    {
        if (isDragging && Vector2.Distance(startMousePos, Input.mousePosition) > 10f)
        {
            float startY = Screen.height - startMousePos.y;
            float currentY = Screen.height - Input.mousePosition.y;

            Rect rect = new Rect(
                Mathf.Min(startMousePos.x, Input.mousePosition.x),
                Mathf.Min(startY, currentY),
                Mathf.Abs(startMousePos.x - Input.mousePosition.x),
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
            Debug.Log("[RTS] Chưa chọn unit nào, không thể ra lệnh.");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(_commandScreenPos);
        if (TryGetCommandHit(ray, out RaycastHit hit))
        {
            Debug.Log($"[RTS] Raycast RightClick trúng: {hit.collider.gameObject.name} tại điểm {hit.point}");

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
            GridSystem grid = FindAnyObjectByType<GridSystem>();
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

            foreach (var unit in selectedUnits)
            {
                if (unit != null)
                {
                    VillagerController villager = unit.GetComponent<VillagerController>();
                    BaseCombatUnitController combatUnit = unit.GetComponent<BaseCombatUnitController>();

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
                        Debug.Log($"[RTS] Da ra lenh {unit.gameObject.name} vao thap canh.");
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
                                Debug.LogWarning("[RTS] Phế tích đang bị quái vật canh giữ! Hãy tiêu diệt chúng trước.");
                            }
                            else if (clickedRuins.IsExplored)
                            {
                                Debug.Log("[RTS] Phế tích này đã được khai quật xong.");
                            }
                            else
                            {
                                villager.CommandExplore(clickedRuins);
                                Debug.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} đi khai quật phế tích.");
                            }
                            continue;
                        }

                        // A. Ưu tiên 1: Click vào công trình đang xây dựng dở dang -> Đi xây
                        if (clickedBuilding != null && !clickedBuilding.IsCompleted)
                        {
                            villager.CommandBuild(clickedBuilding);
                            Debug.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} đi xây dựng {clickedBuilding.gameObject.name}");
                        }
                        // A.2. Ưu tiên 1.2: Click vào công trình thân thiện bị thương -> Sửa chữa
                        else if (clickedEnemy != null && clickedEnemy.faction == UnitFaction.Player &&
                                 (clickedEnemy.GetComponent<BuildingCombatTarget>() != null || clickedEnemy.GetComponent<MainBuildingCombatTarget>() != null) &&
                                 clickedEnemy.currentHealth < clickedEnemy.maxHealth)
                        {
                            villager.CommandRepair(clickedEnemy);
                            Debug.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} đi sửa chữa {clickedEnemy.gameObject.name}");
                        }
                        // B. Ưu tiên 2: Click vào mỏ tài nguyên -> Đi khai thác
                        else if (clickedNode != null)
                        {
                            villager.CommandGather(clickedNode, null);
                            Debug.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} khai thác {clickedNode.ResourceType}");
                        }
                        // B.2. Ưu tiên 2.2: Click vào thú hoang dã -> Đi săn
                        else if (clickedEnemy != null && clickedEnemy.faction == UnitFaction.Neutral && clickedEnemy.GetComponent<WildAnimalController>() != null)
                        {
                            WildAnimalController animal = clickedEnemy.GetComponent<WildAnimalController>();
                            villager.CommandHunt(animal);
                            Debug.Log($"[RTS] Đã ra lệnh cho dân làng {unit.gameObject.name} đi săn thú hoang {clickedEnemy.unitName}");
                        }
                        else if (clickedRiceField != null)
                        {
                            villager.CommandFarm(clickedRiceField);
                            Debug.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} chăm sóc ruộng lúa");
                        }
                        // C. Ưu tiên 3: Click vào đất trống -> Di chuyển
                        else
                        {
                            // Tính vị trí trong đội hình vòng tròn đồng tâm
                            Vector3 targetPos = hit.point + GetFormationOffset(moveIndex, 1.2f);
                            if (Terrain.activeTerrain != null)
                            {
                                targetPos.y = Terrain.activeTerrain.SampleHeight(targetPos) + Terrain.activeTerrain.transform.position.y;
                            }
                            else
                            {
                                targetPos.y = 0f;
                            }
                            villager.CommandMoveTo(targetPos);
                            moveIndex++;
                            Debug.Log($"[RTS] Đã ra lệnh di chuyển cho {unit.gameObject.name} tới vị trí đội hình: {targetPos}");
                        }
                    }
                    else if (combatUnit != null)
                    {
                        // Nếu click vào một đơn vị đối địch khác phe -> Tiến hành tấn công!
                        if (clickedEnemy != null && clickedEnemy.faction != combatUnit.faction)
                        {
                            combatUnit.CommandAttack(clickedEnemy);
                            Debug.Log($"[RTS] Đã ra lệnh {unit.gameObject.name} tấn công {clickedEnemy.unitName}");
                        }
                        else
                        {
                            // Tính vị trí trong đội hình vòng tròn đồng tâm
                            Vector3 targetPos = hit.point + GetFormationOffset(moveIndex, 1.5f);
                            if (Terrain.activeTerrain != null)
                            {
                                targetPos.y = Terrain.activeTerrain.SampleHeight(targetPos) + Terrain.activeTerrain.transform.position.y;
                            }
                            else
                            {
                                targetPos.y = 0f;
                            }
                            combatUnit.CommandMove(targetPos);
                            moveIndex++;
                            Debug.Log($"[RTS] Đã ra lệnh di chuyển {unit.gameObject.name} tới vị trí đội hình: {targetPos}");
                        }
                    }
                }
            }
        }
        else
        {
            Debug.Log("[RTS] Raycast RightClick không trúng mặt đất hoặc object nào có Collider.");
        }
    }

    private bool TryGetCommandHit(Ray ray, out RaycastHit commandHit)
    {
        // 1. Thử dùng SphereCast để quét diện rộng tìm đối tượng tương tác (Unit, Thú, Tài nguyên, Nhà)
        // Giúp người chơi dễ dàng click trúng các mục tiêu nhỏ hoặc đang di chuyển nhanh (như gà)
        RaycastHit[] sphereHits = Physics.SphereCastAll(ray, 0.75f, 1000f);
        System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < sphereHits.Length; i++)
        {
            Collider col = sphereHits[i].collider;
            if (col == null)
            {
                continue;
            }

            if (IsHiddenByFog(col.gameObject))
            {
                continue;
            }

            // Kiểm tra xem đối tượng va chạm có thành phần tương tác được không
            bool isInteractable = col.GetComponentInParent<BaseCombatUnitController>() != null ||
                                  col.GetComponentInParent<ResourceNode>() != null ||
                                  col.GetComponentInParent<ConstructibleBuilding>() != null ||
                                  col.GetComponentInParent<WatchTowerGarrison>() != null ||
                                  col.GetComponentInParent<AncientRuins>() != null;

            if (isInteractable)
            {
                commandHit = sphereHits[i];
                return true;
            }
        }

        // 2. Dự phòng: Dùng Raycast chính xác thông thường (cho click di chuyển mặt đất, vv.)
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider == null)
            {
                continue;
            }

            if (IsHiddenByFog(hits[i].collider.gameObject))
            {
                continue;
            }

            commandHit = hits[i];
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

    private Vector3 GetFormationOffset(int index, float spacing)
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
                    Debug.Log($"[RTS] Đúp chuột trúng unit: {unit.gameObject.name}. Chọn tất cả unit cùng loại trên màn hình.");
                    SelectAllUnitsOfSameTypeOnScreen(unit);
                    return;
                }

                Debug.Log($"[RTS] Đã click trúng unit: {unit.gameObject.name}");
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
                Debug.Log("[RTS] Click trúng Collider thuộc Unit LayerMask nhưng không có component SelectableUnit.");
                // Bấm vào đất hoặc object khác không phải unit -> bỏ chọn hết
                DeselectAll();
            }
        }
        else
        {
            Debug.Log("[RTS] Click chuột trái không trúng Unit nào thuộc LayerMask quy định.");
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
                Debug.Log($"[RTS] Tự động phân công {selectedVillagers[i].gameObject.name} chăm sóc ruộng lúa {nearbyFields[i].gameObject.name}");
            }
            else
            {
                selectedVillagers[i].GoIdle();
                Debug.Log($"[RTS] Cư dân thừa {selectedVillagers[i].gameObject.name} được đặt về Idle");
            }
        }
    }
}
