using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// Controls a single resource column inside the Trade Market UI.
/// Handles buying/selling resources for Gold.
/// </summary>
public class MarketResourceColumnUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Configuration")]
    [SerializeField] private ResourceType _resourceType;

    [Header("UI References")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TMP_Text _stockText;
    [SerializeField] private TMP_InputField _amountInputField;
    [SerializeField] private Button _increaseButton;
    [SerializeField] private Button _decreaseButton;
    [SerializeField] private Button _buySellButton;
    [SerializeField] private TMP_Text _buySellText;
    [SerializeField] private Button _tradeConfirmButton;
    [SerializeField] private TMP_Text _valueText;

    [Header("Trade Settings")]
    [SerializeField] private int _stepAmount = 10;

    private MarketController _market;
    private int _currentTradeAmount = 0;
    private bool _isBuying = false; // true = Buy (Gold -> Resource), false = Sell (Resource -> Gold)
    private TMP_Text _valueTitleText;
    private Color _originalValueColor;
    private bool _hasOriginalColor = false;

    /// <summary>
    /// Set up column with the market instance.
    /// </summary>
    /// <param name="market">The market controller instance.</param>
    public void Setup(MarketController market)
    {
        _market = market;
        _currentTradeAmount = 0;
        _isBuying = false;

        // Find Value titleText component inside Value Border
        var valueBorder = transform.Find("Value Border");
        if (valueBorder != null)
        {
            var titleTransform = valueBorder.Find("Value titleText ") ?? valueBorder.Find("Value titleText");
            if (titleTransform != null)
            {
                _valueTitleText = titleTransform.GetComponent<TMP_Text>();
            }

            var btn = valueBorder.GetComponent<Button>();
            if (btn == null)
            {
                btn = valueBorder.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ToggleBuySell);
        }

        // Update resource icon dynamically from WorldSpaceOverlayManager
        var overlayManager = FindAnyObjectByType<WorldSpaceOverlayManager>();
        if (overlayManager != null && _iconImage != null)
        {
            switch (_resourceType)
            {
                case ResourceType.Wood:
                    if (overlayManager.WoodSprite != null) _iconImage.sprite = overlayManager.WoodSprite;
                    break;
                case ResourceType.Stone:
                    if (overlayManager.StoneSprite != null) _iconImage.sprite = overlayManager.StoneSprite;
                    break;
                case ResourceType.Food:
                    if (overlayManager.FoodSprite != null) _iconImage.sprite = overlayManager.FoodSprite;
                    break;
                case ResourceType.Gold:
                    if (overlayManager.GoldSprite != null) _iconImage.sprite = overlayManager.GoldSprite;
                    break;
            }
        }

        if (_buySellButton != null)
        {
            _buySellButton.onClick.RemoveAllListeners();
            _buySellButton.onClick.AddListener(ExecuteTrade); // Executes trade directly, no confirmation required
        }

        if (_increaseButton != null)
        {
            _increaseButton.onClick.RemoveAllListeners();
            _increaseButton.onClick.AddListener(IncreaseAmount);
        }

        if (_decreaseButton != null)
        {
            _decreaseButton.onClick.RemoveAllListeners();
            _decreaseButton.onClick.AddListener(DecreaseAmount);
        }

        if (_tradeConfirmButton != null)
        {
            _tradeConfirmButton.onClick.RemoveAllListeners();
            _tradeConfirmButton.onClick.AddListener(ExecuteTrade); // Executes trade directly
        }

        if (_amountInputField != null)
        {
            _amountInputField.onEndEdit.RemoveAllListeners();
            _amountInputField.onEndEdit.AddListener(OnInputAmountChanged);
        }

        // Make Icon and NameSupply act as Buy/Sell toggles when clicked
        if (_iconImage != null)
        {
            var btn = _iconImage.GetComponent<Button>();
            if (btn == null)
            {
                btn = _iconImage.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ToggleBuySell);
        }

        var nameSupply = transform.Find("NameSupply");
        if (nameSupply != null)
        {
            var btn = nameSupply.GetComponent<Button>();
            if (btn == null)
            {
                btn = nameSupply.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(ToggleBuySell);
        }

        // Configure for Gold (Non-interactive)
        if (_resourceType == ResourceType.Gold)
        {
            if (_amountInputField != null) _amountInputField.gameObject.SetActive(false);
            if (_increaseButton != null) _increaseButton.gameObject.SetActive(false);
            if (_decreaseButton != null) _decreaseButton.gameObject.SetActive(false);
            if (_buySellButton != null) _buySellButton.gameObject.SetActive(false);
            if (_tradeConfirmButton != null) _tradeConfirmButton.gameObject.SetActive(false);
            if (_valueText != null) _valueText.text = "--";
            if (_valueTitleText != null) _valueTitleText.text = "Value:";

            // Disable dynamic toggles for Gold
            if (_iconImage != null && _iconImage.GetComponent<Button>() != null)
            {
                Destroy(_iconImage.GetComponent<Button>());
            }
            var ns = transform.Find("NameSupply");
            if (ns != null && ns.GetComponent<Button>() != null)
            {
                Destroy(ns.GetComponent<Button>());
            }
            var vb = transform.Find("Value Border");
            if (vb != null && vb.GetComponent<Button>() != null)
            {
                Destroy(vb.GetComponent<Button>());
            }
        }

        RefreshUI();
    }

    /// <summary>
    /// Refresh resource stock, value text, and trade button availability.
    /// </summary>
    public void RefreshUI()
    {
        if (_market == null) return;

        // 1. Refresh player stock
        int playerStock = 0;
        if (ResourceManager.Instance != null)
        {
            playerStock = ResourceManager.Instance.GetResourceAmount(_resourceType);
        }
        if (_stockText != null)
        {
            _stockText.text = $"x{playerStock}";
        }

        if (_resourceType == ResourceType.Gold) return;

        // 2. Refresh price value
        float unitPrice = _market.GetBasePrice(_resourceType);
        if (_market.CurrentPrices.ContainsKey(_resourceType))
        {
            unitPrice = _market.CurrentPrices[_resourceType];
        }

        // Clamp trade amount within bounds
        int maxTrade = GetMaxPossibleTrade();
        _currentTradeAmount = Mathf.Clamp(_currentTradeAmount, 0, maxTrade);

        if (_amountInputField != null)
        {
            _amountInputField.text = $"{_currentTradeAmount}/{maxTrade}";
        }

        if (_buySellText != null)
        {
            _buySellText.text = _isBuying ? "BUY" : "SELL";
        }

        if (_valueText != null)
        {
            if (!_hasOriginalColor)
            {
                _originalValueColor = _valueText.color;
                _hasOriginalColor = true;
            }

            if (_isBuying)
            {
                int requiredGold = Mathf.CeilToInt(_currentTradeAmount * unitPrice);
                _valueText.text = $"-{requiredGold} Gold";
                _valueText.color = new Color(0.95f, 0.26f, 0.21f); // Soft Red
                if (_valueTitleText != null)
                {
                    _valueTitleText.text = "BUY (Click):";
                }
            }
            else
            {
                float averageRate;
                int yieldGold = _market.CalculateTradeResult(_resourceType, ResourceType.Gold, _currentTradeAmount, out averageRate);
                _valueText.text = $"+{yieldGold} Gold";
                _valueText.color = new Color(0.3f, 0.85f, 0.4f); // Soft Green
                if (_valueTitleText != null)
                {
                    _valueTitleText.text = "SELL (Click):";
                }
            }
        }

        // 3. Trade button interactable state
        bool canTrade = _currentTradeAmount > 0;
        if (_isBuying)
        {
            int goldStock = ResourceManager.Instance != null ? ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) : 0;
            int requiredGold = Mathf.CeilToInt(_currentTradeAmount * unitPrice);
            canTrade &= (goldStock >= requiredGold);
        }
        else
        {
            canTrade &= (playerStock >= _currentTradeAmount);
        }

        if (_tradeConfirmButton != null)
        {
            _tradeConfirmButton.interactable = canTrade;
        }
        if (_buySellButton != null)
        {
            _buySellButton.interactable = canTrade;
        }
    }

    private Coroutine _flipRoutine;

    private void ToggleBuySell()
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiClick();
        }

        if (_flipRoutine != null) StopCoroutine(_flipRoutine);
        _flipRoutine = StartCoroutine(FlipCardRoutine());
    }

    private System.Collections.IEnumerator FlipCardRoutine()
    {
        float duration = 0.16f;
        float elapsed = 0f;
        
        // Phase 1: Rotate Y to 90 degrees
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / (duration * 0.5f);
            float angle = Mathf.Lerp(0f, 90f, t * t);
            transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        
        // Midpoint: Swap data
        _isBuying = !_isBuying;
        _currentTradeAmount = 0;
        RefreshUI();
        
        // Phase 2: Rotate Y back to 0 degrees
        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / (duration * 0.5f);
            float tCurve = Mathf.Sin(t * Mathf.PI * 0.5f);
            float angle = Mathf.Lerp(90f, 0f, tCurve);
            transform.localRotation = Quaternion.Euler(0f, angle, 0f);
            yield return null;
        }
        
        transform.localRotation = Quaternion.identity;
        _flipRoutine = null;
    }

    private void IncreaseAmount()
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiHover();
        }

        int maxTrade = GetMaxPossibleTrade();
        _currentTradeAmount = Mathf.Min(_currentTradeAmount + _stepAmount, maxTrade);
        RefreshUI();
    }

    private void DecreaseAmount()
    {
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayUiHover();
        }

        _currentTradeAmount = Mathf.Max(_currentTradeAmount - _stepAmount, 0);
        RefreshUI();
    }

    private void OnInputAmountChanged(string text)
    {
        string valStr = text;
        int slashIdx = text.IndexOf('/');
        if (slashIdx >= 0)
        {
            valStr = text.Substring(0, slashIdx);
        }

        if (int.TryParse(valStr, out int val))
        {
            _currentTradeAmount = val;
        }
        RefreshUI();
    }

    private int GetMaxPossibleTrade()
    {
        if (_market == null || ResourceManager.Instance == null) return 0;

        if (_isBuying)
        {
            int goldStock = ResourceManager.Instance.GetResourceAmount(ResourceType.Gold);
            float unitPrice = _market.CurrentPrices.ContainsKey(_resourceType) ? _market.CurrentPrices[_resourceType] : 1f;
            if (unitPrice <= 0f) return 0;
            return Mathf.FloorToInt(goldStock / unitPrice);
        }
        else
        {
            return ResourceManager.Instance.GetResourceAmount(_resourceType);
        }
    }

    private void ExecuteTrade()
    {
        if (_market == null || _currentTradeAmount <= 0) return;

        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayMarketTrade();
        }

        if (_isBuying)
        {
            float unitPrice = _market.CurrentPrices.ContainsKey(_resourceType) ? _market.CurrentPrices[_resourceType] : 1f;
            int requiredGold = Mathf.CeilToInt(_currentTradeAmount * unitPrice);
            int tradeAmountCached = _currentTradeAmount;

            // Execute trade: Gold -> Resource
            if (_market.ExecuteTrade(ResourceType.Gold, _resourceType, requiredGold))
            {
                Debug.Log($"[MarketResourceColumnUI] Successfully bought {tradeAmountCached} {_resourceType} for {requiredGold} Gold.");
                _currentTradeAmount = 0;

                TriggerIconBounce();
                TriggerValuePulse();
                SpawnFloatingText($"+{tradeAmountCached} {_resourceType} (Pending)", new Color(0.3f, 0.85f, 0.4f), _iconImage != null ? _iconImage.transform.position : transform.position);
                SpawnFloatingText($"-{requiredGold} Gold", new Color(0.95f, 0.26f, 0.21f), _buySellButton != null ? _buySellButton.transform.position : transform.position);

                if (MarketUIController.Instance != null)
                {
                    MarketUIController.Instance.RefreshAll();
                }
            }
        }
        else
        {
            int tradeAmountCached = _currentTradeAmount;
            float averageRate;
            int yieldGold = _market.CalculateTradeResult(_resourceType, ResourceType.Gold, tradeAmountCached, out averageRate);

            // Execute trade: Resource -> Gold
            if (_market.ExecuteTrade(_resourceType, ResourceType.Gold, tradeAmountCached))
            {
                Debug.Log($"[MarketResourceColumnUI] Successfully sold {tradeAmountCached} {_resourceType} for Gold.");
                _currentTradeAmount = 0;

                TriggerIconBounce();
                TriggerValuePulse();
                SpawnFloatingText($"-{tradeAmountCached} {_resourceType}", new Color(0.95f, 0.26f, 0.21f), _iconImage != null ? _iconImage.transform.position : transform.position);
                SpawnFloatingText($"+{yieldGold} Gold (Pending)", new Color(0.3f, 0.85f, 0.4f), _buySellButton != null ? _buySellButton.transform.position : transform.position);

                if (MarketUIController.Instance != null)
                {
                    MarketUIController.Instance.RefreshAll();
                }
            }
        }
    }

    private Coroutine _hoverRoutine;

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_resourceType == ResourceType.Gold) return;

        if (_hoverRoutine != null) StopCoroutine(_hoverRoutine);
        _hoverRoutine = StartCoroutine(AnimateScale(1.035f));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_resourceType == ResourceType.Gold) return;

        if (_hoverRoutine != null) StopCoroutine(_hoverRoutine);
        _hoverRoutine = StartCoroutine(AnimateScale(1.0f));
    }

    private System.Collections.IEnumerator AnimateScale(float targetScale)
    {
        float duration = 0.15f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 finalScale = new Vector3(targetScale, targetScale, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float tSmooth = t * t * (3f - 2f * t); // Smoothstep curve
            transform.localScale = Vector3.Lerp(startScale, finalScale, tSmooth);
            yield return null;
        }

        transform.localScale = finalScale;
        _hoverRoutine = null;
    }

    private Coroutine _bounceRoutine;
    private Coroutine _valuePulseRoutine;

    private void TriggerIconBounce()
    {
        if (_iconImage == null) return;
        if (_bounceRoutine != null) StopCoroutine(_bounceRoutine);
        _bounceRoutine = StartCoroutine(BounceRoutine(_iconImage.transform));
    }

    private System.Collections.IEnumerator BounceRoutine(Transform targetTransform)
    {
        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scaleOffset = Mathf.Sin(t * Mathf.PI * 2.5f) * Mathf.Exp(-5f * t) * 0.25f;
            targetTransform.localScale = originalScale + new Vector3(scaleOffset, scaleOffset, 0f);
            yield return null;
        }

        targetTransform.localScale = originalScale;
        _bounceRoutine = null;
    }

    private void TriggerValuePulse()
    {
        if (_valueText == null) return;
        if (_valuePulseRoutine != null) StopCoroutine(_valuePulseRoutine);
        _valuePulseRoutine = StartCoroutine(ValuePulseRoutine());
    }

    private System.Collections.IEnumerator ValuePulseRoutine()
    {
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 originalScale = Vector3.one;
        Color originalColor = _valueText.color;
        Color highlightColor = _isBuying ? new Color(0.95f, 0.26f, 0.21f) : new Color(0.3f, 0.85f, 0.4f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            float scaleOffset = Mathf.Sin(t * Mathf.PI) * 0.15f;
            _valueText.transform.localScale = originalScale + new Vector3(scaleOffset, scaleOffset, 0f);
            _valueText.color = Color.Lerp(highlightColor, originalColor, t);
            yield return null;
        }

        _valueText.transform.localScale = originalScale;
        _valueText.color = originalColor;
        _valuePulseRoutine = null;
    }

    private void SpawnFloatingText(string message, Color color, Vector3 startPos)
    {
        GameObject go = new GameObject("FloatingText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform.parent, false); // Parent to PanelMarket

        RectTransform rect = go.GetComponent<RectTransform>();
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();

        if (_valueText != null)
        {
            tmp.font = _valueText.font;
            tmp.fontSharedMaterial = _valueText.fontSharedMaterial;
        }
        
        tmp.text = message;
        tmp.color = color;
        tmp.fontSize = 15f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        rect.position = startPos;

        StartCoroutine(FloatTextRoutine(go, rect, tmp));
    }

    private System.Collections.IEnumerator FloatTextRoutine(GameObject go, RectTransform rect, TextMeshProUGUI tmp)
    {
        float duration = 1.0f;
        float elapsed = 0f;
        Vector3 startPos = rect.position;
        Color startColor = tmp.color;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / duration;
            rect.position = startPos + Vector3.up * Mathf.Lerp(0f, 50f, t);
            tmp.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
            yield return null;
        }

        Destroy(go);
    }

    private void OnDisable()
    {
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        if (_iconImage != null) _iconImage.transform.localScale = Vector3.one;
        if (_valueText != null) _valueText.transform.localScale = Vector3.one;

        if (_hoverRoutine != null)
        {
            StopCoroutine(_hoverRoutine);
            _hoverRoutine = null;
        }
        if (_flipRoutine != null)
        {
            StopCoroutine(_flipRoutine);
            _flipRoutine = null;
        }
        if (_bounceRoutine != null)
        {
            StopCoroutine(_bounceRoutine);
            _bounceRoutine = null;
        }
        if (_valuePulseRoutine != null)
        {
            StopCoroutine(_valuePulseRoutine);
            _valuePulseRoutine = null;
        }
    }
}
