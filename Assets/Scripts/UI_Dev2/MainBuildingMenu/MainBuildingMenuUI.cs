using System.Collections.Generic;
using UnityEngine;

public enum MainBuildingTabType
{
    Overview,
    Population,
    Storage,
    Upgrades,
    Management
}

public class MainBuildingMenuUI : MonoBehaviour
{
    [Header("Content Panels")]
    [SerializeField] private GameObject _overviewPanel;
    [SerializeField] private GameObject _populationPanel;
    [SerializeField] private GameObject _storagePanel;
    [SerializeField] private GameObject _upgradesPanel;
    [SerializeField] private GameObject _managementPanel;

    [Header("Tabs")]
    [SerializeField]
    private List<MainBuildingTabUI> _tabs =
        new List<MainBuildingTabUI>();

    [Header("Starting Tab")]
    [SerializeField]
    private MainBuildingTabType _startingTab =
        MainBuildingTabType.Overview;

    public MainBuildingTabType CurrentTab { get; private set; }

    private void Start()
    {
        ShowTab(_startingTab);
    }

    public void ShowTab(MainBuildingTabType tabType)
    {
        CurrentTab = tabType;

        SetPanelActive(
            _overviewPanel,
            tabType == MainBuildingTabType.Overview
        );

        SetPanelActive(
            _populationPanel,
            tabType == MainBuildingTabType.Population
        );

        SetPanelActive(
            _storagePanel,
            tabType == MainBuildingTabType.Storage
        );

        SetPanelActive(
            _upgradesPanel,
            tabType == MainBuildingTabType.Upgrades
        );

        SetPanelActive(
            _managementPanel,
            tabType == MainBuildingTabType.Management
        );

        foreach (MainBuildingTabUI tab in _tabs)
        {
            if (tab == null)
            {
                continue;
            }

            tab.SetSelected(tab.TabType == CurrentTab);
        }
    }

    private static void SetPanelActive(
        GameObject panel,
        bool shouldBeActive
    )
    {
        if (panel != null)
        {
            panel.SetActive(shouldBeActive);
        }
    }

    public void ShowMenu()
    {
        gameObject.SetActive(true);
        ShowTab(_startingTab);
    }

    public void HideMenu()
    {
        gameObject.SetActive(false);
    }
}