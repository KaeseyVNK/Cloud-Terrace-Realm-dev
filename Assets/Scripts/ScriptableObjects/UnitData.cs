using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Unit Data", menuName = "Cloud Terrace/Unit Data")]
public class UnitData : ScriptableObject
{
    [Header("Basic Info")]
    public string unitName;
    [TextArea] public string description;

    [Header("Visuals")]
    public GameObject unitPrefab;

    [Header("Production Requirements")]
    public float productionTime = 5f; // Thời gian sinh ra unit này (giây)
    public List<ResourceCost> productionCosts; // Chi phí để sinh ra
    public List<TechnologyData> requiredTechnologies;

    public bool AreTechnologyRequirementsMet()
    {
        if (requiredTechnologies == null || requiredTechnologies.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < requiredTechnologies.Count; i++)
        {
            TechnologyData technology = requiredTechnologies[i];
            if (technology != null && !TechnologyManager.Instance.IsUnlocked(technology))
            {
                return false;
            }
        }

        return true;
    }

    public string GetMissingTechnologyNames()
    {
        if (requiredTechnologies == null || requiredTechnologies.Count == 0)
        {
            return "";
        }

        string missing = "";
        for (int i = 0; i < requiredTechnologies.Count; i++)
        {
            TechnologyData technology = requiredTechnologies[i];
            if (technology == null || TechnologyManager.Instance.IsUnlocked(technology))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(missing))
            {
                missing += ", ";
            }

            missing += string.IsNullOrEmpty(technology.technologyName) ? technology.name : technology.technologyName;
        }

        return missing;
    }
}
