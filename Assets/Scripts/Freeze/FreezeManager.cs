using UnityEngine;
using System.Collections.Generic;

public class FreezeManager : MonoBehaviour
{
    public static FreezeManager Instance { get; private set; }

    private List<IFreezable> _frozenBuildings = new List<IFreezable>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void RegisterFrozen(IFreezable building)
    {
        if (!_frozenBuildings.Contains(building))
            _frozenBuildings.Add(building);
    }

    public void UnregisterFrozen(IFreezable building)
    {
        _frozenBuildings.Remove(building);
    }

    // Gọi khi trời sáng — unfreeze tất cả
    public void UnfreezeAll()
    {
        var copy = new List<IFreezable>(_frozenBuildings);
        foreach (var b in copy)
            b.Unfreeze();

        GameLog.Log("[FreezeManager] Trời sáng - tất cả công trình đã rã đông!");
    }

    // Tìm building gần nhất để freeze
    public IFreezable FindNearestFreezable(Vector3 position, float radius = 5f)
    {
        var buildings = FindObjectsByType<FreezableBuilding>(FindObjectsInactive.Exclude);
        IFreezable nearest = null;
        float minDist = float.MaxValue;

        foreach (var b in buildings)
        {
            if (b.IsFrozen) continue;
            float d = Vector3.Distance(position, b.GetPosition());
            if (d < minDist && d <= radius)
            {
                minDist = d;
                nearest = b;
            }
        }
        return nearest;
    }
}