using UnityEngine;
using UnityEngine.AI;

public abstract class RangedCombatUnitController : BaseCombatUnitController
{
    [Header("Ranged Attack")]
    [SerializeField] private ArrowProjectile _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _projectileSpawnHeight = 1.4f;
    [SerializeField] private int _projectilePoolPrewarmCount = 12;
    [SerializeField] private float _aimHoldAfterTargetLost = 1.5f;
    [SerializeField] private float _faceTargetRotationSpeed = 12f;

    [Header("Ranged Kiting")]
    [SerializeField] private bool _kiteMeleeThreats = true;
    [SerializeField] private float _kiteTriggerDistance = 5f;
    [SerializeField] private float _kiteRetreatDistance = 5f;
    [SerializeField] private float _meleeThreatAttackRange = 3.5f;
    [SerializeField] private LayerMask _combatUnitLayerMask;
    private LayerMask _effectiveCombatUnitLayerMask;
    [SerializeField] private float _kiteScanInterval = 0.15f;

    private float _nextKiteScanTime;
    private static readonly Collider[] s_kiteOverlapCache = new Collider[128];
    private static readonly Unity.Profiling.ProfilerMarker s_kiteScanMarker = new Unity.Profiling.ProfilerMarker("RTS.Ranged.KiteScan");

    private bool _isHoldingAimAfterTargetLost;
    private float _aimHoldUntil;
    private bool _isKiting;

    protected virtual bool UsesArrowProjectile => true;
    protected virtual bool CanStartKiting() => true;

