using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Technology Data", menuName = "Cloud Terrace/Technology Data")]
public class TechnologyData : ScriptableObject
{
    [Header("Basic Info")]
    public string technologyId;
    public string technologyName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Research Requirements")]
    public float researchTime = 10f;
    public List<ResourceCost> researchCosts;

    [Header("Villager Economy Effects")]
    public int villagerCarryCapacityBonus = 0;
    [Min(1f)] public float villagerMoveSpeedMultiplier = 1f;
    [Min(1f)] public float woodGatherSpeedMultiplier = 1f;
    [Min(1f)] public float stoneGatherSpeedMultiplier = 1f;
    [Min(1f)] public float goldGatherSpeedMultiplier = 1f;
    [Min(1f)] public float foodGatherSpeedMultiplier = 1f;

    [Header("Storage Effects")]
    public int storageCapacityBonus = 0;

    public bool HasVillagerEconomyEffects()
    {
        return villagerCarryCapacityBonus != 0
            || villagerMoveSpeedMultiplier > 1.001f
            || woodGatherSpeedMultiplier > 1.001f
            || stoneGatherSpeedMultiplier > 1.001f
            || goldGatherSpeedMultiplier > 1.001f
            || foodGatherSpeedMultiplier > 1.001f
            || storageCapacityBonus != 0;
    }

    public string GetVillagerEffectText()
    {
        string text = "";

        if (villagerCarryCapacityBonus != 0)
        {
            text += "+ " + villagerCarryCapacityBonus + " suc chua";
        }

        if (storageCapacityBonus != 0)
        {
            if (!string.IsNullOrEmpty(text))
            {
                text += ", ";
            }
            text += "+ " + storageCapacityBonus + " suc chua kho";
        }

        AppendMultiplierEffect(ref text, villagerMoveSpeedMultiplier, "toc chay");
        AppendMultiplierEffect(ref text, woodGatherSpeedMultiplier, "khai thac go");
        AppendMultiplierEffect(ref text, stoneGatherSpeedMultiplier, "khai thac da");
        AppendMultiplierEffect(ref text, goldGatherSpeedMultiplier, "khai thac vang");
        AppendMultiplierEffect(ref text, foodGatherSpeedMultiplier, "thu thap thuc an");

        return string.IsNullOrEmpty(text) ? description : text;
    }

    private void AppendMultiplierEffect(ref string text, float multiplier, string label)
    {
        if (multiplier <= 1.001f)
        {
            return;
        }

        if (!string.IsNullOrEmpty(text))
        {
            text += ", ";
        }

        int percent = Mathf.RoundToInt((multiplier - 1f) * 100f);
        text += "+" + percent + "% " + label;
    }
}
