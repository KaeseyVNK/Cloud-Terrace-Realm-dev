using System;
using System.Collections.Generic;
using UnityEngine;

public class TechnologyManager : MonoBehaviour
{
    private static TechnologyManager _instance;
    private readonly HashSet<string> _unlockedTechnologyIds = new HashSet<string>();
    private readonly List<TechnologyData> _unlockedTechnologies = new List<TechnologyData>();

    public static TechnologyManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<TechnologyManager>();
                if (_instance == null)
                {
                    GameObject obj = new GameObject("TechnologyManager");
                    _instance = obj.AddComponent<TechnologyManager>();
                }
            }

            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    public event Action<TechnologyData> OnTechnologyUnlocked;

    public int VillagerCarryCapacityBonus
    {
        get
        {
            int bonus = 0;
            for (int i = 0; i < _unlockedTechnologies.Count; i++)
            {
                TechnologyData technology = _unlockedTechnologies[i];
                if (technology != null)
                {
                    bonus += technology.villagerCarryCapacityBonus;
                }
            }

            return bonus;
        }
    }

    public float VillagerMoveSpeedMultiplier => GetStackedMultiplier(t => t.villagerMoveSpeedMultiplier);

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    public bool IsUnlocked(TechnologyData technology)
    {
        return technology == null || IsUnlocked(technology.technologyId);
    }

    public bool IsUnlocked(string technologyId)
    {
        return string.IsNullOrWhiteSpace(technologyId) || _unlockedTechnologyIds.Contains(technologyId);
    }

    public bool Unlock(TechnologyData technology)
    {
        if (technology == null || string.IsNullOrWhiteSpace(technology.technologyId))
        {
            return false;
        }

        if (!_unlockedTechnologyIds.Add(technology.technologyId))
        {
            return false;
        }

        _unlockedTechnologies.Add(technology);
        OnTechnologyUnlocked?.Invoke(technology);
        Debug.Log("[Tech] Unlocked: " + technology.technologyName);
        return true;
    }

    public float GetVillagerGatherSpeedMultiplier(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.Wood:
                return GetStackedMultiplier(t => t.woodGatherSpeedMultiplier);
            case ResourceType.Stone:
                return GetStackedMultiplier(t => t.stoneGatherSpeedMultiplier);
            case ResourceType.Gold:
                return GetStackedMultiplier(t => t.goldGatherSpeedMultiplier);
            case ResourceType.Food:
                return GetStackedMultiplier(t => t.foodGatherSpeedMultiplier);
            default:
                return 1f;
        }
    }

    private float GetStackedMultiplier(Func<TechnologyData, float> selector)
    {
        float multiplier = 1f;
        for (int i = 0; i < _unlockedTechnologies.Count; i++)
        {
            TechnologyData technology = _unlockedTechnologies[i];
            if (technology != null)
            {
                multiplier *= Mathf.Max(1f, selector(technology));
            }
        }

        return multiplier;
    }
}
