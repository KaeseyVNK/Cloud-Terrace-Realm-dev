using System;
using System.Collections.Generic;
using UnityEngine;

public class MarketController : MonoBehaviour
{
    [Header("Market Type")]
    public bool isNeutral = true;

    [Header("Base Prices (in Gold value)")]
    [SerializeField] private float _baseWoodPrice = 10f;
    [SerializeField] private float _baseStonePrice = 15f;
    [SerializeField] private float _baseFoodPrice = 10f;

    [Header("Trade Settings")]
    [Tooltip("Tỷ lệ giảm giá khi bán hoặc tăng giá khi mua trên mỗi đơn vị giao dịch (ví dụ: 0.001 = 0.1% mỗi đơn vị)")]
    public float priceChangeRate = 0.001f;
    [Tooltip("Tốc độ tự phục hồi về giá trị gốc (ví dụ: 0.05 = 5% phục hồi mỗi 10 giây)")]
    public float priceRecoverySpeed = 0.05f;

    [Header("Caravan Spawning")]
    [Tooltip("Prefab của dân làng/xe chở hàng dùng để đi giao thương ban đêm")]
    [SerializeField] private GameObject _caravanPrefab;

    // Giá hiện tại của các tài nguyên tại chợ này
    private Dictionary<ResourceType, float> _currentPrices = new Dictionary<ResourceType, float>();
    public Dictionary<ResourceType, float> CurrentPrices => _currentPrices;

    // Cấu trúc đại diện cho từng giao dịch đơn lẻ
    [System.Serializable]
    public struct PendingDelivery
    {
        public ResourceType resourceType;
        public int amount;

        public PendingDelivery(ResourceType resourceType, int amount)
        {
            this.resourceType = resourceType;
            this.amount = amount;
        }
    }

    // Hàng đợi chờ giao vào ban đêm: Resource Type -> Số lượng chờ giao (dành cho hiển thị UI tổng hợp)
    private Dictionary<ResourceType, int> _pendingDeliveries = new Dictionary<ResourceType, int>();
    public Dictionary<ResourceType, int> PendingDeliveries => _pendingDeliveries;

    // Danh sách các giao dịch đơn lẻ chờ giao (để sinh Caravan riêng cho mỗi lần giao dịch)
    private List<PendingDelivery> _pendingDeliveriesList = new List<PendingDelivery>();
    public List<PendingDelivery> PendingDeliveriesList => _pendingDeliveriesList;

    private float _priceRecoveryTimer = 0f;

    private void Awake()
    {
        InitializePrices();
    }

