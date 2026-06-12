using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class CombatKnockupMotion : MonoBehaviour
{
    private Coroutine knockupCoroutine;
    private NavMeshAgent activeAgent;
    private bool shouldRestoreAgent;
    private bool restoreStoppedState;

    public void Play(Vector3 landingPosition, float height, float duration)
    {
        if (knockupCoroutine != null)
        {
            StopCoroutine(knockupCoroutine);
            RestoreAgentSafely(transform.position);
        }

        knockupCoroutine = StartCoroutine(KnockupCoroutine(landingPosition, Mathf.Max(0f, height), Mathf.Max(0.05f, duration)));
    }

    private IEnumerator KnockupCoroutine(Vector3 landingPosition, float height, float duration)
    {
        activeAgent = GetComponent<NavMeshAgent>();
        shouldRestoreAgent = activeAgent != null && activeAgent.enabled;
        bool wasOnNavMesh = shouldRestoreAgent && activeAgent.isOnNavMesh;
        restoreStoppedState = false;

        if (wasOnNavMesh)
        {
            restoreStoppedState = activeAgent.isStopped;
            activeAgent.isStopped = true;
            activeAgent.ResetPath();
        }

        if (shouldRestoreAgent)
        {
            activeAgent.enabled = false;
        }

        Vector3 startPosition = transform.position;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 flatPosition = Vector3.Lerp(startPosition, landingPosition, t);
            float arc = Mathf.Sin(t * Mathf.PI) * height;
            transform.position = flatPosition + Vector3.up * arc;
            yield return null;
        }

        Vector3 finalPosition = landingPosition;
        bool hasValidLanding = NavMesh.SamplePosition(landingPosition, out NavMeshHit hit, 1.5f, NavMesh.AllAreas);
        if (hasValidLanding)
        {
            finalPosition = hit.position;
        }

        transform.position = finalPosition;
        RestoreAgentSafely(finalPosition);
        knockupCoroutine = null;
    }

    private void RestoreAgentSafely(Vector3 position)
    {
        if (!shouldRestoreAgent || activeAgent == null)
        {
            return;
        }

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            shouldRestoreAgent = false;
            activeAgent = null;
            return;
        }

        transform.position = hit.position;
        activeAgent.enabled = true;

        if (activeAgent.isOnNavMesh)
        {
            activeAgent.Warp(hit.position);
            activeAgent.isStopped = restoreStoppedState;
        }

        shouldRestoreAgent = false;
        activeAgent = null;
    }
}
