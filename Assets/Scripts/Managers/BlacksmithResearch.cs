using System.Collections.Generic;
using UnityEngine;

public class BlacksmithResearch : MonoBehaviour
{
    [SerializeField] private List<TechnologyData> availableTechnologies = new List<TechnologyData>();

    private TechnologyData currentResearch;
    private float currentResearchTimer;

    public IReadOnlyList<TechnologyData> AvailableTechnologies => availableTechnologies;
    public TechnologyData CurrentResearch => currentResearch;
    public float CurrentResearchTimer => currentResearchTimer;
    public bool IsResearching => currentResearch != null;

    private void Update()
    {
        if (currentResearch == null)
        {
            return;
        }

        currentResearchTimer -= Time.deltaTime;
        if (currentResearchTimer <= 0f)
        {
            FinishResearch();
        }
    }

    public void RequestResearch(TechnologyData technology)
    {
        if (technology == null)
        {
            return;
        }

        if (!availableTechnologies.Contains(technology))
        {
            Debug.LogWarning("[Tech] Blacksmith cannot research " + technology.technologyName);
            return;
        }

        if (TechnologyManager.Instance.IsUnlocked(technology))
        {
            Debug.Log("[Tech] Already unlocked: " + technology.technologyName);
            return;
        }

        if (currentResearch != null)
        {
            Debug.LogWarning("[Tech] Blacksmith is already researching " + currentResearch.technologyName);
            return;
        }

        if (ResourceManager.Instance == null || !ResourceManager.Instance.CanAfford(technology.researchCosts))
        {
            Debug.LogWarning("[Tech] Not enough resources to research " + technology.technologyName);
            return;
        }

        ResourceManager.Instance.ConsumeCosts(technology.researchCosts);
        currentResearch = technology;
        currentResearchTimer = Mathf.Max(0.1f, technology.researchTime);
        Debug.Log("[Tech] Research started: " + currentResearch.technologyName);
    }

    private void FinishResearch()
    {
        TechnologyData completedTechnology = currentResearch;
        currentResearch = null;
        currentResearchTimer = 0f;
        TechnologyManager.Instance.Unlock(completedTechnology);
    }
}
