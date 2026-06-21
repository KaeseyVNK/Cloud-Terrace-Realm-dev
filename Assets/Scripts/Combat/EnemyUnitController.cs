using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum EnemyTargetRole
{
    Assault,
    Raider,
    SiegeBreaker
}

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

    [Header("Enemy Role")]
    [SerializeField] protected EnemyTargetRole _targetRole = EnemyTargetRole.Assault;

    [Header("Blood Moon Scaling")]
    private Vector3 _defaultScale = Vector3.zero;
    private string _originalUnitName = null;
    [SerializeField] private bool _randomizeRoleOnSpawn = true;
    [SerializeField] private float _assaultWeight = 0.6f;
    [SerializeField] private float _raiderWeight = 0.25f;
    [SerializeField] private float _siegeBreakerWeight = 0.15f;

    private void AssignRandomRole()
    {
        if (!_randomizeRoleOnSpawn) return;

        float total = _assaultWeight + _raiderWeight + _siegeBreakerWeight;
        if (total <= 0f) return;

        float rand = Random.Range(0f, total);
        if (rand < _assaultWeight)
        {
            _targetRole = EnemyTargetRole.Assault;
        }
        else if (rand < _assaultWeight + _raiderWeight)
        {
            _targetRole = EnemyTargetRole.Raider;
        }
        else
        {
            _targetRole = EnemyTargetRole.SiegeBreaker;
        }
    }

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
    private float _appliedSpeedMultiplier = 1f;
    public override int TargetPriorityPenalty => _isRetreating ? 100 : 0;

    private static readonly List<BaseCombatUnitController> s_cachedPlayerBuildingTargets = new List<BaseCombatUnitController>(64);
    private static float s_nextPlayerBuildingCacheRefreshTime;
    private const float PLAYER_BUILDING_CACHE_INTERVAL = 1f;

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

        if (_originalUnitName == null)
        {
            _originalUnitName = string.IsNullOrEmpty(unitName) ? "Enemy Soldier" : unitName;
        }

        if (_defaultScale == Vector3.zero)
        {
            _defaultScale = transform.localScale;
        }

        if (IsBloodMoonActive())
        {
            unitName = $"[Mutant] {_originalUnitName}";
            transform.localScale = _defaultScale * 1.35f;
        }
        else
        {
            unitName = _originalUnitName;
            transform.localScale = _defaultScale;
        }

        currentHealth = maxHealth;
        currentTarget = null;
        lastAttackTime = 0f;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        _repathTimer = 0f;
        _isWaitingForRally = false;
        _isRetreating = false;
        _appliedSpeedMultiplier = 1f;
        _spawnPosition = transform.position;
        ClearScanCache();
        AssignRandomRole();

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
            // Ngẫu nhiên hóa avoidancePriority để chúng tự động tránh nhau tốt hơn, không đi hàng một
            navAgent.avoidancePriority = Random.Range(30, 71);
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
        _appliedSpeedMultiplier = 1f;
        ClearScanCache();
    }

    public new void ApplyStatMultipliers(float healthMult, float damageMult, float speedMult)
    {
        base.ApplyStatMultipliers(healthMult, damageMult, speedMult);
        _appliedSpeedMultiplier = speedMult;
    }

    public void SetRallyPoint(Vector3 position)
    {
        // Thêm offset ngẫu nhiên nhỏ quanh rally point để quái không đi thành hàng một
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distOffset = Random.Range(1.0f, 3.5f);
        Vector3 offsetPos = position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distOffset;

        if (NavMesh.SamplePosition(offsetPos, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            offsetPos = hit.position;
        }

        _rallyPosition = offsetPos;
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

    private void UpdateNightSpeedBuff()
    {
        if (navAgent == null || !navAgent.enabled) return;

        bool isNight = TimeManager.Instance != null && TimeManager.Instance.IsNight;
        bool isMarching = (currentState == CombatState.Idle || currentState == CombatState.Moving);

        float normalSpeed = (baseSpeed > 0 ? baseSpeed : 3.5f) * _appliedSpeedMultiplier;
        float targetSpeed = normalSpeed;

        if (isNight && isMarching)
        {
            // Buff 2.2x tốc độ di chuyển ban đêm khi hành quân
            targetSpeed *= 2.2f;
        }

        if (!Mathf.Approximately(navAgent.speed, targetSpeed))
        {
            navAgent.speed = targetSpeed;
        }
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;

        UpdateNightSpeedBuff();

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

        // 1. Retreat Check
        if (currentHealth < maxHealth * 0.25f && !_isRetreating)
        {
            int playerUnitsCount = 0;
            int alliedUnitsCount = 0;
            int count = Physics.OverlapSphereNonAlloc(transform.position, 15f, s_overlapCache);
            for (int i = 0; i < count; i++)
            {
                Collider col = s_overlapCache[i];
                if (col == null) continue;

                BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
                if (unit != null && unit.currentState != CombatState.Dead)
                {
                    if (unit.faction == this.faction)
                        alliedUnitsCount++;
                    else
                        playerUnitsCount++;
                }
            }
            System.Array.Clear(s_overlapCache, 0, count);
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
        List<BaseCombatUnitController> buildingTargets = GetCachedPlayerBuildingTargets();
        foreach (var t in buildingTargets)
        {
            if (t != null && t.currentState != CombatState.Dead)
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
                
                // Thử tìm vị trí trên NavMesh gần mục tiêu, thêm offset ngẫu nhiên để tránh đi hàng một
                Vector3 destinationPos = targetPos;
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distOffset = Random.Range(1.0f, 3.5f);
                destinationPos += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distOffset;

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
                    foreach (var t in buildingTargets)
                    {
                        if (t != null && t.currentState != CombatState.Dead)
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

        float activeMovingScanRange = (_targetRole == EnemyTargetRole.Raider) ? 25f : scanRange;

        // Chủ động quét tìm kẻ địch trong phạm vi tối đa activeMovingScanRange
        BaseCombatUnitController nearestEnemy = GetThrottledPriorityTarget(
            activeMovingScanRange,
            _enemyMovingScanInterval,
            ref _nextMovingTargetScanTime,
            ref _cachedMovingTarget);
        if (nearestEnemy != null)
        {
            float dist = GetDistanceToTarget(nearestEnemy);
            if (dist <= activeMovingScanRange)
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
        int count = Physics.OverlapSphereNonAlloc(transform.position, searchRange, s_overlapCache);
        BaseCombatUnitController bestTarget = null;
        int bestPriority = int.MaxValue; // Càng nhỏ càng ưu tiên (1: Lính, 2: Dân làng, 3: Công trình)
        float minDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapCache[i];
            if (col == null) continue;

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

        System.Array.Clear(s_overlapCache, 0, count);
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
        if (nearbyPriority > currentPriority)
        {
            return false;
        }
        else if (nearbyPriority == currentPriority)
        {
            // Nếu cùng độ ưu tiên, chỉ retarget nếu mục tiêu mới gần hơn mục tiêu cũ ít nhất 4m
            float currentDist = GetDistanceToTarget(currentTarget);
            float nearbyDist = GetDistanceToTarget(nearbyTarget);
            if (nearbyDist + 4.0f >= currentDist)
            {
                return false;
            }
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
    protected virtual int GetTargetPriority(BaseCombatUnitController unit)
    {
        if (unit == null) return 99;
        if (unit.faction == UnitFaction.Neutral) return 5;

        bool isVillager = IsVillagerTarget(unit);
        bool isBuilding = IsBuildingTarget(unit);
        bool isWatchTower = IsWatchTowerTarget(unit);
        bool isCombatUnit = !isVillager && !isBuilding;

        if (IsBloodMoonActive())
        {
            if (IsMainBuildingTarget(unit)) return 1;
            if (isBuilding) return 2;
            if (isCombatUnit) return 3;
            return 4;
        }

        if (_targetRole == EnemyTargetRole.Raider)
        {
            if (isVillager) return 1;
            if (isCombatUnit) return 2;
            if (isWatchTower) return 3;
            return 4;
        }

        if (_targetRole == EnemyTargetRole.SiegeBreaker)
        {
            if (isWatchTower) return 1;
            if (isBuilding) return 2;
            if (isCombatUnit) return 3;
            return 4;
        }

        // Assault (mặc định)
        if (isCombatUnit) return 1;
        if (isWatchTower)
        {
            var tower = unit.GetComponent<WatchTowerGarrison>();
            if (tower != null && tower.OccupantCount > 0)
            {
                return 1; // Tháp canh có lính bắn tên đồn trú là mối đe dọa trực tiếp (độ ưu tiên cao nhất)
            }
            return 2; // Tháp canh trống ưu tiên cao hơn nhà thường
        }
        if (isVillager) return 3;
        return 4;
    }

    protected bool IsVillagerTarget(BaseCombatUnitController unit)
    {
        return unit != null && (unit.GetComponent<VillagerCombatTarget>() != null || unit.GetComponent<VillagerController>() != null);
    }

    protected bool IsBuildingTarget(BaseCombatUnitController unit)
    {
        return unit != null && (unit.GetComponent<BuildingCombatTarget>() != null || unit.GetComponent<MainBuildingCombatTarget>() != null);
    }

    protected bool IsMainBuildingTarget(BaseCombatUnitController unit)
    {
        return unit != null && unit.GetComponent<MainBuildingCombatTarget>() != null;
    }

    protected bool IsWatchTowerTarget(BaseCombatUnitController unit)
    {
        return unit != null && unit.GetComponent<WatchTowerGarrison>() != null;
    }

    protected bool IsBloodMoonActive()
    {
        return WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.BloodMoon;
    }

    protected override void OnDamagedBy(BaseCombatUnitController attacker)
    {
        if (attacker == null || attacker.currentState == CombatState.Dead) return;

        if (currentTarget == null || 
            currentTarget.currentState == CombatState.Dead || 
            IsBuildingTarget(currentTarget))
        {
            AttackTarget(attacker);
        }
    }

    private static List<BaseCombatUnitController> GetCachedPlayerBuildingTargets()
    {
        if (Time.time < s_nextPlayerBuildingCacheRefreshTime)
        {
            return s_cachedPlayerBuildingTargets;
        }

        s_nextPlayerBuildingCacheRefreshTime = Time.time + PLAYER_BUILDING_CACHE_INTERVAL;
        s_cachedPlayerBuildingTargets.Clear();

        BaseCombatUnitController[] allUnits = FindObjectsByType<BaseCombatUnitController>(FindObjectsSortMode.None);
        foreach (BaseCombatUnitController unit in allUnits)
        {
            if (unit == null || unit.currentState == CombatState.Dead || unit.faction != UnitFaction.Player)
            {
                continue;
            }

            if (unit.GetComponent<BuildingCombatTarget>() != null || unit.GetComponent<MainBuildingCombatTarget>() != null)
            {
                s_cachedPlayerBuildingTargets.Add(unit);
            }
        }

        return s_cachedPlayerBuildingTargets;
    }
}
