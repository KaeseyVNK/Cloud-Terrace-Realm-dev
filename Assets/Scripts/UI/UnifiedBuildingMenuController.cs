using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Coordinates the unified bottom building menu.
    /// Connects the Main Building and Barracks UIs as tabs.
    /// </summary>
    public class UnifiedBuildingMenuController : MonoBehaviour
    {
        public static UnifiedBuildingMenuController Instance { get; private set; }

        [Header("Tab Buttons")]
        [SerializeField] private Button _mainBuildingTabButton;
        [SerializeField] private Button _barracksTabButton;
        [SerializeField] private Button _storageTabButton;
        [SerializeField] private Button _upgradesTabButton;
        [SerializeField] private Button _managementTabButton;

        [Header("Tab Panels")]
        [SerializeField] private GameObject _mainBuildingPanel;
        [SerializeField] private GameObject _barracksPanel;
        [SerializeField] private GameObject _storagePanel;
        [SerializeField] private GameObject _upgradesPanel;
        [SerializeField] private GameObject _managementPanel;

        [Header("Close Button")]
        [SerializeField] private Button _menuCloseButton;

        [Header("Unified Rally Point Button")]
        [SerializeField] private Button _unifiedRallyPointButton;

        [Header("Storage Panel Bindings")]
        [SerializeField] private TMPro.TMP_Text _woodStorageText;
        [SerializeField] private TMPro.TMP_Text _stoneStorageText;
        [SerializeField] private TMPro.TMP_Text _foodStorageText;
        [SerializeField] private TMPro.TMP_Text _goldStorageText;
        [SerializeField] private TMPro.TMP_Text _storageUsedText;
        [SerializeField] private TMPro.TMP_Text _storageFreeText;
        [SerializeField] private TMPro.TMP_Text _storageUsageText;
        [SerializeField] private Image _capacityBarFill;

        private CanvasGroup _canvasGroup;
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<GameObject> _tabPanels = new List<GameObject>();
        private readonly List<CanvasGroup> _tabCanvasGroups = new List<CanvasGroup>();
        private int _currentTabIndex = -1;
        private bool _isMainBuildingTabSupported = true;
        private bool _isBarracksTabSupported = true;
        private bool _isStorageTabSupported = true;
        private bool _isUpgradesTabSupported = true;
        private bool _isManagementTabSupported = true;

        private void Awake()
        {
            Instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // Hide initially
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        private void Start()
        {
            _isMainBuildingTabSupported = _mainBuildingTabButton != null && _mainBuildingTabButton.gameObject.activeSelf;
            _isBarracksTabSupported = _barracksTabButton != null && _barracksTabButton.gameObject.activeSelf;
            _isStorageTabSupported = _storageTabButton != null && _storageTabButton.gameObject.activeSelf;
            _isUpgradesTabSupported = _upgradesTabButton != null && _upgradesTabButton.gameObject.activeSelf;
            _isManagementTabSupported = _managementTabButton != null && _managementTabButton.gameObject.activeSelf;

            // Register tabs
            if (_mainBuildingTabButton != null) _tabButtons.Add(_mainBuildingTabButton);
            if (_barracksTabButton != null) _tabButtons.Add(_barracksTabButton);
            if (_storageTabButton != null) _tabButtons.Add(_storageTabButton);
            if (_upgradesTabButton != null) _tabButtons.Add(_upgradesTabButton);
            if (_managementTabButton != null) _tabButtons.Add(_managementTabButton);

            if (_mainBuildingPanel != null) _tabPanels.Add(_mainBuildingPanel);
            if (_barracksPanel != null) _tabPanels.Add(_barracksPanel);
            if (_storagePanel != null) _tabPanels.Add(_storagePanel);
            if (_upgradesPanel != null) _tabPanels.Add(_upgradesPanel);
            if (_managementPanel != null) _tabPanels.Add(_managementPanel);

            // Set up CanvasGroup for each panel and ensure they remain active
            foreach (var panel in _tabPanels)
            {
                if (panel != null)
                {
                    panel.SetActive(true);
                    var cg = panel.GetComponent<CanvasGroup>();
                    if (cg == null)
                    {
                        cg = panel.AddComponent<CanvasGroup>();
                    }
                    _tabCanvasGroups.Add(cg);
                }
                else
                {
                    _tabCanvasGroups.Add(null);
                }
            }

            // Bind click events
            if (_mainBuildingTabButton != null) _mainBuildingTabButton.onClick.AddListener(() => SwitchToTab(0));
            if (_barracksTabButton != null) _barracksTabButton.onClick.AddListener(() => SwitchToTab(1));
            if (_storageTabButton != null) _storageTabButton.onClick.AddListener(() => SwitchToTab(2));
            if (_upgradesTabButton != null) _upgradesTabButton.onClick.AddListener(() => SwitchToTab(3));
            if (_managementTabButton != null) _managementTabButton.onClick.AddListener(() => SwitchToTab(4));

            if (_menuCloseButton != null)
            {
                _menuCloseButton.onClick.AddListener(CloseMenu);
            }

            if (_unifiedRallyPointButton != null)
            {
                _unifiedRallyPointButton.onClick.AddListener(OnUnifiedRallyPointClicked);
            }

            // Default to Main Building tab
            SwitchToTab(0);
        }

        private void Update()
        {
            // Show menu if either Main Building or Barracks has active selection
            bool hasMainBuildingSelection = MainBuildingUI.Instance != null && MainBuildingUI.Instance.SelectedMainBuilding != null;
            bool hasBarracksSelection = TestProductionUI.Instance != null && TestProductionUI.Instance.SelectedProduction != null;
            BuildingProduction selectedBarracksProduction = hasBarracksSelection ? TestProductionUI.Instance.SelectedProduction : null;

            // Avoid opening barracks tab if it is the main building selected inside TestProductionUI
            if (hasBarracksSelection)
            {
                bool isMain = selectedBarracksProduction.GetComponent<MainBuildingCombatTarget>() != null || selectedBarracksProduction.GetComponentInChildren<MainBuildingCombatTarget>() != null;
                if (isMain)
                {
                    hasBarracksSelection = false;
                    selectedBarracksProduction = null;
                }
            }

            bool hasSelection = hasMainBuildingSelection || hasBarracksSelection;

            float targetAlpha = hasSelection ? 1f : 0f;
            if (Mathf.Abs(_canvasGroup.alpha - targetAlpha) > 0.01f)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, Time.unscaledDeltaTime * 6f);
                _canvasGroup.interactable = hasSelection;
                _canvasGroup.blocksRaycasts = hasSelection;
            }

            // Auto switch tabs based on selection
            if (hasSelection)
            {
                SetAllTabButtonsVisible(true);

                if (_currentTabIndex < 0 || _currentTabIndex >= _tabCanvasGroups.Count)
                {
                    SwitchToTab(hasBarracksSelection && !hasMainBuildingSelection ? 1 : 0);
                }

                SyncActiveTabProductionSources(selectedBarracksProduction);

                // Update Storage tab data if currently visible
                if (_storagePanel != null && _storagePanel.activeSelf && ResourceManager.Instance != null)
                {
                    UpdateStorageData();
                }
            }
            else
            {
                // Reset tab index when nothing is selected
                _currentTabIndex = -1;
                if (ProductionUIController.Instance != null)
                {
                    ProductionUIController.Instance.SetProductionOverride(null);
                    ProductionUIController.Instance.ClearProductionOverride();
                }
                if (MainBuildingUIController.Instance != null)
                {
                    MainBuildingUIController.Instance.SetProductionOverride(null);
                    MainBuildingUIController.Instance.ClearProductionOverride();
                }
            }
        }

        private void UpdateStorageData()
        {
            if (_woodStorageText != null) _woodStorageText.text = ResourceManager.Instance.GetResourceAmount(ResourceType.Wood).ToString();
            if (_stoneStorageText != null) _stoneStorageText.text = ResourceManager.Instance.GetResourceAmount(ResourceType.Stone).ToString();
            if (_foodStorageText != null) _foodStorageText.text = ResourceManager.Instance.GetResourceAmount(ResourceType.Food).ToString();
            if (_goldStorageText != null) _goldStorageText.text = ResourceManager.Instance.GetResourceAmount(ResourceType.Gold).ToString();

            int total = ResourceManager.Instance.GetTotalPrimaryResources();
            int max = ResourceManager.Instance.GetMaxResourceCapacity();

            if (_storageUsedText != null) _storageUsedText.text = $"Used: {total}";
            if (_storageFreeText != null) _storageFreeText.text = $"Free: {max - total}";
            if (_storageUsageText != null) _storageUsageText.text = $"{((float)total / Mathf.Max(1, max) * 100f):F0}%";
            if (_capacityBarFill != null) _capacityBarFill.fillAmount = (float)total / Mathf.Max(1, max);
        }

        public void SwitchToTab(int index)
        {
            _currentTabIndex = index;

            for (int i = 0; i < _tabCanvasGroups.Count; i++)
            {
                var cg = _tabCanvasGroups[i];
                if (cg != null)
                {
                    bool isActive = (i == index);
                    cg.alpha = isActive ? 1f : 0f;
                    cg.interactable = isActive;
                    cg.blocksRaycasts = isActive;
                }
            }

            // Set button opacity based on active status
            for (int i = 0; i < _tabButtons.Count; i++)
            {
                if (_tabButtons[i] != null)
                {
                    var img = _tabButtons[i].GetComponent<Image>();
                    if (img != null)
                    {
                        img.color = (i == index) ? new Color(1f, 1f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.15f);
                    }
                }
            }
        }

        private void SetAllTabButtonsVisible(bool visible)
        {
            if (_mainBuildingTabButton != null) _mainBuildingTabButton.gameObject.SetActive(visible && _isMainBuildingTabSupported);
            if (_barracksTabButton != null) _barracksTabButton.gameObject.SetActive(visible && _isBarracksTabSupported);
            if (_storageTabButton != null) _storageTabButton.gameObject.SetActive(visible && _isStorageTabSupported);
            if (_upgradesTabButton != null) _upgradesTabButton.gameObject.SetActive(visible && _isUpgradesTabSupported);
            if (_managementTabButton != null) _managementTabButton.gameObject.SetActive(visible && _isManagementTabSupported);
        }

        private void SyncActiveTabProductionSources(BuildingProduction selectedBarracksProduction)
        {
            BuildingProduction mainProduction = GetMainBuildingProduction();
            BuildingProduction barracksProduction = GetBarracksProduction(selectedBarracksProduction);

            if (MainBuildingUIController.Instance != null)
            {
                if (_currentTabIndex == 0)
                {
                    MainBuildingUIController.Instance.SetProductionOverride(mainProduction);
                }
                else
                {
                    MainBuildingUIController.Instance.SetProductionOverride(null);
                    MainBuildingUIController.Instance.ClearProductionOverride();
                }
            }

            if (ProductionUIController.Instance != null)
            {
                if (_currentTabIndex == 1)
                {
                    ProductionUIController.Instance.SetProductionOverride(barracksProduction);
                }
                else
                {
                    ProductionUIController.Instance.SetProductionOverride(null);
                    ProductionUIController.Instance.ClearProductionOverride();
                }
            }
        }

        public void CloseMenu()
        {
            if (MainBuildingUI.Instance != null)
            {
                MainBuildingUI.Instance.DeselectMainBuilding();
            }
            if (TestProductionUI.Instance != null)
            {
                TestProductionUI.Instance.DeselectProduction();
            }
        }

        private void OnUnifiedRallyPointClicked()
        {
            if (TestProductionUI.Instance != null)
            {
                TestProductionUI.Instance.BeginRallyTargeting(GetActiveProductionForRally());
            }
        }

        private BuildingProduction GetActiveProductionForRally()
        {
            if (_currentTabIndex == 1)
            {
                BuildingProduction selectedProduction = TestProductionUI.Instance != null ? TestProductionUI.Instance.SelectedProduction : null;
                return GetBarracksProduction(selectedProduction);
            }

            if (_currentTabIndex == 0)
            {
                return GetMainBuildingProduction();
            }

            return null;
        }

        private BuildingProduction GetMainBuildingProduction()
        {
            if (MainBuildingUI.Instance != null && MainBuildingUI.Instance.SelectedMainBuilding != null)
            {
                return MainBuildingUI.Instance.SelectedMainBuilding.GetComponent<BuildingProduction>() ??
                       MainBuildingUI.Instance.SelectedMainBuilding.GetComponentInChildren<BuildingProduction>();
            }

            if (BuildingManager.Instance != null && BuildingManager.Instance.MainBuildingInstance != null)
            {
                return BuildingManager.Instance.MainBuildingInstance.GetComponent<BuildingProduction>() ??
                       BuildingManager.Instance.MainBuildingInstance.GetComponentInChildren<BuildingProduction>();
            }

            return null;
        }

        private BuildingProduction GetBarracksProduction(BuildingProduction selectedProduction)
        {
            if (IsBarracksProduction(selectedProduction) && IsProductionReady(selectedProduction))
            {
                return selectedProduction;
            }

            for (int i = 0; i < BuildingProduction.Registry.Count; i++)
            {
                BuildingProduction production = BuildingProduction.Registry[i];
                if (IsBarracksProduction(production) && IsProductionReady(production))
                {
                    return production;
                }
            }

            return null;
        }

        private bool IsBarracksProduction(BuildingProduction production)
        {
            if (production == null || production.BuildingData == null || string.IsNullOrEmpty(production.BuildingData.buildingName))
            {
                return false;
            }

            string buildingName = production.BuildingData.buildingName.ToLowerInvariant();
            return buildingName.Contains("barrack");
        }

        private bool IsProductionReady(BuildingProduction production)
        {
            if (production == null)
            {
                return false;
            }

            ConstructibleBuilding constructible = production.GetComponent<ConstructibleBuilding>() ??
                                                  production.GetComponentInParent<ConstructibleBuilding>();
            return constructible == null || constructible.IsCompleted;
        }
    }
}
