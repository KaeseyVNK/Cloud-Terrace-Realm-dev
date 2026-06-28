using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public class WatchTowerGarrison : MonoBehaviour
{
    public static readonly List<WatchTowerGarrison> Registry = new List<WatchTowerGarrison>();

    [Header("Garrison")]
    [SerializeField] private int capacity = 4;
    [SerializeField] private Transform entryPoint;
    [SerializeField] private float enterDistance = 1.2f;
    [SerializeField] private float entryNavMeshSampleRadius = 4f;
    [SerializeField] private float incomingReservationTimeout = 8f;
    [SerializeField] private float ejectSpacing = 1.25f;
    

    [Header("Vision")]
    [SerializeField] private float visionRadius = 32f;

    [Header("Attack")]
    [SerializeField] private float attackRange = 24f;
    [SerializeField] private int damagePerArrow = 8;
    [SerializeField] private float attackCooldown = 1.25f;
    [SerializeField] private float arrowBurstInterval = 0.2f;
    [SerializeField] private ArrowProjectile arrowPrefab;
    [SerializeField] private int arrowPoolPrewarmCount = 16;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float arrowSpreadRadius = 0.25f;
    [SerializeField] private LayerMask targetLayers = ~0;

    private readonly List<SelectableUnit> garrisonedUnits = new List<SelectableUnit>();
    private readonly List<SelectableUnit> incomingUnits = new List<SelectableUnit>();
    private readonly Dictionary<SelectableUnit, Vector3> incomingEntryPositions = new Dictionary<SelectableUnit, Vector3>();
    private readonly Dictionary<SelectableUnit, float> incomingReservationTimes = new Dictionary<SelectableUnit, float>();
    private ConstructibleBuilding constructibleBuilding;
    private float lastAttackTime;
    private Coroutine attackBurstCoroutine;
    private static readonly Collider[] s_overlapCache = new Collider[128];

    public int Capacity => capacity;
    public int OccupantCount => garrisonedUnits.Count;
    public bool HasSpace => GetReservedSlotCount() < capacity;
    public float VisionRadius => visionRadius;

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void Awake()
    {
        constructibleBuilding = GetComponent<ConstructibleBuilding>();

        if (arrowPrefab != null && arrowPoolPrewarmCount > 0)
        {
            PoolManager.Instance.Prewarm(arrowPrefab.gameObject, arrowPoolPrewarmCount);
        }
    }

    private void Update()
    {
        if (!IsOperational())
        {
            return;
        }

        ProcessIncomingUnits();
        TryAttack();
    }

    public bool TrySendToGarrison(SelectableUnit unit)
    {
        if (unit == null || !IsOperational() || !HasSpace || garrisonedUnits.Contains(unit) || incomingUnits.Contains(unit))
        {
            return false;
        }

        Vector3 destination = GetReachableEntryPosition();
        VillagerController villager = unit.GetComponent<VillagerController>();
        if (villager != null)
        {
            villager.CommandMoveTo(destination);
        }
        else
        {
            BaseCombatUnitController combatUnit = unit.GetComponent<BaseCombatUnitController>();
            if (combatUnit == null || combatUnit.faction != UnitFaction.Player)
            {
                return false;
            }

            combatUnit.CommandMove(destination);
        }

        incomingUnits.Add(unit);
        incomingEntryPositions[unit] = destination;
        incomingReservationTimes[unit] = Time.time;
        return true;
    }

    public void CancelReservation(SelectableUnit unit)
    {
        if (unit == null)
        {
            return;
        }

        int index = incomingUnits.IndexOf(unit);
        if (index >= 0)
        {
            incomingUnits.RemoveAt(index);
        }

        incomingEntryPositions.Remove(unit);
        incomingReservationTimes.Remove(unit);
    }

    public bool IsTrackingUnit(SelectableUnit unit)
    {
        return unit != null && (garrisonedUnits.Contains(unit) || incomingUnits.Contains(unit));
    }

    public void EjectAll()
    {
        for (int i = garrisonedUnits.Count - 1; i >= 0; i--)
        {
            EjectUnit(garrisonedUnits[i], i);
        }

        garrisonedUnits.Clear();
        incomingUnits.Clear();
        incomingEntryPositions.Clear();
        incomingReservationTimes.Clear();
    }

    public bool IsOperational()
    {
        if (constructibleBuilding == null)
        {
            constructibleBuilding = GetComponent<ConstructibleBuilding>();
        }

        return constructibleBuilding == null || constructibleBuilding.IsCompleted;
    }

    private Vector3 GetEntryPosition()
    {
        return entryPoint != null ? entryPoint.position : transform.position;
    }

    private Vector3 GetReachableEntryPosition()
    {
        Vector3 entryPosition = GetEntryPosition();
        if (NavMesh.SamplePosition(entryPosition, out NavMeshHit hit, entryNavMeshSampleRadius, ~2))
        {
            return hit.position;
        }

        return entryPosition;
    }

    private void ProcessIncomingUnits()
    {
        Vector3 entryPosition = GetEntryPosition();
        float enterDistanceSqr = enterDistance * enterDistance;

        for (int i = incomingUnits.Count - 1; i >= 0; i--)
        {
            SelectableUnit unit = incomingUnits[i];
            if (unit == null)
            {
                RemoveIncomingAt(i, unit);
                continue;
            }

            if (garrisonedUnits.Contains(unit))
            {
                RemoveIncomingAt(i, unit);
                continue;
            }

            if (incomingReservationTimes.TryGetValue(unit, out float reservedAt) &&
                Time.time > reservedAt + incomingReservationTimeout)
            {
                RemoveIncomingAt(i, unit);
                continue;
            }

            Vector3 targetEntryPosition = entryPosition;
            if (incomingEntryPositions.TryGetValue(unit, out Vector3 reachableEntryPosition))
            {
                targetEntryPosition = reachableEntryPosition;
            }

            if ((unit.transform.position - targetEntryPosition).sqrMagnitude <= enterDistanceSqr)
            {
                RemoveIncomingAt(i, unit);
                GarrisonUnit(unit);
            }
        }
    }

    private int GetReservedSlotCount()
    {
        CleanupInvalidIncomingUnits();
        return garrisonedUnits.Count + incomingUnits.Count;
    }

    private void CleanupInvalidIncomingUnits()
    {
        for (int i = incomingUnits.Count - 1; i >= 0; i--)
        {
            SelectableUnit unit = incomingUnits[i];
            if (unit == null || garrisonedUnits.Contains(unit))
            {
                RemoveIncomingAt(i, unit);
                continue;
            }

            if (incomingReservationTimes.TryGetValue(unit, out float reservedAt) &&
                Time.time > reservedAt + incomingReservationTimeout)
            {
                RemoveIncomingAt(i, unit);
            }
        }
    }

    private void RemoveIncomingAt(int index, SelectableUnit unit)
    {
        incomingUnits.RemoveAt(index);
        if (unit != null)
        {
            incomingEntryPositions.Remove(unit);
            incomingReservationTimes.Remove(unit);
        }
    }

    private void GarrisonUnit(SelectableUnit unit)
    {
        if (unit == null || garrisonedUnits.Contains(unit))
        {
            return;
        }

        garrisonedUnits.Add(unit);
        if (UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.DeselectUnit(unit);
        }

        VillagerController villager = unit.GetComponent<VillagerController>();
        if (villager != null)
        {
            villager.ClearAssignedGarrison();
            villager.ChangeState(VillagerState.Sheltered);
        }

        unit.gameObject.SetActive(false);
    }

    private void EjectUnit(SelectableUnit unit, int index)
    {
        if (unit == null)
        {
            return;
        }

        Vector3 offset = Quaternion.Euler(0f, index * 360f / Mathf.Max(1, capacity), 0f) * Vector3.forward * ejectSpacing;
        Vector3 targetPos = GetEntryPosition() + offset;

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 5f, ~2))
        {
            targetPos = hit.position;
        }

        unit.transform.position = targetPos;
        unit.gameObject.SetActive(true);

        NavMeshAgent agent = unit.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(targetPos);
        }

        VillagerController villager = unit.GetComponent<VillagerController>();
        if (villager != null)
        {
            villager.ResumePostShelterState();
        }
    }

    private void TryAttack()
    {
        int arrowCount = garrisonedUnits.Count;
        if (arrowCount <= 0 || attackBurstCoroutine != null || Time.time < lastAttackTime + attackCooldown)
        {
            return;
        }

        BaseCombatUnitController target = FindNearestEnemy();
        if (target == null)
        {
            return;
        }

        lastAttackTime = Time.time;
        attackBurstCoroutine = StartCoroutine(FireArrowBurst(target, arrowCount));
    }

    private IEnumerator FireArrowBurst(BaseCombatUnitController target, int arrowCount)
    {
        for (int i = 0; i < arrowCount; i++)
        {
            if (target == null || target.currentState == CombatState.Dead)
            {
                break;
            }

            FireArrow(target, i);

            if (i < arrowCount - 1 && arrowBurstInterval > 0f)
            {
                yield return new WaitForSeconds(arrowBurstInterval);
            }
        }

        attackBurstCoroutine = null;
    }

    private void FireArrow(BaseCombatUnitController target, int arrowIndex)
    {
        BaseCombatUnitController myCombatController = GetComponent<BaseCombatUnitController>();

        if (arrowPrefab == null)
        {
            target.TakeDamage(damagePerArrow, myCombatController);
            return;
        }

        Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 3f;
        if (arrowSpreadRadius > 0f)
        {
            float angle = arrowIndex * 137.5f * Mathf.Deg2Rad;
            origin += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * arrowSpreadRadius;
        }

        ArrowProjectile projectile = PoolManager.Instance.Spawn(arrowPrefab, origin, Quaternion.identity);
        if (projectile != null)
        {
            projectile.Launch(target, damagePerArrow, myCombatController);
        }
    }

    private BaseCombatUnitController FindNearestEnemy()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, attackRange + 3.0f, s_overlapCache, targetLayers);
        BaseCombatUnitController nearest = null;
        float nearestDistanceSqr = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapCache[i];
            if (col == null)
            {
                continue;
            }

            BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
            if (unit == null || unit.faction == UnitFaction.Player || unit.currentState == CombatState.Dead)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, unit.transform.position);
            float maxAllowedRange = attackRange;
            if (transform.position.y - unit.transform.position.y >= 1.5f)
            {
                maxAllowedRange += 3.0f; // +1.5 ô (với kích thước ô 2m)
            }

            if (distance <= maxAllowedRange)
            {
                float distanceSqr = distance * distance;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearest = unit;
                }
            }
        }

        System.Array.Clear(s_overlapCache, 0, count);
        return nearest;
    }

    private void OnDisable()
    {
        Registry.Remove(this);
        if (attackBurstCoroutine != null)
        {
            StopCoroutine(attackBurstCoroutine);
            attackBurstCoroutine = null;
        }

        EjectAll();
    }
}
