using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class SkeletonMageController : EnemyUnitController, IPoolable
{
    [Header("Magic Attack")]
    [SerializeField] private MagicProjectile magicProjectilePrefab;
    [SerializeField] private Transform castPoint;
    [SerializeField] private float projectileSpawnHeight = 1.45f;
    [SerializeField] private int projectilePoolPrewarmCount = 8;

    [Header("Summoning")]
    [SerializeField] private GameObject normalEnemyPrefab;
    [SerializeField] private GameObject shieldEnemyPrefab;
    [SerializeField] private int normalEnemyCount = 2;
    [SerializeField] private int shieldEnemyCount = 1;
    [SerializeField] private float summonInterval = 30f;
    [SerializeField] private float summonRadius = 2.2f;
    [SerializeField] private float summonWindupDuration = 1.1f;
    [SerializeField] private float summonedRiseDuration = 1.1f;
    [SerializeField] private string summonTriggerName = "Summon";
    [SerializeField] private string isSummoningParameterName = "IsSummoning";
    [SerializeField] private int fallbackMaxActiveSummonedEnemies = 18;

    [Header("Threat Response")]
    [SerializeField] private bool kiteMeleeThreats = true;
    [SerializeField] private float kiteTriggerDistance = 5f;
    [SerializeField] private float kiteRetreatDistance = 4.5f;
    [SerializeField] private float meleeThreatAttackRange = 3.5f;

    private readonly List<SummonedEnemyRiseController> activeSummons = new List<SummonedEnemyRiseController>(4);
    private bool isSummoning;
    private bool isKiting;
    private float nextSummonTime;
    private Coroutine summonCoroutine;
    private Rigidbody mageRigidbody;
    private bool summonLockActive;
    private bool restoreRigidbodyKinematic;
    private bool restoreRigidbodyGravity;
    private RigidbodyConstraints restoreRigidbodyConstraints;
    // Cooldown chống kite loop khi bị block bởi NavMesh obstacle
    private float _nextKiteAllowedTime = 0f;
    private const float KiteCooldown = 0.8f;

    public bool IsSummonMovementLocked => summonLockActive || isSummoning;

    public SkeletonMageController()
    {
        unitName = "Skeleton Mage";
        maxHealth = 90;
        attackDamage = 14;
        attackRange = 8f;
        scanRange = 14f;
        attackCooldown = 2.2f;
        faction = UnitFaction.Enemy;
    }

    protected override void Start()
    {
        LoadDefaultSummonPrefabsIfNeeded();
        LoadDefaultMagicProjectileIfNeeded();
        PrewarmMagicProjectiles();
        nextSummonTime = Time.time + summonInterval;
        base.Start();
    }

    public new void OnSpawnedFromPool()
    {
        base.OnSpawnedFromPool();
        LoadDefaultSummonPrefabsIfNeeded();
        LoadDefaultMagicProjectileIfNeeded();
        StopSummoning();
        nextSummonTime = Time.time + summonInterval;
    }

    public new void OnReturnedToPool()
    {
        StopSummoning();
        base.OnReturnedToPool();
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead)
        {
            isKiting = false;
            StopSummoning();
            return;
        }

        if (isStunned)
        {
            if (isKiting)
            {
                isKiting = false;
                ClearCurrentTargetAndIdle();
            }
            base.Update();
            return;
        }

        if (isSummoning)
        {
            isKiting = false;
            HoldSummonAnimationState();
            return;
        }

        if (isKiting)
        {
            if (HasFinishedKiting())
            {
                isKiting = false;
                _nextKiteAllowedTime = Time.time + KiteCooldown;
                if (currentTarget != null && currentTarget.currentState != CombatState.Dead && currentTarget.gameObject.activeInHierarchy)
                {
                    AttackTarget(currentTarget);
                }
                else
                {
                    ClearCurrentTargetAndIdle();
                }
                // Sau khi kết thúc kite, gọi base.Update() để state machine chạy ngay frame này
                base.Update();
            }
            else
            {
                UpdateAvoidancePriority();
                UpdateAnimationState();
            }
            return;
        }

        if (!isKiting && kiteMeleeThreats && Time.time >= _nextKiteAllowedTime &&
            (currentState == CombatState.Chasing || currentState == CombatState.Attacking))
        {
            if (TryKiteMeleeThreat())
            {
                return;
            }
        }

        if (!isStunned && Time.time >= nextSummonTime && CanStartSummon() &&
            (currentState == CombatState.Chasing || currentState == CombatState.Attacking))
        {
            summonCoroutine = StartCoroutine(SummonRoutine());
            return;
        }

        base.Update();
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

        BaseCombatUnitController attackTarget = currentTarget;
        Vector3 castTargetPosition = attackTarget.transform.position;

        if (magicProjectilePrefab == null || PoolManager.Instance == null)
        {
            attackTarget.TakeDamage(attackDamage, this);
            return;
        }

        Vector3 spawnPosition = castPoint != null ? castPoint.position : transform.position + Vector3.up * projectileSpawnHeight;
        MagicProjectile projectile = PoolManager.Instance.Spawn(magicProjectilePrefab, spawnPosition, Quaternion.identity);
        if (projectile != null)
        {
            projectile.LaunchAtPosition(attackTarget, castTargetPosition, attackDamage, this);
        }
    }

    private IEnumerator SummonRoutine()
    {
        isSummoning = true;
        nextSummonTime = Time.time + summonInterval;
        currentTarget = null;
        activeSummons.Clear();
        BeginSummonMovementLock();

        if (animator != null)
        {
            SetAnimatorTriggerIfExists(summonTriggerName);
            SetAnimatorBoolIfExists(isSummoningParameterName, true);
            SetAnimatorBoolIfExists("IsIdle", false);
            SetAnimatorBoolIfExists("IsMoving", false);
            SetAnimatorBoolIfExists("IsAttacking", false);
        }

        yield return new WaitForSeconds(Mathf.Max(0f, summonWindupDuration));

        SpawnSummonGroup();

        float waitUntil = Time.time + Mathf.Max(0.1f, summonedRiseDuration) + 0.25f;
        while (Time.time < waitUntil && HasActiveRisingSummons())
        {
            yield return null;
        }

        FinishSummoning();
    }

    private bool CanStartSummon()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            return false;
        }

        float distance = GetDistanceToTarget(currentTarget);
        if (distance > scanRange)
        {
            return false;
        }

        return (normalEnemyPrefab != null || shieldEnemyPrefab != null) && GetRemainingSummonSlots() > 0;
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

        Vector3 targetKitePosition = transform.position + escapeDirection.normalized * Mathf.Max(0.5f, kiteRetreatDistance);
        if (!NavMesh.SamplePosition(targetKitePosition, out NavMeshHit hit, 3f, ~2))
        {
            return false;
        }

        if (!IsNavAgentReady())
        {
            return false;
        }

        currentTarget = meleeThreat;
        isManualMoveCommand = false;
        isKiting = true;
        navAgent.isStopped = false;
        navAgent.stoppingDistance = 0.2f;
        navAgent.SetDestination(hit.position);
        ChangeState(CombatState.Moving);
        return true;
    }

    private BaseCombatUnitController FindNearestMeleeThreat()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, kiteTriggerDistance, s_overlapCache);
        BaseCombatUnitController nearestThreat = null;
        float nearestSqrDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapCache[i];
            if (col == null) continue;

            BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
            if (unit == null
                || unit == this
                || unit.currentState == CombatState.Dead
                || unit.faction == faction
                || unit.attackRange > meleeThreatAttackRange
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

        System.Array.Clear(s_overlapCache, 0, count);
        return nearestThreat;
    }

    private bool HasFinishedKiting()
    {
        if (navAgent == null || !navAgent.enabled)
        {
            return true;
        }

        // Xong khi đã tới gần đích
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance + 0.05f)
        {
            return true;
        }

        // Xong khi không có path hợp lệ (bị block hoàn toàn bởi NavMesh)
        if (!navAgent.pathPending && !navAgent.hasPath)
        {
            return true;
        }

        return false;
    }

    private void SpawnSummonGroup()
    {
        int remainingSlots = GetRemainingSummonSlots();
        if (remainingSlots <= 0)
        {
            GameLog.Log("[SkeletonMage] Bỏ qua triệu hồi vì đã đạt giới hạn summoned enemies.");
            return;
        }

        int slotIndex = 0;
        for (int i = 0; i < normalEnemyCount; i++)
        {
            if (remainingSlots <= 0)
            {
                return;
            }

            if (SpawnSummonedEnemy(normalEnemyPrefab, slotIndex++))
            {
                remainingSlots--;
            }
        }

        for (int i = 0; i < shieldEnemyCount; i++)
        {
            if (remainingSlots <= 0)
            {
                return;
            }

            if (SpawnSummonedEnemy(shieldEnemyPrefab, slotIndex++))
            {
                remainingSlots--;
            }
        }
    }

    private bool SpawnSummonedEnemy(GameObject prefab, int slotIndex)
    {
        if (prefab == null || PoolManager.Instance == null)
        {
            return false;
        }

        Vector3 spawnPosition = GetSummonPosition(slotIndex);
        GameObject summon = PoolManager.Instance.Spawn(prefab, spawnPosition, Quaternion.LookRotation(transform.forward, Vector3.up));
        if (summon == null)
        {
            return false;
        }

        SummonedEnemyTracker tracker = summon.GetComponent<SummonedEnemyTracker>();
        if (tracker == null)
        {
            tracker = summon.AddComponent<SummonedEnemyTracker>();
        }

        tracker.MarkAsSummoned();

        SummonedEnemyRiseController riseController = summon.GetComponent<SummonedEnemyRiseController>();
        if (riseController == null)
        {
            riseController = summon.AddComponent<SummonedEnemyRiseController>();
        }

        riseController.PlayRise(summonedRiseDuration);
        activeSummons.Add(riseController);
        return true;
    }

    private int GetRemainingSummonSlots()
    {
        int maxActiveSummons = EnemyManager.Instance != null
            ? EnemyManager.Instance.MaxActiveSummonedEnemies
            : fallbackMaxActiveSummonedEnemies;

        return SummonedEnemyTracker.GetRemainingCapacity(maxActiveSummons);
    }

    private Vector3 GetSummonPosition(int slotIndex)
    {
        float angle = slotIndex * Mathf.PI * 2f / Mathf.Max(1, normalEnemyCount + shieldEnemyCount);
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * summonRadius;
        Vector3 desiredPosition = transform.position + offset;

        if (NavMesh.SamplePosition(desiredPosition, out NavMeshHit hit, 3f, ~2))
        {
            return hit.position;
        }

        return transform.position;
    }

    private bool HasActiveRisingSummons()
    {
        for (int i = activeSummons.Count - 1; i >= 0; i--)
        {
            if (activeSummons[i] == null)
            {
                activeSummons.RemoveAt(i);
                continue;
            }

            if (activeSummons[i].IsRising)
            {
                return true;
            }
        }

        return false;
    }

    private void FinishSummoning()
    {
        isSummoning = false;
        summonCoroutine = null;
        activeSummons.Clear();

        if (animator != null)
        {
            SetAnimatorBoolIfExists(isSummoningParameterName, false);
        }

        EndSummonMovementLock();
        ChangeState(CombatState.Idle);
    }

    private void StopSummoning()
    {
        if (summonCoroutine != null)
        {
            StopCoroutine(summonCoroutine);
            summonCoroutine = null;
        }

        isSummoning = false;
        activeSummons.Clear();
        EndSummonMovementLock();

        if (animator != null)
        {
            SetAnimatorBoolIfExists(isSummoningParameterName, false);
        }
    }

    private void HoldSummonAnimationState()
    {
        if (animator == null)
        {
            return;
        }

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        SetAnimatorBoolIfExists(isSummoningParameterName, true);
    }

    private void BeginSummonMovementLock()
    {
        if (summonLockActive)
        {
            return;
        }

        summonLockActive = true;

        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        mageRigidbody = GetComponent<Rigidbody>();
        if (mageRigidbody != null)
        {
            restoreRigidbodyKinematic = mageRigidbody.isKinematic;
            restoreRigidbodyGravity = mageRigidbody.useGravity;
            restoreRigidbodyConstraints = mageRigidbody.constraints;
            if (!mageRigidbody.isKinematic)
            {
                mageRigidbody.linearVelocity = Vector3.zero;
                mageRigidbody.angularVelocity = Vector3.zero;
            }
            mageRigidbody.useGravity = false;
            mageRigidbody.isKinematic = true;
            mageRigidbody.constraints = RigidbodyConstraints.FreezeAll;
        }
    }

    private void EndSummonMovementLock()
    {
        if (!summonLockActive)
        {
            return;
        }

        summonLockActive = false;

        if (IsNavAgentReady())
        {
            Vector3 currentPosition = transform.position;
            if (NavMesh.SamplePosition(currentPosition, out NavMeshHit hit, 2f, ~2))
            {
                transform.position = hit.position;
                navAgent.Warp(hit.position);
            }

            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
            navAgent.isStopped = true;
        }

        if (mageRigidbody != null)
        {
            if (!mageRigidbody.isKinematic)
            {
                mageRigidbody.linearVelocity = Vector3.zero;
                mageRigidbody.angularVelocity = Vector3.zero;
            }
            mageRigidbody.constraints = restoreRigidbodyConstraints;
            mageRigidbody.isKinematic = restoreRigidbodyKinematic;
            mageRigidbody.useGravity = restoreRigidbodyGravity;
        }
    }

    private void LoadDefaultSummonPrefabsIfNeeded()
    {
#if UNITY_EDITOR
        if (normalEnemyPrefab == null)
        {
            normalEnemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Enemy/Skeleton_Warrior.prefab");
        }

        if (shieldEnemyPrefab == null)
        {
            shieldEnemyPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Enemy/EnemyKnight2H.prefab");
        }
#endif
    }

    private void LoadDefaultMagicProjectileIfNeeded()
    {
#if UNITY_EDITOR
        if (magicProjectilePrefab == null)
        {
            magicProjectilePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<MagicProjectile>("Assets/Prefabs/Projectile/MageMagicProjectile.prefab");
        }
#endif
    }

    private void PrewarmMagicProjectiles()
    {
        if (magicProjectilePrefab != null && projectilePoolPrewarmCount > 0 && PoolManager.Instance != null)
        {
            PoolManager.Instance.Prewarm(magicProjectilePrefab.gameObject, projectilePoolPrewarmCount);
        }
    }
}
