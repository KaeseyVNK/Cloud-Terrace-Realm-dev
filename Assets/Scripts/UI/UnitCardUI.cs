using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Điều khiển một thẻ đơn vị trong giao diện sản xuất.
/// Tự động hiển thị tên, chi phí và trạng thái khóa/mở khóa.
/// </summary>
public class UnitCardUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _portraitIcon;
    [SerializeField] private TMP_Text _unitNameText;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _trainButton;
    [SerializeField] private TMP_Text _trainButtonText;
    [SerializeField] private GameObject _lockedOverlay;

    private UnitData _unitData;
    private Action _onTrainClicked;

    /// <summary>
    /// Khởi tạo card với dữ liệu đơn vị và callback khi bấm nút huấn luyện.
    /// </summary>
    public void Setup(UnitData unitData, Action onTrainClicked)
    {
        _unitData = unitData;
        _onTrainClicked = onTrainClicked;

        if (_unitData == null) return;

        if (_unitNameText != null)
            _unitNameText.text = _unitData.unitName;

        if (_costText != null)
            _costText.text = BuildCostText();

        if (_portraitIcon != null && _unitData.portraitIcon != null)
        {
            _portraitIcon.sprite = _unitData.portraitIcon;
        }

        if (_trainButton != null)
        {
            _trainButton.onClick.RemoveAllListeners();
            _trainButton.onClick.AddListener(OnTrainButtonClicked);
        }

        RefreshState();
    }

    /// <summary>
    /// Cập nhật trạng thái nút dựa trên tài nguyên và công nghệ hiện tại.
    /// </summary>
    public void RefreshState()
    {
        if (_unitData == null) return;

        bool techUnlocked = _unitData.AreTechnologyRequirementsMet();
        bool canAfford = ResourceManager.Instance != null &&
                         ResourceManager.Instance.CanAfford(_unitData.productionCosts);
        bool canTrain = techUnlocked && canAfford;

        if (_lockedOverlay != null)
            _lockedOverlay.SetActive(!techUnlocked);

        if (_trainButton != null)
            _trainButton.interactable = canTrain;

        if (_trainButtonText != null)
        {
            if (!techUnlocked)
                _trainButtonText.text = "Khóa";
            else if (!canAfford)
                _trainButtonText.text = "Thiếu TN";
            else
                _trainButtonText.text = "Huấn luyện";
        }

        if (_costText != null)
            _costText.color = canAfford ? Color.white : new Color(1f, 0.4f, 0.4f, 1f);
    }

    private void OnTrainButtonClicked()
    {
        _onTrainClicked?.Invoke();
    }

    private string BuildCostText()
    {
        if (_unitData.productionCosts == null || _unitData.productionCosts.Count == 0)
            return "Miễn phí";

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (var cost in _unitData.productionCosts)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(cost.amount + " " + GetResourceShortName(cost.resourceType));
        }
        return sb.ToString();
    }

    private string GetResourceShortName(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:  return "Gỗ";
            case ResourceType.Stone: return "Đá";
            case ResourceType.Gold:  return "Vàng";
            case ResourceType.Food:  return "Lúa";
            default:                 return type.ToString();
        }
    }
}
