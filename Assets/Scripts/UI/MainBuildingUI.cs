using UnityEngine;

/// <summary>
/// Handles selection of the Main Building and displays emergency shelter UI.
/// </summary>
public class MainBuildingUI : MonoBehaviour
{
    #region Private Fields

    private static readonly Rect PanelRect = new Rect(10, 10, 320, 200);
    private MainBuildingCombatTarget _selectedMainBuilding;

    #endregion

    #region Public Properties

    /// <summary>
    /// The currently selected Main Building combat target.
    /// </summary>
    public MainBuildingCombatTarget SelectedMainBuilding => _selectedMainBuilding;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Right-click to select main building when no units are selected
        if (Input.GetMouseButtonDown(1))
        {
            if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count > 0)
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (TryGetMainBuildingFromHit(hit, out MainBuildingCombatTarget mb, out GameObject clickedBuilding))
                {
                    ConstructibleBuilding cb = clickedBuilding.GetComponent<ConstructibleBuilding>();
                    if (cb != null && !cb.IsCompleted)
                    {
                        Debug.LogWarning("Không thể chọn: Nhà chính đang trong quá trình xây dựng!");
                        DeselectMainBuilding();
                        return;
                    }

                    SelectMainBuilding(mb);
                    Debug.Log("Đã chọn nhà chính: " + clickedBuilding.name);
                    return;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (_selectedMainBuilding == null)
        {
            return;
        }

        GUI.Box(PanelRect, "Nhà Chính Vương Quốc");

        // Count sheltered vs total villagers
        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Include);
        int totalCount = allVillagers.Length;
        
        int shelteredCount = 0;
        HouseShelter[] shelters = FindObjectsByType<HouseShelter>(FindObjectsInactive.Exclude);
        foreach (var shelter in shelters)
        {
            if (shelter != null)
            {
                shelteredCount += shelter.OccupantCount;
            }
        }

        string countText = $"Dân làng đang trú ẩn: {shelteredCount} / {totalCount}";
        GUI.Label(new Rect(20, 45, 280, 25), countText);

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
        GUI.Label(new Rect(20, 70, 280, 25), weatherText);

        string stateText = HouseShelter.IsEmergencyShelterActive ? "TRẠNG THÁI: YÊU CẦU TRÚ ẨN KHẨN CẤP" : "TRẠNG THÁI: Bình thường";
        GUI.Label(new Rect(20, 95, 280, 25), stateText);

        if (GUI.Button(new Rect(20, 130, 135, 45), "Trú ẩn khẩn cấp\n(Shelter All)"))
        {
            _selectedMainBuilding.OrderAllVillagersToShelter();
        }

        if (GUI.Button(new Rect(165, 130, 135, 45), "Ra ngoài khẩn cấp\n(Evacuate All)"))
        {
            _selectedMainBuilding.OrderAllVillagersToEvacuate();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Selects the Main Building.
    /// </summary>
    /// <param name="mb">The Main Building combat target.</param>
    public void SelectMainBuilding(MainBuildingCombatTarget mb)
    {
        _selectedMainBuilding = mb;

        // Deselect other building UIs to avoid overlap
        TestProductionUI productionUI = FindAnyObjectByType<TestProductionUI>();
        if (productionUI != null)
        {
            productionUI.DeselectProduction();
        }

        WatchTowerGarrisonUI watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
        if (watchTowerUI != null)
        {
            watchTowerUI.DeselectWatchTower();
        }
    }

    /// <summary>
    /// Deselects the Main Building.
    /// </summary>
    public void DeselectMainBuilding()
    {
        _selectedMainBuilding = null;
    }

    #endregion

    #region Private Methods

    private void HandleLeftClickDeselect()
    {
        if (_selectedMainBuilding == null || IsMouseOverPanel())
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && TryGetMainBuildingFromHit(hit, out _, out _))
        {
            return;
        }

        DeselectMainBuilding();
    }

    private bool IsMouseOverPanel()
    {
        Vector2 mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        return PanelRect.Contains(mouse);
    }

    private bool TryGetMainBuildingFromHit(RaycastHit hit, out MainBuildingCombatTarget mainBuilding, out GameObject building)
    {
        mainBuilding = hit.collider.GetComponentInParent<MainBuildingCombatTarget>();
        building = mainBuilding != null ? mainBuilding.gameObject : null;

        if (mainBuilding != null)
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

        mainBuilding = building.GetComponent<MainBuildingCombatTarget>();
        if (mainBuilding == null)
        {
            mainBuilding = building.GetComponentInChildren<MainBuildingCombatTarget>();
        }

        return mainBuilding != null;
    }

    #endregion
}
