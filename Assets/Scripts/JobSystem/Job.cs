using UnityEngine;

public class Job
{
    public ZoneType zoneType;
    public ResourceType targetResource;
    public Vector3 position;
    public bool isAssigned;

    public Job(ZoneType type, ResourceType resource, Vector3 pos)
    {
        zoneType = type;
        targetResource = resource;
        position = pos;
        isAssigned = false;
    }
}