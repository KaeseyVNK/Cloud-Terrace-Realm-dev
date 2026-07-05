using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PriorityQueueUI : MonoBehaviour
{
    [Header("Queue List")]
    public Transform queueContainer;
    public GameObject queueItemPrefab;

    [Header("Detail Panel")]
    public BuildingDetailPanel detailPanel;

    [Header("Toggle")]
    public Button toggleButton;
    public GameObject queuePanel;

    private List<BuildingQueueItem> _items = new List<BuildingQueueItem>();
    private bool _isOpen = false;

    void Start()
    {
        queuePanel.SetActive(false);
        detailPanel.Hide();

        if (ConstructionQueue.Instance != null)
        {
            ConstructionQueue.Instance.OnQueueChanged += RefreshList;
            RefreshList();
        }
        else
        {
            GameLog.LogError("[PriorityQueueUI] ConstructionQueue.Instance is null!");
        }
    }

    void OnDestroy()
    {
        if (ConstructionQueue.Instance != null)
            ConstructionQueue.Instance.OnQueueChanged -= RefreshList;
    }

    public void TogglePanel()
    {
        GameLog.Log("[PriorityQueueUI] TogglePanel called!");
        _isOpen = !_isOpen;
        queuePanel.SetActive(_isOpen);
        if (!_isOpen) detailPanel.Hide();
    }

    private void RefreshList()
    {
        GameLog.Log($"[PriorityQueueUI] RefreshList called. Slots: {ConstructionQueue.Instance.Slots.Count}");

        foreach (var item in _items)
            Destroy(item.gameObject);
        _items.Clear();

        var slots = ConstructionQueue.Instance.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            var go = Instantiate(queueItemPrefab, queueContainer);
            var item = go.GetComponent<BuildingQueueItem>();
            item.Setup(slots[i], i, OnSelectBuilding);
            _items.Add(item);
        }
    }

    private void OnSelectBuilding(BuildingSlot slot)
    {
        detailPanel.Show(slot);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            TogglePanel();
    }
}