using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyGroupRallyController
{
    private GridSystem _gridSystem;

    public EnemyGroupRallyController(GridSystem gridSystem)
    {
        _gridSystem = gridSystem;
    }

    public void SetGridSystem(GridSystem gridSystem)
    {
        _gridSystem = gridSystem;
    }

    public Vector3 GetRallyPosition(Vector3 centerSpawnPos)
    {
        Vector3 mapCenter = Vector3.zero;
        if (_gridSystem != null)
        {
            mapCenter = _gridSystem.GetWorldPosition(_gridSystem.GetWidth() / 2, _gridSystem.GetLength() / 2);
        }

        Vector3 toCenterDir = (mapCenter - centerSpawnPos).normalized;
        Vector3 rallyPos = centerSpawnPos + toCenterDir * 15f;
        if (NavMesh.SamplePosition(rallyPos, out NavMeshHit hit, 15f, NavMesh.AllAreas))
        {
            rallyPos = hit.position;
        }

        return rallyPos;
    }

    public IEnumerator ReleaseGroupWhenReady(List<EnemyUnitController> group, Vector3 rallyPosition, float rallyRadius, float minReadyRatio, float maxWait)
    {
        float elapsed = 0f;
        float safeMaxWait = Mathf.Max(0.5f, maxWait);
        float readyRadius = Mathf.Max(0.1f, rallyRadius);
        float readyRadiusSqr = readyRadius * readyRadius;
        float safeMinReadyRatio = Mathf.Clamp(minReadyRatio, 0.1f, 1f);

        while (elapsed < safeMaxWait)
        {
            int activeCount = 0;
            int readyCount = 0;

            foreach (EnemyUnitController enemy in group)
            {
                if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.currentState == CombatState.Dead)
                {
                    continue;
                }

                activeCount++;
                if ((enemy.transform.position - rallyPosition).sqrMagnitude <= readyRadiusSqr)
                {
                    readyCount++;
                }
            }

            if (activeCount == 0 || (float)readyCount / activeCount >= safeMinReadyRatio)
            {
                break;
            }

            elapsed += 0.5f;
            yield return new WaitForSeconds(0.5f);
        }

        ReleaseGroup(group);
    }

    private void ReleaseGroup(List<EnemyUnitController> group)
    {
        foreach (EnemyUnitController enemy in group)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy && enemy.currentState != CombatState.Dead)
            {
                enemy.ReleaseRally();
            }
        }
    }
}
