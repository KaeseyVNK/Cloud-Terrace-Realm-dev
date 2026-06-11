using UnityEngine;

public enum BuildingPriority
{
    Low,
    Normal,
    High
}

[System.Serializable]
public class BuildingSlot
{
    public string buildingName;
    public BuildingPriority priority = BuildingPriority.Normal;
    public float progress;          // 0 - 100
    public int villagersAssigned;
    public string missingResource;
    public Vector3 worldPosition;
    public GameObject buildingObject;

    public BuildingSlot(string name, Vector3 position, GameObject obj)
    {
        buildingName = name;
        worldPosition = position;
        buildingObject = obj;
        progress = 0f;
        villagersAssigned = 0;
        missingResource = "";
    }
}