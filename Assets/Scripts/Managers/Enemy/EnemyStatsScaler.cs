using UnityEngine;

public class EnemyStatsScaler
{
    private EnemyDifficultySettings _settings;

    public EnemyStatsScaler(EnemyDifficultySettings settings)
    {
        _settings = settings;
    }

    public void SetSettings(EnemyDifficultySettings settings)
    {
        _settings = settings;
    }

    public void GetMultipliers(int nightNumber, WeatherState weather, out float healthMultiplier, out float damageMultiplier, out float speedMultiplier)
    {
        healthMultiplier = 1f;
        damageMultiplier = 1f;
        speedMultiplier = 1f;

        if (weather == WeatherState.BloodMoon)
        {
            healthMultiplier *= 1.5f;
            damageMultiplier *= 1.3f;
            speedMultiplier *= 1.15f;
        }

        int scalingStartNight = Mathf.Max(1, GetScalingStartNight());
        if (nightNumber <= scalingStartNight)
        {
            return;
        }

        int scalingNights = nightNumber - scalingStartNight;
        healthMultiplier *= 1f + scalingNights * GetHealthGrowth();
        damageMultiplier *= 1f + scalingNights * GetDamageGrowth();
        speedMultiplier *= 1f + scalingNights * GetSpeedGrowth();
    }

    private int GetScalingStartNight()
    {
        return _settings != null ? _settings.enemyScalingStartNight : 10;
    }

    private float GetHealthGrowth()
    {
        return _settings != null ? Mathf.Max(0f, _settings.healthGrowthPerNightAfterTutorial) : 0.03f;
    }

    private float GetDamageGrowth()
    {
        return _settings != null ? Mathf.Max(0f, _settings.damageGrowthPerNightAfterTutorial) : 0.02f;
    }

    private float GetSpeedGrowth()
    {
        return _settings != null ? Mathf.Max(0f, _settings.speedGrowthPerNightAfterTutorial) : 0.01f;
    }
}
