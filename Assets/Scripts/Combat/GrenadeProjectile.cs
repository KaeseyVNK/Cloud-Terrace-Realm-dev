using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class GrenadeProjectile : MonoBehaviour, IPoolable
{
    [Header("Flight")]
    [SerializeField] private float speed = 14f;
    [SerializeField] private float arcHeight = 4f;
    [SerializeField] private float minFlightDuration = 0.35f;
    [SerializeField] private float maxFlightDuration = 2.8f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

    [Header("Explosion")]
    [SerializeField] private float explosionRadius = 3.2f;
    [SerializeField] private float knockbackDistance = 2.2f;
    [SerializeField] private float knockupHeight = 1.2f;
    [SerializeField] private float knockupDuration = 0.35f;
    [SerializeField] private float knockbackNavMeshSampleRadius = 2f;
    [SerializeField] private bool useDamageFalloff = true;
    [SerializeField] private GameObject explosionEffectPrefab;

    private readonly HashSet<BaseCombatUnitController> hitUnits = new HashSet<BaseCombatUnitController>();

    private BaseCombatUnitController target;
    private Vector3 fixedTargetPosition;
    private UnitFaction ownerFaction;
    private int damage;
    private float spawnTime;
    private float flightDuration;
    private Vector3 startPosition;
    private Vector3 lastPosition;
    private Vector3 lastTravelDirection = Vector3.forward;
    private bool hasExploded;
    private bool useFixedTargetPosition;

    public void Launch(BaseCombatUnitController newTarget, UnitFaction newOwnerFaction, int newDamage)
    {
        target = newTarget;
        fixedTargetPosition = transform.position;
        ownerFaction = newOwnerFaction;
        damage = newDamage;
        spawnTime = Time.time;
        startPosition = transform.position;
        lastPosition = startPosition;
        hasExploded = false;
        useFixedTargetPosition = false;
        hitUnits.Clear();

        Vector3 targetPosition = GetTargetPosition();
        float distance = Vector3.Distance(startPosition, targetPosition);
        flightDuration = Mathf.Clamp(distance / Mathf.Max(0.1f, speed), minFlightDuration, maxFlightDuration);
        CacheTravelDirection(targetPosition - startPosition);
    }

    public void LaunchAtPosition(Vector3 newTargetPosition, UnitFaction newOwnerFaction, int newDamage)
    {
        target = null;
        fixedTargetPosition = newTargetPosition + targetOffset;
        ownerFaction = newOwnerFaction;
        damage = newDamage;
        spawnTime = Time.time;
        startPosition = transform.position;
        lastPosition = startPosition;
        hasExploded = false;
        useFixedTargetPosition = true;
        hitUnits.Clear();

        float distance = Vector3.Distance(startPosition, fixedTargetPosition);
        flightDuration = Mathf.Clamp(distance / Mathf.Max(0.1f, speed), minFlightDuration, maxFlightDuration);
        CacheTravelDirection(fixedTargetPosition - startPosition);
    }

    private void Update()
    {
        if (hasExploded)
        {
            return;
        }

        if ((!useFixedTargetPosition && (target == null || target.currentState == CombatState.Dead)) || Time.time > spawnTime + maxLifetime)
        {
            Explode(transform.position);
            return;
        }

        Vector3 targetPosition = GetTargetPosition();
        float t = flightDuration <= 0f ? 1f : Mathf.Clamp01((Time.time - spawnTime) / flightDuration);
        Vector3 flatPosition = Vector3.Lerp(startPosition, targetPosition, t);
        float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
        Vector3 nextPosition = flatPosition + Vector3.up * arc;

        Vector3 direction = nextPosition - lastPosition;
        transform.position = nextPosition;
        lastPosition = nextPosition;

        if (direction.sqrMagnitude > 0.001f)
        {
            CacheTravelDirection(direction);
            transform.rotation = Quaternion.LookRotation(lastTravelDirection, Vector3.up);
        }

        if (t >= 1f)
        {
            Explode(targetPosition);
        }
    }

    public void OnSpawnedFromPool()
    {
        target = null;
        fixedTargetPosition = transform.position;
        damage = 0;
        spawnTime = Time.time;
        flightDuration = 0f;
        startPosition = transform.position;
        lastPosition = transform.position;
        lastTravelDirection = transform.forward;
        hasExploded = false;
        useFixedTargetPosition = false;
        hitUnits.Clear();
    }

    public void OnReturnedToPool()
    {
        target = null;
        fixedTargetPosition = transform.position;
        damage = 0;
        flightDuration = 0f;
        lastTravelDirection = transform.forward;
        hasExploded = false;
        useFixedTargetPosition = false;
        hitUnits.Clear();
    }

    private void Explode(Vector3 explosionCenter)
    {
        hasExploded = true;
        hitUnits.Clear();
        SpawnExplosionEffect(explosionCenter);

        Collider[] colliders = Physics.OverlapSphere(explosionCenter, explosionRadius);
        for (int i = 0; i < colliders.Length; i++)
        {
            BaseCombatUnitController unit = colliders[i].GetComponentInParent<BaseCombatUnitController>();
            if (unit == null || unit.currentState == CombatState.Dead || unit.faction == ownerFaction)
            {
                continue;
            }

            if (!hitUnits.Add(unit))
            {
                continue;
            }

            float distance = Vector3.Distance(explosionCenter, unit.transform.position);
            float falloff = useDamageFalloff ? Mathf.Clamp01(1f - distance / Mathf.Max(0.1f, explosionRadius)) : 1f;
            int finalDamage = Mathf.Max(1, Mathf.RoundToInt(damage * Mathf.Lerp(0.45f, 1f, falloff)));
            unit.TakeDamage(finalDamage);

            if (unit.currentState != CombatState.Dead)
            {
                ApplyKnockback(unit, explosionCenter, falloff);
            }
        }

        Release();
    }

    private void ApplyKnockback(BaseCombatUnitController unit, Vector3 explosionCenter, float falloff)
    {
        SkeletonMageController skeletonMage = unit.GetComponent<SkeletonMageController>();
        if (skeletonMage != null && skeletonMage.IsSummonMovementLocked)
        {
            return;
        }

        NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
        if (agent == null || !agent.enabled)
        {
            return;
        }

        Vector3 direction = unit.transform.position - explosionCenter;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = lastTravelDirection;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = explosionCenter - startPosition;
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        direction.Normalize();
        float distance = knockbackDistance * Mathf.Lerp(0.35f, 1f, falloff);
        Vector3 desiredPosition = unit.transform.position + direction * distance;
        Vector3 landingPosition = unit.transform.position;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, knockbackNavMeshSampleRadius, NavMesh.AllAreas))
        {
            Vector3 sampledOffset = hit.position - unit.transform.position;
            sampledOffset.y = 0f;
            if (sampledOffset.sqrMagnitude > 0.01f && Vector3.Dot(sampledOffset.normalized, direction) > 0.25f)
            {
                landingPosition = hit.position;
            }
            else if (NavMesh.SamplePosition(unit.transform.position, out NavMeshHit currentHit, 1.5f, NavMesh.AllAreas))
            {
                landingPosition = currentHit.position;
            }
        }
        else if (NavMesh.SamplePosition(unit.transform.position, out NavMeshHit currentHit, 1.5f, NavMesh.AllAreas))
        {
            landingPosition = currentHit.position;
        }

        CombatKnockupMotion knockupMotion = unit.GetComponent<CombatKnockupMotion>();
        if (knockupMotion == null)
        {
            knockupMotion = unit.gameObject.AddComponent<CombatKnockupMotion>();
        }

        float launchHeight = knockupHeight * Mathf.Lerp(0.45f, 1f, falloff);
        knockupMotion.Play(landingPosition, launchHeight, knockupDuration);
    }

    private void Release()
    {
        PoolManager.Instance.Release(gameObject);
    }

    private void SpawnExplosionEffect(Vector3 explosionCenter)
    {
        if (explosionEffectPrefab == null)
        {
            explosionEffectPrefab = LoadDefaultExplosionEffect();
        }

        if (explosionEffectPrefab == null)
        {
            return;
        }

        GameObject effect = PoolManager.Instance.Spawn(explosionEffectPrefab, explosionCenter, Quaternion.identity);
        if (effect == null)
        {
            return;
        }

        ParticleEffectAutoRelease autoRelease = effect.GetComponent<ParticleEffectAutoRelease>();
        if (autoRelease == null)
        {
            autoRelease = effect.AddComponent<ParticleEffectAutoRelease>();
        }

        autoRelease.PlayAndRelease();
    }

    private Vector3 GetTargetPosition()
    {
        if (useFixedTargetPosition)
        {
            return fixedTargetPosition;
        }

        return target != null ? target.transform.position + targetOffset : transform.position;
    }

    private void CacheTravelDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            lastTravelDirection = direction.normalized;
        }
    }

    private static GameObject LoadDefaultExplosionEffect()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Particals/Explosion.prefab");
#else
        return null;
#endif
    }
}
