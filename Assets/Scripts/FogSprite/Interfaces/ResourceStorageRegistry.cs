using System.Collections.Generic;

public static class ResourceStorageRegistry
{
    public static readonly List<IResourceStorage> Instances = new List<IResourceStorage>();
}
