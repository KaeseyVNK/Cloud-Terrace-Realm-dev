using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class BuildingMenuUI : MonoBehaviour
{
    [Header("Card Generation")]
    [SerializeField] private BuildingCardUI _cardPrefab;
    [SerializeField] private Transform _cardsContainer;

    [Header("Category Tabs")]
    [SerializeField]
    private List<BuildingCategoryTab> _categoryTabs =
        new List<BuildingCategoryTab>();

    [Header("Header")]
    [SerializeField] private TMP_Text _headerTitleText;

    [Header("Starting Category")]
    [SerializeField]
    private BuildingCategory _startingCategory =
        BuildingCategory.Residential;

    private BuildingCategory _currentCategory;

    public BuildingCategory CurrentCategory => _currentCategory;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Coroutine _transitionCoroutine;

    private void EnsureComponents()
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
        }
        if (_canvasGroup == null)
        {
            if (!TryGetComponent(out _canvasGroup))
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Awake()
    {
        EnsureComponents();
    }

    private void Start()
    {
        BuildingManager manager = BuildingManager.Instance;
        bool shouldBeVisible = manager != null && manager.IsBuildMode;

        EnsureComponents();
        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = new Vector2(shouldBeVisible ? 0f : -700f, _rectTransform.anchoredPosition.y);
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = shouldBeVisible ? 1f : 0f;
            _canvasGroup.interactable = shouldBeVisible;
            _canvasGroup.blocksRaycasts = shouldBeVisible;
        }

        if (!shouldBeVisible)
        {
            gameObject.SetActive(false);
        }

        // Call ShowCategory after potential deactivation so that pop-in scaling default can fall back to 1.0 (since activeInHierarchy will be false)
        ShowCategory(_startingCategory);
    }

    public void ShowCategory(BuildingCategory category)
    {
        _currentCategory = category;

        ClearCurrentCards();

        BuildingManager manager = BuildingManager.Instance;

        if (manager == null)
        {
            Debug.LogError(
                "[BuildingMenuUI] Không tìm thấy BuildingManager.Instance.",
                this
            );
            return;
        }

        if (_cardPrefab == null)
        {
            Debug.LogError(
                "[BuildingMenuUI] Card Prefab chưa được gán.",
                this
            );
            return;
        }

        if (_cardsContainer == null)
        {
            Debug.LogError(
                "[BuildingMenuUI] Cards Container chưa được gán.",
                this
            );
            return;
        }

        int displayedCardCount = 0;

        foreach (BuildingData buildingData in manager.AvailableBuildings)
        {
            if (buildingData == null)
            {
                continue;
            }

            if (buildingData == manager.MainBuildingData)
            {
                continue;
            }

            if (buildingData.category != category)
            {
                continue;
            }

            BuildingCardUI newCard =
                Instantiate(_cardPrefab, _cardsContainer, false);

            newCard.Setup(buildingData);
            newCard.AnimatePopIn(displayedCardCount * 0.05f);
            displayedCardCount++;
        }

        UpdateCategoryTabVisuals();
        UpdateHeaderTitle();

        Debug.Log(
            $"[BuildingMenuUI] Category {category}: " +
            $"hiển thị {displayedCardCount} công trình."
        );
    }

    private void ClearCurrentCards()
    {
        if (_cardsContainer == null)
        {
            return;
        }

        for (int i = _cardsContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(_cardsContainer.GetChild(i).gameObject);
        }
    }

    private void UpdateCategoryTabVisuals()
    {
        foreach (BuildingCategoryTab tab in _categoryTabs)
        {
            if (tab == null)
            {
                continue;
            }

            tab.SetSelected(tab.Category == _currentCategory);
        }
    }

    private void UpdateHeaderTitle()
    {
        if (_headerTitleText == null)
        {
            return;
        }

        _headerTitleText.text = _currentCategory switch
        {
            BuildingCategory.Residential => "Residential Buildings",
            BuildingCategory.Production => "Production Buildings",
            BuildingCategory.Goods => "Goods Buildings",
            BuildingCategory.Cultural => "Cultural Buildings",
            BuildingCategory.Hospital => "Hospital",
            BuildingCategory.Military => "Military Buildings",
            BuildingCategory.Roads => "Roads",
            BuildingCategory.Expansion => "Expansion",
            _ => _currentCategory.ToString()
        };
    }

    public void SetMenuVisible(bool visible)
    {
        EnsureComponents();

        if (visible)
        {
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = new Vector2(-700f, _rectTransform.anchoredPosition.y);
                }
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                }
            }

            // Staggered pop-in for all cards when opening the menu
            if (_cardsContainer != null)
            {
                int delayIndex = 0;
                for (int i = 0; i < _cardsContainer.childCount; i++)
                {
                    BuildingCardUI card = _cardsContainer.GetChild(i).GetComponent<BuildingCardUI>();
                    if (card != null)
                    {
                        card.AnimatePopIn(delayIndex * 0.05f);
                        delayIndex++;
                    }
                }
            }

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(TransitionMenuRoutine(true));
        }
        else
        {
            if (!gameObject.activeInHierarchy)
            {
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = new Vector2(-700f, _rectTransform.anchoredPosition.y);
                }
                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = 0f;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }
                return;
            }

            if (_transitionCoroutine != null)
            {
                StopCoroutine(_transitionCoroutine);
            }
            _transitionCoroutine = StartCoroutine(TransitionMenuRoutine(false));
        }
    }

    private System.Collections.IEnumerator TransitionMenuRoutine(bool visible)
    {
        if (visible)
        {
            gameObject.SetActive(true);
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

        float startX = _rectTransform != null ? _rectTransform.anchoredPosition.x : (visible ? -700f : 0f);
        float targetX = visible ? 0f : -700f;
        float startAlpha = _canvasGroup != null ? _canvasGroup.alpha : (visible ? 0f : 1f);
        float targetAlpha = visible ? 1f : 0f;

        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float curveT;
            if (visible)
            {
                // Back Ease Out
                float tMinus1 = t - 1f;
                float s = 1.70158f;
                curveT = tMinus1 * tMinus1 * ((s + 1f) * tMinus1 + s) + 1f;
            }
            else
            {
                // Cubic Ease In-Out
                curveT = t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
            }

            float curX = Mathf.LerpUnclamped(startX, targetX, curveT);
            float curAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = new Vector2(curX, _rectTransform.anchoredPosition.y);
            }
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = curAlpha;
            }

            yield return null;
        }

        if (_rectTransform != null)
        {
            _rectTransform.anchoredPosition = new Vector2(targetX, _rectTransform.anchoredPosition.y);
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = targetAlpha;
        }

        if (!visible)
        {
            gameObject.SetActive(false);
        }

        _transitionCoroutine = null;
    }
}