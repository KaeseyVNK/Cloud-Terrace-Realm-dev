using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyArcherController : EnemyUnitController
{
    [Header("Enemy Archer Ranged Attack")]
    [SerializeField] private ArrowProjectile _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _projectileSpawnHeight = 1.4f;
    [SerializeField] private int _projectilePoolPrewarmCount = 12;
    [SerializeField] private float _aimHoldAfterTargetLost = 1.5f;
    [SerializeField] private float _faceTargetRotationSpeed = 12f;

    [Header("Enemy Archer Kiting")]
    [SerializeField] private bool _kiteMeleeThreats = true;
    [SerializeField] private float _kiteTriggerDistance = 5f;
    [SerializeField] private float _kiteRetreatDistance = 5f;
    [SerializeField] private float _meleeThreatAttackRange = 3.5f;

    private bool _isHoldingAimAfterTargetLost;
    private float _aimHoldUntil;

    // New AI improvement fields for kiting
    private bool _isKiting = false;

    public EnemyArcherController()
    {
        faction = UnitFaction.Enemy;
        unitName = "Enemy Archer";
        attackRange = 12f;
        scanRange = 16f;
        attackDamage = 9;
        attackCooldown = 1.6f;
    }

    protected override void Start()
    {
        base.Start();

        if (_projectilePrefab == null)
        {
            _projectilePrefab = LoadDefaultArrowProjectile();
        }

        if (_projectilePrefab != null && _projectilePoolPrewarmCount > 0)
        {
            PoolManager.Instance.Prewarm(_projectilePrefab.gameObject, _projectilePoolPrewarmCount);
        }
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

        Vector3 firePosition = _firePoint != null ? _firePoint.position : transform.position + Vector3.up * _projectileSpawnHeight;
        ArrowProjectile projectile = PoolManager.Instance.Spawn(_projectilePrefab, firePosition, Quaternion.identity);
        if (projectile != null)
        {
            projectile.Launch(currentTarget, attackDamage);
        }
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;
        if (isStunned) return;

        if (_isKiting)
        {
            if (navAgent == null || !navAgent.enabled || (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance) || (navAgent.velocity.sqrMagnitude == 0f && !navAgent.pathPending))
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

        // Try kiting if not already kiting, and we are in chasing or attacking state
        if (!_isKiting && _kiteMeleeThreats && (currentState == CombatState.Chasing || currentState == CombatState.Attacking))
        {
            if (TryKiteMeleeEnemy())
            {
                return;
            }
        }

        base.Update();
    }

    private bool TryKiteMeleeEnemy()
    {
        if (currentTarget != null && currentTarget.currentState != CombatState.Dead)
        {
            // Check if target is melee (short attack range)
            bool isTargetMelee = currentTarget.attackRange <= _meleeThreatAttackRange;
            if (isTargetMelee && GetDistanceToTarget(currentTarget) < _kiteTriggerDistance)
            {
                Vector3 escapeDir = transform.position - currentTarget.transform.position;
                escapeDir.y = 0f;
                if (escapeDir.sqrMagnitude <= 0.01f)
                {
                    escapeDir = -transform.forward;
                }

                Vector3 targetKitePos = transform.position + escapeDir.normalized * Mathf.Max(0.5f, _kiteRetreatDistance);
                if (UnityEngine.AI.NavMesh.SamplePosition(targetKitePos, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    if (navAgent != null && navAgent.enabled)
                    {
                        navAgent.isStopped = false;
                        navAgent.stoppingDistance = 0.2f;
                        navAgent.SetDestination(hit.position);
                        _isKiting = true;
                        isManualMoveCommand = false;
                        _isHoldingAimAfterTargetLost = false;
                        ChangeState(CombatState.Moving);
                        return true;
                    }
                }
            }
        }
        return false;
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

    protected override int GetTargetPriority(BaseCombatUnitController unit)
    {
        if (unit == null) return 99;
        if (IsBloodMoonActive())
        {
            if (IsWatchTowerTarget(unit)) return 1;
            if (!IsVillagerTarget(unit) && !IsBuildingTarget(unit)) return 2;
            if (IsMainBuildingTarget(unit)) return 3;
            if (IsVillagerTarget(unit)) return 4;
            return 5;
        }

        if (!IsVillagerTarget(unit) && !IsBuildingTarget(unit)) return 1;
        if (IsVillagerTarget(unit)) return 2;
        if (unit.GetComponent<WatchTowerGarrison>() != null) return 2;
        return 4;
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
