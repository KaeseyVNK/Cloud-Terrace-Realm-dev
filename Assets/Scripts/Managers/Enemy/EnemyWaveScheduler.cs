using System.Collections.Generic;
using UnityEngine;

public struct EnemyWaveSchedulerSettings
{
    public int baseEnemyCount;
    public int enemyIncreasePerNight;
    public int maxEnemiesPerWave;
    public int waveIncreaseEveryNights;
    public int maxAutoWavesPerNight;
    public float autoWaveStartTimeRatio;
    public float autoWaveEndTimeRatio;
    public float bloodMoonWaveMultiplier;
    public int minorEventStartNight;
    public int dangerEventStartNight;
}

public class EnemyWaveScheduler
{
    public List<int> GetDueWaveIndices(List<DayWaveConfig> activeNightWaves, bool[] spawnedWavesThisNight, float timeRatio)
    {
        List<int> dueWaveIndices = new List<int>();
        if (activeNightWaves == null || spawnedWavesThisNight == null)
        {
            return dueWaveIndices;
        }

        int count = Mathf.Min(activeNightWaves.Count, spawnedWavesThisNight.Length);
        for (int i = 0; i < count; i++)
        {
            if (spawnedWavesThisNight[i])
            {
                continue;
            }

            DayWaveConfig wave = activeNightWaves[i];
            if (wave != null && timeRatio >= wave.spawnTimeRatio)
            {
                dueWaveIndices.Add(i);
            }
        }

        return dueWaveIndices;
    }

    public bool HasPendingWaves(List<DayWaveConfig> activeNightWaves, bool[] spawnedWavesThisNight)
    {
        if (activeNightWaves == null || activeNightWaves.Count == 0 || spawnedWavesThisNight == null)
        {
            return false;
        }

        int count = Mathf.Min(activeNightWaves.Count, spawnedWavesThisNight.Length);
        for (int i = 0; i < count; i++)
        {
            if (!spawnedWavesThisNight[i])
            {
                return true;
            }
        }

        return false;
    }

    public bool AllWavesSpawned(List<DayWaveConfig> activeNightWaves, bool[] spawnedWavesThisNight)
    {
        return !HasPendingWaves(activeNightWaves, spawnedWavesThisNight);
    }

    public List<DayWaveConfig> PrepareNightRaidSchedule(
        EnemyWaveSchedulerSettings settings,
        bool useCustomWaveOverrides,
        List<DayWaveConfig> customWaves,
        List<EnemyRosterEntry> enemyRoster,
        GameObject fallbackEnemyPrefab,
        int night,
        WeatherState weather)
    {
        List<DayWaveConfig> activeNightWaves = new List<DayWaveConfig>();

        if (useCustomWaveOverrides && customWaves != null)
        {
            for (int i = 0; i < customWaves.Count; i++)
            {
                DayWaveConfig wave = customWaves[i];
                if (wave != null && wave.specificNight == night)
                {
                    activeNightWaves.Add(wave);
                }
            }

            if (activeNightWaves.Count == 0)
            {
                for (int i = 0; i < customWaves.Count; i++)
                {
                    DayWaveConfig wave = customWaves[i];
                    if (wave != null && wave.specificNight <= 0)
                    {
                        activeNightWaves.Add(wave);
                    }
                }
            }
        }

        if (activeNightWaves.Count == 0)
        {
            BuildAutoNightWaves(settings, enemyRoster, fallbackEnemyPrefab, night, weather, activeNightWaves);
        }

        return activeNightWaves;
    }

    public int GetEnemyCountForNight(EnemyWaveSchedulerSettings settings, int nightNumber)
    {
        int safeNightNumber = Mathf.Max(1, nightNumber);
        int scaledCount = Mathf.Max(0, settings.baseEnemyCount) + (safeNightNumber - 1) * Mathf.Max(0, settings.enemyIncreasePerNight);
        return Mathf.Clamp(scaledCount, 0, Mathf.Max(0, settings.maxEnemiesPerWave));
    }

    public bool TryRollSpecialEvent(EnemyWaveSchedulerSettings settings, int night, float minorChance, float dangerChance, out string eventName, out int bonusWaves, out float countMultiplier)
    {
        eventName = string.Empty;
        bonusWaves = 0;
        countMultiplier = 1f;

        if (night >= settings.dangerEventStartNight && Random.value < Mathf.Clamp01(dangerChance))
        {
            eventName = "Danger Event";
            bonusWaves = 2;
            countMultiplier = 1.35f;
            return true;
        }

        if (night >= settings.minorEventStartNight && Random.value < Mathf.Clamp01(minorChance))
        {
            eventName = "Minor Event";
            bonusWaves = 1;
            countMultiplier = 1.15f;
            return true;
        }

        return false;
    }