    private void ResolveCombatUnitLayerMask()
    {
        if (_combatUnitLayerMask.value != 0)
        {
            _effectiveCombatUnitLayerMask = _combatUnitLayerMask;
        }
        else
        {
            _effectiveCombatUnitLayerMask = LayerMask.GetMask("Unit", "Unit Enemy");
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_effectiveCombatUnitLayerMask.value == 0)
            {
                Debug.LogWarning($"[Ranged] Failed to retrieve layers 'Unit', 'Unit Enemy' for fallback on {gameObject.name}.");
            }
            else
            {
                Debug.LogWarning($"[Ranged] _combatUnitLayerMask is unassigned (0) on {gameObject.name}. Falling back to default Unit/Enemy layers.");
            }
            #endif
        }
    }

    protected override void Start()
    {
        maxAutoChaseDistance = Mathf.Min(maxAutoChaseDistance, 8f);
        _nextKiteScanTime = Time.time + Random.Range(0f, _kiteScanInterval);
        ResolveCombatUnitLayerMask();
        base.Start();

        if (!UsesArrowProjectile)
        {
            return;
        }

        if (_projectilePrefab == null)
        {
            _projectilePrefab = LoadDefaultArrowProjectile();
        }

        if (_projectilePrefab != null && _projectilePoolPrewarmCount > 0)
        {
            PoolManager.Instance.Prewarm(_projectilePrefab.gameObject, _projectilePoolPrewarmCount);
        }
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;
        if (isStunned) return;

        if (IsKnockupActive())
        {
            _isKiting = false;
            _isHoldingAimAfterTargetLost = false;
            SetAimAnimatorBool(false);
            UpdateKnockupAnimationState();
            return;
        }

        if (IsPostKnockupRecovering())
        {
            _isKiting = false;
            _isHoldingAimAfterTargetLost = false;
            SetAimAnimatorBool(false);
            UpdatePostKnockupRecoveryAnimationState();
            return;
        }

        if (_isKiting)
        {
            if (HasFinishedKiting())
            {
                _isKiting = false;
                if (currentTarget != null && currentTarget.currentState != CombatState.Dead && currentTarget.gameObject.activeInHierarchy)
                {
                    AttackTarget(currentTarget);
                }
            }
            else
            {
                UpdateAvoidancePriority();
                UpdateAnimationState();
                return;
            }
        }

        if (!_isKiting && CanStartKiting() && _kiteMeleeThreats && (currentState == CombatState.Chasing || currentState == CombatState.Attacking))
        {
            if (TryKiteMeleeThreat())
            {
                return;
            }
        }

        base.Update();
    }

    protected override void PerformAttack()
    {
        lastAttackTime = Time.time;
        _isHoldingAimAfterTargetLost = false;

        if (animator != null)
        {
            SetAnimatorTriggerIfExists("Attack");
        }

        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            return;
        }

        FaceCurrentTarget(_faceTargetRotationSpeed);

        if (_projectilePrefab == null)
        {
            currentTarget.TakeDamage(attackDamage);
            return;
        }

        BaseCombatUnitController attackTarget = currentTarget;
        Vector3 targetPosition = attackTarget.transform.position;
        Vector3 firePosition = _firePoint != null ? _firePoint.position : transform.position + Vector3.up * _projectileSpawnHeight;
        ArrowProjectile projectile = PoolManager.Instance.Spawn(_projectilePrefab, firePosition, Quaternion.identity);
        if (projectile != null)
        {
            projectile.LaunchAtPosition(attackTarget, targetPosition, attackDamage);
        }
    }

    protected override void HandleAttackingState()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead || !currentTarget.gameObject.activeInHierarchy)
        {
            BaseCombatUnitController nextTarget = ScanForNearestEnemy();
            if (nextTarget != null)
            {
                _isHoldingAimAfterTargetLost = false;
                AttackTarget(nextTarget);
                return;
            }

            HoldAimAfterTargetLost();
            return;
        }

        _isHoldingAimAfterTargetLost = false;
        base.HandleAttackingState();
    }

    public override void CommandMove(Vector3 position)
    {
        _isHoldingAimAfterTargetLost = false;
        _isKiting = false;
        base.CommandMove(position);

        if (animator != null)
        {
            SetAimAnimatorBool(false);
        }
    }

    private bool TryKiteMeleeThreat()
    {
        BaseCombatUnitController meleeThreat = FindNearestMeleeThreat();
        if (meleeThreat == null)
        {
            return false;
        }

        Vector3 escapeDirection = transform.position - meleeThreat.transform.position;
        escapeDirection.y = 0f;
        if (escapeDirection.sqrMagnitude <= 0.01f)
        {
            escapeDirection = -transform.forward;
        }

        Vector3 targetKitePosition = transform.position + escapeDirection.normalized * Mathf.Max(0.5f, _kiteRetreatDistance);
        if (!NavMesh.SamplePosition(targetKitePosition, out NavMeshHit hit, 3f, ~2))
        {
            return false;
        }

        if (navAgent == null || !navAgent.enabled)
        {
            return false;
        }

        currentTarget = meleeThreat;
        _isHoldingAimAfterTargetLost = false;
        _isKiting = true;
        isManualMoveCommand = false;
        navAgent.isStopped = false;
        navAgent.stoppingDistance = 0.2f;
        navAgent.SetDestination(hit.position);
        ChangeState(CombatState.Moving);
        SetAimAnimatorBool(false);
        return true;
    }

    private BaseCombatUnitController FindNearestMeleeThreat()
    {
        if (Time.time < _nextKiteScanTime)
        {
            return null;
        }

        _nextKiteScanTime = Time.time + _kiteScanInterval + Random.Range(0f, 0.05f);

        using (s_kiteScanMarker.Auto())
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnitPerformanceMetrics.KiteScanCount++;
            #endif

            int count = Physics.OverlapSphereNonAlloc(transform.position, _kiteTriggerDistance, s_kiteOverlapCache, _effectiveCombatUnitLayerMask, QueryTriggerInteraction.Collide);

            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (count >= s_kiteOverlapCache.Length)
            {
                UnitPerformanceMetrics.BufferOverflowCount++;
            }
            #endif

            BaseCombatUnitController nearestThreat = null;
            float nearestSqrDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = s_kiteOverlapCache[i];
                if (col == null) continue;

                BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
                if (unit == null
                    || unit == this
                    || unit.currentState == CombatState.Dead
                    || unit.faction == faction
                    || unit.attackRange > _meleeThreatAttackRange
                    || !unit.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float sqrDistance = (unit.transform.position - transform.position).sqrMagnitude;
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestThreat = unit;
                    nearestSqrDistance = sqrDistance;
                }
            }

            return nearestThreat;
        }
    }

    private bool HasFinishedKiting()
    {
        return navAgent == null
            || !navAgent.enabled
            || (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
            || (navAgent.velocity.sqrMagnitude <= 0.01f && !navAgent.pathPending);
    }

    protected override void UpdateAnimationState()
    {
        base.UpdateAnimationState();

        if (animator == null || currentState == CombatState.Dead)
        {
            return;
        }

        if (HasAimAnimatorParameter())
        {
            bool shouldAim = ShouldUseAimAnimation();
            SetAimAnimatorBool(shouldAim);

            if (shouldAim)
            {
                SetAnimatorBoolIfExists("IsMoving", false);
                FaceCurrentTarget(_faceTargetRotationSpeed);
            }

            if (_isHoldingAimAfterTargetLost)
            {
                SetAnimatorBoolIfExists("IsAttacking", false);
            }
        }
    }

    private void HoldAimAfterTargetLost()
    {
        if (!_isHoldingAimAfterTargetLost)
        {
            _isHoldingAimAfterTargetLost = true;
            _aimHoldUntil = Time.time + Mathf.Max(0f, _aimHoldAfterTargetLost);
            currentTarget = null;

            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
        }

        if (Time.time < _aimHoldUntil)
        {
            return;
        }

        _isHoldingAimAfterTargetLost = false;
        ClearCurrentTargetAndIdle();
    }

    private bool ShouldUseAimAnimation()
    {
        return _isHoldingAimAfterTargetLost || currentState == CombatState.Attacking;
    }

    private void FaceCurrentTarget(float rotationSpeed)
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead || !currentTarget.gameObject.activeInHierarchy)
        {
            return;
        }

        Vector3 direction = currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            return;
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction.normalized), Time.deltaTime * rotationSpeed);
    }

    private bool HasAimAnimatorParameter()
    {
        return HasAnimatorParameter("isAiming", AnimatorControllerParameterType.Bool)
            || HasAnimatorParameter("IsAiming", AnimatorControllerParameterType.Bool);
    }

    private void SetAimAnimatorBool(bool value)
    {
        SetAnimatorBoolIfExists("isAiming", value);
        SetAnimatorBoolIfExists("IsAiming", value);
    }

    private static ArrowProjectile LoadDefaultArrowProjectile()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<ArrowProjectile>("Assets/Prefabs/Projectile/ArrowProjectile.prefab");
#else
        return null;
#endif
    }
}
