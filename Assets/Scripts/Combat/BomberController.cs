using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class BomberController : RangedCombatUnitController, IPoolable
{
    [Header("Grenade Attack")]
    [SerializeField] private GrenadeProjectile _grenadePrefab;
    [SerializeField] private Transform _throwPoint;
    [SerializeField] private float _grenadeSpawnHeight = 1.45f;
    [SerializeField] private int _grenadePoolPrewarmCount = 8;
    [SerializeField] private float _throwFacingRotationSpeed = 12f;
    [SerializeField] private string _throwAnimationStateName = "Attack";
    [SerializeField] private float _throwNormalizedTime = 0.85f;
    [SerializeField] private float _throwEventFallbackDelay = 0.9f;

    private BaseCombatUnitController _queuedThrowTarget;
    private int _queuedThrowDamage;
    private float _queuedThrowExpireTime;

    public BomberController()
    {
        faction = UnitFaction.Player;
        unitName = "Bomer";
        attackRange = 10f;
        scanRange = 13f;
        attackDamage = 24;
        attackCooldown = 2.4f;
    }

    protected override bool UsesArrowProjectile => false;

    protected override void Start()
    {
        returnToPoolOnDeath = true;
        base.Start();

        if (_grenadePrefab == null)
        {
            _grenadePrefab = LoadDefaultGrenadeProjectile();
        }

        if (_grenadePrefab != null && _grenadePoolPrewarmCount > 0)
        {
            PoolManager.Instance.Prewarm(_grenadePrefab.gameObject, _grenadePoolPrewarmCount);
        }
    }

    protected override void PerformAttack()
    {
        lastAttackTime = Time.time;

        if (animator != null)
        {
            SetAnimatorTriggerIfExists("Attack");
        }

        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            return;
        }

        FaceCurrentTarget();
        _queuedThrowTarget = currentTarget;
        _queuedThrowDamage = attackDamage;
        _queuedThrowExpireTime = Time.time + Mathf.Max(0.1f, _throwEventFallbackDelay);
    }

    protected override void Update()
    {
        base.Update();

        if (_queuedThrowTarget == null)
        {
            return;
        }

        if (ShouldThrowFromAnimationProgress() || Time.time >= _queuedThrowExpireTime)
        {
            ThrowGrenade();
        }
    }

    public void ThrowGrenade()
    {
        BaseCombatUnitController throwTarget = _queuedThrowTarget;
        int throwDamage = _queuedThrowDamage;
        _queuedThrowTarget = null;
        _queuedThrowDamage = 0;
        _queuedThrowExpireTime = 0f;

        if (throwTarget == null || throwTarget.currentState == CombatState.Dead)
        {
            return;
        }

        FaceTarget(throwTarget);

        if (_grenadePrefab == null)
        {
            throwTarget.TakeDamage(Mathf.Max(1, throwDamage));
            return;
        }

        Vector3 throwPosition = _throwPoint != null ? _throwPoint.position : transform.position + Vector3.up * _grenadeSpawnHeight;
        GrenadeProjectile projectile = PoolManager.Instance.Spawn(_grenadePrefab, throwPosition, Quaternion.identity);
        if (projectile != null)
        {
            projectile.Launch(throwTarget, faction, Mathf.Max(1, throwDamage));
        }
    }

    public void OnSpawnedFromPool()
    {
        returnToPoolOnDeath = true;
        faction = UnitFaction.Player;
        unitName = string.IsNullOrEmpty(unitName) ? "Bomer" : unitName;
        currentHealth = maxHealth;
        currentTarget = null;
        _queuedThrowTarget = null;
        _queuedThrowDamage = 0;
        _queuedThrowExpireTime = 0f;
        lastAttackTime = 0f;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;

        navAgent = GetComponent<NavMeshAgent>();
        if (navAgent != null)
        {
            if (!navAgent.enabled)
            {
                navAgent.enabled = true;
            }

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }

            navAgent.isStopped = false;
            navAgent.stoppingDistance = 0.2f;
            navAgent.ResetPath();
        }

        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        SelectableUnit selectable = GetComponent<SelectableUnit>();
        if (selectable != null)
        {
            selectable.Deselect();
        }

        ChangeState(CombatState.Idle);
    }

    public void OnReturnedToPool()
    {
        currentTarget = null;
        _queuedThrowTarget = null;
        _queuedThrowDamage = 0;
        _queuedThrowExpireTime = 0f;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;

        SelectableUnit selectable = GetComponent<SelectableUnit>();
        if (selectable != null)
        {
            if (UnitSelectionManager.Instance != null)
            {
                UnitSelectionManager.Instance.DeselectUnit(selectable);
            }

            selectable.Deselect();
        }
    }

    private void FaceCurrentTarget()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead || !currentTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        FaceTarget(currentTarget);
    }

    private void FaceTarget(BaseCombatUnitController target)
    {
        if (target == null || target.currentState == CombatState.Dead || !target.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), Time.deltaTime * _throwFacingRotationSpeed);
    }

    private bool ShouldThrowFromAnimationProgress()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!string.IsNullOrEmpty(_throwAnimationStateName) && !stateInfo.IsName(_throwAnimationStateName))
        {
            return false;
        }

        return !animator.IsInTransition(0) && stateInfo.normalizedTime >= Mathf.Clamp01(_throwNormalizedTime);
    }

    private static GrenadeProjectile LoadDefaultGrenadeProjectile()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GrenadeProjectile>("Assets/Prefabs/Projectile/GrenadeProjectile.prefab");
#else
        return null;
#endif
    }
}
