using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyUnitController : BaseCombatUnitController, IPoolable
{
    [Header("Enemy Search")]
    [Tooltip("Search radius used when the enemy has no current target.")]
    [SerializeField] private float _idleSearchRange = 60f;
    [Tooltip("Search radius used while chasing a low-priority target like a building.")]
    [SerializeField] private float _chaseRetargetRange = 12f;
    [SerializeField] private float _enemyIdleScanInterval = 0.3f;
    [SerializeField] private float _enemyMovingScanInterval = 0.25f;
    [SerializeField] private float _enemyRetargetScanInterval = 0.45f;

    private float _repathTimer = 0f;
    private float _nextIdleTargetScanTime = 0f;
    private float _nextMovingTargetScanTime = 0f;
    private float _nextRetargetScanTime = 0f;
    private BaseCombatUnitController _cachedIdleTarget;
    private BaseCombatUnitController _cachedMovingTarget;
    private BaseCombatUnitController _cachedRetargetTarget;
    private const float REPATH_INTERVAL = 2f;

    // New AI improvement fields
    private bool _isWaitingForRally = false;
    private Vector3 _rallyPosition;
    private bool _isRetreating = false;
    private float _retreatTimer = 0f;
    private Vector3 _spawnPosition;

    public EnemyUnitController()
    {
        faction = UnitFaction.Enemy;
        unitName = "Enemy Soldier";
    }

    protected override void Start()
    {
        returnToPoolOnDeath = true;
        base.Start();
    }

    public void OnSpawnedFromPool()
    {
        RestoreBaseStats();
        returnToPoolOnDeath = true;
        faction = UnitFaction.Enemy;
        unitName = string.IsNullOrEmpty(unitName) ? "Enemy Soldier" : unitName;
        currentHealth = maxHealth;
        currentTarget = null;
        lastAttackTime = 0f;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        _repathTimer = 0f;
        _isWaitingForRally = false;
        _isRetreating = false;
        _spawnPosition = transform.position;
        ClearScanCache();

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

        ChangeState(CombatState.Idle);
    }

    public void OnReturnedToPool()
    {
        currentTarget = null;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        _repathTimer = 0f;
        _isWaitingForRally = false;
        _isRetreating = false;
        ClearScanCache();
    }

    public void SetRallyPoint(Vector3 position)
    {
        _rallyPosition = position;
        _isWaitingForRally = true;
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(_rallyPosition);
            ChangeState(CombatState.Moving);
        }
    }

    public void ReleaseRally()
    {
        _isWaitingForRally = false;
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;
        if (isStunned) return;

        // 1. Retreat Check
        if (currentHealth < maxHealth * 0.25f && !_isRetreating)
        {
            int playerUnitsCount = 0;
            int alliedUnitsCount = 0;
            Collider[] colliders = Physics.OverlapSphere(transform.position, 15f);
            foreach (var col in colliders)
            {
                BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
                if (unit != null && unit.currentState != CombatState.Dead)
                {
                    if (unit.faction == this.faction)
                        alliedUnitsCount++;
                    else
                        playerUnitsCount++;
                }
            }
            if (playerUnitsCount > alliedUnitsCount)
            {
                _isRetreating = true;
                _retreatTimer = 5f;
                ClearScanCache();
                currentTarget = null;
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(_spawnPosition);
                    ChangeState(CombatState.Moving);
                }
            }
        }

        if (_isRetreating)
        {
            _retreatTimer -= Time.deltaTime;
            if (_retreatTimer <= 0f)
            {
                _isRetreating = false;
            }
            else
            {
                if (navAgent != null && navAgent.enabled && navAgent.destination != _spawnPosition)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(_spawnPosition);
                    ChangeState(CombatState.Moving);
                }
                UpdateAvoidancePriority();
                UpdateAnimationState();
                return;
            }
        }

        base.Update();
    }

    protected override void HandleIdleState()
    {
        // 1. Quét tìm mục tiêu đối địch (Militia, Villager...) trong tầm quét tự động của BaseCombatUnitController
        BaseCombatUnitController nearestEnemy = GetThrottledPriorityTarget(
            _idleSearchRange,
            _enemyIdleScanInterval,
            ref _nextIdleTargetScanTime,
            ref _cachedIdleTarget);
        if (nearestEnemy != null)
        {
            AttackTarget(nearestEnemy);
            return;
        }

        // 2. Nếu đang chờ tập hợp tại Rally Point
        if (_isWaitingForRally)
        {
            if (navAgent != null && navAgent.enabled)
            {
                if (navAgent.destination != _rallyPosition)
                {
                    navAgent.isStopped = false;
                    navAgent.SetDestination(_rallyPosition);
                    ChangeState(CombatState.Moving);
                }
            }
            return;
        }

        // 3. Tìm mục tiêu hành quân (Target Diversification): Công trình/Nhà lính/Tường gần nhất của Player
        BaseCombatUnitController targetMarchObject = null;
        float minBuildDist = float.MaxValue;
        var allUnits = FindObjectsByType<BaseCombatUnitController>(FindObjectsSortMode.None);
        foreach (var t in allUnits)
        {
            if (t != null && t.currentState != CombatState.Dead && t.faction == UnitFaction.Player && IsBuildingTarget(t))
            {
                float d = Vector3.Distance(transform.position, t.transform.position);
                if (d < minBuildDist)
                {
                    minBuildDist = d;
                    targetMarchObject = t;
                }
            }
        }

        Vector3 targetPos;
        if (targetMarchObject != null)
        {
            targetPos = targetMarchObject.transform.position;
        }
        else
        {
            // Fallback: Tìm nhà chính của người chơi
            BuildingManager buildingMgr = FindAnyObjectByType<BuildingManager>();
            GameObject mainBuilding = buildingMgr != null ? buildingMgr.MainBuildingInstance : null;
            if (mainBuilding != null)
            {
                targetPos = mainBuilding.transform.position;
            }
            else
            {
                return; // Không tìm thấy mục tiêu nào để di chuyển
            }
        }

        // 4. Kiểm tra xem đường đi có bị chặn bởi bức tường/công trình nào không (Obstacle Breaker)
        _repathTimer += Time.deltaTime;
        if (_repathTimer >= REPATH_INTERVAL || !navAgent.hasPath)
        {
            _repathTimer = 0f;
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = false;
                
                // Thử tìm vị trí trên NavMesh gần mục tiêu
                Vector3 destinationPos = targetPos;
                if (UnityEngine.AI.NavMesh.SamplePosition(destinationPos, out UnityEngine.AI.NavMeshHit hit, 30f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    destinationPos = hit.position;
                }

                navAgent.SetDestination(destinationPos);
                ChangeState(CombatState.Moving);

                // Nếu sau khi set path, NavMesh agent báo không có đường đi hoặc đường đi bị chặn (PathPartial)
                if (navAgent.pathStatus == NavMeshPathStatus.PathPartial || !navAgent.hasPath)
                {
                    // Quét các công trình/bức tường cản trở trong phạm vi 15 mét và tấn công phá hủy chúng trước
                    BaseCombatUnitController blockingWall = null;
                    float wallMinDist = float.MaxValue;
                    foreach (var t in allUnits)
                    {
                        if (t != null && t.currentState != CombatState.Dead && t.faction == UnitFaction.Player && IsBuildingTarget(t))
                        {
                            float d = Vector3.Distance(transform.position, t.transform.position);
                            if (d <= 15f && d < wallMinDist)
                            {
                                wallMinDist = d;
                                blockingWall = t;
                            }
                        }
                    }
                    if (blockingWall != null)
                    {
                        AttackTarget(blockingWall);
                        return;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cấu hình di chuyển hành quân của kẻ địch: chủ động quét và tấn công mọi thực thể của người chơi (dân làng, công trình, lính) gặp dọc đường đi.
    /// </summary>
    protected override void HandleMovingState()
    {
        if (navAgent == null) return;

        // Chủ động quét tìm kẻ địch trong phạm vi tối đa scanRange
        BaseCombatUnitController nearestEnemy = GetThrottledPriorityTarget(
            scanRange,
            _enemyMovingScanInterval,
            ref _nextMovingTargetScanTime,
            ref _cachedMovingTarget);
        if (nearestEnemy != null)
        {
            float dist = GetDistanceToTarget(nearestEnemy);
            if (dist <= scanRange)
            {
                AttackTarget(nearestEnemy);
                return;
            }
        }

        // Kiểm tra xem đã đến đích của hành quân chưa (nhà chính)
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
        {
            if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude == 0f)
            {
                ChangeState(CombatState.Idle);
            }
        }
    }

    protected override void HandleChasingState()
    {
        if (TryAcquireNewTargetWhenCurrentTargetIsGone())
        {
            return;
        }

        if (TryRetargetToHigherPriorityEnemy())
        {
            return;
        }

        base.HandleChasingState();
    }

    protected override void HandleAttackingState()
    {
        if (TryAcquireNewTargetWhenCurrentTargetIsGone())
        {
            return;
        }

        base.HandleAttackingState();
    }

    /// <summary>
    /// Dò tìm và quét mục tiêu xung quanh với độ ưu tiên giảm dần: Lính tuần tra/chiến đấu -> Dân làng -> Công trình.
    /// </summary>
    protected override BaseCombatUnitController ScanForNearestEnemy()
    {
        return ScanForNearestEnemy(scanRange);
    }

    private BaseCombatUnitController ScanForNearestEnemy(float searchRange)
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, searchRange);
        BaseCombatUnitController bestTarget = null;
        int bestPriority = int.MaxValue; // Càng nhỏ càng ưu tiên (1: Lính, 2: Dân làng, 3: Công trình)
        float minDistance = float.MaxValue;

        foreach (var col in colliders)
        {
            BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
            if (unit != null && unit.currentState != CombatState.Dead && unit.faction != this.faction)
            {
                int priority = GetTargetPriority(unit);
                float dist = GetDistanceToTarget(unit);

                // Độ ưu tiên cao hơn (priority nhỏ hơn) hoặc cùng ưu tiên nhưng khoảng cách gần hơn
                if (priority < bestPriority)
                {
                    bestPriority = priority;
                    minDistance = dist;
                    bestTarget = unit;
                }
                else if (priority == bestPriority && dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = unit;
                }
            }
        }

        return bestTarget;
    }

    private bool TryRetargetToHigherPriorityEnemy()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            return false;
        }

        int currentPriority = GetTargetPriority(currentTarget);
        BaseCombatUnitController nearbyTarget = GetThrottledPriorityTarget(
            _chaseRetargetRange,
            _enemyRetargetScanInterval,
            ref _nextRetargetScanTime,
            ref _cachedRetargetTarget);
        if (nearbyTarget == null || nearbyTarget == currentTarget)
        {
            return false;
        }

        int nearbyPriority = GetTargetPriority(nearbyTarget);
        if (nearbyPriority >= currentPriority)
        {
            return false;
        }

        AttackTarget(nearbyTarget);
        return true;
    }

    private bool TryAcquireNewTargetWhenCurrentTargetIsGone()
    {
        if (currentTarget != null && currentTarget.currentState != CombatState.Dead && currentTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        currentTarget = null;
        blockedTimer = 0f;
        ClearScanCache();

        BaseCombatUnitController nextTarget = ScanForNearestEnemy(_idleSearchRange);
        if (nextTarget != null)
        {
            AttackTarget(nextTarget);
            return true;
        }

        ChangeState(CombatState.Idle);
        return true;
    }

    private BaseCombatUnitController GetThrottledPriorityTarget(
        float searchRange,
        float interval,
        ref float nextScanTime,
        ref BaseCombatUnitController cachedTarget)
    {
        if (IsValidScanTarget(cachedTarget, searchRange) && Time.time < nextScanTime)
        {
            return cachedTarget;
        }

        if (Time.time < nextScanTime)
        {
            return null;
        }

        cachedTarget = ScanForNearestEnemy(searchRange);
        nextScanTime = Time.time + Mathf.Max(0.05f, interval) + Random.Range(0f, 0.05f);

        if (!IsValidScanTarget(cachedTarget, searchRange))
        {
            cachedTarget = null;
        }

        return cachedTarget;
    }

    private void ClearScanCache()
    {
        _cachedIdleTarget = null;
        _cachedMovingTarget = null;
        _cachedRetargetTarget = null;
        _nextIdleTargetScanTime = 0f;
        _nextMovingTargetScanTime = 0f;
        _nextRetargetScanTime = 0f;
    }

    /// <summary>
    /// Trả về mức độ ưu tiên của mục tiêu. Số nhỏ hơn biểu thị mức độ ưu tiên cao hơn.
    /// </summary>
    private int GetTargetPriority(BaseCombatUnitController unit)
    {
        if (unit == null) return 99;

        bool isVillager = unit.GetComponent<VillagerCombatTarget>() != null || unit.GetComponent<VillagerController>() != null;
        bool isBuilding = IsBuildingTarget(unit);

        // 1. Ưu tiên cao nhất: Lính chiến đấu của người chơi (Militia, v.v.)
        if (!isVillager && !isBuilding)
        {
            return 1;
        }

        // 2. Ưu tiên nhì: Dân làng
        if (isVillager)
        {
            return 2;
        }

        // 3. Ưu tiên cuối: Công trình
        return 3;
    }

    private bool IsBuildingTarget(BaseCombatUnitController unit)
    {
        return unit != null && (unit.GetComponent<BuildingCombatTarget>() != null || unit.GetComponent<MainBuildingCombatTarget>() != null);
    }
}
