using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class SummonedEnemyRiseController : MonoBehaviour
{
    [SerializeField] private float undergroundOffset = 1.2f;
    [SerializeField] private string riseTriggerName = "Rise";
    [SerializeField] private string riseStateName = "Rise";
    [SerializeField] private string isRisingParameterName = "IsRising";
    [SerializeField] private bool waitForRiseAnimationEvent = true;
    [SerializeField] private float riseEventFallbackDuration = 4f;

    private Coroutine riseCoroutine;
    private NavMeshAgent navAgent;
    private Animator animator;
    private BaseCombatUnitController combatUnit;
    private Rigidbody unitRigidbody;
    private bool riseAnimationFinished;

    public bool IsRising { get; private set; }

    public void PlayRise(float duration)
    {
        if (riseCoroutine != null)
        {
            StopCoroutine(riseCoroutine);
        }

        riseCoroutine = StartCoroutine(RiseRoutine(Mathf.Max(0.05f, duration)));
    }

    private IEnumerator RiseRoutine(float duration)
    {
        IsRising = true;
        riseAnimationFinished = false;
        navAgent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        combatUnit = GetComponent<BaseCombatUnitController>();
        unitRigidbody = GetComponent<Rigidbody>();

        bool restoreCombatController = combatUnit != null && combatUnit.enabled;
        if (combatUnit != null)
        {
            combatUnit.ChangeState(CombatState.Idle);
            combatUnit.enabled = false;
        }

        bool restoreAgent = navAgent != null && navAgent.enabled;
        bool restoreStopped = false;
        if (restoreAgent && navAgent.isOnNavMesh)
        {
            restoreStopped = navAgent.isStopped;
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        bool restoreRigidbodyKinematic = false;
        bool restoreRigidbodyGravity = false;
        RigidbodyConstraints restoreRigidbodyConstraints = RigidbodyConstraints.None;
        if (unitRigidbody != null)
        {
            restoreRigidbodyKinematic = unitRigidbody.isKinematic;
            restoreRigidbodyGravity = unitRigidbody.useGravity;
            restoreRigidbodyConstraints = unitRigidbody.constraints;
            unitRigidbody.linearVelocity = Vector3.zero;
            unitRigidbody.angularVelocity = Vector3.zero;
            unitRigidbody.useGravity = false;
            unitRigidbody.isKinematic = true;
            unitRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }

        if (animator != null)
        {
            SetAnimatorBoolIfExists(isRisingParameterName, true);
            SetAnimatorBoolIfExists("IsIdle", false);
            SetAnimatorBoolIfExists("IsMoving", false);
            SetAnimatorBoolIfExists("IsAttacking", false);
            SetAnimatorTriggerIfExists(riseTriggerName);
            PlayRiseStateIfExists();
        }

        Vector3 finalPosition = transform.position;
        Vector3 startPosition = finalPosition + Vector3.down * undergroundOffset;
        transform.position = startPosition;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(startPosition, finalPosition, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        transform.position = finalPosition;

        if (waitForRiseAnimationEvent)
        {
            float waitElapsed = 0f;
            float fallbackDuration = Mathf.Max(0.1f, riseEventFallbackDuration);
            while (!riseAnimationFinished && waitElapsed < fallbackDuration)
            {
                waitElapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (navAgent != null)
        {
            navAgent.enabled = restoreAgent;
            if (restoreAgent && NavMesh.SamplePosition(finalPosition, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
                if (navAgent.isOnNavMesh)
                {
                    navAgent.Warp(hit.position);
                    navAgent.ResetPath();
                    navAgent.velocity = Vector3.zero;
                    navAgent.isStopped = true;
                }
            }
        }

        if (unitRigidbody != null)
        {
            unitRigidbody.linearVelocity = Vector3.zero;
            unitRigidbody.angularVelocity = Vector3.zero;
            unitRigidbody.constraints = restoreRigidbodyConstraints;
            unitRigidbody.isKinematic = restoreRigidbodyKinematic;
            unitRigidbody.useGravity = restoreRigidbodyGravity;
        }

        if (animator != null)
        {
            SetAnimatorBoolIfExists(isRisingParameterName, false);
        }

        IsRising = false;
        riseAnimationFinished = false;
        if (combatUnit != null)
        {
            combatUnit.enabled = restoreCombatController;
            combatUnit.ChangeState(CombatState.Idle);
        }

        riseCoroutine = null;
    }

    public void OnRiseAnimationFinished()
    {
        riseAnimationFinished = true;
    }

    public void FinishRise()
    {
        OnRiseAnimationFinished();
    }

    private void SetAnimatorBoolIfExists(string parameterName, bool value)
    {
        if (animator == null)
        {
            return;
        }

        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool(parameterName, value);
                return;
            }
        }
    }

    private void SetAnimatorTriggerIfExists(string parameterName)
    {
        if (animator == null)
        {
            return;
        }

        for (int i = 0; i < animator.parameterCount; i++)
        {
            AnimatorControllerParameter parameter = animator.parameters[i];
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Trigger)
            {
                animator.SetTrigger(parameterName);
                return;
            }
        }
    }

    private void PlayRiseStateIfExists()
    {
        if (animator == null || string.IsNullOrEmpty(riseStateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(riseStateName);
        if (animator.HasState(0, stateHash))
        {
            animator.CrossFadeInFixedTime(stateHash, 0.05f, 0, 0f);
            animator.Update(0f);
        }
    }
}
