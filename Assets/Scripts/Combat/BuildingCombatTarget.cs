using UnityEngine;

/// <summary>
/// Thành phần chiến đấu đại diện cho các công trình của người chơi.
/// Cho phép quân địch phát hiện, nhắm mục tiêu và tấn công phá hủy công trình.
/// </summary>
public class BuildingCombatTarget : BaseCombatUnitController
{
    /// <summary>
    /// Khởi tạo các thông số cơ bản cho công trình.
    /// </summary>
    public BuildingCombatTarget()
    {
        faction = UnitFaction.Player;
        unitName = "Building";
    }

    /// <summary>
    /// Khởi chạy khi đối tượng được tạo, cấu hình để công trình không di chuyển.
    /// </summary>
    protected override void Start()
    {
        base.Start();

        // Công trình là vật thể tĩnh, vô hiệu hóa NavMeshAgent nếu có
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }
    }

    /// <summary>
    /// Cập nhật trạng thái chiến đấu của công trình (bỏ qua di chuyển hoặc tự quét địch).
    /// </summary>
    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;
    }

    /// <summary>
    /// Xử lý logic khi công trình bị tiêu diệt hoàn toàn.
    /// </summary>
    protected override void Die()
    {
        currentState = CombatState.Dead;

        // Báo cho BuildingManager dọn dẹp ô lưới và phá hủy GameObject
        var buildingMgr = FindAnyObjectByType<BuildingManager>();
        if (buildingMgr != null)
        {
            buildingMgr.DestroyBuilding(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
