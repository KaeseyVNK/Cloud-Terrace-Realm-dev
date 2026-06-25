using UnityEngine;

public enum UpgradeCardType
{
    StatBuff,
    Unlock,
    Instant
}

/// <summary>
/// Độ hiếm của thẻ nâng cấp, ảnh hưởng đến màu sắc, xác suất xuất hiện và sức mạnh.
/// </summary>
public enum CardRarity
{
    Common    = 0,  // Xám — xác suất cao nhất
    Rare      = 1,  // Xanh lam
    Epic      = 2,  // Tím
    Legendary = 3   // Vàng cam — rất hiếm
}

[CreateAssetMenu(fileName = "New Upgrade Card", menuName = "Cloud Terrace/Upgrade Card")]
public class UpgradeCardData : ScriptableObject
{
    [Header("Basic Info")]
    public string cardId;
    public string cardName;
    [TextArea] public string description;
    public Sprite icon;
    public UpgradeCardType cardType;

    [Header("Rarity")]
    [Tooltip("Độ hiếm của thẻ: Common > Rare > Epic > Legendary")]
    public CardRarity rarity = CardRarity.Common;

    /// <summary>Trọng số rút thẻ: Common=100, Rare=40, Epic=15, Legendary=4.</summary>
    public int RarityWeight => rarity switch
    {
        CardRarity.Common    => 100,
        CardRarity.Rare      => 40,
        CardRarity.Epic      => 15,
        CardRarity.Legendary => 4,
        _                    => 100
    };

    /// <summary>Màu hiển thị tương ứng với rarity.</summary>
    public Color RarityColor => rarity switch
    {
        CardRarity.Common    => new Color(0.78f, 0.78f, 0.78f),  // Xám bạc
        CardRarity.Rare      => new Color(0.25f, 0.60f, 1.00f),  // Xanh lam
        CardRarity.Epic      => new Color(0.70f, 0.30f, 1.00f),  // Tím
        CardRarity.Legendary => new Color(1.00f, 0.75f, 0.10f),  // Vàng cam
        _                    => Color.white
    };

    /// <summary>Tên hiển thị rarity.</summary>
    public string RarityDisplayName => rarity switch
    {
        CardRarity.Common    => "PHỔ THÔNG",
        CardRarity.Rare      => "HIẾM",
        CardRarity.Epic      => "SỬ THI",
        CardRarity.Legendary => "HUYỀN THOẠI",
        _                    => ""
    };

    [Header("Stat Buff Settings")]
    [Tooltip("Hệ số tăng máu cho quân lính (ví dụ 1.25 là +25%)")]
    public float unitHealthMultiplier = 1f;
    [Tooltip("Hệ số tăng sát thương cho quân lính")]
    public float unitDamageMultiplier = 1f;
    [Tooltip("Hệ số di chuyển cho quân lính")]
    public float unitSpeedMultiplier = 1f;

    [Tooltip("Hệ số di chuyển cho dân làng")]
    public float villagerSpeedMultiplier = 1f;
    [Tooltip("Hệ số tăng sức mang vác cho dân làng (+1, +2...)")]
    public int villagerCarryCapacityBonus = 0;
    
    [Tooltip("Hệ số tốc độ khai thác gỗ")]
    public float woodGatherMultiplier = 1f;
    [Tooltip("Hệ số tốc độ khai thác đá")]
    public float stoneGatherMultiplier = 1f;
    [Tooltip("Hệ số tốc độ khai thác vàng")]
    public float goldGatherMultiplier = 1f;
    [Tooltip("Hệ số tốc độ khai thác thức ăn")]
    public float foodGatherMultiplier = 1f;

    [Header("Unlock Settings")]
    [Tooltip("Công trình sẽ mở khóa khi chọn thẻ này")]
    public BuildingData buildingToUnlock;
    [Tooltip("Lính sẽ mở khóa khi chọn thẻ này")]
    public UnitData unitToUnlock;

    [Header("Instant Rewards")]
    [Tooltip("Tài nguyên vàng tặng ngay")]
    public ResourceType instantResourceType;
    [Tooltip("Số lượng tài nguyên tặng ngay (loại đa dụng)")]
    public int instantResourceAmount = 0;

    [Tooltip("Gỗ tặng ngay (có thể dùng kết hợp với các tài nguyên khác)")]
    public int instantWoodAmount = 0;
    [Tooltip("Đá tặng ngay")]
    public int instantStoneAmount = 0;
    [Tooltip("Thức ăn tặng ngay")]
    public int instantFoodAmount = 0;

    [Tooltip("Số lượng dân binh (Militia) sinh ra ngay tại nhà chính")]
    public int instantMilitiaCount = 0;

    [Tooltip("Hồi phục đầy máu cho nhà chính")]
    public bool healMainBuilding = false;
    [Tooltip("Hồi phục một phần máu nhà chính (0.0 = 0%, 0.5 = 50%, 1.0 = 100%). Dùng thay cho healMainBuilding nếu muốn hồi phục cụ thể.")]
    [Range(0f, 1f)]
    public float healMainBuildingPercent = 0f;

    [Header("Passive Buffs (StatBuff type)")]
    [Tooltip("Hệ số tăng máu tối đa cho tất cả công trình của người chơi")]
    public float buildingMaxHealthMultiplier = 1f;
    [Tooltip("Tăng giới hạn dân số (+n người)")]
    public int populationCapBonus = 0;
}
