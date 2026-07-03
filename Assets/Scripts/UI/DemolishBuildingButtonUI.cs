using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DemolishBuildingButtonUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Image _background;
    [SerializeField] private string _idleText = "Demolish";
    [SerializeField] private string _activeText = "Demolishing";
    [SerializeField] private Color _idleColor = new Color32(110, 38, 38, 230);
    [SerializeField] private Color _activeColor = new Color32(210, 48, 48, 255);

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_background == null)
        {
            _background = GetComponent<Image>();
        }
        if (_label == null)
        {
            _label = GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void OnEnable()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        _button.onClick.AddListener(ToggleDemolishMode);
        Refresh();
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(ToggleDemolishMode);
        }
    }

    private void Update()
    {
        Refresh();
    }

    private void ToggleDemolishMode()
    {
        if (BuildingManager.Instance == null)
        {
            return;
        }

        BuildingManager.Instance.ToggleDeleteMode();
        Refresh();
    }

    private void Refresh()
    {
        bool isActive = BuildingManager.Instance != null && BuildingManager.Instance.IsDeleteMode;
        if (_label != null)
        {
            _label.text = isActive ? _activeText : _idleText;
        }

        if (_background != null)
        {
            _background.color = isActive ? _activeColor : _idleColor;
        }
    }
}
