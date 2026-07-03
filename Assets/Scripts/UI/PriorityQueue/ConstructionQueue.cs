using UnityEngine;
using System.Collections.Generic;

public class ConstructionQueue : MonoBehaviour
{
    public static ConstructionQueue Instance { get; private set; }

    private List<BuildingSlot> _slots = new List<BuildingSlot>();
    public IReadOnlyList<BuildingSlot> Slots => _slots;

    public event System.Action OnQueueChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void AddBuilding(string name, Vector3 position, GameObject obj)
    {
        var slot = new BuildingSlot(name, position, obj);
        _slots.Add(slot);
        SortByPriority();
        OnQueueChanged?.Invoke();
    }

    public void RemoveBuilding(BuildingSlot slot)
    {
        _slots.Remove(slot);
        OnQueueChanged?.Invoke();
    }

    public void SetPriority(BuildingSlot slot, BuildingPriority priority)
    {
        slot.priority = priority;
        SortByPriority();
        OnQueueChanged?.Invoke();
    }

    public void MoveUp(BuildingSlot slot)
    {
        int index = _slots.IndexOf(slot);
        if (index <= 0) return;
        _slots.RemoveAt(index);
        _slots.Insert(index - 1, slot);
        OnQueueChanged?.Invoke();
    }

    public void MoveDown(BuildingSlot slot)
    {
        int index = _slots.IndexOf(slot);
        if (index < 0 || index >= _slots.Count - 1) return;
        _slots.RemoveAt(index);
        _slots.Insert(index + 1, slot);
        OnQueueChanged?.Invoke();
    }

    private void SortByPriority()
    {
        _slots.Sort((a, b) => b.priority.CompareTo(a.priority));
    }

    // Test: thêm building giả để xem UI
    [ContextMenu("Add Test Building")]
    public void AddTestBuilding()
    {
        AddBuilding("Warehouse", Vector3.zero, null);
        AddBuilding("House", Vector3.one, null);
        AddBuilding("Workshop", Vector3.one * 2, null);
    }
}