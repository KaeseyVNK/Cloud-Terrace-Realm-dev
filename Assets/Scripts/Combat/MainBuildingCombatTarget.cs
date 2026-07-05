using UnityEngine;

public class MainBuildingCombatTarget : BaseCombatUnitController
{
    public MainBuildingCombatTarget()
    {
        faction = UnitFaction.Player;
        unitName = "Main Building";
    }

    protected override void Start()
    {
        base.Start();

        // Tắt NavMeshAgent vì nhà chính cố định không di chuyển
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }
    }

    protected override void Update()
    {
        // Nhà chính không cần chạy AI di chuyển, tấn công của lính
        if (currentState == CombatState.Dead) return;
    }

    /// <summary>
    /// Kích hoạt chế độ trú ẩn khẩn cấp toàn bản đồ.
    /// </summary>
    public void OrderAllVillagersToShelter()
    {
        if (currentState == CombatState.Dead) return;

        HouseShelter.IsEmergencyShelterActive = true;
        GameLog.Log("[MainBuilding] Yêu cầu toàn bộ dân làng trú ẩn khẩn cấp!");
    }

    /// <summary>
    /// Tắt chế độ trú ẩn khẩn cấp, buộc dân làng rời khỏi nhà.
    /// </summary>
    public void OrderAllVillagersToEvacuate()
    {
        if (currentState == CombatState.Dead) return;

        HouseShelter.IsEmergencyShelterActive = false;
        GameLog.Log("[MainBuilding] Yêu cầu toàn bộ dân làng ra ngoài khẩn cấp!");

        // Set cờ override cho tất cả dân làng để họ không chạy ngay vào nhà nếu trời đang mưa/đêm
        var villagers = VillagerController.AllVillagers;
        for (int i = 0; i < villagers.Count; i++)
        {
            var villager = villagers[i];
            if (villager != null)
            {
                villager.SetOverrideShelter(true);
            }
        }

        // Trục xuất toàn bộ dân khỏi nhà
        var shelters = HouseShelter.Registry;
        for (int i = 0; i < shelters.Count; i++)
        {
            var shelter = shelters[i];
            if (shelter != null)
            {
                shelter.EjectAll();
            }
        }

        var garrisons = WatchTowerGarrison.Registry;
        for (int i = 0; i < garrisons.Count; i++)
        {
            var garrison = garrisons[i];
            if (garrison != null)
            {
                garrison.EjectAll();
            }
        }
    }

    protected override void Die()
    {
        currentState = CombatState.Dead;
        GameLog.Log("[GAME OVER] Nhà chính đã bị tiêu diệt! Bạn đã thất bại!");
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.TriggerDefeat();
        }

        // Phá hủy GameObject nhà chính
        Destroy(gameObject);
    }
}
