using UnityEngine;

public interface IResourceStorage
{
    bool HasResources();
    int TakeResources(int amount);
    Vector3 GetPosition();
}