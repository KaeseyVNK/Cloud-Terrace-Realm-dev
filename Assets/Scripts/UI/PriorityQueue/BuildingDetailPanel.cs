using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildingDetailPanel : MonoBehaviour
{
    [Header("Info")]
    public TextMeshProUGUI buildingNameText;
    public TextMeshProUGUI villagersText;
    public TextMeshProUGUI missingResourceText;
    public Slider progressSlider;

    [Header("Priority Buttons")]
    public Button btnLow;
    public Button btnNormal;
    public Button btnHigh;

    private BuildingSlot _current;

    public void Show(BuildingSlot slot)
    {
        _current = slot;
        gameObject.SetActive(true);
        Refresh();

        btnLow.onClick.RemoveAllListeners();
        btnNormal.onClick.RemoveAllListeners();
        btnHigh.onClick.RemoveAllListeners();

        btnLow.onClick.AddListener(() => SetPriority(BuildingPriority.Low));
        btnNormal.onClick.AddListener(() => SetPriority(BuildingPriority.Normal));
        btnHigh.onClick.AddListener(() => SetPriority(BuildingPriority.High));
    }

    public void Hide()
    {
        gameObject.SetActive(false);
        _current = null;
    }

    private void Refresh()
    {
        if (_current == null) return;

        buildingNameText.text = _current.buildingName;
        villagersText.text = $"Assigned Villagers: {_current.villagersAssigned}";
        missingResourceText.text = string.IsNullOrEmpty(_current.missingResource)
            ? "Sufficient Resources"
            : $"Missing: {_current.missingResource}";
        progressSlider.value = _current.progress / 100f;

        // Highlight nút đang active
        SetButtonHighlight(btnLow, _current.priority == BuildingPriority.Low);
        SetButtonHighlight(btnNormal, _current.priority == BuildingPriority.Normal);
        SetButtonHighlight(btnHigh, _current.priority == BuildingPriority.High);
    }

    private void SetPriority(BuildingPriority p)
    {
        if (_current == null) return;
        ConstructionQueue.Instance.SetPriority(_current, p);
        Refresh();
    }

    private void SetButtonHighlight(Button btn, bool active)
    {
        var colors = btn.colors;
        colors.normalColor = active
            ? new Color(1f, 0.75f, 0.2f)   // vàng cam khi active
            : Color.white;
        btn.colors = colors;
    }
}