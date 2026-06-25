using UnityEngine;

/// <summary>
/// Thành phần đại diện cho Cổng Hư Vô (Void Portal) / Tổ Sinh Quái (Spawning Nest).
/// Khi bị phá hủy sẽ kích hoạt tính năng chọn thẻ nâng cấp.
/// </summary>
public class VoidPortal : BaseCombatUnitController
{
    public VoidPortal()
    {
        faction = UnitFaction.Enemy;
        unitName = "Void Portal";
    }

    protected override void Start()
    {
        base.Start();

        // Cổng Hư Vô là vật thể tĩnh, vô hiệu hóa NavMeshAgent nếu có
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }
    }

    protected override void Update()
    {
        // Cổng Hư Vô không cần chạy AI di chuyển, quét địch của lính
        if (currentState == CombatState.Dead) return;
    }

    private System.Collections.Generic.List<GridCell> _occupiedCells = new System.Collections.Generic.List<GridCell>();

    public void SetOccupiedCells(System.Collections.Generic.List<GridCell> cells)
    {
        _occupiedCells = cells;
    }

    protected override void OnDeath()
    {
        Debug.Log("[VoidPortal] Cổng Hư Vô đã bị tiêu diệt! Kích hoạt chọn thẻ nâng cấp.");
        
        // Giải phóng các ô lưới đã bị chiếm dụng trên bản đồ để có thể xây dựng lại
        if (_occupiedCells != null && _occupiedCells.Count > 0)
        {
            for (int i = 0; i < _occupiedCells.Count; i++)
            {
                var cell = _occupiedCells[i];
                if (cell != null)
                {
                    cell.isBuildable = true;
                    cell.isWalkable = true;
                    cell.hasResource = false;
                    cell.resourceObject = null;
                }
            }
        }

        if (CardManager.Instance != null)
        {
            CardManager.Instance.TriggerCardDraft();
        }

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowBloodMoonAlert(
                "CỔNG HƯ VÔ ĐÃ BỊ PHÁ HỦY",
                "Bạn đã phá hủy thành công một Cổng Hư Vô và nhận được cơ hội rút Thẻ Nâng Cấp! 🏆",
                5f
            );
        }

        base.OnDeath();
    }
}
