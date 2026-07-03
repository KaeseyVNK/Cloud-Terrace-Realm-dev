using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Controller for the dedicated Main Building UGUI panel.
/// Automatically opens when the Main Building is selected and updates the weather/shelter controls.
/// </summary>
public class MainBuildingUIController : MonoBehaviour
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static MainBuildingUIController Instance { get; private set; }

    #region Serialized Fields

    [Header("Panel Root")]
    [SerializeField] private GameObject _panelRoot;

    [Header("Header")]
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private Button   _rallyPointButton;

    [Header("Unit List")]
    [SerializeField] private Transform    _unitCardsContainer;
    [SerializeField] private UnitCardUI   _unitCardPrefab;

    [Header("Queue Area")]
    [SerializeField] private Slider       _progressBar;
    [SerializeField] private TMP_Text     _queueLabel;
    [SerializeField] private Transform    _queueSlotsParent;

    [Header("Close Button")]
    [SerializeField] private Button _closeButton;

    [Header("Main Building Controls")]
    [SerializeField] private TMP_Text _weatherText;
    [SerializeField] private TMP_Text _shelterStatusText;
    [SerializeField] private Toggle _autoShelterToggle;
    [SerializeField] private Button _shelterAllButton;
    [SerializeField] private Button _evacuateAllButton;

    #endregion

    #region Private Fields

    private BuildingProduction _currentProduction;
    private readonly List<UnitCardUI> _spawnedCards = new List<UnitCardUI>();
    private CanvasGroup _canvasGroup;
    private GameObject _slotTemplate;
    private readonly List<GameObject> _activeSlots = new List<GameObject>();
    private UnitCardUI _unitCardTemplate;
    private Coroutine _fadeCoroutine;
    private BuildingProduction _productionOverride;

    #endregion

    #region Unity Lifecycle

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
        // Cache the unit card template and hide it from Content if it resides in the scene
        if (_unitCardPrefab != null)
        {
            _unitCardTemplate = _unitCardPrefab;
            bool isPrefabAsset = string.IsNullOrEmpty(_unitCardPrefab.gameObject.scene.name);
            if (!isPrefabAsset)
            {
                _unitCardTemplate.gameObject.SetActive(false);
                // Reparent out of Content so ClearUnitCards won't destroy it
                _unitCardTemplate.transform.SetParent(transform, false);
            }
        }

        // Cache the queue slot template and disable it
        if (_queueSlotsParent != null && _queueSlotsParent.childCount > 0)
        {
            _slotTemplate = _queueSlotsParent.GetChild(0).gameObject;
            _slotTemplate.SetActive(false);

            for (int i = _queueSlotsParent.childCount - 1; i >= 1; i--)
            {
                var child = _queueSlotsParent.GetChild(i).gameObject;
                if (child != null)
                {
                    child.SetActive(false);
                    Destroy(child);
                }
            }
        }

        if (_rallyPointButton != null)
            _rallyPointButton.onClick.AddListener(OnRallyPointButtonClicked);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(OnCloseButtonClicked);

        if (_autoShelterToggle != null)
        {
            _autoShelterToggle.isOn = VillagerController.ShouldShelterAtNight;
            _autoShelterToggle.onValueChanged.AddListener((val) => {
                VillagerController.ShouldShelterAtNight = val;
            });
        }

        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged += OnResourceChanged;

        if (TechnologyManager.Instance != null)
            TechnologyManager.Instance.OnTechnologyUnlocked += OnTechnologyUnlocked;
    }

    private void OnDestroy()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceChanged -= OnResourceChanged;

        if (TechnologyManager.HasInstance)
            TechnologyManager.Instance.OnTechnologyUnlocked -= OnTechnologyUnlocked;

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        BuildingProduction activeProduction = _productionOverride;
        if (activeProduction == null && MainBuildingUI.Instance != null && MainBuildingUI.Instance.SelectedMainBuilding != null)
        {
            activeProduction = MainBuildingUI.Instance.SelectedMainBuilding.GetComponent<BuildingProduction>() ?? 
                               MainBuildingUI.Instance.SelectedMainBuilding.GetComponentInChildren<BuildingProduction>();
        }

        if (activeProduction != _currentProduction)
        {
            SetProduction(activeProduction);
        }

        if (_currentProduction != null)
        {
            if (_progressBar != null)
                UpdateProgressBar();
            UpdateQueueSlots();
            UpdateMainBuildingControls();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Binds the Main Building production data to this UI panel.
    /// </summary>
    /// <param name="production">The Main Building's BuildingProduction component.</param>
    public void SetProduction(BuildingProduction production)
    {
        if (_currentProduction == production)
        {
            return;
        }

        _currentProduction = production;

        if (_currentProduction != null)
            OpenPanel();
        else
            ClosePanel();
    }

    public void SetProductionOverride(BuildingProduction production)
    {
        _productionOverride = production;
        SetProduction(production);
    }

    public void ClearProductionOverride()
    {
        _productionOverride = null;
    }

    #endregion

    #region Private Methods

    private void OpenPanel()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(true, 0.2f));

        if (_titleText != null && _currentProduction.BuildingData != null)
            _titleText.text = _currentProduction.BuildingData.buildingName;

        PopulateUnitCards();
        UpdateProgressBar();
        UpdateQueueSlots();

        // Bind emergency control listeners
        MainBuildingCombatTarget mainBuilding = _currentProduction.GetComponent<MainBuildingCombatTarget>();
        if (mainBuilding == null)
        {
            mainBuilding = _currentProduction.GetComponentInChildren<MainBuildingCombatTarget>();
        }

        if (mainBuilding != null)
        {
            if (_shelterAllButton != null)
            {
                _shelterAllButton.onClick.RemoveAllListeners();
                _shelterAllButton.onClick.AddListener(() => mainBuilding.OrderAllVillagersToShelter());
            }

            if (_evacuateAllButton != null)
            {
                _evacuateAllButton.onClick.RemoveAllListeners();
                _evacuateAllButton.onClick.AddListener(() => mainBuilding.OrderAllVillagersToEvacuate());
            }
        }
    }

    private void ClosePanel()
    {
        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeRoutine(false, 0.15f));

        ClearUnitCards();
        ClearQueueSlots();
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

    private void PopulateUnitCards()
    {
        ClearUnitCards();

        if (_currentProduction?.BuildingData?.producibleUnits == null) return;

        UnitCardUI templateToUse = _unitCardTemplate != null ? _unitCardTemplate : _unitCardPrefab;
        if (templateToUse == null || _unitCardsContainer == null) return;

        foreach (var unitData in _currentProduction.BuildingData.producibleUnits)
        {
            if (unitData == null) continue;

            if (CardManager.Instance != null && !CardManager.Instance.IsUnitDiscovered(unitData))
                continue;

            UnitCardUI card = Instantiate(templateToUse, _unitCardsContainer, false);
            card.gameObject.SetActive(true);
            UnitData capturedUnit = unitData;
            card.Setup(unitData, () => _currentProduction?.RequestProduceUnit(capturedUnit));
            _spawnedCards.Add(card);
        }
    }

    private void ClearUnitCards()
    {
        foreach (var card in _spawnedCards)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        _spawnedCards.Clear();

        if (_unitCardsContainer != null)
        {
            foreach (Transform child in _unitCardsContainer)
            {
                if (child != null)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }
    }

    private void ClearQueueSlots()
    {
        foreach (var slot in _activeSlots)
        {
            if (slot != null)
                Destroy(slot);
        }
        _activeSlots.Clear();
    }

    private void UpdateProgressBar()
    {
        if (_progressBar == null || _currentProduction == null) return;

        bool isProducing = _currentProduction.CurrentProducingUnit != null;
        _progressBar.gameObject.SetActive(isProducing);

        if (isProducing)
        {
            float totalTime = _currentProduction.CurrentProducingUnit.productionTime;
            float remaining = _currentProduction.CurrentProductionTimer;
            _progressBar.value = totalTime > 0f ? 1f - (remaining / totalTime) : 0f;

            if (_queueLabel != null)
                _queueLabel.text = $"Queue: {_currentProduction.CurrentProducingUnit.unitName} ({(_progressBar.value * 100f):F0}%)";
        }
        else
        {
            if (_queueLabel != null)
                _queueLabel.text = "Queue";
        }
    }

    private void UpdateQueueSlots()
    {
        if (_queueSlotsParent == null || _currentProduction == null || _slotTemplate == null) return;

        var queueList = new List<UnitData>(_currentProduction.ProductionQueue);

        while (_activeSlots.Count < queueList.Count)
        {
            GameObject newSlot = Instantiate(_slotTemplate, _queueSlotsParent);
            newSlot.SetActive(true);
            _activeSlots.Add(newSlot);
        }

        while (_activeSlots.Count > queueList.Count)
        {
            GameObject slotToDestroy = _activeSlots[_activeSlots.Count - 1];
            _activeSlots.RemoveAt(_activeSlots.Count - 1);
            if (slotToDestroy != null)
                Destroy(slotToDestroy);
        }

        for (int i = 0; i < queueList.Count; i++)
        {
            var unit = queueList[i];
            var slotGO = _activeSlots[i];
            if (slotGO == null) continue;

            if (slotGO.transform.childCount > 0)
            {
                var iconChild = slotGO.transform.GetChild(0);
                var img = iconChild.GetComponent<Image>();
                if (img != null)
                {
                    if (unit != null && unit.portraitIcon != null)
                    {
                        img.sprite = unit.portraitIcon;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = new Color(1, 1, 1, 0);
                    }
                }
            }

            var btn = slotGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                int capturedIndex = i;
                btn.onClick.AddListener(() => CancelQueueItem(capturedIndex));
            }
        }
    }

    private void CancelQueueItem(int index)
    {
        if (_currentProduction != null)
        {
            bool success = _currentProduction.CancelQueueItem(index);
            if (success)
            {
                UpdateQueueSlots();
                RefreshAllCardStates();
            }
        }
    }

    private void RefreshAllCardStates()
    {
        foreach (var card in _spawnedCards)
            card?.RefreshState();
    }

    private void OnResourceChanged(ResourceType type, int amount)
    {
        RefreshAllCardStates();
    }

    private void OnTechnologyUnlocked(TechnologyData tech)
    {
        RefreshAllCardStates();
    }

    private void OnRallyPointButtonClicked()
    {
        if (TestProductionUI.Instance != null)
        {
            TestProductionUI.Instance.ToggleRallyTargeting(_currentProduction);
        }
    }

    private void OnCloseButtonClicked()
    {
        TestProductionUI.Instance?.DeselectProduction();
    }

    private void UpdateMainBuildingControls()
    {
        if (_currentProduction == null) return;

        // 1. Update weather info
        if (_weatherText != null)
        {
            string weatherTextStr = "Weather: ";
            if (WeatherManager.Instance != null)
            {
                weatherTextStr += WeatherManager.Instance.CurrentWeather.ToString();
            }
            else
            {
                weatherTextStr += "Clear";
            }
            if (TimeManager.Instance != null && TimeManager.Instance.IsNight)
            {
                weatherTextStr += " (Night)";
            }
            else
            {
                weatherTextStr += " (Day)";
            }
            _weatherText.text = weatherTextStr;
        }

        // 2. Update shelter status
        if (_shelterStatusText != null)
        {
            string stateText = HouseShelter.IsEmergencyShelterActive ? "STATUS: EMERGENCY SHELTER ACTIVE" : "STATUS: Normal";
            _shelterStatusText.text = stateText;
            _shelterStatusText.color = HouseShelter.IsEmergencyShelterActive ? Color.red : Color.yellow;
        }

        // 3. Sync auto shelter checkbox value
        if (_autoShelterToggle != null)
        {
            _autoShelterToggle.SetIsOnWithoutNotify(VillagerController.ShouldShelterAtNight);
        }
    }

    #endregion
}
