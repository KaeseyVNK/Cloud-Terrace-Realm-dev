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

        SetPanelActive(false);
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

    private void SetPanelActive(bool active)
    {
        if (_panelRoot == null) return;

        if (_panelRoot != gameObject)
        {
            _panelRoot.SetActive(active);
        }
        else
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = active ? 1f : 0f;
                _canvasGroup.interactable = active;
                _canvasGroup.blocksRaycasts = active;
            }
        }
    }

    private void OpenMenu()
    {
        SetPanelActive(true);
        PopulateCards();
    }

    private void CloseMenuInternal()
    {
        SetPanelActive(false);
        ClearCards();
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
