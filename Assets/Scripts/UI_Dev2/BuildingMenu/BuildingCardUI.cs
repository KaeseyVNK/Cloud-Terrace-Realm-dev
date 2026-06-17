using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingCardUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("Card UI")]
    [SerializeField] private TMP_Text _buildingNameText;
    [SerializeField] private Image _buildingIconImage;
    [SerializeField] private TMP_Text _costText;
    [SerializeField] private Button _buildButton;

    [Header("Tooltip")]
    [SerializeField] private GameObject _tooltipRoot;
    [SerializeField] private TMP_Text _tooltipTitleText;
    [SerializeField] private TMP_Text _tooltipDescriptionText;

    [Header("Cost Icons")]
    [SerializeField] private GameObject _woodCostRoot;
    [SerializeField] private TMP_Text _woodCostAmountText;

    [SerializeField] private GameObject _stoneCostRoot;
    [SerializeField] private TMP_Text _stoneCostAmountText;

    [SerializeField] private GameObject _goldCostRoot;
    [SerializeField] private TMP_Text _goldCostAmountText;

    private BuildingData _buildingData;

    private void Awake()
    {
        if (_buildButton != null)
        {
            _buildButton.onClick.AddListener(OnBuildButtonClicked);
        }

        HideTooltip();
    }

    private void OnDestroy()
    {
        if (_buildButton != null)
        {
            _buildButton.onClick.RemoveListener(OnBuildButtonClicked);
        }
    }

    /// <summary>
    /// Gán dữ liệu công trình vào card.
    /// BuildingMenuUI sẽ gọi hàm này khi tạo card.
    /// </summary>
    public void Setup(BuildingData buildingData)
    {
        _buildingData = buildingData;

        if (_buildingData == null)
        {
            Debug.LogWarning("[BuildingCardUI] BuildingData đang null.", this);
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (_buildingNameText != null)
        {
            _buildingNameText.text = _buildingData.buildingName;
        }

        SetupIcon();
        // SetupCostText();
        SetupCostIcons();
        SetupTooltip();
    }

    private void SetupIcon()
    {
        if (_buildingIconImage == null)
        {
            return;
        }

        if (_buildingData.icon != null)
        {
            _buildingIconImage.sprite = _buildingData.icon;
            _buildingIconImage.enabled = true;
        }
        else
        {
            // Chưa có icon thì chỉ ẩn hình, card vẫn hoạt động.
            _buildingIconImage.sprite = null;
            _buildingIconImage.enabled = false;
        }
    }

    private void SetupCostText()
    {
        if (_costText == null)
        {
            return;
        }

        if (_buildingData.buildCosts == null ||
            _buildingData.buildCosts.Count == 0)
        {
            _costText.text = "Free";
            return;
        }

        StringBuilder builder = new StringBuilder();

        for (int i = 0; i < _buildingData.buildCosts.Count; i++)
        {
            ResourceCost cost = _buildingData.buildCosts[i];

            if (cost == null)
            {
                continue;
            }

            if (cost.amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("   ");
            }

            builder.Append(cost.resourceType);
            builder.Append(": ");
            builder.Append(cost.amount);
        }

        _costText.text = builder.Length > 0 ? builder.ToString() : "Free";
    }

    private void SetupTooltip()
    {
        if (_tooltipTitleText != null)
        {
            _tooltipTitleText.text = _buildingData.buildingName;
        }

        if (_tooltipDescriptionText != null)
        {
            _tooltipDescriptionText.text =
                string.IsNullOrWhiteSpace(_buildingData.description)
                    ? "No description available."
                    : _buildingData.description;
        }

        HideTooltip();
    }

    private void OnBuildButtonClicked()
    {
        if (_buildingData == null)
        {
            Debug.LogError("[BuildingCardUI] BuildingData is null.", this);
            return;
        }

        Debug.Log($"[BuildingCardUI] Click BUILD: {_buildingData.buildingName}", this);

        BuildingManager manager = BuildingManager.Instance;

        if (manager == null)
        {
            Debug.LogError("[BuildingCardUI] Không tìm thấy BuildingManager.Instance.", this);
            return;
        }

        manager.SelectBuilding(_buildingData);

        // Bắt buộc bật Build Mode khi chọn từ menu Dev2.
        manager.IsBuildMode = true;
        manager.IsDeleteMode = false;

        Debug.Log($"[BuildingCardUI] Đã bật Build Mode cho: {_buildingData.buildingName}", this);
    }

    // Phải bật Build Mode trước vì SelectBuilding dùng trạng thái này
    // để quyết định có hiển thị ghost building hay không.
    //manager.IsDeleteMode = false;
        //manager.IsBuildMode = true;
        //manager.SelectBuilding(_buildingData);

        //Debug.Log(
            //$"[BuildingCardUI] Đã chọn công trình: {_buildingData.buildingName}"
        //);
    //}

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_buildingData == null || _tooltipRoot == null)
        {
            return;
        }

        _tooltipRoot.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    private void HideTooltip()
    {
        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);
        }
    }

    private void SetupCostIcons()
    {
        SetCostRow(_woodCostRoot, _woodCostAmountText, 0);
        SetCostRow(_stoneCostRoot, _stoneCostAmountText, 0);
        SetCostRow(_goldCostRoot, _goldCostAmountText, 0);

        if (_buildingData == null || _buildingData.buildCosts == null)
        {
            return;
        }

        foreach (ResourceCost cost in _buildingData.buildCosts)
        {
            if (cost == null || cost.amount <= 0)
            {
                continue;
            }

            switch (cost.resourceType)
            {
                case ResourceType.Wood:
                    SetCostRow(_woodCostRoot, _woodCostAmountText, cost.amount);
                    break;

                case ResourceType.Stone:
                    SetCostRow(_stoneCostRoot, _stoneCostAmountText, cost.amount);
                    break;

                case ResourceType.Gold:
                    SetCostRow(_goldCostRoot, _goldCostAmountText, cost.amount);
                    break;
            }
        }
    }

    private void SetCostRow(GameObject root, TMP_Text amountText, int amount)
    {
        if (root != null)
        {
            root.SetActive(amount > 0);
        }

        if (amountText != null)
        {
            amountText.text = amount.ToString();
        }
    }
}