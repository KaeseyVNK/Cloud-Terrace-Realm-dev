using UnityEngine;

public class SummonedEnemyTracker : MonoBehaviour, IPoolable
{
    private static int s_activeSummonedEnemies;

    private bool isRegistered;

    public static int ActiveSummonedEnemies => s_activeSummonedEnemies;

    public static int GetRemainingCapacity(int maxActiveSummons)
    {
        if (maxActiveSummons <= 0)
        {
            return 0;
        }

        return Mathf.Max(0, maxActiveSummons - s_activeSummonedEnemies);
    }

    public void MarkAsSummoned()
    {
        if (isRegistered)
        {
            return;
        }

        isRegistered = true;
        s_activeSummonedEnemies++;
    }

    public void MarkAsNonSummoned()
    {
        Unregister();
    }

    public void OnSpawnedFromPool()
    {
        Unregister();
    }

    public void OnReturnedToPool()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (!isRegistered)
        {
            return;
        }

        isRegistered = false;
        s_activeSummonedEnemies = Mathf.Max(0, s_activeSummonedEnemies - 1);
    }
}
