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
}
