using UnityEngine;

public class TestProductionUI : MonoBehaviour
{
    private const float PanelX = 10f;
    private const float PanelY = 10f;
    private const float ProductionPanelWidth = 520f;
    private const float ResearchPanelWidth = 520f;
    private const float PanelMaxBottomPadding = 20f;
    private const float ProductionRowHeight = 56f;
    private const float ResearchRowHeight = 78f;

    public static TestProductionUI Instance { get; private set; }

    private BuildingProduction selectedProduction;
    private BlacksmithResearch selectedResearch;

    public BuildingProduction SelectedProduction => selectedProduction;
    public BlacksmithResearch SelectedResearch => selectedResearch;

    private Vector2 productionScrollPosition;
    private Vector2 researchScrollPosition;
    private bool _isRallyTargetingMode = false;
    private BuildingProduction _rallyTargetOverride;

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        // Nhấn Esc để hủy chế độ đặt Rally Point
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            _isRallyTargetingMode = false;
            _rallyTargetOverride = null;
        }

        if (_isRallyTargetingMode)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (!IsMouseOverActivePanel())
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                    BuildingProduction targetProd = _rallyTargetOverride != null ? _rallyTargetOverride : selectedProduction;
                    if (targetProd == null && MainBuildingUI.Instance != null && MainBuildingUI.Instance.SelectedMainBuilding != null)
                    {
                        targetProd = MainBuildingUI.Instance.SelectedMainBuilding.GetComponent<BuildingProduction>() ?? 
                                     MainBuildingUI.Instance.SelectedMainBuilding.GetComponentInChildren<BuildingProduction>();
                    }

                    if (targetProd != null)
                    {
                        Debug.Log("[TestProductionUI] Setting Rally Point for: " + targetProd.gameObject.name + " at position " + hit.point);
                        targetProd.SetRallyFromHit(hit);
                        targetProd.SetRallyFlagVisible(true);
                    }
                    else
                    {
                        Debug.LogWarning("[TestProductionUI] Cannot set Rally Point: targetProd is null!");
                    }
                    }
                    _isRallyTargetingMode = false;
                    _rallyTargetOverride = null;
                }
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Chọn công trình bằng chuột phải, hoặc bằng chuột trái/chạm
        bool isSelectTriggered = Input.GetMouseButtonDown(1) || 
                                 (Input.GetMouseButtonDown(0) && !IsMouseOverActivePanel());

        if (isSelectTriggered)
        {

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (TryGetProductionFromHit(hit, out BuildingProduction prod, out GameObject clickedBuilding))
                {
                    ConstructibleBuilding cb = clickedBuilding.GetComponent<ConstructibleBuilding>();
                    if (cb != null && !cb.IsCompleted)
                    {
                        Debug.LogWarning("Không thể chọn: Công trình này đang được xây dựng chưa hoàn thành!");
                        DeselectProduction();
                        return;
                    }

                    SelectProduction(prod);
                    Debug.Log("Đã chọn công trình để sản xuất: " + clickedBuilding.name);
                    return;
                }

                if (TryGetResearchFromHit(hit, out BlacksmithResearch research, out GameObject clickedResearchBuilding))
                {
                    ConstructibleBuilding cb = clickedResearchBuilding.GetComponent<ConstructibleBuilding>();
                    if (cb != null && !cb.IsCompleted)
                    {
                        Debug.LogWarning("Không thể chọn: Công trình này đang được xây dựng chưa hoàn thành!");
                        DeselectResearch();
                        return;
                    }

                    SelectResearch(research);
                    Debug.Log("Đã chọn lò rèn để nghiên cứu: " + clickedResearchBuilding.name);
                    return;
                }

                // Nếu click chuột phải (hoặc tap khi đã chọn công trình sản xuất và không bấm trúng cái gì khác)
                // Ta chỉ đặt rally point bằng chuột phải. Nếu là chuột trái, ta không tự động đặt rally point ở đây (tránh nhầm lẫn với deselect).
                if (Input.GetMouseButtonDown(1) && selectedProduction != null)
                {
                    selectedProduction.SetRallyFromHit(hit);
                    return;
                }

                if (Input.GetMouseButtonDown(1))
                {
                    Debug.Log("Không có công trình sản xuất nào đang được chọn để đặt rally point.");
                }
            }
            else
            {
                if (Input.GetMouseButtonDown(1))
                {
                    Debug.Log("Chuột phải không trúng bất kỳ Collider nào!");
                }
            }
        }
    }

    private void HandleLeftClickDeselect()
    {
        if ((selectedProduction == null && selectedResearch == null) || IsMouseOverActivePanel())
        {
            return;
        }

        // Avoid deselecting when clicking on UGUI elements (e.g. blacksmith cards)
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (TryGetProductionFromHit(hit, out _, out _))
            {
                return;
            }

            if (TryGetResearchFromHit(hit, out _, out _))
            {
                return;
            }

            // Cũng bỏ qua bỏ chọn nếu bấm trúng tháp canh phòng thủ
            if (hit.collider.GetComponentInParent<WatchTowerGarrison>() != null)
            {
                return;
            }
        }

        DeselectProduction();
        DeselectResearch();
    }

    private void SelectProduction(BuildingProduction production)
    {
        Debug.Log("[TestProductionUI] SelectProduction called for: " + (production != null ? production.gameObject.name : "null"));

        if (selectedProduction != null && selectedProduction != production)
        {
            selectedProduction.SetRallyFlagVisible(false);
        }

        selectedProduction = production;
        selectedResearch = null;

        if (UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.DeselectAll();
        }

        if (selectedProduction != null)
        {
            selectedProduction.SetRallyFlagVisible(true);

            // Bỏ chọn tháp canh để tránh chồng chéo UI
            WatchTowerGarrisonUI watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
            if (watchTowerUI != null)
            {
                watchTowerUI.DeselectWatchTower();
            }

            MainBuildingUI mainBuildingUI = FindAnyObjectByType<MainBuildingUI>();
            if (mainBuildingUI != null)
            {
                var mbTarget = production.GetComponent<MainBuildingCombatTarget>() ?? 
                               production.GetComponentInChildren<MainBuildingCombatTarget>();
                if (mbTarget != null)
                {
                    mainBuildingUI.SelectMainBuilding(mbTarget);
                }
                else
                {
                    mainBuildingUI.DeselectMainBuilding();
                }
            }
        }
    }

    public void DeselectProduction()
    {
        if (selectedProduction != null)
        {
            selectedProduction.SetRallyFlagVisible(false);
        }

        selectedProduction = null;
        _isRallyTargetingMode = false;
        _rallyTargetOverride = null;
    }

    public void BeginRallyTargeting(BuildingProduction targetProduction = null)
    {
        _rallyTargetOverride = targetProduction;
        _isRallyTargetingMode = true;
    }

    public void ToggleRallyTargeting(BuildingProduction targetProduction = null)
    {
        if (_isRallyTargetingMode && (_rallyTargetOverride == targetProduction || targetProduction == null))
        {
            _isRallyTargetingMode = false;
            _rallyTargetOverride = null;
            return;
        }

        BeginRallyTargeting(targetProduction);
    }

    private void SelectResearch(BlacksmithResearch research)
    {
        if (selectedProduction != null)
        {
            selectedProduction.SetRallyFlagVisible(false);
        }

        selectedProduction = null;
        selectedResearch = research;

        WatchTowerGarrisonUI watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
        if (watchTowerUI != null)
        {
            watchTowerUI.DeselectWatchTower();
        }

        MainBuildingUI mainBuildingUI = FindAnyObjectByType<MainBuildingUI>();
        if (mainBuildingUI != null)
        {
            mainBuildingUI.DeselectMainBuilding();
        }
    }

    public void DeselectResearch()
    {
        selectedResearch = null;
    }

    private bool IsMouseOverActivePanel()
    {
        Vector2 mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        return (selectedProduction != null && GetProductionPanelRect().Contains(mouse))
            || (selectedResearch != null && GetResearchPanelRect().Contains(mouse));
    }

    private bool TryGetProductionFromHit(RaycastHit hit, out BuildingProduction production, out GameObject building)
    {
        production = hit.collider.GetComponentInParent<BuildingProduction>();
        building = production != null ? production.gameObject : null;

        if (production != null)
        {
            return true;
        }

        GridSystem grid = FindAnyObjectByType<GridSystem>();
        if (grid == null || BuildingManager.Instance == null)
        {
            return false;
        }

        grid.GetXY(hit.point, out int gridX, out int gridZ);
        GridCell cell = grid.GetCell(gridX, gridZ);
        if (cell == null)
        {
            return false;
        }

        building = BuildingManager.Instance.GetBuildingAtCell(cell);
        if (building == null)
        {
            return false;
        }

        production = building.GetComponent<BuildingProduction>();
        if (production == null)
        {
            production = building.GetComponentInChildren<BuildingProduction>();
        }

        return production != null;
    }

    private bool TryGetResearchFromHit(RaycastHit hit, out BlacksmithResearch research, out GameObject building)
    {
        research = hit.collider.GetComponentInParent<BlacksmithResearch>();
        building = research != null ? research.gameObject : null;

        if (research != null)
        {
            return true;
        }

        GridSystem grid = FindAnyObjectByType<GridSystem>();
        if (grid == null || BuildingManager.Instance == null)
        {
            return false;
        }

        grid.GetXY(hit.point, out int gridX, out int gridZ);
        GridCell cell = grid.GetCell(gridX, gridZ);
        if (cell == null)
        {
            return false;
        }

        building = BuildingManager.Instance.GetBuildingAtCell(cell);
        if (building == null)
        {
            return false;
        }

        research = building.GetComponent<BlacksmithResearch>();
        if (research == null)
        {
            research = building.GetComponentInChildren<BlacksmithResearch>();
        }

        return research != null;
    }

    void OnGUI()
    {
        // Legacy IMGUI rendering is disabled.
    }

    private void DrawResearchPanel()
    {
        Rect panelRect = GetResearchPanelRect();
        GUI.Box(panelRect, "Research: Blacksmith");

        if (selectedResearch.AvailableTechnologies == null || selectedResearch.AvailableTechnologies.Count == 0)
        {
            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 32f, panelRect.width - 20f, 20), "No technology available to research.");
            return;
        }

        Rect viewport = new Rect(panelRect.x + 10f, panelRect.y + 28f, panelRect.width - 20f, panelRect.height - 38f);
        Rect content = new Rect(0f, 0f, viewport.width - 18f, GetResearchContentHeight());
        researchScrollPosition = GUI.BeginScrollView(viewport, researchScrollPosition, content);

        int yPos = 0;
        foreach (TechnologyData technology in selectedResearch.AvailableTechnologies)
        {
            if (technology == null)
            {
                continue;
            }

            bool unlocked = TechnologyManager.Instance.IsUnlocked(technology);
            bool isResearchingThis = selectedResearch.CurrentResearch == technology;
            bool canClick = !unlocked && !selectedResearch.IsResearching;
            string buttonText = unlocked ? "Unlocked: " + technology.technologyName : "Research: " + technology.technologyName;

            GUI.Box(new Rect(0, yPos, content.width, ResearchRowHeight - 6f), "");
            GUI.enabled = canClick;
            if (GUI.Button(new Rect(10, yPos + 10, 190, 30), buttonText))
            {
                selectedResearch.RequestResearch(technology);
            }

            GUI.enabled = true;
            GUI.Label(new Rect(210, yPos + 6, content.width - 220, 20), technology.technologyName);
            GUI.Label(new Rect(210, yPos + 28, content.width - 220, 20), "Cost: " + GetCostText(technology.researchCosts));
            GUI.Label(new Rect(210, yPos + 50, content.width - 220, 20), "Effect: " + technology.GetVillagerEffectText());
            if (isResearchingThis)
            {
                float progress = 1f - (selectedResearch.CurrentResearchTimer / Mathf.Max(0.1f, technology.researchTime));
                GUI.HorizontalScrollbar(new Rect(10, yPos + 48, 190, 15), 0, progress, 0, 1);
            }

            yPos += Mathf.RoundToInt(ResearchRowHeight);
        }

        GUI.EndScrollView();
    }

    private string GetCostText(System.Collections.Generic.List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0)
        {
            return "Free";
        }

        string costText = "";
        for (int i = 0; i < costs.Count; i++)
        {
            ResourceCost cost = costs[i];
            costText += $"{cost.amount} {cost.resourceType} ";
        }

        return costText;
    }

    private Rect GetProductionPanelRect()
    {
        float extraHeight = 0f;
        if (selectedProduction != null)
        {
            MainBuildingCombatTarget mainBuilding = selectedProduction.GetComponent<MainBuildingCombatTarget>();
            if (mainBuilding == null)
            {
                mainBuilding = selectedProduction.GetComponentInChildren<MainBuildingCombatTarget>();
            }
            if (mainBuilding != null)
            {
                extraHeight = 110f;
            }
        }
        float height = Mathf.Clamp(GetProductionContentHeight() + 88f + extraHeight, 200f, Mathf.Max(200f, Screen.height - PanelMaxBottomPadding));
        return new Rect(PanelX, PanelY, ProductionPanelWidth, height);
    }

    private Rect GetResearchPanelRect()
    {
        float height = Mathf.Clamp(GetResearchContentHeight() + 66f, 170f, Mathf.Max(170f, Screen.height - PanelMaxBottomPadding));
        return new Rect(PanelX, PanelY, ResearchPanelWidth, height);
    }

    private float GetProductionContentHeight()
    {
        int unitCount = selectedProduction != null && selectedProduction.BuildingData != null && selectedProduction.BuildingData.producibleUnits != null
            ? selectedProduction.BuildingData.producibleUnits.Count
            : 0;
        float height = Mathf.Max(1, unitCount) * ProductionRowHeight;
        if (selectedProduction != null && selectedProduction.CurrentProducingUnit != null)
        {
            height += 70f;
        }

        return height;
    }

    private float GetResearchContentHeight()
    {
        int techCount = selectedResearch != null && selectedResearch.AvailableTechnologies != null
            ? selectedResearch.AvailableTechnologies.Count
            : 0;
        return Mathf.Max(1, techCount) * ResearchRowHeight;
    }
}
