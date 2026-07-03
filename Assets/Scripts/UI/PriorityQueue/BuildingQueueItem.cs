using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingQueueItem : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI indexText;
    public TextMeshProUGUI buildingNameText;
    public TextMeshProUGUI progressText;
    public TextMeshProUGUI priorityBadge;
    public Button moveUpButton;
    public Button moveDownButton;
    public Button selectButton;

    private BuildingSlot _slot;

    public void Setup(BuildingSlot slot, int index, System.Action<BuildingSlot> onSelect)
    {
        _slot = slot;

        indexText.text = (index + 1).ToString();
        buildingNameText.text = slot.buildingName;
        progressText.text = $"{Mathf.RoundToInt(slot.progress)}%";
        priorityBadge.text = slot.priority switch
        {
            BuildingPriority.High => "High",
            BuildingPriority.Normal => "Normal",
            _ => "Low"
        };

        moveUpButton.onClick.RemoveAllListeners();
        moveUpButton.onClick.AddListener(() =>
        {
            ConstructionQueue.Instance.MoveUp(_slot);
        });

        moveDownButton.onClick.RemoveAllListeners();
        moveDownButton.onClick.AddListener(() =>
        {
            ConstructionQueue.Instance.MoveDown(_slot);
        });

        selectButton.onClick.RemoveAllListeners();
        selectButton.onClick.AddListener(() => onSelect?.Invoke(_slot));
    }
}