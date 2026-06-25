using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Handles player interaction and displays the trade UI for Neutral Markets.
/// </summary>
public class MarketUI : MonoBehaviour
{
    #region Private Fields

    private static readonly Rect PanelRect = new Rect(10, 10, 480, 420);
    private MarketController _selectedMarket;

    // State for transaction
    private ResourceType _sellResource = ResourceType.Wood;
    private ResourceType _buyResource = ResourceType.Gold;
    private int _tradeAmount = 10;

    #endregion

    #region Public Properties

    /// <summary>
    /// The currently selected MarketController.
    /// </summary>
    public MarketController SelectedMarket => _selectedMarket;

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        // Check if cursor is over any UGUI element
        bool isOverUI = UnityEngine.EventSystems.EventSystem.current != null && 
                         UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        if (Input.GetMouseButtonDown(0) && !isOverUI)
        {
            HandleLeftClickDeselect();
        }

        // Select market building on Right-click (always) or Left-click (when no units are selected and not over UI)
        bool isSelectTriggered = Input.GetMouseButtonDown(1) || 
                                 (Input.GetMouseButtonDown(0) && UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count == 0 && !isOverUI);

        if (isSelectTriggered)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (TryGetMarketFromHit(hit, out MarketController mc, out GameObject clickedBuilding))
                {
                    // Kiểm tra sương mù (Fog of War) - Nếu chợ đang bị ẩn trong sương mù thì không cho chọn
                    FogVisibilityTarget visibilityTarget = clickedBuilding.GetComponentInParent<FogVisibilityTarget>();
                    if (visibilityTarget != null && !visibilityTarget.IsVisible)
                    {
                        return; // Bị ẩn trong sương mù, bỏ qua không chọn
                    }

                    ConstructibleBuilding cb = clickedBuilding.GetComponent<ConstructibleBuilding>();
                    if (cb != null && !cb.IsCompleted)
                    {
                        Debug.LogWarning("Không thể chọn: Chợ đang trong quá trình xây dựng!");
                        DeselectMarket();
                        return;
                    }

                    if (mc.isNeutral)
                    {
                        SelectMarket(mc);
                        Debug.Log("Đã chọn chợ trung lập: " + clickedBuilding.name);
                        return;
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        // Old IMGUI rendering is disabled. MarketUIController manages the new UGUI Trade Market UI.
        return;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Selects the Neutral Market to display UI.
    /// </summary>
    /// <param name="market">The selected MarketController.</param>
    public void SelectMarket(MarketController market)
    {
        _selectedMarket = market;

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

        MainBuildingUI mainBuildingUI = FindAnyObjectByType<MainBuildingUI>();
        if (mainBuildingUI != null)
        {
            mainBuildingUI.DeselectMainBuilding();
        }

        // Open UGUI Market UI
        MarketUIController uiController = MarketUIController.Instance;
        if (uiController == null)
        {
            uiController = FindAnyObjectByType<MarketUIController>(FindObjectsInactive.Include);
        }

        if (uiController != null)
        {
            uiController.OpenMenu(market);
        }
    }

    /// <summary>
    /// Deselects the Neutral Market.
    /// </summary>
    public void DeselectMarket()
    {
        _selectedMarket = null;

        // Close UGUI Market UI
        MarketUIController uiController = MarketUIController.Instance;
        if (uiController == null)
        {
            uiController = FindAnyObjectByType<MarketUIController>(FindObjectsInactive.Include);
        }

        if (uiController != null)
        {
            uiController.CloseMenu();
        }
    }

    #endregion

    #region Private Methods

    private void HandleLeftClickDeselect()
    {
        if (_selectedMarket == null || IsMouseOverPanel())
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
        if (Physics.Raycast(ray, out RaycastHit hit) && TryGetMarketFromHit(hit, out _, out _))
        {
            return;
        }

        DeselectMarket();
    }

    private bool IsMouseOverPanel()
    {
        Vector2 mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        return PanelRect.Contains(mouse);
    }

    private bool TryGetMarketFromHit(RaycastHit hit, out MarketController market, out GameObject building)
    {
        market = hit.collider.GetComponentInParent<MarketController>();
        building = market != null ? market.gameObject : null;

        if (market != null)
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

        market = building.GetComponent<MarketController>();
        if (market == null)
        {
            market = building.GetComponentInChildren<MarketController>();
        }

        return market != null;
    }

    private void DrawResourceButtons(Rect rect, ref ResourceType selected, bool includeGold)
    {
        float buttonWidth = rect.width / (includeGold ? 4f : 3f);
        
        List<ResourceType> types = new List<ResourceType> { ResourceType.Wood, ResourceType.Stone, ResourceType.Food };
        if (includeGold)
        {
            types.Add(ResourceType.Gold);
        }

        for (int i = 0; i < types.Count; i++)
        {
            ResourceType type = types[i];
            Rect btnRect = new Rect(rect.x + (i * buttonWidth), rect.y, buttonWidth - 2f, rect.height);
            
            // Highlight selected button
            GUI.color = (selected == type) ? Color.green : Color.white;
            if (GUI.Button(btnRect, type.ToString()))
            {
                selected = type;
            }
            GUI.color = Color.white;
        }
    }

    #endregion
}
