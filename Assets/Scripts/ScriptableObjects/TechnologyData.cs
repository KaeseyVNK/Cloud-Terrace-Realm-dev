using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Technology Data", menuName = "Cloud Terrace/Technology Data")]
public class TechnologyData : ScriptableObject
{
    [Header("Basic Info")]
    public string technologyId;
    public string technologyName;
    [TextArea] public string description;

    [Header("Research Requirements")]
    public float researchTime = 10f;
    public List<ResourceCost> researchCosts;
}
