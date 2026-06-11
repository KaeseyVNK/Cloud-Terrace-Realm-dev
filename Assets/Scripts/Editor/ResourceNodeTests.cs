using NUnit.Framework;
using UnityEngine;

public class ResourceNodeTests
{
    [Test]
    public void TestSecondaryResourceWoodDefaults()
    {
        GameObject go = new GameObject("TestNode");
        ResourceNode node = go.AddComponent<ResourceNode>();
        node.Initialize(ResourceType.Wood, 100, null);

        Assert.IsTrue(node.HasSecondaryResource);
        Assert.AreEqual(ResourceType.Food, node.SecondaryResourceType);
        Assert.AreEqual(1.0f, node.SecondaryYieldRatio);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TestSecondaryResourceStoneDefaults()
    {
        GameObject go = new GameObject("TestNode");
        ResourceNode node = go.AddComponent<ResourceNode>();
        node.Initialize(ResourceType.Stone, 100, null);

        Assert.IsFalse(node.HasSecondaryResource);

        Object.DestroyImmediate(go);
    }

    [Test]
    public void TestCanHarvestWithConstructibleBuilding()
    {
        GameObject go = new GameObject("TestBuildingNode");
        ResourceNode node = go.AddComponent<ResourceNode>();
        node.Initialize(ResourceType.Food, 100, null);

        ConstructibleBuilding building = go.AddComponent<ConstructibleBuilding>();
        
        // Initially, the building is not completed
        Assert.IsFalse(building.IsCompleted);
        Assert.IsFalse(node.CanHarvest);

        // When building is marked completed, it should be harvestable
        building.IsCompleted = true;
        Assert.IsTrue(node.CanHarvest);

        Object.DestroyImmediate(go);
    }
}
