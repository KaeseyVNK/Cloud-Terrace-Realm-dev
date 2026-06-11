using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class MilitiaController : BaseCombatUnitController, IPoolable
{
    public MilitiaController()
    {
        faction = UnitFaction.Player;
        unitName = "Militia";
    }

    protected override void Start()
    {
        returnToPoolOnDeath = true;
        base.Start();
    }

    public void OnSpawnedFromPool()
    {
        returnToPoolOnDeath = true;
        faction = UnitFaction.Player;
        unitName = string.IsNullOrEmpty(unitName) ? "Militia" : unitName;
        currentHealth = maxHealth;
        currentTarget = null;
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

    // Ở đây bạn có thể mở rộng các tính năng đặc thù cho Dân Quân (Militia),
    // ví dụ: Tự động hồi máu khi đứng gần Nhà chính (Town Hall), 
    // tăng tốc độ di chuyển khi có báo động, v.v.
}
