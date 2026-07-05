using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BuildingCardUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
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
    private Transform _tooltipOriginalParent;
    private int _tooltipOriginalSiblingIndex;
    private Vector2 _tooltipOriginalAnchorMin;
    private Vector2 _tooltipOriginalAnchorMax;
    private Vector2 _tooltipOriginalPivot;
    private Vector2 _tooltipOriginalAnchoredPosition;
    private Vector2 _tooltipOriginalSizeDelta;
    private bool _hasCachedTooltipOriginalValues;

    private Coroutine _hoverScaleCoroutine;
    private Coroutine _popInCoroutine;
    private bool _isUnlocked = true;
    private bool _isPointerDown;
    private bool _isHoldTooltipShown;
    private float _pointerDownTime;
    private const float HoldTooltipThreshold = 0.4f;
    private const float HoverScaleFactor = 1.05f;
    private const float ScaleDuration = 0.15f;

    private void Awake()
    {
        if (_buildButton != null)
        {
            _buildButton.onClick.RemoveListener(OnBuildButtonClicked);
            _buildButton.enabled = false;
        }

        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_buildButton != null)
        {
            _buildButton.onClick.RemoveListener(OnBuildButtonClicked);
        }
    }

    private void Update()
    {
        if (!_isPointerDown || _isHoldTooltipShown)
        {
            return;
        }

        if (Time.unscaledTime - _pointerDownTime >= HoldTooltipThreshold)
        {
            _isHoldTooltipShown = true;
            ShowTooltip();
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
            GameLog.LogWarning("[BuildingCardUI] BuildingData đang null.", this);
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
        _isUnlocked = IsBuildingUnlocked();
        UpdateUnlockedState();
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
            if (!_isUnlocked)
            {
                _tooltipDescriptionText.text = GetRequirementText();
            }
            else
            {
                _tooltipDescriptionText.text =
                    string.IsNullOrWhiteSpace(_buildingData.description)
                        ? "No description available."
                        : _buildingData.description;
            }
        }

        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);
        }
    }

    private void OnBuildButtonClicked()
    {
        if (_buildingData == null)
        {
            GameLog.LogError("[BuildingCardUI] BuildingData is null.", this);
            return;
        }

        if (!_isUnlocked)
        {
            GameLog.Log($"[BuildingCardUI] Locked building: {_buildingData.buildingName}", this);
            return;
        }

        GameLog.Log($"[BuildingCardUI] Click BUILD: {_buildingData.buildingName}", this);

        BuildingManager manager = BuildingManager.Instance;

        if (manager == null)
        {
            GameLog.LogError("[BuildingCardUI] Không tìm thấy BuildingManager.Instance.", this);
            return;
        }

        manager.SelectBuilding(_buildingData);

        // Bắt buộc bật Build Mode khi chọn từ menu Dev2.
        manager.IsBuildMode = true;
        manager.IsDeleteMode = false;

        GameLog.Log($"[BuildingCardUI] Đã bật Build Mode cho: {_buildingData.buildingName}", this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_buildingData == null)
        {
            return;
        }

        if (_hoverScaleCoroutine != null) StopCoroutine(_hoverScaleCoroutine);
        if (_popInCoroutine != null)
        {
            StopCoroutine(_popInCoroutine);
            _popInCoroutine = null;
        }
        _hoverScaleCoroutine = StartCoroutine(ScaleTo(Vector3.one * HoverScaleFactor, ScaleDuration));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();

        if (_hoverScaleCoroutine != null) StopCoroutine(_hoverScaleCoroutine);
        if (_popInCoroutine != null)
        {
            StopCoroutine(_popInCoroutine);
            _popInCoroutine = null;
        }
        _hoverScaleCoroutine = StartCoroutine(ScaleTo(Vector3.one, ScaleDuration));
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (_buildingData == null)
        {
            return;
        }

        _isPointerDown = true;
        _isHoldTooltipShown = false;
        _pointerDownTime = Time.unscaledTime;
        HideTooltip();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        bool shouldBuild = _isPointerDown && !_isHoldTooltipShown;
        bool shouldHideTooltip = _isHoldTooltipShown;
        _isPointerDown = false;

        if (shouldBuild)
        {
            OnBuildButtonClicked();
        }
        else if (shouldHideTooltip)
        {
            HideTooltip();
        }
    }

    public void AnimatePopIn(float delay)
    {
        if (_popInCoroutine != null)
        {
            StopCoroutine(_popInCoroutine);
            _popInCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            transform.localScale = Vector3.zero;
            _popInCoroutine = StartCoroutine(PopInRoutine(delay));
        }
        else
        {
            transform.localScale = Vector3.one;
        }
    }

    private System.Collections.IEnumerator PopInRoutine(float delay)
    {
        transform.localScale = Vector3.zero;

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        float duration = 0.4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Back Ease Out
            float tMinus1 = t - 1f;
            float s = 1.70158f;
            float currentScale = tMinus1 * tMinus1 * ((s + 1f) * tMinus1 + s) + 1f;

            transform.localScale = Vector3.one * currentScale;
            yield return null;
        }

        transform.localScale = Vector3.one;
        _popInCoroutine = null;
    }

    private System.Collections.IEnumerator ScaleTo(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed / duration);
            yield return null;
        }
        transform.localScale = targetScale;
        _hoverScaleCoroutine = null;
    }

    private void HideTooltip()
    {
        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);

            if (_hasCachedTooltipOriginalValues)
            {
                if (_tooltipOriginalParent != null &&
                    _tooltipRoot.transform.parent != _tooltipOriginalParent)
                {
                    _tooltipRoot.transform.SetParent(_tooltipOriginalParent, false);
                }

                RectTransform tooltipRect = _tooltipRoot.transform as RectTransform;
                if (tooltipRect != null)
                {
                    tooltipRect.anchorMin = _tooltipOriginalAnchorMin;
                    tooltipRect.anchorMax = _tooltipOriginalAnchorMax;
                    tooltipRect.pivot = _tooltipOriginalPivot;
                    tooltipRect.anchoredPosition = _tooltipOriginalAnchoredPosition;
                    tooltipRect.sizeDelta = _tooltipOriginalSizeDelta;
                    tooltipRect.SetSiblingIndex(_tooltipOriginalSiblingIndex);
                }
            }
        }
    }

    private void CacheTooltipOriginalValues()
    {
        if (_hasCachedTooltipOriginalValues) return;

        if (_tooltipRoot != null)
        {
            _tooltipOriginalParent = _tooltipRoot.transform.parent;
            RectTransform tooltipRect = _tooltipRoot.transform as RectTransform;
            if (tooltipRect != null)
            {
                _tooltipOriginalSiblingIndex = tooltipRect.GetSiblingIndex();
                _tooltipOriginalAnchorMin = tooltipRect.anchorMin;
                _tooltipOriginalAnchorMax = tooltipRect.anchorMax;
                _tooltipOriginalPivot = tooltipRect.pivot;
                _tooltipOriginalAnchoredPosition = tooltipRect.anchoredPosition;
                _tooltipOriginalSizeDelta = tooltipRect.sizeDelta;

                // Fallbacks if RectTransform hasn't been initialized by UI Canvas layout yet
                if (_tooltipOriginalSizeDelta == Vector2.zero)
                {
                    _tooltipOriginalSizeDelta = new Vector2(326.77f, 107.00f);
                }
                if (_tooltipOriginalAnchorMin == Vector2.zero && _tooltipOriginalAnchorMax == Vector2.zero)
                {
                    _tooltipOriginalAnchorMin = new Vector2(0.5f, 0.5f);
                    _tooltipOriginalAnchorMax = new Vector2(0.5f, 0.5f);
                    _tooltipOriginalPivot = new Vector2(0.5f, 0.5f);
                    _tooltipOriginalAnchoredPosition = new Vector2(267.80f, 53.50f);
                }
            }
            _hasCachedTooltipOriginalValues = true;
        }
    }

    private void MoveTooltipToTopLayer()
    {
        RectTransform tooltipTransform = _tooltipRoot.transform as RectTransform;
        RectTransform layer = FindTooltipLayer() as RectTransform;

        if (tooltipTransform == null || layer == null)
        {
            _tooltipRoot.transform.SetAsLastSibling();
            return;
        }

        RectTransform cardRect = transform as RectTransform;
        Vector3[] cardCorners = new Vector3[4];
        cardRect.GetWorldCorners(cardCorners);

        Vector3 rightCenterWorld = (cardCorners[2] + cardCorners[3]) * 0.5f;
        Vector3 leftCenterWorld = (cardCorners[0] + cardCorners[1]) * 0.5f;

        tooltipTransform.SetParent(layer, false);
        tooltipTransform.anchorMin = new Vector2(0f, 0.5f);
        tooltipTransform.anchorMax = new Vector2(0f, 0.5f);
        tooltipTransform.pivot = new Vector2(0f, 0.5f);
        tooltipTransform.sizeDelta = _tooltipOriginalSizeDelta;

        Vector2 rightCenterLocal =
            layer.InverseTransformPoint(rightCenterWorld);
        Vector2 leftCenterLocal =
            layer.InverseTransformPoint(leftCenterWorld);

        const float margin = 12f;
        Rect layerRect = layer.rect;
        Vector2 tooltipSize = tooltipTransform.rect.size;
        if (tooltipSize.x <= 0f || tooltipSize.y <= 0f)
        {
            tooltipSize = tooltipTransform.sizeDelta;
        }

        float layerLeftToPivot = layerRect.width * layer.pivot.x;
        float x = rightCenterLocal.x + layerLeftToPivot + margin;

        if (x + tooltipSize.x > layerRect.width - margin)
        {
            x = leftCenterLocal.x + layerLeftToPivot - tooltipSize.x - margin;
        }

        x = Mathf.Clamp(x, margin, Mathf.Max(margin, layerRect.width - tooltipSize.x - margin));

        float minY = -layerRect.height * 0.5f + tooltipSize.y * 0.5f + margin;
        float maxY = layerRect.height * 0.5f - tooltipSize.y * 0.5f - margin;
        float y = Mathf.Clamp(rightCenterLocal.y, minY, maxY);

        tooltipTransform.anchoredPosition = new Vector2(x, y);
        tooltipTransform.SetAsLastSibling();
    }

    private Transform FindTooltipLayer()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "BuildingMenuRoot")
            {
                Transform layer = current.Find("TooltipLayer");
                if (layer != null)
                {
                    return layer;
                }

                return current;
            }

            current = current.parent;
        }

        return null;
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
            root.SetActive(true);
        }

        if (amountText != null)
        {
            amountText.text = amount.ToString();
        }
    }

    private void ShowTooltip()
    {
        if (_buildingData == null || _tooltipRoot == null)
        {
            return;
        }

        CacheTooltipOriginalValues();
        _tooltipRoot.SetActive(true);
        MoveTooltipToTopLayer();
    }

    private bool IsBuildingUnlocked()
    {
        if (_buildingData == null)
        {
            return false;
        }

        if (_buildingData.requiredBuildings == null || _buildingData.requiredBuildings.Count == 0)
        {
            return true;
        }

        BuildingManager manager = BuildingManager.Instance;
        if (manager == null)
        {
            return false;
        }

        foreach (BuildingData requiredBuilding in _buildingData.requiredBuildings)
        {
            if (requiredBuilding == null)
            {
                continue;
            }

            if (!manager.BuiltBuildingCounts.TryGetValue(requiredBuilding, out int count) || count <= 0)
            {
                return false;
            }
        }

        return true;
    }

    private void UpdateUnlockedState()
    {
        if (_buildButton != null)
        {
            _buildButton.interactable = _isUnlocked;
            _buildButton.enabled = false;
        }

        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.alpha = _isUnlocked ? 1f : 0.48f;
        canvasGroup.interactable = _isUnlocked;
        canvasGroup.blocksRaycasts = true;

        if (_buildingNameText != null && !_isUnlocked)
        {
            _buildingNameText.text = _buildingData.buildingName + " (Locked)";
        }
    }

    private string GetRequirementText()
    {
        if (_buildingData == null ||
            _buildingData.requiredBuildings == null ||
            _buildingData.requiredBuildings.Count == 0)
        {
            return "Locked";
        }

        StringBuilder builder = new StringBuilder("Requires: ");
        for (int i = 0; i < _buildingData.requiredBuildings.Count; i++)
        {
            BuildingData requiredBuilding = _buildingData.requiredBuildings[i];
            if (requiredBuilding == null)
            {
                continue;
            }

            if (builder.Length > "Requires: ".Length)
            {
                builder.Append(", ");
            }

            builder.Append(requiredBuilding.buildingName);
        }

        return builder.ToString();
    }
}
