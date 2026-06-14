using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDifficultySettings", menuName = "Enemy/Difficulty Settings")]
public class EnemyDifficultySettings : ScriptableObject
{
    [Header("Base Count Scaling")]
    [Min(0)] public int baseEnemyCount = 3;
    [Min(0)] public int enemyIncreasePerNight = 2;
    [Min(1)] public int maxEnemiesPerWave = 30;
    [Min(1)] public int maxActiveEnemies = 60;

    [Header("Auto Wave Schedule")]
    [Min(1)] public int waveIncreaseEveryNights = 3;
    [Min(1)] public int maxAutoWavesPerNight = 6;
    [Range(0.5f, 0.95f)] public float autoWaveStartTimeRatio = 0.55f;
    [Range(0.55f, 0.99f)] public float autoWaveEndTimeRatio = 0.9f;

    [Header("Blood Moon Multipliers")]
    [Min(1f)] public float bloodMoonWaveMultiplier = 1.75f;
    [Min(1f)] public float bloodMoonCountMultiplier = 1.6f;

    [Header("Post Tutorial Scaling")]
    [Min(1)] public int enemyScalingStartNight = 10;
    [Min(0f)] public float healthGrowthPerNightAfterTutorial = 0.03f;
    [Min(0f)] public float damageGrowthPerNightAfterTutorial = 0.02f;
    [Min(0f)] public float speedGrowthPerNightAfterTutorial = 0.01f;

    [Header("Special Events")]
    [Min(1)] public int minorEventStartNight = 5;
    [Min(1)] public int dangerEventStartNight = 10;
    [Range(0f, 1f)] public float minorNightEventChance = 0.12f;
    [Range(0f, 1f)] public float dangerNightEventChance = 0.08f;
    [Range(0f, 1f)] public float minorDayEventChance = 0.08f;
    [Range(0f, 1f)] public float dangerDayEventChance = 0.05f;

    [Header("Summons")]
    [Tooltip("Summoned enemies count toward combat pressure but have a separate cap so summoners cannot flood the map forever.")]
    [Min(0)] public int maxActiveSummonedEnemies = 18;
}
