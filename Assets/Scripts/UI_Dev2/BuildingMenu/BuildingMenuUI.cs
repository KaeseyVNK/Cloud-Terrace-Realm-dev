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

    private void Start()
    {
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
                Instantiate(_cardPrefab, _cardsContainer);

            newCard.Setup(buildingData);
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
}