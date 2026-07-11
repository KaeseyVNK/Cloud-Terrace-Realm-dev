using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class ArcherController : RangedCombatUnitController, IPoolable
{
    public ArcherController()
    {
        faction = UnitFaction.Player;
        unitName = "Archer";
        attackRange = 12f;
        scanRange = 14f;
        attackDamage = 10;
        attackCooldown = 1.4f;
    }

    protected override void Start()
    {
        returnToPoolOnDeath = true;
        faction = UnitFaction.Player;
        base.Start();
    }

    public void OnSpawnedFromPool()
    {
        returnToPoolOnDeath = true;
        faction = UnitFaction.Player;
        unitName = string.IsNullOrEmpty(unitName) ? "Archer" : unitName;
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

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 15f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }

            if (navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
                navAgent.stoppingDistance = 0.2f;
                navAgent.ResetPath();
            }
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
            rb.isKinematic = true;
            rb.useGravity = false;
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
}
