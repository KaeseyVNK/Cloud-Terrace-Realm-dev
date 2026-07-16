using UnityEngine;

public static class PopulationManager
{
    private static int _cachedCurrentVillagers;
    private static int _cachedMaxVillagers;
    private static int _cachedReservedVillagers;
    private static float _lastRefreshTime = -999f;
    private const float CacheDuration = 0.2f; // Cache trong 200ms để tối ưu hiệu năng

    private static void RefreshIfNeeded()
    {
        if (Time.time < _lastRefreshTime + CacheDuration && Application.isPlaying)
        {
            return;
        }
        _lastRefreshTime = Time.time;

        // 1. Quét dân làng từ danh sách SpawnedVillagers tĩnh và binh lính từ BaseCombatUnitController.Registry
        int currentPop = VillagerController.SpawnedVillagers.Count;
        for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
        {
            BaseCombatUnitController unit = BaseCombatUnitController.Registry[i];
            if (unit != null && unit.faction == UnitFaction.Player &&
                unit.GetComponent<BuildingCombatTarget>() == null &&
                unit.GetComponent<MainBuildingCombatTarget>() == null &&
                unit.GetComponent<ConstructibleBuilding>() == null)
            {
                currentPop++;
            }
        }
        _cachedCurrentVillagers = currentPop;

        // 2. Quét sức chứa từ HouseShelter (chỉ công trình có housingCapacity > 0 mới được gắn)
        int capacity = 0;
        for (int i = 0; i < HouseShelter.Registry.Count; i++)
        {
            HouseShelter shelter = HouseShelter.Registry[i];
            if (shelter != null && shelter.gameObject.activeInHierarchy && shelter.IsOperational())
            {
                capacity += Mathf.Max(0, shelter.Capacity);
            }
        }

        // Card bonus (nếu có) cộng thêm vào cap
        if (CardManager.Instance != null)
        {
            capacity += Mathf.Max(0, CardManager.Instance.PopulationCapBonus);
        }

        _cachedMaxVillagers = capacity;

        // 3. Quét hàng chờ sản xuất lính từ Registry tĩnh của BuildingProduction
        int reserved = 0;
        for (int i = 0; i < BuildingProduction.Registry.Count; i++)
        {
            BuildingProduction production = BuildingProduction.Registry[i];
            if (production != null && production.gameObject.activeInHierarchy)
            {
                reserved += production.QueuedVillagerCount;
            }
        }
        _cachedReservedVillagers = reserved;
    }

    public static int CurrentVillagers
    {
        get
        {
            RefreshIfNeeded();
            return _cachedCurrentVillagers;
        }
    }

    public static int MaxVillagers
    {
        get
        {
            RefreshIfNeeded();
            return _cachedMaxVillagers;
        }
    }

    public static int ReservedVillagers
    {
        get
        {
            RefreshIfNeeded();
            return _cachedReservedVillagers;
        }
    }

    public static int UsedVillagerSlots => CurrentVillagers + ReservedVillagers;

    /// <summary>
    /// Buộc tính lại pop cap / reserved ở lần đọc tiếp theo (ví dụ khi nhà mới hoàn thành).
    /// </summary>
    public static void InvalidateCache()
    {
        _lastRefreshTime = -999f;
    }

    public static bool IsVillagerUnit(UnitData unit)
    {
        if (unit == null || unit.unitPrefab == null)
        {
            return false;
        }

        return unit.unitPrefab.GetComponent<VillagerController>() != null ||
               unit.unitPrefab.GetComponentInChildren<VillagerController>(true) != null ||
               unit.unitPrefab.GetComponent<BaseCombatUnitController>() != null ||
               unit.unitPrefab.GetComponentInChildren<BaseCombatUnitController>(true) != null;
    }

    public static bool CanQueueVillager(out string reason)
    {
        int maxVillagers = MaxVillagers;
        int usedSlots = UsedVillagerSlots;
        if (maxVillagers <= 0)
        {
            reason = "Chưa có nhà dân để chứa dân làng.";
            return false;
        }

        if (usedSlots >= maxVillagers)
        {
            reason = $"Giới hạn dân làng đã đầy ({usedSlots}/{maxVillagers}). Hãy xây thêm nhà dân.";
            return false;
        }

        reason = "";
        return true;
    }
}
