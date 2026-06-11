using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

public class EnemyRaidTests
{
    [Test]
    public void TestMainBuildingCombatTargetInitialization()
    {
        GameObject buildingGo = new GameObject("TestMainHouse");
        // base.Start() requires NavMeshAgent component which is automatically added due to [RequireComponent]
        NavMeshAgent agent = buildingGo.AddComponent<NavMeshAgent>();
        MainBuildingCombatTarget target = buildingGo.AddComponent<MainBuildingCombatTarget>();

        // Set health directly to test
        target.maxHealth = 500;
        target.currentHealth = 500;

        Assert.AreEqual(UnitFaction.Player, target.faction);
        Assert.AreEqual("Main Building", target.unitName);
        Assert.AreEqual(500, target.maxHealth);

        // Clean up
        Object.DestroyImmediate(buildingGo);
    }

    [Test]
    public void TestEnemyUnitControllerFaction()
    {
        GameObject enemyGo = new GameObject("TestEnemy");
        enemyGo.AddComponent<NavMeshAgent>();
        EnemyUnitController enemy = enemyGo.AddComponent<EnemyUnitController>();

        Assert.AreEqual(UnitFaction.Enemy, enemy.faction);
        Assert.AreEqual("Enemy Soldier", enemy.unitName);

        Object.DestroyImmediate(enemyGo);
    }

    [Test]
    public void TestEnemyManagerNightSpawnsScale()
    {
        GameObject managerGo = new GameObject("TestEnemyManager");
        EnemyManager manager = managerGo.AddComponent<EnemyManager>();
        manager.BaseEnemyCount = 3;
        manager.EnemyIncreasePerNight = 2;
        manager.MaxEnemiesPerWave = 6;

        Assert.AreEqual(3, manager.GetEnemyCountForNight(1));
        Assert.AreEqual(5, manager.GetEnemyCountForNight(2));
        Assert.AreEqual(6, manager.GetEnemyCountForNight(3));

        Object.DestroyImmediate(managerGo);
    }
}
