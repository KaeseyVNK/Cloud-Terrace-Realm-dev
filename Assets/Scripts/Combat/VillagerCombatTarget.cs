using UnityEngine;

/// <summary>
/// Thành phần chiến đấu đại diện cho dân làng (Villager).
/// Nhận sát thương từ kẻ địch và quản lý việc vô hiệu hóa dân làng khi chết.
/// </summary>
public class VillagerCombatTarget : BaseCombatUnitController
{
    private VillagerController _villagerController;

    /// <summary>
    /// Khởi tạo các thông số cơ bản cho dân làng.
    /// </summary>
    public VillagerCombatTarget()
    {
        faction = UnitFaction.Player;
        unitName = "Villager";
    }

    /// <summary>
    /// Thiết lập thông số máu ban đầu và liên kết với VillagerController.
    /// </summary>
    protected override void Start()
    {
        maxHealth = 60; // Dân làng có lượng máu thấp hơn lính chiến đấu
        base.Start();

        _villagerController = GetComponent<VillagerController>();
    }

    /// <summary>
    /// Cập nhật trạng thái chiến đấu của dân làng (bỏ qua phần AI chiến đấu để giữ nguyên hoạt động của VillagerController).
    /// </summary>
    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;
    }

    /// <summary>
    /// Xử lý logic khi dân làng bị tiêu diệt hoàn toàn.
    /// </summary>
    protected override void Die()
    {
        currentState = CombatState.Dead;

        // Vô hiệu hóa bộ điều khiển dân làng để dừng mọi hoạt động di chuyển/làm việc
        if (_villagerController != null)
        {
            _villagerController.enabled = false;
        }

        base.Die();
    }
}
