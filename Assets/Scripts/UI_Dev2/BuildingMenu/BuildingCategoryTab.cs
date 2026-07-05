using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingCategoryTab : MonoBehaviour
{
    [Header("Behaviour")]
    [SerializeField] private BuildingMenuUI _menuUI;
    [SerializeField] private BuildingCategory _category;

    [Header("References")]
    [SerializeField] private Button _button;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private TMP_Text _labelText;

    [Header("Visual States")]
    [SerializeField]
    private Color _activeColor =
        new Color(0.65f, 0.39f, 0.12f, 1f);

    [SerializeField]
    private Color _inactiveColor =
        new Color(0.32f, 0.30f, 0.27f, 1f);

    [SerializeField] private Color _activeTextColor = Color.white;

    [SerializeField]
    private Color _inactiveTextColor =
        new Color(0.85f, 0.82f, 0.75f, 1f);

    public BuildingCategory Category => _category;

    private void Awake()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnTabClicked);
        }
    }

    private void Start()
    {
        if (_labelText != null)
        {
            _labelText.text = GetCategoryDisplayName(_category);
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
            GameLog.LogError("[BuildingCategoryTab] Chưa gán BuildingMenuUI.", this);
            return;
        }

        _menuUI.ShowCategory(_category);
    }

    public void SetSelected(bool isSelected)
    {
        if (_backgroundImage != null)
        {
            _backgroundImage.color = isSelected ? _activeColor : _inactiveColor;
        }

        if (_labelText != null)
        {
            _labelText.color = isSelected ? _activeTextColor : _inactiveTextColor;
        }
    }

    private static string GetCategoryDisplayName(BuildingCategory category)
    {
        return category switch
        {
            BuildingCategory.Residential => "Residential Buildings",
            BuildingCategory.Production => "Production Buildings",
            BuildingCategory.Goods => "Goods Buildings",
            BuildingCategory.Cultural => "Cultural Buildings",
            BuildingCategory.Hospital => "Hospital",
            BuildingCategory.Military => "Military Buildings",
            BuildingCategory.Roads => "Roads",
            BuildingCategory.Expansion => "Expansion",
            _ => category.ToString()
        };
    }
}