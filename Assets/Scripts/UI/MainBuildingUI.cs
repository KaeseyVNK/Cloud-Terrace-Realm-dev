using UnityEngine;

/// <summary>
/// Handles selection of the Main Building and displays emergency shelter UI.
/// </summary>
public class MainBuildingUI : MonoBehaviour
{
    #region Private Fields

    private static readonly Rect PanelRect = new Rect(10, 10, 320, 200);
    private MainBuildingCombatTarget _selectedMainBuilding;

    // Cache variables for performance optimization
    private int _cachedTotalVillagers = 0;
    private int _cachedShelteredVillagers = 0;
    private float _nextCountRefreshTime = 0f;
    private const float CountRefreshInterval = 0.5f; // Refresh every 500ms

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
        // Avoid clicks when hovering or interacting with UGUI (such as Pause/Options panels)
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Right-click or left-click/touch (when no units are selected) to select main building
        bool isSelectTriggered = Input.GetMouseButtonDown(1) || 
                                 (Input.GetMouseButtonDown(0) && UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count == 0 && !IsMouseOverPanel());

        if (isSelectTriggered)
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

        // Throttle count checks to every 500ms to avoid huge CPU overhead and GC allocations in OnGUI
        if (_selectedMainBuilding != null && Time.time >= _nextCountRefreshTime)
        {
            _nextCountRefreshTime = Time.time + CountRefreshInterval;
            RefreshVillagerCounts();
        }
    }

    private void RefreshVillagerCounts()
    {
        _cachedTotalVillagers = VillagerController.AllVillagers != null ? VillagerController.AllVillagers.Count : 0;
        
        int shelteredCount = 0;
        var shelters = HouseShelter.Registry;
        if (shelters != null)
        {
            for (int i = 0; i < shelters.Count; i++)
            {
                var shelter = shelters[i];
                if (shelter != null)
                {
                    shelteredCount += shelter.OccupantCount;
                }
            }
        }
        _cachedShelteredVillagers = shelteredCount;
    }

    private void OnGUI()
    {
        // Giao diện đã được tích hợp trực tiếp vào TestProductionUI để tránh chồng chéo các bảng ở góc trái.
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
            productionUI.DeselectResearch();
        }

        WatchTowerGarrisonUI watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
        if (watchTowerUI != null)
        {
            watchTowerUI.DeselectWatchTower();
        }
        
        // Refresh counts immediately upon selection
        RefreshVillagerCounts();
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

        // Avoid deselecting when clicking on UGUI elements
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
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
