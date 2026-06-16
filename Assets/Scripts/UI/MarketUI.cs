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
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Right-click to select market building when no units are selected
        if (Input.GetMouseButtonDown(1))
        {
            if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count > 0)
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (TryGetMarketFromHit(hit, out MarketController mc, out GameObject clickedBuilding))
                {
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
        if (_selectedMarket == null)
        {
            return;
        }

        GUI.Box(PanelRect, "Chợ Trao Đổi Trung Lập");

        // Display current prices
        float startY = 40f;
        GUI.Label(new Rect(PanelRect.x + 20, PanelRect.y + startY, 440, 20), "Tỷ giá hiện tại (quy đổi ra Vàng):");
        
        float priceX = PanelRect.x + 20;
        float priceY = PanelRect.y + startY + 20;
        
        string woodPriceStr = $"Gỗ: {_selectedMarket.CurrentPrices[ResourceType.Wood]:F1}g";
        string stonePriceStr = $"Đá: {_selectedMarket.CurrentPrices[ResourceType.Stone]:F1}g";
        string foodPriceStr = $"Lương thực: {_selectedMarket.CurrentPrices[ResourceType.Food]:F1}g";
        
        GUI.Label(new Rect(priceX, priceY, 130, 20), woodPriceStr);
        GUI.Label(new Rect(priceX + 140, priceY, 130, 20), stonePriceStr);
        GUI.Label(new Rect(priceX + 280, priceY, 130, 20), foodPriceStr);

        // Trade selection
        float tradeSelectionY = priceY + 30f;
        GUI.Label(new Rect(PanelRect.x + 20, PanelRect.y + tradeSelectionY, 200, 20), "Bán tài nguyên:");
        GUI.Label(new Rect(PanelRect.x + 250, PanelRect.y + tradeSelectionY, 200, 20), "Mua tài nguyên:");

        float buttonY = tradeSelectionY + 22f;
        // Draw sell selection buttons
        DrawResourceButtons(new Rect(PanelRect.x + 20, PanelRect.y + buttonY, 200, 25), ref _sellResource, false);
        // Draw buy selection buttons
        DrawResourceButtons(new Rect(PanelRect.x + 250, PanelRect.y + buttonY, 200, 25), ref _buyResource, true);

        // Trade amount buttons
        float amountY = buttonY + 70f;
        GUI.Label(new Rect(PanelRect.x + 20, PanelRect.y + amountY, 200, 20), $"Số lượng bán: {_tradeAmount}");
        
        float amountBtnX = PanelRect.x + 20;
        float amountBtnY = amountY + 22f;
        if (GUI.Button(new Rect(amountBtnX, amountBtnY, 50, 30), "-100")) _tradeAmount = Mathf.Max(10, _tradeAmount - 100);
        if (GUI.Button(new Rect(amountBtnX + 55, amountBtnY, 50, 30), "-10")) _tradeAmount = Mathf.Max(10, _tradeAmount - 10);
        if (GUI.Button(new Rect(amountBtnX + 110, amountBtnY, 50, 30), "+10")) _tradeAmount += 10;
        if (GUI.Button(new Rect(amountBtnX + 165, amountBtnY, 50, 30), "+100")) _tradeAmount += 100;
        
        if (GUI.Button(new Rect(amountBtnX + 225, amountBtnY, 70, 30), "Tối đa"))
        {
            if (ResourceManager.Instance != null)
            {
                _tradeAmount = ResourceManager.Instance.GetResourceAmount(_sellResource);
                if (_tradeAmount <= 0) _tradeAmount = 10;
            }
        }

        // Calculate transaction results
        float averageRate;
        int receiveAmount = _selectedMarket.CalculateTradeResult(_sellResource, _buyResource, _tradeAmount, out averageRate);

        float reviewY = amountBtnY + 45f;
        GUI.Box(new Rect(PanelRect.x + 20, PanelRect.y + reviewY, 440, 65), "");
        
        string reviewText = $"Giao dịch: Bán {_tradeAmount} {_sellResource} -> Nhận {receiveAmount} {_buyResource}\n" +
                           $"Tỷ giá trung bình: 1 {_sellResource} = {averageRate:F2} {_buyResource}\n" +
                           $"* Xe thồ hàng sẽ chuyển tài nguyên về nhà lúc đêm xuống.";
        
        GUI.Label(new Rect(PanelRect.x + 30, PanelRect.y + reviewY + 5f, 420, 55), reviewText);

        // Execute button
        float execY = reviewY + 75f;
        bool canTrade = ResourceManager.Instance != null &&
                        ResourceManager.Instance.GetResourceAmount(_sellResource) >= _tradeAmount &&
                        _sellResource != _buyResource &&
                        receiveAmount > 0;

        GUI.enabled = canTrade;
        if (GUI.Button(new Rect(PanelRect.x + 20, PanelRect.y + execY, 200, 40), "Xác nhận giao dịch"))
        {
            if (_selectedMarket.ExecuteTrade(_sellResource, _buyResource, _tradeAmount))
            {
                Debug.Log("[MarketUI] Giao dịch thành công!");
                // Keep the trade amount capped at new balance if Max was selected
                int playerStock = ResourceManager.Instance.GetResourceAmount(_sellResource);
                if (_tradeAmount > playerStock)
                {
                    _tradeAmount = Mathf.Max(10, playerStock);
                }
            }
        }
        GUI.enabled = true;

        // Pending deliveries
        float pendingX = PanelRect.x + 240;
        float pendingY = execY;
        GUI.Box(new Rect(pendingX, PanelRect.y + pendingY, 220, 75), "Hàng chờ giao đêm nay:");
        
        float textY = pendingY + 20f;
        string pendingWood = $"Gỗ: {_selectedMarket.PendingDeliveries[ResourceType.Wood]}";
        string pendingStone = $"Đá: {_selectedMarket.PendingDeliveries[ResourceType.Stone]}";
        string pendingFood = $"Thực phẩm: {_selectedMarket.PendingDeliveries[ResourceType.Food]}";
        string pendingGold = $"Vàng: {_selectedMarket.PendingDeliveries[ResourceType.Gold]}";

        GUI.Label(new Rect(pendingX + 10, PanelRect.y + textY, 100, 20), pendingWood);
        GUI.Label(new Rect(pendingX + 110, PanelRect.y + textY, 100, 20), pendingStone);
        GUI.Label(new Rect(pendingX + 10, PanelRect.y + textY + 20, 100, 20), pendingFood);
        GUI.Label(new Rect(pendingX + 110, PanelRect.y + textY + 20, 100, 20), pendingGold);
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
    }

    /// <summary>
    /// Deselects the Neutral Market.
    /// </summary>
    public void DeselectMarket()
    {
        _selectedMarket = null;
    }

    #endregion

    #region Private Methods

    private void HandleLeftClickDeselect()
    {
        if (_selectedMarket == null || IsMouseOverPanel())
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
