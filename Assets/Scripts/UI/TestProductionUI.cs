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

    private void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Nhấn chuột phải để chọn công trình hoặc đặt rally point khi không có unit nào đang được chọn.
        if (Input.GetMouseButtonDown(1))
        {
            if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count > 0)
            {
                return;
            }

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

                if (selectedProduction != null)
                {
                    selectedProduction.SetRallyFromHit(hit);
                    return;
                }

                Debug.Log("Không có công trình sản xuất nào đang được chọn để đặt rally point.");
            }
            else
            {
                Debug.Log("Chuột phải không trúng bất kỳ Collider nào!");
            }
        }
    }

    private void HandleLeftClickDeselect()
    {
        if ((selectedProduction == null && selectedResearch == null) || IsMouseOverActivePanel())
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
        if (selectedProduction != null && selectedProduction != production)
        {
            selectedProduction.SetRallyFlagVisible(false);
        }

        selectedProduction = production;
        selectedResearch = null;
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
                mainBuildingUI.DeselectMainBuilding();
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
        if (selectedResearch != null)
        {
            DrawResearchPanel();
            return;
        }

        if (selectedProduction == null)
        {
            GUI.Label(new Rect(10, 10, 420, 20), "Bấm CHUỘT PHẢI vào một công trình để sản xuất hoặc nghiên cứu.");
            return;
        }

        if (selectedProduction.BuildingData == null || selectedProduction.BuildingData.producibleUnits == null)
        {
            GUI.Label(new Rect(10, 10, 420, 20), "Công trình này không có dữ liệu sản xuất.");
            return;
        }

        float extraHeight = 0f;
        MainBuildingCombatTarget mainBuilding = selectedProduction.GetComponent<MainBuildingCombatTarget>();
        if (mainBuilding == null)
        {
            mainBuilding = selectedProduction.GetComponentInChildren<MainBuildingCombatTarget>();
        }
        if (mainBuilding != null)
        {
            extraHeight = 140f;
        }

        Rect panelRect = GetProductionPanelRect();
        GUI.Box(panelRect, "Sản Xuất: " + selectedProduction.BuildingData.buildingName);

        GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 26f, panelRect.width - 20f, 20f),
            $"Dân làng: {PopulationManager.CurrentVillagers} + {PopulationManager.ReservedVillagers} / {PopulationManager.MaxVillagers}");

        Rect viewport = new Rect(panelRect.x + 10f, panelRect.y + 50f, panelRect.width - 20f, panelRect.height - 60f - extraHeight);
        Rect content = new Rect(0f, 0f, viewport.width - 18f, GetProductionContentHeight());
        productionScrollPosition = GUI.BeginScrollView(viewport, productionScrollPosition, content);

        int yPos = 0;
        foreach (var unit in selectedProduction.BuildingData.producibleUnits)
        {
            if (unit == null) continue;

            bool techUnlocked = unit.AreTechnologyRequirementsMet();
            string costText = GetCostText(unit.productionCosts);
            string detailText = techUnlocked ? "Chi phí: " + costText : "Cần công nghệ: " + unit.GetMissingTechnologyNames();
            bool isVillager = PopulationManager.IsVillagerUnit(unit);
            bool populationAvailable = true;
            string populationReason = "";
            if (isVillager)
            {
                populationAvailable = PopulationManager.CanQueueVillager(out populationReason);
                if (!populationAvailable)
                {
                    detailText = populationReason;
                }
            }

            GUI.Box(new Rect(0, yPos, content.width, ProductionRowHeight - 6f), "");
            GUI.enabled = techUnlocked && populationAvailable;
            string buttonText = techUnlocked ? "Mua " + unit.unitName : "Khóa " + unit.unitName;
            if (techUnlocked && isVillager && !populationAvailable)
            {
                buttonText = "Đầy dân";
            }

            if (GUI.Button(new Rect(10, yPos + 10, 165, 30), buttonText))
            {
                selectedProduction.RequestProduceUnit(unit);
            }

            GUI.enabled = true;
            GUI.Label(new Rect(185, yPos + 6, content.width - 195, 20), unit.unitName);
            GUI.Label(new Rect(185, yPos + 28, content.width - 195, 20), detailText);
            yPos += Mathf.RoundToInt(ProductionRowHeight);
        }

        if (selectedProduction.CurrentProducingUnit != null)
        {
            yPos += 8;
            GUI.Box(new Rect(0, yPos, content.width, 54), "");
            GUI.Label(new Rect(10, yPos + 6, content.width - 20, 20), "Đang tạo: " + selectedProduction.CurrentProducingUnit.unitName);

            float progress = 1f - (selectedProduction.CurrentProductionTimer / selectedProduction.CurrentProducingUnit.productionTime);
            GUI.HorizontalScrollbar(new Rect(10, yPos + 30, 360, 18), 0, progress, 0, 1);
            GUI.Label(new Rect(380, yPos + 28, 80, 20), (progress * 100).ToString("F0") + "%");
        }

        GUI.EndScrollView();

        // Vẽ thêm nút điều khiển nhà chính nếu đây là Nhà Chính
        if (mainBuilding != null)
        {
            float startY = panelRect.yMax - extraHeight + 10f;

            // Hiển thị thông tin thời tiết
            string weatherText = "Thời tiết: ";
            if (WeatherManager.Instance != null)
            {
                weatherText += WeatherManager.Instance.CurrentWeather.ToString();
            }
            else
            {
                weatherText += "Clear";
            }
            if (TimeManager.Instance != null && TimeManager.Instance.IsNight)
            {
                weatherText += " (Ban Đêm)";
            }
            else
            {
                weatherText += " (Ban Ngày)";
            }
            GUI.Label(new Rect(panelRect.x + 15f, startY, panelRect.width - 30f, 20f), weatherText);

            string stateText = HouseShelter.IsEmergencyShelterActive ? "TRẠNG THÁI: YÊU CẦU TRÚ ẨN KHẨN CẤP" : "TRẠNG THÁI: Bình thường";
            GUI.Label(new Rect(panelRect.x + 15f, startY + 22f, panelRect.width - 30f, 20f), stateText);

            // Toggle yêu cầu dân làng trú ẩn vào ban đêm
            bool currentShelterAtNight = VillagerController.ShouldShelterAtNight;
            bool newShelterAtNight = GUI.Toggle(new Rect(panelRect.x + 15f, startY + 44f, panelRect.width - 30f, 20f), currentShelterAtNight, " Cư dân tự động đi trú ẩn vào ban đêm");
            if (newShelterAtNight != currentShelterAtNight)
            {
                VillagerController.ShouldShelterAtNight = newShelterAtNight;
            }

            if (GUI.Button(new Rect(panelRect.x + 15f, startY + 69f, 235f, 45f), "Trú ẩn khẩn cấp\n(Shelter All)"))
            {
                mainBuilding.OrderAllVillagersToShelter();
            }

            if (GUI.Button(new Rect(panelRect.x + 260f, startY + 69f, 235f, 45f), "Ra ngoài khẩn cấp\n(Evacuate All)"))
            {
                mainBuilding.OrderAllVillagersToEvacuate();
            }
        }
    }

    private void DrawResearchPanel()
    {
        Rect panelRect = GetResearchPanelRect();
        GUI.Box(panelRect, "Nghiên Cứu: Blacksmith");

        if (selectedResearch.AvailableTechnologies == null || selectedResearch.AvailableTechnologies.Count == 0)
        {
            GUI.Label(new Rect(panelRect.x + 10f, panelRect.y + 32f, panelRect.width - 20f, 20), "Blacksmith chưa có công nghệ để nghiên cứu.");
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
            string buttonText = unlocked ? "Đã mở " + technology.technologyName : "Nghiên cứu " + technology.technologyName;

            GUI.Box(new Rect(0, yPos, content.width, ResearchRowHeight - 6f), "");
            GUI.enabled = canClick;
            if (GUI.Button(new Rect(10, yPos + 10, 190, 30), buttonText))
            {
                selectedResearch.RequestResearch(technology);
            }

            GUI.enabled = true;
            GUI.Label(new Rect(210, yPos + 6, content.width - 220, 20), technology.technologyName);
            GUI.Label(new Rect(210, yPos + 28, content.width - 220, 20), "Chi phí: " + GetCostText(technology.researchCosts));
            GUI.Label(new Rect(210, yPos + 50, content.width - 220, 20), "Tác dụng: " + technology.GetVillagerEffectText());
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
            return "Miễn phí";
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
