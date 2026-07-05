using System;
using System.Collections.Generic;
using UnityEngine;

public class TechnologyManager : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
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

    public float StorageCapacityBonus
    {
        get
        {
            int bonus = 0;
            for (int i = 0; i < _unlockedTechnologies.Count; i++)
            {
                TechnologyData technology = _unlockedTechnologies[i];
                if (technology != null)
                {
                    bonus += technology.storageCapacityBonus;
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
            EnsureSaveableEntity("Global_TechnologyManager");
            return;
        }

        if (_instance != this)
        {
            Destroy(gameObject);
        }
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
        GameLog.Log("[Tech] Unlocked: " + technology.technologyName);
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

    [Serializable]
    private class TechnologySaveState
    {
        public List<string> unlockedTechnologyIds = new List<string>();
    }

    public string CaptureState()
    {
        TechnologySaveState state = new TechnologySaveState();
        foreach (string id in _unlockedTechnologyIds)
        {
            if (!string.IsNullOrEmpty(id))
            {
                state.unlockedTechnologyIds.Add(id);
            }
        }
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        TechnologySaveState state = JsonUtility.FromJson<TechnologySaveState>(stateJson);
        if (state == null || state.unlockedTechnologyIds == null) return;

        _unlockedTechnologyIds.Clear();
        _unlockedTechnologies.Clear();

        foreach (string id in state.unlockedTechnologyIds)
        {
            TechnologyData tech = FindTechnologyDataByID(id);
            if (tech != null)
            {
                _unlockedTechnologyIds.Add(id);
                _unlockedTechnologies.Add(tech);
                OnTechnologyUnlocked?.Invoke(tech);
            }
        }
    }

    private TechnologyData FindTechnologyDataByID(string technologyId)
    {
        if (string.IsNullOrEmpty(technologyId)) return null;

        foreach (BlacksmithResearch research in BlacksmithResearch.ActiveBlacksmiths)
        {
            if (research == null) continue;
            foreach (TechnologyData tech in research.AvailableTechnologies)
            {
                if (tech != null && tech.technologyId == technologyId)
                {
                    return tech;
                }
            }
        }

        foreach (TechnologyData tech in BlacksmithResearch.GlobalCardTechnologies)
        {
            if (tech != null && tech.technologyId == technologyId)
            {
                return tech;
            }
        }

        return null;
    }
}
