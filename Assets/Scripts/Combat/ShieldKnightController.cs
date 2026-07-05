using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
public class ShieldKnightController : MilitiaController
{
    [Header("Shield Block")]
    [SerializeField, Range(0f, 1f)] private float _blockChance = 0.35f;
    [SerializeField, Range(0f, 180f)] private float _frontBlockAngle = 120f;
    [SerializeField] private float _blockCooldown = 1f;
    [SerializeField] private float _blockVisualDuration = 0.9f;
    [SerializeField] private float _knockbackRange = 3f;
    [SerializeField] private float _knockbackDistance = 1.25f;
    [SerializeField] private float _knockbackDuration = 0.18f;

    private float _nextBlockTime;
    private float _blockVisualTimer;

    public ShieldKnightController()
    {
        faction = UnitFaction.Player;
        unitName = "Shield Knight";
    }

    protected override void Start()
    {
        base.Start();
        unitName = string.IsNullOrEmpty(unitName) || unitName == "Militia" ? "Shield Knight" : unitName;
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;
        if (isStunned) return;

        if (IsKnockupActive())
        {
            UpdateKnockupAnimationState();
            return;
        }

        if (IsPostKnockupRecovering())
        {
            UpdatePostKnockupRecoveryAnimationState();
            return;
        }

        if (_blockVisualTimer > 0f)
        {
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && !navAgent.isStopped)
            {
                navAgent.isStopped = true;
                navAgent.velocity = Vector3.zero;
            }

            UpdateBlockVisual();
            UpdateAnimationState();
            return;
        }

        base.Update();
        UpdateBlockVisual();
    }

    protected override void UpdateAnimationState()
    {
        base.UpdateAnimationState();

        if (_blockVisualTimer > 0f)
        {
            SetAnimatorBoolIfExists("IsIdle", false);
            SetAnimatorBoolIfExists("IsMoving", false);
            SetAnimatorBoolIfExists("IsAttacking", false);
            SetAnimatorBoolIfExists("IsBlocking", true);
        }
    }

    public override void TakeDamage(int damage, BaseCombatUnitController attacker = null)
    {
        if (CanBlockIncomingDamage(damage))
        {
            TriggerBlockFeedback();
            TryKnockbackCurrentTarget();
            GameLog.Log($"[ShieldKnight] {unitName} blocked all {damage} damage.");
            return;
        }

        base.TakeDamage(damage, attacker);
    }

    private bool CanBlockIncomingDamage(int damage)
    {
        if (damage <= 0 || currentState == CombatState.Dead)
        {
            return false;
        }

        if (Time.time < _nextBlockTime || Random.value > _blockChance)
        {
            return false;
        }

        return IsCurrentThreatInFront();
    }

    private bool IsCurrentThreatInFront()
    {
        if (currentTarget == null)
        {
            return true;
        }

        Vector3 directionToThreat = currentTarget.transform.position - transform.position;
        directionToThreat.y = 0f;
        if (directionToThreat.sqrMagnitude <= 0.01f)
        {
            return true;
        }

        float angle = Vector3.Angle(transform.forward, directionToThreat.normalized);
        return angle <= _frontBlockAngle * 0.5f;
    }

    private void TriggerBlockFeedback()
    {
        _nextBlockTime = Time.time + Mathf.Max(0f, _blockCooldown);
        _blockVisualTimer = Mathf.Max(0.05f, _blockVisualDuration);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        SetAnimatorBoolIfExists("IsBlocking", true);
        SetAnimatorTriggerIfExists("Block");
    }

    private void TryKnockbackCurrentTarget()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead || currentTarget.faction == faction)
        {
            return;
        }

        Vector3 direction = currentTarget.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.01f)
        {
            direction = transform.forward;
        }

        if (direction.sqrMagnitude > _knockbackRange * _knockbackRange)
        {
            return;
        }

        StartCoroutine(KnockbackTarget(currentTarget, direction.normalized));
    }

    private IEnumerator KnockbackTarget(BaseCombatUnitController target, Vector3 direction)
    {
        if (target == null || direction.sqrMagnitude <= 0.01f)
        {
            yield break;
        }

        Transform targetTransform = target.transform;
        NavMeshAgent targetAgent = target.GetComponent<NavMeshAgent>();
        Vector3 startPosition = targetTransform.position;
        Vector3 endPosition = startPosition + direction * Mathf.Max(0f, _knockbackDistance);

        if (NavMesh.SamplePosition(endPosition, out NavMeshHit hit, 2f, ~2))
        {
            endPosition = hit.position;
        }

        bool hadAgent = targetAgent != null && targetAgent.enabled;
        bool wasStopped = false;
        if (hadAgent)
        {
            wasStopped = targetAgent.isStopped;
            targetAgent.isStopped = true;
            targetAgent.ResetPath();
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, _knockbackDuration);
        while (elapsed < duration)
        {
            if (target == null || target.currentState == CombatState.Dead)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            Vector3 nextPosition = Vector3.Lerp(startPosition, endPosition, elapsed / duration);
            if (hadAgent && targetAgent.enabled)
            {
                targetAgent.Warp(nextPosition);
            }
            else
            {
                targetTransform.position = nextPosition;
            }

            yield return null;
        }

        if (hadAgent && targetAgent != null && targetAgent.enabled)
        {
            targetAgent.Warp(endPosition);
            targetAgent.isStopped = wasStopped;
        }
    }

    private void UpdateBlockVisual()
    {
        if (_blockVisualTimer <= 0f)
        {
            return;
        }

        _blockVisualTimer -= Time.deltaTime;
        if (_blockVisualTimer <= 0f)
        {
            SetAnimatorBoolIfExists("IsBlocking", false);

            if (currentState != CombatState.Dead && !isStunned && navAgent != null && navAgent.enabled)
            {
                if (currentState == CombatState.Moving || currentState == CombatState.Chasing)
                {
                    navAgent.isStopped = false;
                }
            }
        }
    }

    public override void CommandMove(Vector3 position)
    {
        CancelBlock();
        base.CommandMove(position);
    }

    public override void CommandAttack(BaseCombatUnitController target)
    {
        CancelBlock();
        base.CommandAttack(target);
    }

    private void CancelBlock()
    {
        if (_blockVisualTimer > 0f)
        {
            _blockVisualTimer = 0f;
            SetAnimatorBoolIfExists("IsBlocking", false);
        }
    }
}