    public List<EnemyWaveEntry> BuildWeightedEnemyEntries(EnemyWaveSchedulerSettings settings, List<EnemyRosterEntry> enemyRoster, GameObject fallbackEnemyPrefab, int night, int totalCount)
    {
        List<EnemyWaveEntry> entries = new List<EnemyWaveEntry>();
        List<EnemyRosterEntry> unlockedRoster = new List<EnemyRosterEntry>();
        float totalWeight = 0f;

        if (enemyRoster != null)
        {
            for (int i = 0; i < enemyRoster.Count; i++)
            {
                EnemyRosterEntry rosterEntry = enemyRoster[i];
                if (rosterEntry == null || GetRosterPrefab(rosterEntry, fallbackEnemyPrefab) == null || night < Mathf.Max(1, rosterEntry.unlockDay) || rosterEntry.weight <= 0f)
                {
                    continue;
                }

                unlockedRoster.Add(rosterEntry);
                totalWeight += rosterEntry.weight;
            }
        }

        if (unlockedRoster.Count == 0)
        {
            if (fallbackEnemyPrefab != null)
            {
                entries.Add(CreateFixedCountWaveEntry("Enemy", fallbackEnemyPrefab, Mathf.Max(1, totalCount), settings.maxEnemiesPerWave));
            }
            return entries;
        }

        int remainingCount = Mathf.Max(1, totalCount);
        int guaranteedCount = remainingCount >= unlockedRoster.Count ? 1 : 0;
        if (guaranteedCount > 0)
        {
            remainingCount -= unlockedRoster.Count;
        }

        int assignedCount = 0;
        List<int> counts = new List<int>();
        for (int i = 0; i < unlockedRoster.Count; i++)
        {
            int count = guaranteedCount;
            if (remainingCount > 0 && totalWeight > 0f)
            {
                count += Mathf.RoundToInt(remainingCount * (unlockedRoster[i].weight / totalWeight));
            }

            int maxCount = Mathf.Max(0, unlockedRoster[i].maxCountPerWave);
            if (maxCount > 0)
            {
                count = Mathf.Min(count, maxCount);
            }

            counts.Add(count);
            assignedCount += count;
        }

        int safety = 0;
        while (assignedCount > totalCount && safety < 128)
        {
            int trimIndex = GetLowestWeightAssignedRosterIndex(unlockedRoster, counts);
            if (trimIndex < 0)
            {
                break;
            }

            counts[trimIndex]--;
            assignedCount--;
            safety++;
        }

        safety = 0;
        while (assignedCount < totalCount && safety < 128)
        {
            int bestIndex = GetHighestWeightRosterIndex(unlockedRoster, counts);
            if (bestIndex < 0)
            {
                break;
            }

            counts[bestIndex]++;
            assignedCount++;
            safety++;
        }

        for (int i = 0; i < unlockedRoster.Count; i++)
        {
            if (counts[i] <= 0)
            {
                continue;
            }

            EnemyRosterEntry rosterEntry = unlockedRoster[i];
            entries.Add(CreateFixedCountWaveEntry(rosterEntry.name, GetRosterPrefab(rosterEntry, fallbackEnemyPrefab), counts[i], rosterEntry.maxCountPerWave));
        }

        return entries;
    }

    private void BuildAutoNightWaves(EnemyWaveSchedulerSettings settings, List<EnemyRosterEntry> enemyRoster, GameObject fallbackEnemyPrefab, int night, WeatherState weather, List<DayWaveConfig> activeNightWaves)
    {
        int interval = Mathf.Max(1, settings.waveIncreaseEveryNights);
        int normalWaveCount = Mathf.Clamp(1 + ((night - 1) / interval), 1, Mathf.Max(1, settings.maxAutoWavesPerNight));
        int waveCount = normalWaveCount;
        float countMultiplier = 1f;
        string eventName = string.Empty;

        if (weather == WeatherState.BloodMoon)
        {
            waveCount = Mathf.CeilToInt(waveCount * Mathf.Max(1f, settings.bloodMoonWaveMultiplier));
            eventName = "Blood Moon";
        }

        // Night special events are rolled by EnemyManager so it can use its inspector-configured chances.
        waveCount = Mathf.Max(1, waveCount);
        int perWaveBaseCount = GetEnemyCountForNight(settings, night);
        float startRatio = Mathf.Min(settings.autoWaveStartTimeRatio, settings.autoWaveEndTimeRatio);
        float endRatio = Mathf.Max(settings.autoWaveStartTimeRatio, settings.autoWaveEndTimeRatio);

        for (int i = 0; i < waveCount; i++)
        {
            float spawnRatio = waveCount == 1
                ? Mathf.Clamp((startRatio + endRatio) * 0.5f, 0.5f, 0.99f)
                : Mathf.Lerp(startRatio, endRatio, (float)i / (waveCount - 1));

            DayWaveConfig wave = new DayWaveConfig
            {
                specificNight = 0,
                waveName = string.IsNullOrEmpty(eventName) ? $"Night {night} Wave {i + 1}" : $"{eventName} Night {night} Wave {i + 1}",
                spawnTimeRatio = spawnRatio,
                countMultiplier = countMultiplier
            };

            wave.enemies.AddRange(BuildWeightedEnemyEntries(settings, enemyRoster, fallbackEnemyPrefab, night, perWaveBaseCount));
            activeNightWaves.Add(wave);
        }
    }

