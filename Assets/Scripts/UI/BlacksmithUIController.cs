using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for the blacksmith research card interface. 
/// Automatically detects when a Blacksmith is selected, populates technology cards,
/// and lays them out in a fanned arc.
/// </summary>
public class BlacksmithUIController : MonoBehaviour
{
    public static BlacksmithUIController Instance { get; private set; }

    [Header("UI Panel Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Card Generator")]
    [SerializeField] private BlacksmithCardUI _cardPrefab;
    [SerializeField] private RectTransform _cardsContainer;

    [Header("Fan Layout Settings")]
    [SerializeField] private float _fanRadius = 1100f;
    [SerializeField] private float _maxSpanAngle = 20f;
    [SerializeField] private float _maxAnglePerCard = 6f;

    private BlacksmithResearch _currentResearchSystem;
    private readonly List<BlacksmithCardUI> _spawnedCards = new List<BlacksmithCardUI>();

    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;

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

        if (_panelRoot != null)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
            _panelRoot.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }
    }

    private void Start()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += OnResourceChanged;
        }
        if (TechnologyManager.Instance != null)
        {
            TechnologyManager.Instance.OnTechnologyUnlocked += OnTechnologyUnlocked;
        }
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;
        }
        if (TechnologyManager.HasInstance)
        {
            TechnologyManager.Instance.OnTechnologyUnlocked -= OnTechnologyUnlocked;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        // Automatically check if a blacksmith research system is selected
        BlacksmithResearch activeResearch = null;
        if (TestProductionUI.Instance != null)
        {
            activeResearch = TestProductionUI.Instance.SelectedResearch;
        }

        if (activeResearch != _currentResearchSystem)
        {
            SetResearchSystem(activeResearch);
        }
    }

    private void SetResearchSystem(BlacksmithResearch research)
    {
        _currentResearchSystem = research;

        if (_currentResearchSystem != null)
        {
            OpenMenu();
        }
        else
        {
            CloseMenuInternal();
        }
    }

    private void OpenMenu()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(true, 0.2f));
        PopulateCards();
    }

    private void CloseMenuInternal()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(false, 0.15f));
        ClearCards();
    }

    private System.Collections.IEnumerator FadeRoutine(bool show, float duration)
    {
        if (_panelRoot == null) yield break;

        if (show)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(true);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = true;
                _canvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = false;
                _canvasGroup.blocksRaycasts = false;
            }
        }

        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : (show ? 0f : 1f);
        float targetAlpha = show ? 1f : 0f;

        Vector3 startScale = show ? new Vector3(0.92f, 0.92f, 1f) : Vector3.one;
        Vector3 targetScale = show ? Vector3.one : new Vector3(0.95f, 0.95f, 1f);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tSmooth = t * t * (3f - 2f * t);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, tSmooth);
            }

            _panelRoot.transform.localScale = Vector3.Lerp(startScale, targetScale, tSmooth);
            yield return null;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = targetAlpha;
        }
        _panelRoot.transform.localScale = targetScale;

        if (!show)
        {
            if (_panelRoot != gameObject)
            {
                _panelRoot.SetActive(false);
            }
        }

        _fadeCoroutine = null;
    }

    private void PopulateCards()
    {
        ClearCards();

        if (_currentResearchSystem == null || _cardPrefab == null || _cardsContainer == null) return;

        var techs = _currentResearchSystem.AvailableTechnologies;
        if (techs == null || techs.Count == 0) return;

        for (int i = 0; i < techs.Count; i++)
        {
            var tech = techs[i];
            if (tech == null) continue;

            BlacksmithCardUI newCard = Instantiate(_cardPrefab, _cardsContainer, false);
            newCard.Setup(tech, _currentResearchSystem);
            _spawnedCards.Add(newCard);
        }

        LayoutCards();
    }

    private void LayoutCards()
    {
        int count = _spawnedCards.Count;
        if (count == 0) return;

        float maxAngle = _maxSpanAngle;
        
        // Restrict angle span if there are only a few cards to prevent extreme fanning
        float angleSpacing = count > 1 ? (maxAngle * 2f) / (count - 1) : 0f;
        if (count > 1 && angleSpacing > _maxAnglePerCard)
        {
            float actualSpan = _maxAnglePerCard * (count - 1);
            maxAngle = actualSpan / 2f;
        }

        for (int i = 0; i < count; i++)
        {
            float angle = 0f;
            if (count > 1)
            {
                angle = Mathf.Lerp(-maxAngle, maxAngle, (float)i / (count - 1));
            }

            float rad = angle * Mathf.Deg2Rad;

            // Compute fanned circular position offsets
            float x = _fanRadius * Mathf.Sin(rad);
            float y = _fanRadius * Mathf.Cos(rad) - _fanRadius;

            Vector3 fannedPos = new Vector3(x, y, 0f);
            Quaternion fannedRot = Quaternion.Euler(0f, 0f, -angle);

            _spawnedCards[i].SetLayoutDefaults(fannedPos, fannedRot, i);
        }
    }

    private void ClearCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
            {
                Destroy(card.gameObject);
            }
        }
        _spawnedCards.Clear();
    }

    private void UpdateCardStatuses()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
            {
                card.UpdateStatus();
            }
        }
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        UpdateCardStatuses();
    }

    private void OnTechnologyUnlocked(TechnologyData tech)
    {
        UpdateCardStatuses();
    }
}
