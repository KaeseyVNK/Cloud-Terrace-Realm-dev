using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager that batches and distributes visibility updates for FogVisibilityTarget components.
/// This avoids the overhead of having thousands of independent Update() loops.
/// </summary>
public class FogVisibilityManager : MonoBehaviour
{
    private static readonly List<FogVisibilityTarget> _targets = new List<FogVisibilityTarget>();
    private static FogVisibilityManager _instance;

    private int _currentIndex = 0;
    private float _defaultUpdateInterval = 0.2f;

    public static FogVisibilityManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("FogVisibilityManager");
                _instance = go.AddComponent<FogVisibilityManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Registers a visibility target to be updated by the central loop.
    /// </summary>
    /// <param name="target">The target to register.</param>
    public static void Register(FogVisibilityTarget target)
    {
        if (target == null) return;

        // Ensure instance exists
        var manager = Instance;

        if (!_targets.Contains(target))
        {
            _targets.Add(target);
        }
    }

    /// <summary>
    /// Unregisters a visibility target.
    /// </summary>
    /// <param name="target">The target to unregister.</param>
    public static void Unregister(FogVisibilityTarget target)
    {
        _targets.Remove(target);
    }

    private void Update()
    {
        int count = _targets.Count;
        if (count == 0) return;

        // Distribute updates evenly over the default update interval (0.2 seconds)
        int targetsToUpdateCount = Mathf.CeilToInt(count * (Time.deltaTime / _defaultUpdateInterval));
        targetsToUpdateCount = Mathf.Clamp(targetsToUpdateCount, 1, count);

        for (int i = 0; i < targetsToUpdateCount; i++)
        {
            if (_currentIndex >= _targets.Count)
            {
                _currentIndex = 0;
            }

            if (_targets.Count == 0)
            {
                break;
            }

            FogVisibilityTarget target = _targets[_currentIndex];
            if (target != null && target.isActiveAndEnabled)
            {
                target.ManualUpdate();
                _currentIndex++;
            }
            else
            {
                // Clean up null or disabled targets from the list
                _targets.RemoveAt(_currentIndex);
                // Do not increment _currentIndex since we removed the element at the current index,
                // so the next element shifted to the current index.
            }
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
        _targets.Clear();
    }
}
