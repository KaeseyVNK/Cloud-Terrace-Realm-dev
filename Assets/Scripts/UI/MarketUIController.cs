using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for the UGUI Trade Market UI.
/// Manages columns for Wood, Stone, Food, and Gold and handles menu toggle states.
/// </summary>
public class MarketUIController : MonoBehaviour
{
    public static MarketUIController Instance { get; private set; }

    [Header("UI Panel Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Resource Columns")]
    [SerializeField] private MarketResourceColumnUI _woodColumn;
    [SerializeField] private MarketResourceColumnUI _stoneColumn;
    [SerializeField] private MarketResourceColumnUI _foodColumn;
    [SerializeField] private MarketResourceColumnUI _goldColumn;

    [Header("Footer References")]
    [SerializeField] private Button _closeButton;

    [Header("Mercenary UI")]
    [SerializeField] private Button _hireMercenaryButton;
    [SerializeField] private TMP_Text _mercenaryStatusText;

    private MarketController _currentMarket;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(CloseMenu);
        }

        if (_hireMercenaryButton != null)
        {
            _hireMercenaryButton.onClick.AddListener(OnHireMercenaryClicked);
        }

        SetPanelActive(false);
    }

    private void Start()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        }
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Open the trade menu for a specific neutral market.
    /// </summary>
    /// <param name="market">The selected market.</param>
    public void OpenMenu(MarketController market)
    {
        _currentMarket = market;
        SetPanelActive(true);

        if (_woodColumn != null) _woodColumn.Setup(market);
        if (_stoneColumn != null) _stoneColumn.Setup(market);
        if (_foodColumn != null) _foodColumn.Setup(market);
        if (_goldColumn != null) _goldColumn.Setup(market);

        UpdateMercenaryUI();
    }

    /// <summary>
    /// Close the trade menu and notify the selection system.
    /// </summary>
    public void CloseMenu()
    {
        _currentMarket = null;
        SetPanelActive(false);

        MarketUI marketUI = FindAnyObjectByType<MarketUI>();
        if (marketUI != null && marketUI.SelectedMarket != null)
        {
            marketUI.DeselectMarket();
        }
    }

    /// <summary>
    /// Refresh all columns manually.
    /// </summary>
    public void RefreshAll()
    {
        if (_woodColumn != null) _woodColumn.RefreshUI();
        if (_stoneColumn != null) _stoneColumn.RefreshUI();
        if (_foodColumn != null) _foodColumn.RefreshUI();
        if (_goldColumn != null) _goldColumn.RefreshUI();

        UpdateMercenaryUI();
    }

    private void OnHireMercenaryClicked()
    {
        if (_currentMarket != null)
        {
            if (_currentMarket.TryHireMercenary())
            {
                UpdateMercenaryUI();
            }
        }
    }

    private void UpdateMercenaryUI()
    {
        if (_currentMarket == null) return;

        if (_mercenaryStatusText != null)
        {
            if (_currentMarket.isNeutral)
            {
                _mercenaryStatusText.text = $"Vệ sĩ: {_currentMarket.HiredGuardCount}/{_currentMarket.MaxMercenaries} ({_currentMarket.MercenaryCost} Vàng)";
            }
            else
            {
                _mercenaryStatusText.text = "";
            }
        }

        if (_hireMercenaryButton != null)
        {
            _hireMercenaryButton.gameObject.SetActive(_currentMarket.isNeutral);
            bool hasSpace = _currentMarket.HiredGuardCount < _currentMarket.MaxMercenaries;
            bool canAfford = ResourceManager.Instance != null && ResourceManager.Instance.GetResourceAmount(ResourceType.Gold) >= _currentMarket.MercenaryCost;
            _hireMercenaryButton.interactable = hasSpace && canAfford;
        }
    }

    private Coroutine _activeTransition;

    private void SetPanelActive(bool active)
    {
        if (_panelRoot == null) return;

        if (active)
        {
            _panelRoot.SetActive(true);
        }

        if (_activeTransition != null)
        {
            StopCoroutine(_activeTransition);
        }

        _activeTransition = StartCoroutine(TransitionPanelRoutine(active));
    }

    private System.Collections.IEnumerator TransitionPanelRoutine(bool active)
    {
        float duration = 0.22f;
        float elapsed = 0f;

        var rectTransform = _panelRoot.GetComponent<RectTransform>();
        
        if (active)
        {
            _panelRoot.SetActive(true);
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
            
            Vector3 targetScale = Vector3.one;
            Vector3 startScale = new Vector3(0.85f, 0.85f, 0.85f);
            
            if (rectTransform != null) rectTransform.localScale = startScale;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime; // Use unscaledDeltaTime to support paused state
                float t = elapsed / duration;
                
                // Smooth ease out curve (Sine)
                float tCurve = Mathf.Sin(t * Mathf.PI * 0.5f);
                
                if (rectTransform != null) rectTransform.localScale = Vector3.Lerp(startScale, targetScale, tCurve);
                if (_canvasGroup != null) _canvasGroup.alpha = tCurve;
                
                yield return null;
            }

            if (rectTransform != null) rectTransform.localScale = targetScale;
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }
        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }

            Vector3 startScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
            Vector3 targetScale = new Vector3(0.85f, 0.85f, 0.85f);
            float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                
                // Ease in curve
                float tCurve = t * t;
                
                if (rectTransform != null) rectTransform.localScale = Vector3.Lerp(startScale, targetScale, tCurve);
                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, tCurve);
                
                yield return null;
            }

            if (rectTransform != null) rectTransform.localScale = targetScale;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
            
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
        }

        _activeTransition = null;
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        RefreshAll();
    }
}
