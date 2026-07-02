using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainBuildingTabUI : MonoBehaviour
{
    [Header("Behaviour")]
    [SerializeField] private MainBuildingMenuUI _menuUI;
    [SerializeField] private MainBuildingTabType _tabType;

    [Header("References")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private TMP_Text _labelText;

    [Header("Visual States")]
    [SerializeField]
    private Color _activeColor =
        new Color(0.25f, 0.40f, 0.50f, 1f);

    [SerializeField]
    private Color _inactiveColor =
        new Color(0.05f, 0.08f, 0.12f, 0.9f);

    [SerializeField] private Color _activeTextColor = Color.white;

    [SerializeField]
    private Color _inactiveTextColor =
        new Color(0.72f, 0.80f, 0.84f, 1f);

    public MainBuildingTabType TabType => _tabType;

    private void Awake()
    {
        AutoAssignReferences();

        if (_button != null)
        {
            _button.onClick.AddListener(OnTabClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnTabClicked);
        }
    }

    private void OnTabClicked()
    {
        if (_menuUI == null)
        {
            Debug.LogError(
                "[MainBuildingTabUI] Chưa gán MainBuildingMenuUI.",
                this
            );

            return;
        }

        _menuUI.ShowTab(_tabType);
    }

    public void SetSelected(bool isSelected)
    {
        if (_backgroundImage != null)
        {
            _backgroundImage.color =
                isSelected ? _activeColor : _inactiveColor;
        }

        if (_labelText != null)
        {
            _labelText.color =
                isSelected
                    ? _activeTextColor
                    : _inactiveTextColor;
        }
    }

    private void AutoAssignReferences()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        if (_backgroundImage == null)
        {
            _backgroundImage = GetComponent<Image>();
        }

        if (_labelText == null)
        {
            _labelText = GetComponentInChildren<TMP_Text>();
        }
    }

    private void Reset()
    {
        AutoAssignReferences();
    }
}