using System;
using System.Collections.Generic;
using UnityEngine;

public class TechnologyManager : MonoBehaviour
{
    private static TechnologyManager _instance;
    private readonly HashSet<string> _unlockedTechnologyIds = new HashSet<string>();

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

    public event Action<TechnologyData> OnTechnologyUnlocked;

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

        OnTechnologyUnlocked?.Invoke(technology);
        Debug.Log("[Tech] Unlocked: " + technology.technologyName);
        return true;
    }
}
