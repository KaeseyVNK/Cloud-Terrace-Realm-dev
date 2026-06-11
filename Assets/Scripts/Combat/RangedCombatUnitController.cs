using UnityEngine;

public abstract class RangedCombatUnitController : BaseCombatUnitController
{
    [Header("Ranged Attack")]
    [SerializeField] private ArrowProjectile _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _projectileSpawnHeight = 1.4f;
    [SerializeField] private int _projectilePoolPrewarmCount = 12;
    [SerializeField] private float _aimHoldAfterTargetLost = 1.5f;
    [SerializeField] private float _faceTargetRotationSpeed = 12f;

    private bool _isHoldingAimAfterTargetLost;
    private float _aimHoldUntil;

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
        base.CommandMove(position);

        if (animator != null)
        {
            SetAimAnimatorBool(false);
        }
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
