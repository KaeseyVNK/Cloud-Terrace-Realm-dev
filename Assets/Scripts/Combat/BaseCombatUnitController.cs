using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public abstract class BaseCombatUnitController : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
{
    public static readonly List<BaseCombatUnitController> Registry = new List<BaseCombatUnitController>();

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [Header("Faction & Identity")]
    public UnitFaction faction;
    public string unitName = "Combat Unit";
    [HideInInspector] public string prefabName;

    [Header("Stats")]
    public int maxHealth = 100;
    public int currentHealth;
    public int attackDamage = 15;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;
    public float scanRange = 15f; // Tầm tự động phát hiện kẻ địch

    [Header("Combat Scan Filtering")]
    [SerializeField] protected LayerMask _combatTargetLayerMask;
    protected LayerMask _effectiveCombatTargetLayerMask;

    [Header("Scan Optimization")]
    [SerializeField] protected float idleScanInterval = 0.25f;
    [SerializeField] protected float movingScanInterval = 0.35f;

    [Header("State")]
    public CombatState currentState = CombatState.Idle;
    public bool autoAggroDuringMove = true; // Tự động tự vệ tấn công địch khi hành quân ngang qua
    public virtual int TargetPriorityPenalty => 0;

    [Header("Chase Leash")]
    [SerializeField] protected bool useAutoChaseLeash = true;
    [SerializeField] protected float maxAutoChaseDistance = 12f;
    [SerializeField] private float attackRangeExitBuffer = 0.35f;
    [SerializeField] private float knockupRecoveryDuration = 0.25f;
    [SerializeField] private float movingAnimationVelocityThreshold = 0.08f;
    [SerializeField] private float movingAnimationHoldTime = 0.18f;

    protected NavMeshAgent navAgent;
    public NavMeshAgent NavAgent => navAgent;
    protected Animator animator;
    protected BaseCombatUnitController currentTarget;
    protected float lastAttackTime = 0f;
    protected bool isStunned = false;
    protected Coroutine staggerCoroutine;
    protected Coroutine flashCoroutine;

    // Hệ thống tối ưu tính toán leo dốc
    private float _nextSlopeCheckTime = 0f;
    private bool _isUphill = false;
    private float _currentBaseSpeed = -1f;
    private static MaterialPropertyBlock _hitFlashPropertyBlock;
    private readonly List<Renderer> _hitFlashRenderers = new List<Renderer>();

    private static MaterialPropertyBlock GetHitFlashPropertyBlock()
    {
        if (_hitFlashPropertyBlock == null)
        {
            _hitFlashPropertyBlock = new MaterialPropertyBlock();
        }
        return _hitFlashPropertyBlock;
    }

    protected float blockedTimer = 0f;
    protected bool isManualMoveCommand = false;
    public bool isHoldPosition = false;
    protected bool returnToPoolOnDeath = false;
    private Vector3 chaseAnchorPosition;
    private bool hasChaseAnchor;
    private bool isManualAttackTarget;
    private float nextIdleScanTime;
    private float nextMovingScanTime;
    private BaseCombatUnitController cachedIdleScanTarget;
    private BaseCombatUnitController cachedMovingScanTarget;
    private float knockupRecoveryUntil;
    private float movingAnimationHoldUntil;
    private float _nextChaseRepathTime = 0f;
    private float _nextBetterTargetCheckTime = 0f;
    // Watchdog: đếm thời gian unit đang ở Moving state nhưng velocity ≈ 0 (bị kẹt)
    private float _movingStuckTimer = 0f;
    private const float MovingStuckTimeout = 2.0f; // Giây trước khi tự chuyển về Idle
    // Watchdog: đếm thời gian unit bị kẹt trong Chasing state (velocity≈0, không tìm được enemy thay thế)
    private float _chasingStuckTimer = 0f;
    private const float ChasingStuckTimeout = 3.0f; // Giây trước khi bỏ mục tiêu và về Idle
    // Lưu điểm đích di chuyển thủ công để có thể đặt lại path khi NavMesh bị rebuild bất đồng bộ
    private Vector3 _manualMoveDestination;
    private float _noPathTimer = 0f;
    private const float NoPathTimeout = 1.0f; // Cho phép 1 giây mất path tạm thời trước khi về Idle

    private TechnologyManager _subscribedTechnologyManager;
    private bool _isRegisteredInRegistry = false;

    // Cache tĩnh dùng chung cho các hàm Physics.OverlapSphereNonAlloc
    protected static readonly Collider[] s_combatOverlapCache = new Collider[256];
    protected static readonly Unity.Profiling.ProfilerMarker s_combatScanMarker = new Unity.Profiling.ProfilerMarker("RTS.Combat.Scan");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static float _lastOverflowWarningTime = 0f;
#endif
    private ObstacleAvoidanceType _defaultAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    private bool _isHungry = false;
    public bool IsHungry => _isHungry;
    public void SetHungry(bool hungry)
    {
        _isHungry = hungry;
        ApplyPlayerTechnologyStats();
    }

    // Base stats caching to avoid accumulated multipliers on pool recycle
    protected int baseMaxHealth = -1;
    protected int baseAttackDamage = -1;
    protected float baseSpeed = -1f;

    // Cache component FogVisibilityTarget phục vụ cho AI Culling tối ưu hiệu năng
    protected FogVisibilityTarget _fogVisibility;

    protected void InitializeBaseStatsIfNeeded()
    {
        if (baseMaxHealth == -1)
        {
            baseMaxHealth = maxHealth;
            baseAttackDamage = attackDamage;
            if (navAgent == null)
            {
                navAgent = GetComponent<NavMeshAgent>();
            }
            if (navAgent != null)
            {
                baseSpeed = navAgent.speed;
                _currentBaseSpeed = baseSpeed;
            }
        }
    }

    public void ApplyStatMultipliers(float healthMult, float damageMult, float speedMult)
    {
        InitializeBaseStatsIfNeeded();
        
        maxHealth = Mathf.RoundToInt(baseMaxHealth * healthMult);
        currentHealth = maxHealth;
        attackDamage = Mathf.RoundToInt(baseAttackDamage * damageMult);
        
        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }
        if (navAgent != null && baseSpeed > 0)
        {
            _currentBaseSpeed = baseSpeed * speedMult;
            navAgent.speed = _currentBaseSpeed;
        }
    }

    public void RestoreBaseStats()
    {
        if (baseMaxHealth != -1)
        {
            maxHealth = baseMaxHealth;
            currentHealth = maxHealth;
            attackDamage = baseAttackDamage;
            if (navAgent != null && baseSpeed > 0)
            {
                _currentBaseSpeed = baseSpeed;
                navAgent.speed = baseSpeed;
            }
        }
    }

    /// <summary>
    /// Overrides the max health and base max health of this unit (e.g. for buildings with dynamic data).
    /// </summary>
    public void SetMaxHealth(int newMaxHealth)
    {
        baseMaxHealth = newMaxHealth;
        maxHealth = newMaxHealth;
        currentHealth = newMaxHealth;
    }

    /// <summary>
    /// Overrides the attack damage and base attack damage of this unit.
    /// </summary>
    public void SetAttackDamage(int newDamage)
    {
        baseAttackDamage = newDamage;
        attackDamage = newDamage;
    }

    private void ResolveCombatTargetLayerMask()
    {
        if (_combatTargetLayerMask.value != 0)
        {
            _effectiveCombatTargetLayerMask = _combatTargetLayerMask;
        }
        else
        {
            _effectiveCombatTargetLayerMask = LayerMask.GetMask("Unit", "Unit Enemy", "Buiding");
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_effectiveCombatTargetLayerMask.value == 0)
            {
                Debug.LogWarning($"[Combat] Failed to retrieve layers 'Unit', 'Unit Enemy', 'Buiding' for fallback on {gameObject.name}.");
            }
            #endif
        }
    }

    protected virtual void Start()
    {
        ResolveCombatTargetLayerMask();
        InitializeBaseStatsIfNeeded();
        currentHealth = maxHealth;
        navAgent = GetComponent<NavMeshAgent>();

        // Khóa Rigidbody thành Kinematic để tránh xung đột vật lý trọng lực làm kẹt hoặc tụt dốc khi di chuyển bằng NavMeshAgent
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        
        // Tự động kéo khớp (warp) unit về vị trí NavMesh gần nhất nếu bị thả lệch hoặc lơ lửng ngoài vùng đi lại
        if (navAgent != null && !navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 25f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
            else
            {
                navAgent.enabled = false;
                Debug.LogWarning($"[NavMesh] Agent {gameObject.name} ở quá xa vùng NavMesh đã bake. Tạm thời vô hiệu hóa NavMeshAgent để tránh lỗi Console.");
            }
        }

        animator = GetComponentInChildren<Animator>();
        
        // Cấu hình ban đầu cho NavMeshAgent
        if (navAgent != null && navAgent.enabled)
        {
            _defaultAvoidanceType = navAgent.obstacleAvoidanceType;
            navAgent.avoidancePriority = Random.Range(30, 71);

            // Thiết lập areaMask trùng khớp với WalkableNavMeshAreaMask (~2) để đơn vị có thể đi qua mọi địa hình đã bake (bao gồm cầu gỗ)
            navAgent.areaMask = ~2;

            // Tối ưu hóa tìm đường và di chuyển chống khựng/kẹt góc đồi núi
            navAgent.radius = 0.35f; // Khớp chuẩn xác với agentRadius đã bake (0.35) để không cọ xát vách đá
            navAgent.angularSpeed = 720f; // Quay đầu tức thì để bám cua mượt mà, không bị trượt bánh
            navAgent.acceleration = 32f; // Tăng/giảm tốc cực nhanh giúp chuyển trạng thái mượt, chống khựng giật
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance; // Tránh giật lag khi chen chúc
        }

        // Tự động gắn FogVisibilityTarget cho kẻ địch/đơn vị ngoài phe người chơi
        if (faction != UnitFaction.Player && 
            AOSFogOfWarBridge.Instance != null && 
            AOSFogOfWarBridge.Instance.AutoAddVisibilityTargets)
        {
            bool isEnemy = GetComponent<EnemyUnitController>() != null;
            bool shouldAdd = (isEnemy && AOSFogOfWarBridge.Instance.HideEnemiesOutsideVision) ||
                             (!isEnemy && AOSFogOfWarBridge.Instance.HideNonPlayerCombatTargetsOutsideVision);
            if (shouldAdd && GetComponent<FogVisibilityTarget>() == null)
            {
                gameObject.AddComponent<FogVisibilityTarget>();
            }
        }

        _fogVisibility = GetComponent<FogVisibilityTarget>();

        // Tự động gắn đèn cho unit của người chơi (Sử dụng Fake Light vòng sáng tối ưu hiệu năng)
        if (faction == UnitFaction.Player && 
            GetComponent<ConstructibleBuilding>() == null && 
            !(this is BuildingCombatTarget) && 
            !(this is MainBuildingCombatTarget))
        {
            if (gameObject.GetComponent<UnitLightController>() == null)
            {
                gameObject.AddComponent<UnitLightController>();
            }
        }

        SubscribeToTechnologyEvent();
        ApplyPlayerTechnologyStats();
    }

    public void ApplyPlayerTechnologyStats()
    {
        if (faction != UnitFaction.Player) return;
        InitializeBaseStatsIfNeeded();

        float healthMult = 1f;
        float damageMult = 1f;
        float speedMult = 1f;

        if (TechnologyManager.HasInstance)
        {
            if (TechnologyManager.Instance.IsUnlocked("ancient_weaponry"))
            {
                healthMult += 0.3f;
                damageMult += 0.3f;
            }
        }

        if (CardManager.Instance != null)
        {
            healthMult *= CardManager.Instance.CombatUnitMaxHealthMultiplier;
            damageMult *= CardManager.Instance.CombatUnitAttackDamageMultiplier;
            speedMult *= CardManager.Instance.CombatUnitMoveSpeedMultiplier;
        }

        if (_isHungry)
        {
            speedMult *= 0.7f;
        }

        int prevMaxHealth = maxHealth;
        maxHealth = Mathf.RoundToInt(baseMaxHealth * healthMult);
        attackDamage = Mathf.RoundToInt(baseAttackDamage * damageMult);

        if (navAgent == null)
        {
            navAgent = GetComponent<NavMeshAgent>();
        }
        if (navAgent != null && baseSpeed > 0)
        {
            _currentBaseSpeed = baseSpeed * speedMult;
            navAgent.speed = _currentBaseSpeed;
        }

        if (maxHealth != prevMaxHealth)
        {
            float hpRatio = prevMaxHealth > 0 ? (float)currentHealth / prevMaxHealth : 1f;
            currentHealth = Mathf.Clamp(Mathf.RoundToInt(maxHealth * hpRatio), 1, maxHealth);
        }
    }

    private void SubscribeToTechnologyEvent()
    {
        if (_subscribedTechnologyManager != null) return;
        if (faction != UnitFaction.Player) return;

        if (TechnologyManager.HasInstance)
        {
            _subscribedTechnologyManager = TechnologyManager.Instance;
            _subscribedTechnologyManager.OnTechnologyUnlocked += HandleTechnologyUnlocked;
        }
    }

    private void UnsubscribeFromTechnologyEvent()
    {
        if (_subscribedTechnologyManager != null)
        {
            _subscribedTechnologyManager.OnTechnologyUnlocked -= HandleTechnologyUnlocked;
            _subscribedTechnologyManager = null;
        }
    }

    protected virtual void OnEnable()
    {
        if (!_isRegisteredInRegistry)
        {
            Registry.Add(this);
            _isRegisteredInRegistry = true;
        }
        SubscribeToTechnologyEvent();
        ApplyPlayerTechnologyStats();
    }

    protected virtual void OnDisable()
    {
        if (_isRegisteredInRegistry)
        {
            Registry.Remove(this);
            _isRegisteredInRegistry = false;
        }
        UnsubscribeFromTechnologyEvent();

        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        RestoreHitFlashColors();
    }

    private void HandleTechnologyUnlocked(TechnologyData tech)
    {
        ApplyPlayerTechnologyStats();
    }

    private void UpdateSlopeMovementSpeed()
    {
        if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;

        if (_currentBaseSpeed < 0f)
        {
            _currentBaseSpeed = baseSpeed;
        }

        if (Time.time >= _nextSlopeCheckTime)
        {
            _nextSlopeCheckTime = Time.time + 0.15f; // Check ~7 times per second
            _isUphill = false;

            if (navAgent.velocity.sqrMagnitude > 0.05f && Terrain.activeTerrain != null)
            {
                Vector3 direction = navAgent.velocity.normalized;
                Vector3 testPos = transform.position + direction * 1.0f;
                float currentHeight = Terrain.activeTerrain.SampleHeight(transform.position);
                float aheadHeight = Terrain.activeTerrain.SampleHeight(testPos);
                if (aheadHeight - currentHeight >= 0.25f)
                {
                    _isUphill = true;
                }
            }

            float speedMult = _isUphill ? 0.7f : 1.0f;
            float targetSpeed = _currentBaseSpeed * speedMult;
            if (Mathf.Abs(navAgent.speed - targetSpeed) > 0.01f)
            {
                navAgent.speed = targetSpeed;
            }
        }
    }

    protected virtual void Update()
    {
        if (currentState == CombatState.Dead) return;

        UpdateSlopeMovementSpeed();

        // Tối ưu hóa hiệu năng (AI Culling): Đóng băng hoạt động của quái canh gác khi nằm ngoài tầm nhìn (sương mù)
        if (faction != UnitFaction.Player && _fogVisibility != null && !_fogVisibility.IsVisible)
        {
            if (this is EnemyUnitController enemy && enemy.IsGuard && currentState != CombatState.Chasing && currentState != CombatState.Attacking)
            {
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && !navAgent.isStopped)
                {
                    navAgent.isStopped = true;
                }
                return; // Bỏ qua toàn bộ Update logic
            }
        }
        else
        {
            // Khi hiển thị trở lại, khôi phục di chuyển nếu trước đó bị đóng băng
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh && navAgent.isStopped && currentState == CombatState.Moving)
            {
                navAgent.isStopped = false;
            }
        }

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

        switch (currentState)
        {
            case CombatState.Idle:
                HandleIdleState();
                break;
            case CombatState.Moving:
                HandleMovingState();
                break;
            case CombatState.Chasing:
                HandleChasingState();
                break;
            case CombatState.Attacking:
                HandleAttackingState();
                break;
        }

        UpdateAnimationState();
    }

    // --- LOGIC CHO TỪNG TRẠNG THÁI ---

    protected virtual void HandleIdleState()
    {
        // Tự động quét tìm mục tiêu đối địch xung quanh
        BaseCombatUnitController nearestEnemy = GetThrottledNearestEnemy(ref nextIdleScanTime, ref cachedIdleScanTarget, idleScanInterval, scanRange);
        if (nearestEnemy != null)
        {
            AttackTarget(nearestEnemy);
        }
    }

    protected virtual void HandleMovingState()
    {
        if (!IsNavAgentReady()) return;

        // Tự động quét tự vệ khi đang di chuyển hành quân
        if (autoAggroDuringMove && !isManualMoveCommand)
        {
            BaseCombatUnitController nearestEnemy = GetThrottledNearestEnemy(ref nextMovingScanTime, ref cachedMovingScanTarget, movingScanInterval, scanRange);
            if (nearestEnemy != null)
            {
                // Tầm quét chủ động khi đang di chuyển hẹp hơn tầm quét rảnh rỗi một chút để tránh lệch quá xa lộ trình
                float dist = Vector3.Distance(transform.position, nearestEnemy.transform.position);
                if (dist <= scanRange * 0.6f)
                {
                    AttackTarget(nearestEnemy);
                    return;
                }
            }
        }

        // 1. Điều kiện kết thúc di chuyển khi đã đến đích (Ưu tiên kiểm tra đầu tiên để chuyển trạng thái tức thì)
        if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
        {
            if (!navAgent.hasPath || navAgent.velocity.sqrMagnitude < 0.25f)
            {
                isManualMoveCommand = false;
                _movingStuckTimer = 0f;
                _noPathTimer = 0f;
                navAgent.isStopped = true;
                navAgent.ResetPath();
                ChangeState(CombatState.Idle);
                return;
            }
        }

        // 2. Xử lý khi mất đường đi ở khoảng cách xa (khi NavMesh được rebuild hoặc không tìm được đường)
        if (!navAgent.pathPending && !navAgent.hasPath)
        {
            _noPathTimer += Time.deltaTime;
            if (_noPathTimer >= NoPathTimeout)
            {
                isManualMoveCommand = false;
                _movingStuckTimer = 0f;
                _noPathTimer = 0f;
                navAgent.isStopped = true;
                navAgent.ResetPath();
                ChangeState(CombatState.Idle);
                return;
            }
        }
        else
        {
            _noPathTimer = 0f; // Có path rồi, reset bộ đếm
        }

        // 3. Watchdog: phát hiện unit bị kẹt (có path nhưng velocity ≈ 0 quá lâu)
        bool agentIsStuck = navAgent.hasPath
            && !navAgent.pathPending
            && navAgent.velocity.sqrMagnitude < 0.01f
            && navAgent.remainingDistance > navAgent.stoppingDistance + 0.1f;

        if (agentIsStuck)
        {
            _movingStuckTimer += Time.deltaTime;
            if (_movingStuckTimer >= MovingStuckTimeout)
            {
                // Bị kẹt quá lâu → dừng di chuyển và về Idle để tránh animation running kẹt
                isManualMoveCommand = false;
                _movingStuckTimer = 0f;
                navAgent.isStopped = true;
                navAgent.ResetPath();
                ChangeState(CombatState.Idle);
            }
        }
        else
        {
            _movingStuckTimer = 0f;
        }
    }

    protected virtual float GetDistanceToTarget(BaseCombatUnitController target)
    {
        if (target == null) return 0f;
        Collider col = GetActiveTargetCollider(target);
        
        float selfRadius = 0f;
        if (navAgent != null)
        {
            selfRadius = navAgent.radius;
        }
        else if (TryGetComponent(out Collider c))
        {
            selfRadius = c.bounds.extents.x;
        }

        if (col != null && target.navAgent == null)
        {
            Vector3 closestPoint = col.ClosestPoint(transform.position);
            closestPoint.y = transform.position.y;
            float distToEdge = Vector3.Distance(transform.position, closestPoint);
            return Mathf.Max(0f, distToEdge - selfRadius);
        }
        
        float dist = Vector3.Distance(transform.position, target.transform.position);
        
        // Trừ đi bán kính của bản thân và mục tiêu để tính khoảng cách thực tế giữa 2 vỏ vật lý
        float targetRadius = 0f;
        if (target.navAgent != null)
        {
            targetRadius = target.navAgent.radius;
        }
        else if (target.TryGetComponent(out Collider targetCol))
        {
            targetRadius = targetCol.bounds.extents.x;
        }

        return Mathf.Max(0f, dist - selfRadius - targetRadius);
    }


    public bool IsRangedUnit()
    {
        return (this is RangedCombatUnitController) || (this.GetType().Name.Contains("Archer")) || (this.GetType().Name.Contains("Mage"));
    }

    protected virtual float GetAttackRangeForTarget(BaseCombatUnitController target)
    {
        float baseRange = attackRange;
        if (target != null && IsRangedUnit())
        {
            float selfY = transform.position.y;
            float targetY = target.transform.position.y;
            if (selfY - targetY >= 1.5f)
            {
                baseRange += 3.0f; // +1.5 ô (với kích thước ô 2m)
            }
        }
        return baseRange;
    }

    protected virtual Collider GetActiveTargetCollider(BaseCombatUnitController target)
    {
        if (target == null) return null;

        Collider[] colliders = target.GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            if (col != null && col.enabled && col.gameObject.activeInHierarchy && !col.isTrigger)
            {
                return col;
            }
        }

        foreach (Collider col in colliders)
        {
            if (col != null && col.enabled && col.gameObject.activeInHierarchy)
            {
                return col;
            }
        }

        return null;
    }

    protected virtual void HandleChasingState()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        if (TrySwitchToBetterTarget())
        {
            return;
        }

        if (HasExceededAutoChaseLeash())
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        float effectiveAttackRange = GetAttackRangeForTarget(currentTarget);

        if (IsNavAgentReady())
        {
            navAgent.stoppingDistance = effectiveAttackRange * 0.5f; // Đảm bảo dừng lại khi nằm trong tầm chém
        }

        float distance = GetDistanceToTarget(currentTarget);

        if (isHoldPosition && distance > effectiveAttackRange)
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        if (distance <= effectiveAttackRange)
        {
            // Trong tầm đánh -> Dừng lại và bắt đầu tấn công
            if (IsNavAgentReady())
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            ChangeState(CombatState.Attacking);
            blockedTimer = 0f;
        }
        else
        {
            // Ngoài tầm đánh -> Tiếp tục đuổi theo mục tiêu
            if (IsNavAgentReady())
            {
                navAgent.isStopped = false;

                // Throttled SetDestination to avoid NavMesh stuttering
                if (Time.time >= _nextChaseRepathTime || !navAgent.hasPath)
                {
                    _nextChaseRepathTime = Time.time + 0.2f + Random.Range(0f, 0.05f);
                    navAgent.SetDestination(GetChaseDestination());
                }

                // Chống kẹt: Nếu đang di chuyển đuổi theo nhưng bị các đồng đội đi trước chặn đường (vận tốc ~ 0)
                if (navAgent.velocity.sqrMagnitude < 0.05f && navAgent.hasPath)
                {
                    blockedTimer += Time.deltaTime;
                    _chasingStuckTimer += Time.deltaTime;

                    if (blockedTimer > 1.5f) // Bị kẹt quá 1.5 giây
                    {
                        blockedTimer = 0f;
                        // Tìm một kẻ địch khác gần nhất để đánh thay thế
                        BaseCombatUnitController alternativeEnemy = ScanForNearestEnemy();
                        if (alternativeEnemy != null && alternativeEnemy != currentTarget)
                        {
                            _chasingStuckTimer = 0f;
                            AttackTarget(alternativeEnemy);
                        }
                        else if (_chasingStuckTimer >= ChasingStuckTimeout)
                        {
                            // Không tìm được enemy thay thế sau nhiều lần thử → bỏ mục tiêu, về Idle
                            _chasingStuckTimer = 0f;
                            ClearCurrentTargetAndIdle();
                        }
                    }
                }
                else
                {
                    blockedTimer = 0f;
                    _chasingStuckTimer = 0f;
                }
            }
        }
    }

    protected virtual Vector3 GetChaseDestination()
    {
        if (currentTarget == null) return transform.position;

        Collider col = GetActiveTargetCollider(currentTarget);
        if (col != null && currentTarget.GetComponent<UnityEngine.AI.NavMeshAgent>() == null)
        {
            // Đối với mục tiêu tĩnh (như nhà cửa), di chuyển trực tiếp đến điểm gần nhất trên Collider của nó
            Vector3 closestPoint = col.ClosestPoint(transform.position);
            closestPoint.y = transform.position.y;
            return closestPoint;
        }

        // Sử dụng GetHashCode để tính toán góc lệch duy nhất cho từng unit (tránh chụm vào 1 điểm)
        int id = GetHashCode();
        float angle = (id % 8) * 45f * Mathf.Deg2Rad; // Phân phối thành 8 hướng quanh mục tiêu
        
        // Vị trí đứng chém tối ưu (cách tâm mục tiêu bằng 65% tầm đánh)
        float offsetDistance = attackRange * 0.65f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * offsetDistance;
        
        return currentTarget.transform.position + offset;
    }

    protected virtual void HandleAttackingState()
    {
        if (currentTarget == null || currentTarget.currentState == CombatState.Dead)
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        if (TrySwitchToBetterTarget())
        {
            return;
        }

        if (HasExceededAutoChaseLeash())
        {
            ClearCurrentTargetAndIdle();
            return;
        }

        float distance = GetDistanceToTarget(currentTarget);

        // Nếu mục tiêu di chuyển ra xa quá tầm đánh -> Đuổi theo
        if (distance > GetAttackRangeForTarget(currentTarget) + attackRangeExitBuffer)
        {
            ChangeState(CombatState.Chasing);
            return;
        }

        // Xoay mặt mượt mà về phía mục tiêu (Chỉ xoay khi khoảng cách đủ lớn để tránh bị xoay vòng vòng khi đứng quá sát)
        Vector3 direction = currentTarget.transform.position - transform.position;
        direction.y = 0; // Giữ thăng bằng trục Y
        
        if (direction.magnitude > 0.25f)
        {
            Vector3 normDirection = direction.normalized;
            if (normDirection.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(normDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
            }
        }

        // Tấn công dựa trên Cooldown
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            PerformAttack();
        }
    }

    // --- HÀNH ĐỘNG TẤN CÔNG & SÁT THƯƠNG ---

    protected virtual void PerformAttack()
    {
        lastAttackTime = Time.time;
        
        // Kích hoạt trigger animation tấn công
        if (animator != null)
        {
            SetAnimatorTriggerIfExists("Attack");
        }

        // Gây sát thương lên mục tiêu
        if (currentTarget != null)
        {
            BaseCombatUnitController attackTarget = currentTarget;
            attackTarget.TakeDamage(attackDamage, this);
        }
    }

    public virtual void TakeDamage(int damage, BaseCombatUnitController attacker = null)
    {
        if (currentState == CombatState.Dead) return;

        if (attacker != null)
        {
            float attackerY = attacker.transform.position.y;
            float targetY = transform.position.y;
            if (attackerY - targetY >= 1.5f)
            {
                damage = Mathf.RoundToInt(damage * 1.25f); // +25% sát thương từ trên cao
            }
        }

        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth);

        if (faction == UnitFaction.Player && damage > 0)
        {
            var minimap = FindAnyObjectByType<MinimapUIController>();
            if (minimap != null)
            {
                minimap.ShowPing(transform.position, Color.red, 2f);
            }
        }

        if (MyGame.Audio.AudioManager.Instance != null && attacker != null)
        {
            if (!(attacker is RangedCombatUnitController))
            {
                MyGame.Audio.AudioManager.Instance.PlaySwordHit(transform.position);
            }
        }

        // Kích hoạt nhấp nháy đỏ phản hồi thị giác (không làm gián đoạn hành động/animation)
        TriggerHitFlash();

        if (attacker != null)
        {
            OnDamagedBy(attacker);
        }

        if (currentHealth <= 0)
        {
            if (attacker != null)
            {
                Vector3 direction = attacker.transform.position - transform.position;
                direction.y = 0f;
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized);
                }
            }
            Die();
        }
    }

    protected virtual void OnDamagedBy(BaseCombatUnitController attacker)
    {
    }

    protected virtual IEnumerator StaggerCoroutine(float duration)
    {
        isStunned = true;
        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
        }

        yield return new WaitForSeconds(duration);

        isStunned = false;
        
        // Nếu vẫn còn sống và đang đi/đuổi quái, tiếp tục di chuyển
        if (currentState != CombatState.Dead && IsNavAgentReady())
        {
            if (currentState == CombatState.Moving || currentState == CombatState.Chasing)
            {
                navAgent.isStopped = false;
            }
        }
    }

    protected virtual void TriggerHitFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            RestoreHitFlashColors();
        }
        flashCoroutine = StartCoroutine(FlashRedCoroutine());
    }

    private IEnumerator FlashRedCoroutine()
    {
        _hitFlashRenderers.Clear();
        GetComponentsInChildren<Renderer>(true, _hitFlashRenderers);

        var block = GetHitFlashPropertyBlock();

        foreach (var r in _hitFlashRenderers)
        {
            if (r != null && r.sharedMaterial != null)
            {
                // Kiểm tra thuộc tính màu cho cả URP và Standard shader trên sharedMaterial
                if (r.sharedMaterial.HasProperty(BaseColorProperty))
                {
                    block.SetColor(BaseColorProperty, Color.red);
                    r.SetPropertyBlock(block);
                }
                else if (r.sharedMaterial.HasProperty(ColorProperty))
                {
                    block.SetColor(ColorProperty, Color.red);
                    r.SetPropertyBlock(block);
                }
            }
        }

        yield return new WaitForSeconds(0.12f);

        // Trả lại màu gốc
        RestoreHitFlashColors();
        flashCoroutine = null;
    }

    private void RestoreHitFlashColors()
    {
        foreach (var r in _hitFlashRenderers)
        {
            if (r != null)
            {
                r.SetPropertyBlock(null);
            }
        }
        _hitFlashRenderers.Clear();
    }

    protected virtual void Die()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }
        RestoreHitFlashColors();

        currentTarget = null;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        isManualAttackTarget = false;
        hasChaseAnchor = false;
        ChangeState(CombatState.Dead);
        
        if (navAgent != null)
        {
            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
            }
            navAgent.enabled = false;
        }

        // Đóng băng vật lý Rigidbody (nếu có) để tránh rơi tự do xuyên đất ngay lập tức
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (animator != null)
        {
            SetAnimatorBoolIfExists("IsDead", true);
        }

        OnDeath();
    }

    protected virtual void OnDeath()
    {
        // Cho nằm trên đất 4 giây, sau đó chìm xuống đất 1.5 giây rồi biến mất hẳn (tổng cộng 5.5 giây)
        StartCoroutine(DestroyAfterDelay(4f, 1.5f));
    }

    private IEnumerator DestroyAfterDelay(float lieDelay, float sinkDuration)
    {
        // 1. Cho xác nằm im trên mặt đất
        yield return new WaitForSeconds(lieDelay);

        // 2. Chìm dần xuống mặt đất (Sink Effect) để biến mất mượt mà
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = startPos + Vector3.down * 1.5f; // Chìm sâu xuống 1.5m

        while (elapsed < sinkDuration)
        {
            elapsed += Time.deltaTime;
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / sinkDuration);
            yield return null;
        }

        // 3. Tiến hành giải phóng bộ nhớ hoặc trả về pool nếu unit này được pool quản lý
        if (returnToPoolOnDeath)
        {
            PoolManager.Instance.Release(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- ĐIỀU KHIỂN COMMAND (API CHO RTS CHUỘT PHẢI) ---

    public virtual void CommandMove(Vector3 position)
    {
        if (currentState == CombatState.Dead) return;

        currentTarget = null;
        isManualMoveCommand = true;
        isManualAttackTarget = false;
        isHoldPosition = false;
        hasChaseAnchor = false;
        blockedTimer = 0f;

        if (animator != null)
        {
            ResetAnimatorTriggerIfExists("Attack");
            SetAnimatorBoolIfExists("IsAttacking", false);
        }

        if (!TrySetManualMoveDestination(position, out Vector3 resolvedDestination))
        {
            isManualMoveCommand = false;
            _manualMoveDestination = transform.position;
            _noPathTimer = 0f;
            _movingStuckTimer = 0f;
            ChangeState(CombatState.Idle);
            return;
        }

        _manualMoveDestination = resolvedDestination;
        _noPathTimer = 0f;
        ChangeState(CombatState.Moving);
    }

    private bool TrySetManualMoveDestination(Vector3 requestedPosition, out Vector3 resolvedDestination)
    {
        resolvedDestination = requestedPosition;
        if (!IsNavAgentReady())
        {
            return false;
        }

        navAgent.stoppingDistance = 0.2f;
        navAgent.isStopped = false;

        // Thử thiết lập điểm đến trực tiếp (bất đồng bộ)
        if (navAgent.SetDestination(requestedPosition))
        {
            return true;
        }

        // Dự phòng: Lấy điểm gần nhất có thể đi được trên NavMesh trong phạm vi rộng (100 mét)
        if (NavMesh.SamplePosition(requestedPosition, out NavMeshHit hit, 100f, ~2))
        {
            resolvedDestination = hit.position;
            if (navAgent.SetDestination(resolvedDestination))
            {
                return true;
            }
        }

        navAgent.isStopped = true;
        navAgent.ResetPath();
        return false;
    }

    public virtual void CommandStop()
    {
        if (currentState == CombatState.Dead) return;

        currentTarget = null;
        isManualMoveCommand = false;
        isManualAttackTarget = false;
        isHoldPosition = false;
        hasChaseAnchor = false;
        blockedTimer = 0f;

        if (animator != null)
        {
            ResetAnimatorTriggerIfExists("Attack");
            SetAnimatorBoolIfExists("IsAttacking", false);
        }

        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
        ChangeState(CombatState.Idle);
    }

    public virtual void CommandHoldPosition()
    {
        if (currentState == CombatState.Dead) return;

        currentTarget = null;
        isManualMoveCommand = false;
        isManualAttackTarget = false;
        isHoldPosition = true;
        hasChaseAnchor = false;
        blockedTimer = 0f;

        if (animator != null)
        {
            ResetAnimatorTriggerIfExists("Attack");
            SetAnimatorBoolIfExists("IsAttacking", false);
        }

        if (IsNavAgentReady())
        {
            navAgent.isStopped = true;
            navAgent.ResetPath();
        }
        ChangeState(CombatState.Idle);
    }

    public virtual void CommandAttackMove(Vector3 position)
    {
        if (currentState == CombatState.Dead) return;

        currentTarget = null;
        isManualMoveCommand = false; // Set to false to allow auto-aggro during movement (Attack Move)
        isManualAttackTarget = false;
        isHoldPosition = false;
        hasChaseAnchor = false;
        blockedTimer = 0f;

        if (animator != null)
        {
            ResetAnimatorTriggerIfExists("Attack");
            SetAnimatorBoolIfExists("IsAttacking", false);
        }

        if (IsNavAgentReady())
        {
            navAgent.stoppingDistance = 0.2f;
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
        }
        ChangeState(CombatState.Moving);
    }

    public virtual void CommandAttack(BaseCombatUnitController target)
    {
        if (currentState == CombatState.Dead) return;
        if (target == null || target.currentState == CombatState.Dead) return;

        isManualMoveCommand = false;
        isHoldPosition = false;
        AttackTarget(target, true);
    }

    protected void AttackTarget(BaseCombatUnitController target, bool manualAttack = false)
    {
        isManualMoveCommand = false;
        isManualAttackTarget = manualAttack;
        if (!hasChaseAnchor)
        {
            chaseAnchorPosition = transform.position;
            hasChaseAnchor = true;
        }
        currentTarget = target;
        ChangeState(CombatState.Chasing);
    }

    protected void ClearCurrentTargetAndIdle()
    {
        currentTarget = null;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        isManualAttackTarget = false;

        if (hasChaseAnchor)
        {
            Vector3 targetDest = chaseAnchorPosition;
            hasChaseAnchor = false;

            if (IsNavAgentReady())
            {
                navAgent.stoppingDistance = 0.2f;
                navAgent.isStopped = false;
                navAgent.SetDestination(targetDest);
                ChangeState(CombatState.Moving);
            }
            else
            {
                ChangeState(CombatState.Idle);
            }
        }
        else
        {
            if (IsNavAgentReady())
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            ChangeState(CombatState.Idle);
        }
    }

    // --- TIỆN ÍCH DÒ TÌM KẺ ĐỊCH ---

    protected virtual BaseCombatUnitController ScanForNearestEnemy()
    {
        using (s_combatScanMarker.Auto())
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnitPerformanceMetrics.CombatScanCount++;
            #endif

            // Quét tất cả các Collider trong bán kính scanRange dùng NonAlloc để tránh rác GC
            int count = Physics.OverlapSphereNonAlloc(transform.position, scanRange, s_combatOverlapCache, _effectiveCombatTargetLayerMask, QueryTriggerInteraction.Collide);

            if (count >= s_combatOverlapCache.Length)
            {
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnitPerformanceMetrics.BufferOverflowCount++;
                if (Time.time >= _lastOverflowWarningTime + 3.0f)
                {
                    Debug.LogWarning($"[Combat] s_combatOverlapCache (capacity {s_combatOverlapCache.Length}) is full on {gameObject.name}. Some targets might have been missed!");
                    _lastOverflowWarningTime = Time.time;
                }
                #endif
            }

            BaseCombatUnitController nearest = null;
            float minDistance = float.MaxValue;
            int bestPenalty = int.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = s_combatOverlapCache[i];
                if (col == null) continue;

                BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
                if (unit != null && unit.currentState != CombatState.Dead)
                {
                    bool isHostile = false;
                    if (this.faction == UnitFaction.Player)
                    {
                        isHostile = (unit.faction == UnitFaction.Enemy);
                    }
                    else if (this.faction == UnitFaction.Enemy)
                    {
                        isHostile = (unit.faction == UnitFaction.Player);
                    }

                    if (isHostile)
                    {
                        float dist = GetDistanceToTarget(unit);
                        int penalty = Mathf.Max(0, unit.TargetPriorityPenalty);
                        if (penalty < bestPenalty || (penalty == bestPenalty && dist < minDistance))
                        {
                            bestPenalty = penalty;
                            minDistance = dist;
                            nearest = unit;
                        }
                    }
                }
            }

            return nearest;
        }
    }

    protected bool TrySwitchToBetterTarget()
    {
        if (currentTarget == null || currentTarget.TargetPriorityPenalty <= 0)
        {
            return false;
        }

        if (Time.time < _nextBetterTargetCheckTime)
        {
            return false;
        }

        _nextBetterTargetCheckTime = Time.time + 0.5f;

        BaseCombatUnitController betterTarget = ScanForNearestEnemy();
        if (betterTarget == null || betterTarget == currentTarget)
        {
            return false;
        }

        if (!IsBetterTargetThanCurrent(betterTarget, currentTarget))
        {
            return false;
        }

        AttackTarget(betterTarget);
        return true;
    }

    private bool IsBetterTargetThanCurrent(BaseCombatUnitController candidate, BaseCombatUnitController current)
    {
        int candidatePenalty = Mathf.Max(0, candidate.TargetPriorityPenalty);
        int currentPenalty = Mathf.Max(0, current.TargetPriorityPenalty);
        if (candidatePenalty < currentPenalty)
        {
            return true;
        }

        if (candidatePenalty > currentPenalty)
        {
            return false;
        }

        return GetDistanceToTarget(candidate) + 1f < GetDistanceToTarget(current);
    }

    private bool HasExceededAutoChaseLeash()
    {
        if (!useAutoChaseLeash || isManualAttackTarget || !hasChaseAnchor)
        {
            return false;
        }

        if (maxAutoChaseDistance <= 0f)
        {
            return false;
        }

        Vector3 fromAnchor = transform.position - chaseAnchorPosition;
        fromAnchor.y = 0f;
        return fromAnchor.sqrMagnitude > maxAutoChaseDistance * maxAutoChaseDistance;
    }

    protected BaseCombatUnitController GetThrottledNearestEnemy(
        ref float nextScanTime,
        ref BaseCombatUnitController cachedTarget,
        float interval,
        float maxRange)
    {
        if (IsValidScanTarget(cachedTarget, maxRange) && Time.time < nextScanTime)
        {
            return cachedTarget;
        }

        if (Time.time < nextScanTime)
        {
            return null;
        }

        cachedTarget = ScanForNearestEnemy();
        nextScanTime = Time.time + Mathf.Max(0.05f, interval) + Random.Range(0f, 0.05f);

        if (!IsValidScanTarget(cachedTarget, maxRange))
        {
            cachedTarget = null;
        }

        return cachedTarget;
    }

    protected bool IsValidScanTarget(BaseCombatUnitController target, float maxRange)
    {
        if (target == null || target == this || target.currentState == CombatState.Dead)
        {
            return false;
        }

        if (!target.gameObject.activeInHierarchy)
        {
            return false;
        }

        bool isHostile = false;
        if (this.faction == UnitFaction.Player)
        {
            isHostile = (target.faction == UnitFaction.Enemy);
        }
        else if (this.faction == UnitFaction.Enemy)
        {
            isHostile = (target.faction == UnitFaction.Player);
        }

        if (!isHostile)
        {
            return false;
        }

        return GetDistanceToTarget(target) <= maxRange;
    }

    // --- QUẢN LÝ TRẠNG THÁI & ANIMATOR ---

    public void ChangeState(CombatState newState)
    {
        if (currentState == CombatState.Dead && newState != CombatState.Dead && currentHealth <= 0)
        {
            return;
        }

        currentState = newState;

        if (currentState == CombatState.Moving || currentState == CombatState.Chasing)
        {
            movingAnimationHoldUntil = Time.time + Mathf.Max(0f, movingAnimationHoldTime);
            _movingStuckTimer = 0f;
            _chasingStuckTimer = 0f;
        }
        else if (currentState == CombatState.Idle || currentState == CombatState.Attacking || currentState == CombatState.Dead)
        {
            // Reset timer animation để animation ngừng ngay khi chuyển về trạng thái không di chuyển
            movingAnimationHoldUntil = 0f;
            _movingStuckTimer = 0f;
        }

        if (IsNavAgentReady())
        {
            if (currentState == CombatState.Idle || currentState == CombatState.Attacking || currentState == CombatState.Dead)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.updateRotation = false; // Tắt tự động xoay của NavMesh khi không di chuyển
                navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance; // Tắt avoidance khi đứng yên hoặc chết
            }
            else
            {
                navAgent.isStopped = false;
                navAgent.updateRotation = true;  // Bật lại tự động xoay khi di chuyển/đuổi theo mục tiêu
                navAgent.obstacleAvoidanceType = _defaultAvoidanceType; // Bật lại avoidance mặc định khi di chuyển
            }
        }

        if (currentState == CombatState.Dead)
        {
            ForceDeadAnimationState();
        }

        UpdateAvoidancePriority();
    }

    protected virtual void UpdateAnimationState()
    {
        if (animator == null) return;

        if (currentState == CombatState.Dead)
        {
            ForceDeadAnimationState();
            return;
        }

        bool useMovingAnimation = ShouldUseMovingAnimation();

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", currentState == CombatState.Idle && !useMovingAnimation);
        SetAnimatorBoolIfExists("IsMoving", useMovingAnimation);
        SetAnimatorBoolIfExists("IsAttacking", currentState == CombatState.Attacking);
    }

    private bool ShouldUseMovingAnimation()
    {
        if (currentState == CombatState.Chasing)
        {
            // Nếu đang Chasing nhưng velocity ≈0 quá lâu (đang bị kẹt) → không phát animation running
            if (IsNavAgentReady() && navAgent.velocity.sqrMagnitude < 0.01f && _chasingStuckTimer > 0.5f)
            {
                return false;
            }
            movingAnimationHoldUntil = Time.time + Mathf.Max(0f, movingAnimationHoldTime);
            return true;
        }

        if (Time.time < movingAnimationHoldUntil && currentState != CombatState.Attacking && currentState != CombatState.Dead)
        {
            return true;
        }

        if (currentState != CombatState.Moving)
        {
            return false;
        }

        if (!IsNavAgentReady())
        {
            return false;
        }

        return navAgent.velocity.sqrMagnitude > movingAnimationVelocityThreshold * movingAnimationVelocityThreshold;
    }

    protected bool IsKnockupActive()
    {
        CombatKnockupMotion knockupMotion = GetComponent<CombatKnockupMotion>();
        return knockupMotion != null && knockupMotion.IsActive;
    }

    protected bool IsPostKnockupRecovering()
    {
        return Time.time < knockupRecoveryUntil;
    }

    protected void UpdateKnockupAnimationState()
    {
        if (animator == null) return;

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        ResetAnimatorTriggerIfExists("Attack");
    }

    protected void UpdatePostKnockupRecoveryAnimationState()
    {
        if (animator == null) return;

        SetAnimatorBoolIfExists("IsDead", false);
        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        ResetAnimatorTriggerIfExists("Attack");
    }

    public void RecoverFromKnockup()
    {
        if (currentState == CombatState.Dead)
        {
            return;
        }

        blockedTimer = 0f;
        isManualMoveCommand = false;
        knockupRecoveryUntil = Time.time + Mathf.Max(0f, knockupRecoveryDuration);

        if (animator != null)
        {
            UpdatePostKnockupRecoveryAnimationState();
        }

        if (currentTarget != null && currentTarget.currentState != CombatState.Dead && currentTarget.gameObject.activeInHierarchy)
        {
            float distance = GetDistanceToTarget(currentTarget);
            float effectiveAttackRange = GetAttackRangeForTarget(currentTarget);
            ChangeState(distance <= effectiveAttackRange ? CombatState.Attacking : CombatState.Chasing);
            return;
        }

        ClearCurrentTargetAndIdle();
    }

    protected void ForceDeadAnimationState()
    {
        if (animator == null) return;

        SetAnimatorBoolIfExists("IsIdle", false);
        SetAnimatorBoolIfExists("IsMoving", false);
        SetAnimatorBoolIfExists("IsAttacking", false);
        SetAnimatorBoolIfExists("IsDead", true);
        ResetAnimatorTriggerIfExists("Attack");
    }

    protected void SetAnimatorBoolIfExists(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Bool))
        {
            animator.SetBool(parameterName, value);
        }
    }

    protected void SetAnimatorTriggerIfExists(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(parameterName);
        }
    }

    protected void ResetAnimatorTriggerIfExists(string parameterName)
    {
        if (HasAnimatorParameter(parameterName, AnimatorControllerParameterType.Trigger))
        {
            animator.ResetTrigger(parameterName);
        }
    }

    protected bool HasAnimatorParameter(string parameterName, AnimatorControllerParameterType parameterType)
    {
        if (animator == null)
        {
            return false;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == parameterType && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void UpdateAvoidancePriority()
    {
        if (navAgent == null || !navAgent.enabled) return;

        if (currentState == CombatState.Dead)
        {
            navAgent.avoidancePriority = 99; // Thấp nhất khi chết
            return;
        }

        // Thêm offset duy nhất dựa trên HashCode để tránh việc các unit có cùng độ ưu tiên đẩy nhau vô tận
        int uniqueOffset = Mathf.Abs(GetHashCode()) % 5;
        
        bool isRanged = (this is RangedCombatUnitController) || (this.GetType().Name.Contains("Archer"));

        if (currentState == CombatState.Attacking)
        {
            // Đang trực tiếp chiến đấu: Cận chiến ưu tiên rất cao (5-9) để đứng vững chém; Archer ưu tiên thấp hơn (35-39)
            navAgent.avoidancePriority = (isRanged ? 35 : 5) + uniqueOffset;
        }
        else if (currentState == CombatState.Chasing && currentTarget != null)
        {
            // Đang đuổi theo địch: Cận chiến ưu tiên cực cao (10-14) để chen qua đám đông; Archer ưu tiên vừa phải (45-49)
            navAgent.avoidancePriority = (isRanged ? 45 : 10) + uniqueOffset;
        }
        else if (currentState == CombatState.Moving)
        {
            // Đang di chuyển hành quân: Cận chiến ưu tiên trung-cao (20-24); Archer ưu tiên (50-54)
            navAgent.avoidancePriority = (isRanged ? 50 : 20) + uniqueOffset;
        }
        else // Idle (Đang đứng yên)
        {
            // Rảnh rỗi/đứng yên: Cận chiến (70-74); Archer (85-89) để dễ dàng nhường đường
            navAgent.avoidancePriority = (isRanged ? 85 : 70) + uniqueOffset;
        }
    }

    protected bool IsNavAgentReady()
    {
        return navAgent != null && navAgent.enabled && navAgent.isOnNavMesh;
    }

    [System.Serializable]
    private class CombatUnitSaveState
    {
        public int currentHealth;
        public string unitName;
    }

    public virtual string CaptureState()
    {
        var state = new CombatUnitSaveState
        {
            currentHealth = this.currentHealth,
            unitName = this.unitName
        };
        return JsonUtility.ToJson(state);
    }

    public virtual void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        var state = JsonUtility.FromJson<CombatUnitSaveState>(stateJson);
        if (state == null) return;

        this.unitName = state.unitName;
        this.currentHealth = Mathf.Clamp(state.currentHealth, 1, this.maxHealth);
    }
}
