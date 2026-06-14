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
        // Bắt đầu click trái
        if (Input.GetMouseButtonDown(0))
        {
            startMousePos = Input.mousePosition;
            isDragging = true;
        }

        // Thả chuột trái
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            // Nếu kéo chuột quá 10 pixel thì tính là quét vùng
            if (Vector2.Distance(startMousePos, Input.mousePosition) > 10f)
            {
                HandleBoxSelection();
            }
            else
            {
                HandleClickSelection();
            }
        }
        
        // Khi click chuột phải ra lệnh
        if (Input.GetMouseButtonDown(1))
        {
            HandleRightClickCommand();
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

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (TryGetCommandHit(ray, out RaycastHit hit))
        {
            Debug.Log($"[RTS] Raycast RightClick trúng: {hit.collider.gameObject.name} tại điểm {hit.point}");
            
            // 1. Kiểm tra xem click thẳng vào công trình đang cần xây dựng không
            ConstructibleBuilding clickedBuilding = hit.collider.GetComponentInParent<ConstructibleBuilding>();
            WatchTowerGarrison clickedWatchTower = hit.collider.GetComponentInParent<WatchTowerGarrison>();

            // 2. Kiểm tra xem click thẳng vào tài nguyên (Collider của cây/đá)
            ResourceNode clickedNode = hit.collider.GetComponentInParent<ResourceNode>();
            if (clickedNode != null && IsHiddenByFog(clickedNode.gameObject))
            {
                clickedNode = null;
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

            if (clickedNode != null)
            {
                clickedNode.TriggerBounceEffect();
            }

            // 4. Kiểm tra xem click vào Combat Unit đối thủ không
            BaseCombatUnitController clickedEnemy = hit.collider.GetComponentInParent<BaseCombatUnitController>();

            // Chỉ số dùng để tính toán điểm đội hình di chuyển thường
            int moveIndex = 0;

            foreach (var unit in selectedUnits)
            {
                if (unit != null)
                {
                    VillagerController villager = unit.GetComponent<VillagerController>();
                    BaseCombatUnitController combatUnit = unit.GetComponent<BaseCombatUnitController>();

                    if (clickedWatchTower != null && clickedWatchTower.TrySendToGarrison(unit))
                    {
                        Debug.Log($"[RTS] Da ra lenh {unit.gameObject.name} vao thap canh.");
                        continue;
                    }

                    if (villager != null)
                    {
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

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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
}
