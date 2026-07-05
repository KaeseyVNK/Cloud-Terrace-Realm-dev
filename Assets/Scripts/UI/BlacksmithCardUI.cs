using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Manages a single technology card in the Blacksmith UI.
/// Reuses the layout structure of BuildingCardUI including individual cost rows and hover tooltips.
/// </summary>
public class BlacksmithCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("UI References")]
    [SerializeField] private TMP_Text _technologyNameText;
    [SerializeField] private Image _technologyIconImage;
    [SerializeField] private TMP_Text _costText; // Optional fallback cost text
    [SerializeField] private Image _progressBarFill;

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

    [Header("Status Overlays (Optional)")]
    [SerializeField] private GameObject _completedOverlay;
    [SerializeField] private GameObject _lockedOverlay;
    [SerializeField] private GameObject _researchingOverlay;
    [SerializeField] private TMP_Text _statusOverlayText; // E.g., "COMPLETED", "LOCKED"

    [Header("Hover Settings")]
    [SerializeField] private float _hoverScaleFactor = 1.15f;
    [SerializeField] private float _hoverOffsetY = 50f;
    [SerializeField] private float _lerpSpeed = 12f;

    private TechnologyData _technologyData;
    private BlacksmithResearch _researchSystem;
    private bool _isHovered = false;
    private float _visualProgress = 0f;
    private bool _isTransitioningToComplete = false;
    private Transform _visualRoot;

    // Fanned layout defaults
    private Vector3 _originalLocalPos;
    private Quaternion _originalLocalRot;
    private int _originalSiblingIndex;
    private bool _hasLayoutDefaults = false;

    // Animation targets
    private Vector3 _targetLocalPos;
    private Quaternion _targetLocalRot;
    private Vector3 _targetScale = Vector3.one;

    // Tooltip layer caching
    private Transform _tooltipOriginalParent;
    private int _tooltipOriginalSiblingIndex;
    private Vector2 _tooltipOriginalAnchorMin;
    private Vector2 _tooltipOriginalAnchorMax;
    private Vector2 _tooltipOriginalPivot;
    private Vector2 _tooltipOriginalAnchoredPosition;
    private Vector2 _tooltipOriginalSizeDelta;
    private bool _hasCachedTooltipOriginalValues;

    private void Awake()
    {
        // Dynamically create a visual container to isolate animated movements from the static raycast target (root)
        GameObject visualRootObj = new GameObject("VisualRoot", typeof(RectTransform));
        _visualRoot = visualRootObj.transform;
        _visualRoot.SetParent(transform, false);

        RectTransform rt = _visualRoot as RectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        // Reparent all visual elements under the VisualRoot container (except TooltipRoot)
        List<Transform> childrenToMove = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child != _visualRoot && child.name != "TooltipRoot")
            {
                childrenToMove.Add(child);
            }
        }

        foreach (Transform child in childrenToMove)
        {
            child.SetParent(_visualRoot, false);
        }

        // Transfer the card background rendering to the VisualRoot, keeping the root image transparent for static raycasts
        Image rootImage = GetComponent<Image>();
        if (rootImage != null)
        {
            Image visualImage = visualRootObj.AddComponent<Image>();
            visualImage.sprite = rootImage.sprite;
            visualImage.color = rootImage.color;
            visualImage.type = rootImage.type;
            visualImage.material = rootImage.material;
            visualImage.raycastTarget = false;

            // Make the root raycast area invisible
            rootImage.color = new Color(0f, 0f, 0f, 0f);
            rootImage.raycastTarget = true;
        }

        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);
        }
        
        if (_completedOverlay != null) _completedOverlay.SetActive(false);
        if (_lockedOverlay != null) _lockedOverlay.SetActive(false);
        if (_researchingOverlay != null) _researchingOverlay.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_tooltipRoot != null && _hasCachedTooltipOriginalValues)
        {
            // If the tooltip was moved to the top layer, it won't be destroyed automatically with the card.
            // We must destroy it explicitly to prevent scene leaks.
            if (_tooltipOriginalParent != null && _tooltipRoot.transform.parent != _tooltipOriginalParent)
            {
                Destroy(_tooltipRoot);
            }
        }
    }

    /// <summary>
    /// Configures the card with technology data and references.
    /// </summary>
    public void Setup(TechnologyData tech, BlacksmithResearch research)
    {
        _technologyData = tech;
        _researchSystem = research;

        if (tech == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (_technologyNameText != null)
        {
            _technologyNameText.text = tech.technologyName.ToUpper();
        }

        if (_technologyIconImage != null)
        {
            if (tech.icon != null)
            {
                _technologyIconImage.sprite = tech.icon;
                _technologyIconImage.enabled = true;
            }
            else
            {
                _technologyIconImage.enabled = false;
            }
        }

        // Setup the tooltip titles and descriptions
        if (_tooltipTitleText != null)
        {
            _tooltipTitleText.text = tech.technologyName;
        }
        if (_tooltipDescriptionText != null)
        {
            string effectText = tech.GetVillagerEffectText();
            if (string.IsNullOrEmpty(effectText))
            {
                effectText = tech.description;
            }
            _tooltipDescriptionText.text = effectText;
        }

        // Setup costs (both fallback text and individual icon rows)
        SetupFallbackCostText(tech.researchCosts);
        SetupCostIcons(tech.researchCosts);
        
        _isTransitioningToComplete = false;
        if (tech != null && TechnologyManager.Instance != null && TechnologyManager.Instance.IsUnlocked(tech))
        {
            _visualProgress = 1f;
        }
        else if (tech != null && research != null && research.CurrentResearch == tech)
        {
            float elapsed = tech.researchTime - research.CurrentResearchTimer;
            _visualProgress = elapsed / Mathf.Max(0.1f, tech.researchTime);
        }
        else
        {
            _visualProgress = 0f;
        }

        UpdateStatus();
    }

    /// <summary>
    /// Stores the fanned layout values to revert to when hover ends.
    /// </summary>
    public void SetLayoutDefaults(Vector3 pos, Quaternion rot, int siblingIndex)
    {
        _originalLocalPos = pos;
        _originalLocalRot = rot;
        _originalSiblingIndex = siblingIndex;
        _hasLayoutDefaults = true;

        // Position the static root in the fanned arc layout
        transform.localPosition = pos;
        transform.localRotation = rot;
        transform.localScale = Vector3.one;

        if (!_isHovered)
        {
            _targetLocalPos = Vector3.zero;
            _targetLocalRot = Quaternion.identity;
            _targetScale = Vector3.one;
            
            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
                _visualRoot.localScale = Vector3.one;
            }
        }
    }

    private void Update()
    {
        if (!_hasLayoutDefaults || _technologyData == null) return;

        // Smoothly interpolate position, rotation, and scale of the visual container, leaving the root raycast target static
        if (_visualRoot != null)
        {
            _visualRoot.localPosition = Vector3.Lerp(_visualRoot.localPosition, _targetLocalPos, Time.deltaTime * _lerpSpeed);
            _visualRoot.localRotation = Quaternion.Slerp(_visualRoot.localRotation, _targetLocalRot, Time.deltaTime * _lerpSpeed);
            _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _targetScale, Time.deltaTime * _lerpSpeed);
        }

        bool isResearchingThis = _researchSystem != null && _researchSystem.CurrentResearch == _technologyData;

        if (isResearchingThis || _isTransitioningToComplete)
        {
            float targetProgress = 1f;
            if (isResearchingThis)
            {
                targetProgress = 1f - (_researchSystem.CurrentResearchTimer / Mathf.Max(0.1f, _technologyData.researchTime));
            }

            // Smoothly move visual progress towards target progress
            _visualProgress = Mathf.MoveTowards(_visualProgress, targetProgress, Time.deltaTime * 1.5f);

            // If we reached 100% and were transitioning, complete the transition
            if (_isTransitioningToComplete && _visualProgress >= 0.995f)
            {
                _visualProgress = 1f;
                _isTransitioningToComplete = false;
                UpdateStatus(); // This will disable researching overlay and enable completed overlay!
            }

            if (_progressBarFill != null)
            {
                _progressBarFill.enabled = true;
                _progressBarFill.fillAmount = _visualProgress;
            }

            // Update the green vertical progress overlay dynamically on top of the card
            if (_researchingOverlay != null)
            {
                _researchingOverlay.SetActive(true);
                Image overlayImg = _researchingOverlay.GetComponent<Image>();
                if (overlayImg != null)
                {
                    if (overlayImg.sprite == null)
                    {
                        overlayImg.sprite = GetDefaultWhiteSprite();
                    }
                    overlayImg.color = new Color(0.15f, 0.85f, 0.25f, 0.45f); // Semi-transparent vibrant green
                    overlayImg.type = Image.Type.Filled;
                    overlayImg.fillMethod = Image.FillMethod.Vertical;
                    overlayImg.fillOrigin = (int)Image.OriginVertical.Bottom; // Fills vertically from bottom to top
                    overlayImg.fillAmount = _visualProgress;
                }
            }

            if (_statusOverlayText != null)
            {
                _statusOverlayText.text = $"{(_visualProgress * 100f):F0}%";
            }
        }
        else
        {
            if (_progressBarFill != null)
            {
                if (_progressBarFill == _technologyIconImage)
                {
                    // If the progress fill and icon image share the same UI component,
                    // keep it enabled so the icon remains visible, but ensure it is fully filled.
                    _progressBarFill.fillAmount = 1f;
                }
                else
                {
                    _progressBarFill.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Updates the visual status of the card based on resource availability and unlock state.
    /// </summary>
    public void UpdateStatus()
    {
        if (_technologyData == null || _researchSystem == null) return;

        bool unlocked = TechnologyManager.Instance.IsUnlocked(_technologyData);
        bool isResearchingThis = _researchSystem.CurrentResearch == _technologyData;
        bool isResearchingOther = _researchSystem.IsResearching && !isResearchingThis;

        // Disable overlays initially
        if (_completedOverlay != null) _completedOverlay.SetActive(false);
        if (_lockedOverlay != null) _lockedOverlay.SetActive(false);
        if (_researchingOverlay != null) _researchingOverlay.SetActive(false);

        if (unlocked)
        {
            // If we are currently transitioning the visual progress to 100%,
            // delay showing the completed overlay until the progress fills up completely.
            if (_visualProgress < 0.995f)
            {
                _isTransitioningToComplete = true;
                if (_researchingOverlay != null) _researchingOverlay.SetActive(true);
            }
            else
            {
                _isTransitioningToComplete = false;
                if (_completedOverlay != null) _completedOverlay.SetActive(true);
                if (_statusOverlayText != null) _statusOverlayText.text = "HOÀN THÀNH";
            }
        }
        else if (isResearchingThis)
        {
            if (_researchingOverlay != null) _researchingOverlay.SetActive(true);
            // Percentage text is updated dynamically in Update()
        }
        else if (isResearchingOther)
        {
            if (_lockedOverlay != null) _lockedOverlay.SetActive(true);
            if (_statusOverlayText != null) _statusOverlayText.text = "ĐANG BẬN";
        }
        else
        {
            bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(_technologyData.researchCosts);
            if (!canAfford)
            {
                if (_lockedOverlay != null) _lockedOverlay.SetActive(true);
                if (_statusOverlayText != null) _statusOverlayText.text = "THIẾU T.NGUYÊN";
            }
        }
    }

    private void SetupFallbackCostText(List<ResourceCost> costs)
    {
        if (_costText == null) return;

        if (costs == null || costs.Count == 0)
        {
            _costText.text = "Miễn phí";
            return;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < costs.Count; i++)
        {
            if (i > 0) sb.Append("  ");
            string icon = GetResourceIcon(costs[i].resourceType);
            sb.Append($"{icon} {costs[i].amount}");
        }
        _costText.text = sb.ToString();
    }

    private void SetupCostIcons(List<ResourceCost> costs)
    {
        SetCostRow(_woodCostRoot, _woodCostAmountText, 0);
        SetCostRow(_stoneCostRoot, _stoneCostAmountText, 0);
        SetCostRow(_goldCostRoot, _goldCostAmountText, 0);

        if (costs == null) return;

        foreach (var cost in costs)
        {
            if (cost == null || cost.amount <= 0) continue;

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

    private string GetResourceIcon(ResourceType type)
    {
        return type switch
        {
            ResourceType.Wood => "🪵",
            ResourceType.Stone => "🪨",
            ResourceType.Food => "🌾",
            ResourceType.Gold => "🪙",
            ResourceType.AncientRelic => "🏺",
            _ => "📦"
        };
    }

    private void OnCardClicked()
    {
        if (_technologyData == null || _researchSystem == null)
        {
            GameLog.LogError("[BlacksmithCardUI] OnCardClicked: tech or research system is null");
            return;
        }

        GameLog.Log($"[BlacksmithCardUI] OnCardClicked: Requesting research for {_technologyData.technologyName}");
        _researchSystem.RequestResearch(_technologyData);
        UpdateStatus();
    }

    private bool IsInteractable()
    {
        if (_technologyData == null || _researchSystem == null) return false;

        bool unlocked = TechnologyManager.Instance.IsUnlocked(_technologyData);
        bool isResearchingThis = _researchSystem.CurrentResearch == _technologyData;
        bool isResearchingOther = _researchSystem.IsResearching && !isResearchingThis;

        if (unlocked || isResearchingThis || isResearchingOther) return false;

        bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(_technologyData.researchCosts);
        return canAfford;
    }

    // --- Hover Tooltip Positioning Logic (Reused from BuildingCardUI) ---

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

                // Fallbacks if not initialized
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

        RectTransform cardRect = (_visualRoot != null ? _visualRoot : transform) as RectTransform;
        Vector3[] cardCorners = new Vector3[4];
        cardRect.GetWorldCorners(cardCorners);

        Vector3 rightCenterWorld = (cardCorners[2] + cardCorners[3]) * 0.5f;
        Vector3 leftCenterWorld = (cardCorners[0] + cardCorners[1]) * 0.5f;

        tooltipTransform.SetParent(layer, false);
        tooltipTransform.anchorMin = new Vector2(0f, 0.5f);
        tooltipTransform.anchorMax = new Vector2(0f, 0.5f);
        tooltipTransform.pivot = new Vector2(0f, 0.5f);
        tooltipTransform.sizeDelta = _tooltipOriginalSizeDelta;

        Vector2 rightCenterLocal = layer.InverseTransformPoint(rightCenterWorld);
        Vector2 leftCenterLocal = layer.InverseTransformPoint(leftCenterWorld);

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

    private void HideTooltip()
    {
        if (_tooltipRoot != null)
        {
            _tooltipRoot.SetActive(false);

            if (_hasCachedTooltipOriginalValues)
            {
                if (_tooltipOriginalParent != null && _tooltipRoot.transform.parent != _tooltipOriginalParent)
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

    private Transform FindTooltipLayer()
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == "BuildingMenuRoot" || current.name == "BlacksmithUIPanel")
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

        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : null;
    }

    // --- Event System Handlers ---

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        _targetScale = Vector3.one * _hoverScaleFactor;

        // Visual offsets relative to the static fanned root
        _targetLocalPos = Vector3.up * _hoverOffsetY;
        _targetLocalRot = Quaternion.Inverse(transform.localRotation); // Keep visual container upright

        // Bring card to front
        transform.SetAsLastSibling();

        // Show hover tooltip
        if (_tooltipRoot != null)
        {
            CacheTooltipOriginalValues();
            _tooltipRoot.SetActive(true);
            MoveTooltipToTopLayer();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _targetScale = Vector3.one;
        _targetLocalPos = Vector3.zero;
        _targetLocalRot = Quaternion.identity;

        // Revert card sibling index
        transform.SetSiblingIndex(_originalSiblingIndex);

        // Hide hover tooltip
        HideTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        bool interactable = IsInteractable();
        GameLog.Log($"[BlacksmithCardUI] OnPointerClick: LeftClick={eventData.button == PointerEventData.InputButton.Left}, IsInteractable={interactable}, Tech={_technologyData?.technologyName}");
        
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (interactable)
            {
                OnCardClicked();
            }
            else
            {
                if (_technologyData == null)
                {
                    GameLog.LogWarning("[BlacksmithCardUI] Click blocked: _technologyData is null");
                }
                else if (_researchSystem == null)
                {
                    GameLog.LogWarning("[BlacksmithCardUI] Click blocked: _researchSystem is null");
                }
                else
                {
                    bool unlocked = TechnologyManager.Instance != null && TechnologyManager.Instance.IsUnlocked(_technologyData);
                    bool isResearchingThis = _researchSystem.CurrentResearch == _technologyData;
                    bool isResearchingOther = _researchSystem.IsResearching && !isResearchingThis;
                    bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(_technologyData.researchCosts);
                    GameLog.LogWarning($"[BlacksmithCardUI] Click blocked for {_technologyData.technologyName}: Unlocked={unlocked}, ResearchingThis={isResearchingThis}, ResearchingOther={isResearchingOther}, CanAfford={canAfford}");
                }
            }
        }
    }

    private static Sprite _defaultWhiteSprite;

    private static Sprite GetDefaultWhiteSprite()
    {
        if (_defaultWhiteSprite == null)
        {
            Texture2D tex = new Texture2D(2, 2);
            for (int y = 0; y < tex.height; y++)
            {
                for (int x = 0; x < tex.width; x++)
                {
                    tex.SetPixel(x, y, Color.white);
                }
            }
            tex.Apply();
            _defaultWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        return _defaultWhiteSprite;
    }
}
