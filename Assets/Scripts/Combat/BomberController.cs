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

    [Header("Bomber Kiting")]
    [SerializeField] private bool _retreatAfterThrowWhenThreatened = true;
    [SerializeField] private float _postThrowRetreatTriggerDistance = 5f;
    [SerializeField] private float _postThrowRetreatDistance = 4.5f;
    [SerializeField] private float _postThrowMeleeThreatAttackRange = 3.5f;

    private BaseCombatUnitController _queuedThrowTarget;
    private Vector3 _queuedThrowTargetPosition;
    private int _queuedThrowDamage;
    private float _queuedThrowExpireTime;
    private bool _hasQueuedThrow;

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
    protected override bool CanStartKiting() => !_hasQueuedThrow;

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
        _queuedThrowTargetPosition = currentTarget.transform.position;
        _queuedThrowDamage = attackDamage;
        _queuedThrowExpireTime = Time.time + Mathf.Max(0.1f, _throwEventFallbackDelay);
        _hasQueuedThrow = true;
    }

    protected override void Update()
    {
        base.Update();

        if (!_hasQueuedThrow)
        {
            return;
        }

        if (!CanReleaseQueuedThrow())
        {
            ClearQueuedThrow();
            return;
        }

        if (ShouldThrowFromAnimationProgress() || Time.time >= _queuedThrowExpireTime)
        {
            ThrowGrenade();
        }
    }

    protected override void HandleAttackingState()
    {
        if (!_hasQueuedThrow)
        {
            base.HandleAttackingState();
            return;
        }

        if (currentState == CombatState.Dead || isStunned)
        {
            ClearQueuedThrow();
            return;
        }

        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        FaceTarget(_queuedThrowTarget);
    }

    public void ThrowGrenade()
    {
        if (!CanReleaseQueuedThrow())
        {
            ClearQueuedThrow();
            return;
        }

        BaseCombatUnitController throwTarget = _queuedThrowTarget;
        Vector3 throwTargetPosition = _queuedThrowTargetPosition;
        int throwDamage = _queuedThrowDamage;
        ClearQueuedThrow();

        FaceTarget(throwTarget);

        if (_grenadePrefab == null)
        {
            if (throwTarget != null && throwTarget.currentState != CombatState.Dead)
            {
                throwTarget.TakeDamage(Mathf.Max(1, throwDamage));
            }

            return;
        }

        Vector3 throwPosition = _throwPoint != null ? _throwPoint.position : transform.position + Vector3.up * _grenadeSpawnHeight;
        GrenadeProjectile projectile = PoolManager.Instance.Spawn(_grenadePrefab, throwPosition, Quaternion.identity);
        if (projectile != null)
        {
            projectile.LaunchAtPosition(throwTargetPosition, faction, Mathf.Max(1, throwDamage));
        }

        TryRetreatAfterThrow(throwTarget);
    }

    public override void CommandMove(Vector3 position)
    {
        ClearQueuedThrow();
        base.CommandMove(position);
    }

    public void OnSpawnedFromPool()
    {
        returnToPoolOnDeath = true;
        faction = UnitFaction.Player;
        unitName = string.IsNullOrEmpty(unitName) ? "Bomer" : unitName;
        currentHealth = maxHealth;
        currentTarget = null;
        ClearQueuedThrow();
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

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, ~2))
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
        ClearQueuedThrow();
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

    private bool CanReleaseQueuedThrow()
    {
        if (currentState != CombatState.Attacking || isStunned)
        {
            return false;
        }

        return true;
    }

    private bool TryRetreatAfterThrow(BaseCombatUnitController threat)
    {
        if (!_retreatAfterThrowWhenThreatened || threat == null || threat.currentState == CombatState.Dead || !threat.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (threat.attackRange > _postThrowMeleeThreatAttackRange || GetDistanceToTarget(threat) > _postThrowRetreatTriggerDistance)
        {
            return false;
        }

        if (!IsNavAgentReady())
        {
            return false;
        }

        Vector3 escapeDirection = transform.position - threat.transform.position;
        escapeDirection.y = 0f;
        if (escapeDirection.sqrMagnitude <= 0.01f)
        {
            escapeDirection = -transform.forward;
        }

        Vector3 retreatPosition = transform.position + escapeDirection.normalized * Mathf.Max(0.5f, _postThrowRetreatDistance);
        if (!NavMesh.SamplePosition(retreatPosition, out NavMeshHit hit, 3f, ~2))
        {
            return false;
        }

        currentTarget = threat;
        isManualMoveCommand = false;
        navAgent.isStopped = false;
        navAgent.stoppingDistance = 0.2f;
        navAgent.SetDestination(hit.position);
        ChangeState(CombatState.Moving);
        SetAnimatorBoolIfExists("isAiming", false);
        SetAnimatorBoolIfExists("IsAiming", false);
        return true;
    }

    private void ClearQueuedThrow()
    {
        _queuedThrowTarget = null;
        _queuedThrowTargetPosition = Vector3.zero;
        _queuedThrowDamage = 0;
        _queuedThrowExpireTime = 0f;
        _hasQueuedThrow = false;
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