    public void ApplyNightSpecialEvent(EnemyWaveSchedulerSettings settings, int night, float minorChance, float dangerChance, List<DayWaveConfig> activeNightWaves)
    {
        if (activeNightWaves == null || activeNightWaves.Count == 0)
        {
            return;
        }

        if (!TryRollSpecialEvent(settings, night, minorChance, dangerChance, out string eventName, out int bonusWaves, out float eventCountMultiplier))
        {
            return;
        }

        int baseWaveCount = activeNightWaves.Count;
        for (int i = 0; i < activeNightWaves.Count; i++)
        {
            activeNightWaves[i].countMultiplier *= eventCountMultiplier;
            activeNightWaves[i].waveName = string.IsNullOrEmpty(activeNightWaves[i].waveName)
                ? eventName
                : $"{eventName} + {activeNightWaves[i].waveName}";
        }

        for (int i = 0; i < bonusWaves; i++)
        {
            DayWaveConfig source = activeNightWaves[Mathf.Min(i, baseWaveCount - 1)];
            DayWaveConfig bonusWave = CloneWave(source);
            bonusWave.waveName = $"{eventName} Bonus Wave {i + 1}";
            bonusWave.spawnTimeRatio = Mathf.Clamp(source.spawnTimeRatio + 0.04f * (i + 1), 0.5f, 0.99f);
            activeNightWaves.Add(bonusWave);
        }
    }

    private DayWaveConfig CloneWave(DayWaveConfig source)
    {
        DayWaveConfig clone = new DayWaveConfig
        {
            specificNight = source.specificNight,
            waveName = source.waveName,
            spawnTimeRatio = source.spawnTimeRatio,
            countMultiplier = source.countMultiplier,
            enemies = new List<EnemyWaveEntry>()
        };

        if (source.enemies != null)
        {
            for (int i = 0; i < source.enemies.Count; i++)
            {
                EnemyWaveEntry entry = source.enemies[i];
                if (entry == null)
                {
                    continue;
                }

                clone.enemies.Add(new EnemyWaveEntry
                {
                    name = entry.name,
                    prefab = entry.prefab,
                    baseCount = entry.baseCount,
                    increasePerNight = entry.increasePerNight,
                    maxCount = entry.maxCount
                });
            }
        }

        return clone;
    }

    private int GetHighestWeightRosterIndex(List<EnemyRosterEntry> roster, List<int> counts)
    {
        int bestIndex = -1;
        float bestWeight = float.NegativeInfinity;
        for (int i = 0; i < roster.Count; i++)
        {
            int maxCount = Mathf.Max(0, roster[i].maxCountPerWave);
            if (maxCount > 0 && counts[i] >= maxCount)
            {
                continue;
            }

            if (roster[i].weight > bestWeight)
            {
                bestWeight = roster[i].weight;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private int GetLowestWeightAssignedRosterIndex(List<EnemyRosterEntry> roster, List<int> counts)
    {
        int bestIndex = -1;
        float bestWeight = float.PositiveInfinity;
        for (int i = 0; i < roster.Count; i++)
        {
            if (counts[i] <= 0)
            {
                continue;
            }

            if (roster[i].weight < bestWeight)
            {
                bestWeight = roster[i].weight;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private EnemyWaveEntry CreateFixedCountWaveEntry(string enemyName, GameObject prefab, int count, int maxCount)
    {
        return new EnemyWaveEntry
        {
            name = string.IsNullOrEmpty(enemyName) ? "Enemy" : enemyName,
            prefab = prefab,
            baseCount = Mathf.Max(0, count),
            increasePerNight = 0,
            maxCount = Mathf.Max(Mathf.Max(0, count), maxCount)
        };
    }

    private GameObject GetRosterPrefab(EnemyRosterEntry entry, GameObject fallbackEnemyPrefab)
    {
        return entry != null && entry.prefab != null ? entry.prefab : fallbackEnemyPrefab;
    }
}