    private void Start()
    {
        // Đăng ký sự kiện Ngày/Đêm từ TimeManager để kích hoạt xe giao hàng ban đêm
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }
    }

    private void Update()
    {
        if (!isNeutral) return;

        // Phục hồi giá cả theo thời gian về mức cân bằng
        _priceRecoveryTimer += Time.deltaTime;
        if (_priceRecoveryTimer >= 5f)
        {
            _priceRecoveryTimer = 0f;
            RecoverPricesToLimit();
        }
    }

    private void InitializePrices()
    {
        _currentPrices[ResourceType.Wood] = _baseWoodPrice;
        _currentPrices[ResourceType.Stone] = _baseStonePrice;
        _currentPrices[ResourceType.Food] = _baseFoodPrice;
        _currentPrices[ResourceType.Gold] = 1.0f; // Vàng là đơn vị neo giá

        _pendingDeliveries[ResourceType.Wood] = 0;
        _pendingDeliveries[ResourceType.Stone] = 0;
        _pendingDeliveries[ResourceType.Food] = 0;
        _pendingDeliveries[ResourceType.Gold] = 0;

        _pendingDeliveriesList.Clear();
    }

    public float GetBasePrice(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return _baseWoodPrice;
            case ResourceType.Stone: return _baseStonePrice;
            case ResourceType.Food: return _baseFoodPrice;
            case ResourceType.Gold: return 1.0f;
            default: return 0f;
        }
    }

    /// <summary>
    /// Tính toán số lượng nhận được khi giao dịch (chưa thực hiện giao dịch)
    /// </summary>
    public int CalculateTradeResult(ResourceType fromType, ResourceType toType, int amount, out float averageRate)
    {
        averageRate = 0f;
        if (amount <= 0 || fromType == toType) return 0;

        float fromVal = _currentPrices.ContainsKey(fromType) ? _currentPrices[fromType] : 1f;
        float toVal = _currentPrices.ContainsKey(toType) ? _currentPrices[toType] : 1f;

        // Tổng giá trị tính bằng Vàng của tài nguyên đem bán
        float goldValue = amount * fromVal;

        // Số lượng tài nguyên mua được tương ứng
        int resultAmount = Mathf.FloorToInt(goldValue / toVal);
        if (resultAmount > 0)
        {
            averageRate = (float)resultAmount / amount;
        }
        return resultAmount;
    }

    /// <summary>
    /// Thực hiện giao dịch đổi tài nguyên
    /// </summary>
    public bool ExecuteTrade(ResourceType fromType, ResourceType toType, int amount)
    {
        if (ResourceManager.Instance == null || amount <= 0 || fromType == toType) return false;
        if (ResourceManager.Instance.GetResourceAmount(fromType) < amount)
        {
            Debug.LogWarning("[Market] Không đủ tài nguyên đem bán!");
            return false;
        }

        float averageRate;
        int receiveAmount = CalculateTradeResult(fromType, toType, amount, out averageRate);
        if (receiveAmount <= 0) return false;

        // 1. Khấu trừ tài nguyên bán ngay lập tức của người chơi
        ResourceManager.Instance.TryConsumeResource(fromType, amount);

        // 2. Nạp tài nguyên nhận được vào hàng đợi chờ giao ban đêm
        _pendingDeliveries[toType] += receiveAmount;
        _pendingDeliveriesList.Add(new PendingDelivery(toType, receiveAmount));

        // 3. Cập nhật tỷ giá (Trượt giá cung cầu - Price Slippage)
        // Tài nguyên bán ra: giảm giá do tăng nguồn cung
        if (fromType != ResourceType.Gold)
        {
            float basePrice = GetBasePrice(fromType);
            float priceDrop = amount * priceChangeRate * basePrice;
            _currentPrices[fromType] = Mathf.Max(basePrice * 0.2f, _currentPrices[fromType] - priceDrop);
        }

        // Tài nguyên mua vào: tăng giá do tăng cầu
        if (toType != ResourceType.Gold)
        {
            float basePrice = GetBasePrice(toType);
            float priceRise = receiveAmount * priceChangeRate * basePrice;
            _currentPrices[toType] = Mathf.Min(basePrice * 5.0f, _currentPrices[toType] + priceRise);
        }

        Debug.Log($"[Market] Giao dịch thành công: Bán {amount} {fromType} lấy {receiveAmount} {toType}. Tỉ lệ: {averageRate:F2}. Hàng sẽ được giao vào đêm nay!");
        return true;
    }

    private void RecoverPricesToLimit()
    {
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            if (type == ResourceType.Gold || !_currentPrices.ContainsKey(type)) continue;

            float basePrice = GetBasePrice(type);
            float currentPrice = _currentPrices[type];

            // Dần đưa giá về giá trị gốc
            float difference = basePrice - currentPrice;
            if (Mathf.Abs(difference) > 0.01f)
            {
                _currentPrices[type] = currentPrice + difference * priceRecoverySpeed;
            }
            else
            {
                _currentPrices[type] = basePrice;
            }
        }
    }

    private void HandleDayNightChanged(bool isNight)
    {
        // Khi bắt đầu đêm (isNight == true), xuất phát xe giao hàng
        if (isNeutral && isNight)
        {
            SpawnDeliveryCaravans();
        }
    }

    private void SpawnDeliveryCaravans()
    {
        // Tìm địa điểm nhận hàng: Market của người chơi, hoặc Main Building nếu chưa xây Market
        Transform targetDestination = FindPlayerMarketOrMainBuilding();
        if (targetDestination == null)
        {
            Debug.LogWarning("[Market] Không tìm thấy điểm nhận hàng (Market người chơi hoặc Nhà chính)!");
            return;
        }

        // Kiểm tra xem có đơn hàng nào cần giao không
        if (_pendingDeliveriesList.Count > 0)
        {
            foreach (var delivery in _pendingDeliveriesList)
            {
                if (delivery.amount > 0)
                {
                    // Sinh xe giao thương riêng biệt cho mỗi lần giao dịch
                    SpawnCaravan(delivery.resourceType, delivery.amount, targetDestination);
                }
            }

            // Xóa danh sách hàng chờ giao sau khi đã sinh caravan
            _pendingDeliveriesList.Clear();

            // Reset dictionary hiển thị UI
            _pendingDeliveries[ResourceType.Wood] = 0;
            _pendingDeliveries[ResourceType.Stone] = 0;
            _pendingDeliveries[ResourceType.Food] = 0;
            _pendingDeliveries[ResourceType.Gold] = 0;
        }
    }

    private Transform FindPlayerMarketOrMainBuilding()
    {
        // 1. Tìm Market của người chơi
        MarketController[] markets = FindObjectsByType<MarketController>(FindObjectsInactive.Include);
        foreach (var market in markets)
        {
            if (market != null && !market.isNeutral)
            {
                // Kiểm tra xem công trình đã xây xong chưa
                ConstructibleBuilding cb = market.GetComponent<ConstructibleBuilding>();
                if (cb == null || cb.IsCompleted)
                {
                    return market.transform;
                }
            }
        }

        // 2. Dự phòng: Tìm Nhà chính của người chơi
        if (BuildingManager.Instance != null && BuildingManager.Instance.MainBuildingInstance != null)
        {
            return BuildingManager.Instance.MainBuildingInstance.transform;
        }

        return null;
    }

    private void SpawnCaravan(ResourceType type, int amount, Transform destination)
    {
        // Sử dụng prefab xe chở hàng được gán hoặc tải tự động dân làng làm placeholder
        GameObject prefab = _caravanPrefab;
        if (prefab == null)
        {
            // Tự động tìm dân thường làm placeholder
            GameManager gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                // Lấy prefab dân làng từ GameManager làm tạm thời
                var field = typeof(GameManager).GetField("_villagerPrefab", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    prefab = field.GetValue(gm) as GameObject;
                }
            }
        }

        if (prefab == null)
        {
            Debug.LogError("[Market] Không tìm thấy Prefab để tạo Caravan giao thương!");
            return;
        }

        // Tạo đối tượng Caravan
        Vector3 spawnPos = transform.position + Vector3.forward * 1.5f; // Spawn hơi lệch ra ngoài chòi chợ
        GameObject caravanObj = Instantiate(prefab, spawnPos, Quaternion.identity);
        caravanObj.name = $"Caravan_{type}_{amount}";

        // Thêm TradeCaravanController
        TradeCaravanController caravan = caravanObj.AddComponent<TradeCaravanController>();
        caravan.Initialize(type, amount, destination);

        // Vô hiệu hóa các script quản lý dân làng thông thường để tránh người chơi điều khiển
        VillagerController vc = caravanObj.GetComponent<VillagerController>();
        if (vc != null)
        {
            vc.enabled = false;
            VillagerController.AllVillagers.Remove(vc);
        }

        SelectableUnit su = caravanObj.GetComponent<SelectableUnit>();
        if (su != null)
        {
            su.enabled = false;
        }

        // Thêm VillagerCombatTarget để kẻ địch/thú dữ có thể tấn công xe hàng ban đêm
        VillagerCombatTarget combatTarget = caravanObj.GetComponent<VillagerCombatTarget>();
        if (combatTarget == null)
        {
            combatTarget = caravanObj.AddComponent<VillagerCombatTarget>();
        }
        combatTarget.faction = UnitFaction.Player;
        combatTarget.SetMaxHealth(80); // Trâu bò hơn dân thường 1 tí
        combatTarget.unitName = "Xe Giao Thương";

        // Tải vật liệu nổi bật hoặc làm nổi caravan (tùy chọn trong tương lai)
        Debug.Log($"[Market] Đêm xuống! Xe Giao Thương đã xuất phát: Mang {amount} {type} về {destination.name}.");
    }

    public void SetCaravanPrefab(GameObject prefab)
    {
        _caravanPrefab = prefab;
    }
}
