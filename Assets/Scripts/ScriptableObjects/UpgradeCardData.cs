using UnityEngine;

public enum UpgradeCardType
{
    StatBuff,
    Unlock,
    Instant
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
    [Tooltip("Tài nguyên tặng ngay")]
    public ResourceType instantResourceType;
    public int instantResourceAmount = 0;
    [Tooltip("Số lượng dân binh (Militia) sinh ra ngay tại nhà chính")]
    public int instantMilitiaCount = 0;
    [Tooltip("Hồi phục đầy máu cho nhà chính")]
    public bool healMainBuilding = false;
}
