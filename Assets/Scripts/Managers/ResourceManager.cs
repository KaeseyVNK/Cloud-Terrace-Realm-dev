using System;
using System.Collections.Generic;
using UnityEngine;

// Định nghĩa các loại tài nguyên trong game
public enum ResourceType
{
    Wood,
    Stone,
    Food,
    Water,
    Gold,
    AncientRelic
}

public class ResourceManager : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
{
    // Singleton pattern 
    public static ResourceManager Instance { get; private set; }

    private Dictionary<ResourceType, int> resourceInventory;

    public event Action<ResourceType, int> OnResourceChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSaveableEntity("Global_ResourceManager");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitInventory();
    }

    private void EnsureSaveableEntity(string saveID)
    {
        var saveable = GetComponent<CloudTerraceRealm.SaveSystem.SaveableEntity>();
        if (saveable == null)
        {
            saveable = gameObject.AddComponent<CloudTerraceRealm.SaveSystem.SaveableEntity>();
            var field = typeof(CloudTerraceRealm.SaveSystem.SaveableEntity).GetField("_saveID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(saveable, saveID);
            }
        }
    }

    // Khởi tạo kho mặc định
    private void InitInventory()
    {
        resourceInventory = new Dictionary<ResourceType, int>();
        
        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resourceInventory.Add(type, 0);
        }

        AddResource(ResourceType.Wood, 550);
        AddResource(ResourceType.Stone, 350);
        AddResource(ResourceType.Gold, 250);        
        AddResource(ResourceType.Food, 350);
    }


    public void AddResource(ResourceType type, int amount)
    {
        if (amount < 0) return;

        resourceInventory[type] += amount;
        OnResourceChanged?.Invoke(type, resourceInventory[type]);
    }

    public bool TryConsumeResource(ResourceType type, int amount)
    {
        if (amount < 0) return false;
        
        if (resourceInventory[type] >= amount)
        {
            resourceInventory[type] -= amount;
            OnResourceChanged?.Invoke(type, resourceInventory[type]);
            return true;
        }
        GameLog.Log($"Không đủ {type}! Bạn cần {amount} nhưng chỉ có {resourceInventory[type]}.");
        return false;
    }

    public int GetResourceAmount(ResourceType type)
    {
        return resourceInventory.ContainsKey(type) ? resourceInventory[type] : 0;
    }

    public bool CanAfford(List<ResourceCost> costs)
    {
        if (costs == null || costs.Count == 0) return true;
        foreach (var cost in costs)
        {
            if (GetResourceAmount(cost.resourceType) < cost.amount)
            {
                GameLog.Log($"Không đủ {cost.resourceType}! Bạn cần {cost.amount} nhưng chỉ có {GetResourceAmount(cost.resourceType)}.");
                return false;
            }
        }
        return true;
    }

    public void ConsumeCosts(List<ResourceCost> costs)
    {
        if (costs == null) return;
        foreach (var cost in costs)
        {
            TryConsumeResource(cost.resourceType, cost.amount);
        }
    }

    /// <summary>
    /// Tính tổng số lượng của 4 loại tài nguyên chính (Wood, Stone, Food, Gold).
    /// </summary>
    public int GetTotalPrimaryResources()
    {
        return GetResourceAmount(ResourceType.Wood) +
               GetResourceAmount(ResourceType.Stone) +
               GetResourceAmount(ResourceType.Food) +
               GetResourceAmount(ResourceType.Gold);
    }

    /// <summary>
    /// Lấy sức chứa kho tối đa của người chơi (Base 1000 + 1000 cho mỗi công trình Storage + Tech Bonus).
    /// </summary>
    public int GetMaxResourceCapacity()
    {
        int baseCapacity = 1000;
        int storageCount = 0;
        if (BuildingManager.Instance != null)
        {
            storageCount = BuildingManager.Instance.GetCompletedStorageCount();
        }

        int techBonus = 0;
        if (TechnologyManager.HasInstance)
        {
            techBonus = (int)TechnologyManager.Instance.StorageCapacityBonus;
        }

        return baseCapacity + storageCount * 1000 + techBonus;
    }

    [Serializable]
    private class ResourceSaveState
    {
        public List<ResourceEntry> resources = new List<ResourceEntry>();
    }

    [Serializable]
    private class ResourceEntry
    {
        public ResourceType type;
        public int amount;
    }

    public string CaptureState()
    {
        ResourceSaveState state = new ResourceSaveState();
        foreach (var kvp in resourceInventory)
        {
            state.resources.Add(new ResourceEntry { type = kvp.Key, amount = kvp.Value });
        }
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        ResourceSaveState state = JsonUtility.FromJson<ResourceSaveState>(stateJson);
        if (state == null || state.resources == null) return;

        foreach (var entry in state.resources)
        {
            resourceInventory[entry.type] = entry.amount;
            OnResourceChanged?.Invoke(entry.type, entry.amount);
        }
    }
}