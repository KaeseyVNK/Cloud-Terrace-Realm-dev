using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class CombatKnockupMotion : MonoBehaviour
{
    private const float LandingCollisionGraceDuration = 0.25f;
    private const float LandingCollisionCheckRadius = 1.25f;

    private struct CollisionIgnorePair
    {
        public Collider OwnCollider;
        public Collider OtherCollider;
    }

    private Coroutine knockupCoroutine;
    private Coroutine collisionRestoreCoroutine;
    private NavMeshAgent activeAgent;
    private bool shouldRestoreAgent;
    private bool restoreStoppedState;
    private Rigidbody activeRigidbody;
    private bool shouldRestoreRigidbody;
    private bool restoreRigidbodyKinematic;
    private bool restoreRigidbodyGravity;
    private RigidbodyConstraints restoreRigidbodyConstraints;
    private Collider[] physicalColliders;
    private bool[] restoreColliderEnabledStates;
    private readonly List<CollisionIgnorePair> activeCollisionIgnores = new List<CollisionIgnorePair>();

    public bool IsActive => knockupCoroutine != null;

    public void Play(Vector3 landingPosition, float height, float duration)
    {
        if (knockupCoroutine != null)
        {
            StopCoroutine(knockupCoroutine);
            RestoreActiveCollisionIgnores();
            RestorePhysicsSafely();
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

        DisablePhysicsCollisions();

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
        bool hasValidLanding = NavMesh.SamplePosition(landingPosition, out NavMeshHit hit, 1.5f, ~2);
        if (hasValidLanding)
        {
            finalPosition = hit.position;
        }

        transform.position = finalPosition;
        BeginLandingCollisionGrace(finalPosition);
        RestorePhysicsSafely();
        RestoreAgentSafely(finalPosition);
        Physics.SyncTransforms();
        knockupCoroutine = null;

        BaseCombatUnitController combatUnit = GetComponent<BaseCombatUnitController>();
        if (combatUnit != null)
        {
            combatUnit.RecoverFromKnockup();
        }
    }

    private void RestoreAgentSafely(Vector3 position)
    {
        if (!shouldRestoreAgent || activeAgent == null)
        {
            return;
        }

        if (!NavMesh.SamplePosition(position, out NavMeshHit hit, 2f, ~2))
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

    private void DisablePhysicsCollisions()
    {
        activeRigidbody = GetComponent<Rigidbody>();
        shouldRestoreRigidbody = activeRigidbody != null;
        if (shouldRestoreRigidbody)
        {
            restoreRigidbodyKinematic = activeRigidbody.isKinematic;
            restoreRigidbodyGravity = activeRigidbody.useGravity;
            restoreRigidbodyConstraints = activeRigidbody.constraints;
            activeRigidbody.linearVelocity = Vector3.zero;
            activeRigidbody.angularVelocity = Vector3.zero;
            activeRigidbody.useGravity = false;
            activeRigidbody.isKinematic = true;
            activeRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        int physicalColliderCount = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] != null && !allColliders[i].isTrigger)
            {
                physicalColliderCount++;
            }
        }

        physicalColliders = new Collider[physicalColliderCount];
        restoreColliderEnabledStates = new bool[physicalColliderCount];

        int index = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider col = allColliders[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            physicalColliders[index] = col;
            restoreColliderEnabledStates[index] = col.enabled;
            col.enabled = false;
            index++;
        }
    }

    private void RestorePhysicsSafely()
    {
        if (physicalColliders != null && restoreColliderEnabledStates != null)
        {
            int count = Mathf.Min(physicalColliders.Length, restoreColliderEnabledStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (physicalColliders[i] != null)
                {
                    physicalColliders[i].enabled = restoreColliderEnabledStates[i];
                }
            }
        }

        physicalColliders = null;
        restoreColliderEnabledStates = null;

        if (shouldRestoreRigidbody && activeRigidbody != null)
        {
            activeRigidbody.linearVelocity = Vector3.zero;
            activeRigidbody.angularVelocity = Vector3.zero;
            activeRigidbody.constraints = restoreRigidbodyConstraints;
            activeRigidbody.isKinematic = restoreRigidbodyKinematic;
            activeRigidbody.useGravity = restoreRigidbodyGravity;
        }

        shouldRestoreRigidbody = false;
        activeRigidbody = null;
    }

    private void BeginLandingCollisionGrace(Vector3 finalPosition)
    {
        if (physicalColliders == null || physicalColliders.Length == 0)
        {
            return;
        }

        RestoreActiveCollisionIgnores();

        Vector3 checkCenter = finalPosition + Vector3.up * 0.5f;
        Collider[] nearbyColliders = Physics.OverlapSphere(checkCenter, LandingCollisionCheckRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < physicalColliders.Length; i++)
        {
            Collider ownCollider = physicalColliders[i];
            if (ownCollider == null || ownCollider.isTrigger)
            {
                continue;
            }

            for (int j = 0; j < nearbyColliders.Length; j++)
            {
                Collider otherCollider = nearbyColliders[j];
                if (otherCollider == null || otherCollider == ownCollider || otherCollider.isTrigger || otherCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                BaseCombatUnitController otherUnit = otherCollider.GetComponentInParent<BaseCombatUnitController>();
                if (otherUnit == null)
                {
                    continue;
                }

                Physics.IgnoreCollision(ownCollider, otherCollider, true);
                activeCollisionIgnores.Add(new CollisionIgnorePair
                {
                    OwnCollider = ownCollider,
                    OtherCollider = otherCollider
                });
            }
        }

        if (activeCollisionIgnores.Count > 0)
        {
            collisionRestoreCoroutine = StartCoroutine(RestoreCollisionIgnoresAfterDelay(LandingCollisionGraceDuration));
        }
    }

    private IEnumerator RestoreCollisionIgnoresAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        collisionRestoreCoroutine = null;
        RestoreActiveCollisionIgnores(false);
    }

    private void RestoreActiveCollisionIgnores(bool stopCoroutine = true)
    {
        if (stopCoroutine && collisionRestoreCoroutine != null)
        {
            StopCoroutine(collisionRestoreCoroutine);
            collisionRestoreCoroutine = null;
        }

        for (int i = 0; i < activeCollisionIgnores.Count; i++)
        {
            CollisionIgnorePair pair = activeCollisionIgnores[i];
            if (pair.OwnCollider != null && pair.OtherCollider != null)
            {
                Physics.IgnoreCollision(pair.OwnCollider, pair.OtherCollider, false);
            }
        }

        activeCollisionIgnores.Clear();
    }

    private void OnDisable()
    {
        RestoreActiveCollisionIgnores();
        RestorePhysicsSafely();
    }
}
