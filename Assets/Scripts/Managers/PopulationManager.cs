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

        // 1. Quét dân làng
        VillagerController[] villagers = Object.FindObjectsByType<VillagerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        _cachedCurrentVillagers = villagers != null ? villagers.Length : 0;

        // 2. Quét sức chứa nhà dân
        int capacity = 0;
        HouseShelter[] shelters = Object.FindObjectsByType<HouseShelter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (shelters != null)
        {
            for (int i = 0; i < shelters.Length; i++)
            {
                HouseShelter shelter = shelters[i];
                if (shelter != null && shelter.IsOperational())
                {
                    capacity += Mathf.Max(0, shelter.Capacity);
                }
            }
        }
        _cachedMaxVillagers = capacity;

        // 3. Quét hàng chờ sản xuất lính
        int reserved = 0;
        BuildingProduction[] productions = Object.FindObjectsByType<BuildingProduction>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (productions != null)
        {
            for (int i = 0; i < productions.Length; i++)
            {
                if (productions[i] != null)
                {
                    reserved += productions[i].QueuedVillagerCount;
                }
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

    public static bool IsVillagerUnit(UnitData unit)
    {
        if (unit == null || unit.unitPrefab == null)
        {
            return false;
        }

        return unit.unitPrefab.GetComponent<VillagerController>() != null ||
               unit.unitPrefab.GetComponentInChildren<VillagerController>(true) != null;
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
