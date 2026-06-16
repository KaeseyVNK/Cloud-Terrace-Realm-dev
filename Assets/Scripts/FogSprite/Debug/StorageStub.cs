using UnityEngine;

public class StorageStub : MonoBehaviour, IResourceStorage
{
    public int resources = 100;

    public bool HasResources() => resources > 0;

    public int TakeResources(int amount)
    {
        int taken = Mathf.Min(amount, resources);
        resources -= taken;
        return taken;
    }

    private void OnEnable()
    {
        ResourceStorageRegistry.Instances.Add(this);
    }

    private void OnDisable()
    {
        ResourceStorageRegistry.Instances.Remove(this);
    }

    public Vector3 GetPosition() => transform.position;
}