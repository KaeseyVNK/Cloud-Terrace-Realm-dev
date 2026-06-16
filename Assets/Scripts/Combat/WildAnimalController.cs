using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

/// <summary>
/// Bộ điều khiển dành cho Động vật hoang dã (Wander, Flee, và sinh Food khi chết).
/// Kế thừa từ BaseCombatUnitController để tích hợp vào hệ thống RTS chung.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class WildAnimalController : BaseCombatUnitController, IPoolable
{
    [Header("Cấu hình Động Vật")]
    [Tooltip("Lượng thức ăn sinh ra khi bị tiêu diệt")]
    [SerializeField] private int _foodAmount = 50;

    [Tooltip("Khoảng cách tối đa để phát hiện mối đe dọa (lính hoặc quái vật)")]
    [SerializeField] private float _fleeRange = 6f;

    [Tooltip("Khoảng cách chạy trốn tối đa")]
    [SerializeField] private float _fleeDistance = 12f;

    [Tooltip("Tốc độ di chuyển lang thang bình thường")]
    [SerializeField] private float _walkSpeed = 1.5f;

    [Tooltip("Tốc độ chạy trốn khi có nguy hiểm")]
    [SerializeField] private float _fleeSpeed = 4.5f;

    [Tooltip("Bán kính di chuyển lang thang")]
    [SerializeField] private float _wanderRadius = 8f;

    private float _nextWanderTime = 0f;
    private bool _isFleeing = false;
    private float _fleeEndTime = 0f;
    private float _nextFleeScanTime = 0f;
    private Vector3 _wanderTarget;
    private Transform _currentThreat;
    private float _animVert = 0f;
    private float _animState = 0f;
    private FogVisibilityTarget _fogVisibility;

    public int FoodAmount
    {
        get => _foodAmount;
        set => _foodAmount = value;
    }

    protected override void Start()
    {
        // 1. Tìm và xóa script di chuyển CreatureMover mặc định trước để giải phóng dependency
        MonoBehaviour creatureMover = GetComponent("CreatureMover") as MonoBehaviour;
        if (creatureMover != null)
        {
            DestroyImmediate(creatureMover);
        }

        // 2. Sau đó mới gỡ bỏ CharacterController
        CharacterController cc = GetComponent<CharacterController>();
        float height = 2f;
        float radius = 0.5f;
        
        if (cc != null)
        {
            height = cc.height;
            radius = cc.radius;
            Vector3 center = cc.center;
            
            Destroy(cc);
            
            CapsuleCollider col = gameObject.AddComponent<CapsuleCollider>();
            col.height = height;
            col.radius = radius;
            col.center = center;
        }

        faction = UnitFaction.Neutral;
        autoAggroDuringMove = false; // Không tự động tấn công ai khi đang di chuyển
        returnToPoolOnDeath = true;  // Sử dụng pooling và trả về pool khi chết

        base.Start();

        if (navAgent != null)
        {
            navAgent.speed = _walkSpeed;
            
            if (cc != null)
            {
                navAgent.radius = radius;
                navAgent.height = height;
            }

            // Đảm bảo agent được kích hoạt và warp lên NavMesh
            if (!navAgent.enabled)
            {
                navAgent.enabled = true;
            }
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                navAgent.Warp(hit.position);
            }
        }

        // Tự động thêm động vật vào trình quản lý nếu chưa có
        if (WildlifeManager.Instance != null)
        {
            WildlifeManager.Instance.RegisterAnimal(this);
        }

        // Tìm kiếm và lưu vết component FogVisibilityTarget
        _fogVisibility = GetComponent<FogVisibilityTarget>();
        if (_fogVisibility == null)
        {
            _fogVisibility = GetComponentInChildren<FogVisibilityTarget>();
        }
        if (_fogVisibility == null)
        {
            _fogVisibility = GetComponentInParent<FogVisibilityTarget>();
        }
    }

    protected override void Update()
    {
        if (currentState == CombatState.Dead) return;

        bool isVisible = _fogVisibility == null || _fogVisibility.IsVisible;

        if (!isVisible)
        {
            // Ngoài sương mù: Quét mối đe dọa cực kỳ chậm (2.0 giây mỗi lần) để tiết kiệm CPU
            if (Time.time >= _nextFleeScanTime)
            {
                _nextFleeScanTime = Time.time + 2.0f;
                ScanForThreats();
            }

            // Kiểm tra hết thời gian hoảng loạn chạy trốn
            if (_isFleeing && Time.time >= _fleeEndTime)
            {
                _isFleeing = false;
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.speed = _walkSpeed;
                }
            }

            // Nếu không bị hoảng loạn chạy trốn, đóng băng di chuyển và bỏ qua việc cập nhật AI tiếp theo
            if (!_isFleeing)
            {
                if (navAgent != null && navAgent.enabled && !navAgent.isStopped)
                {
                    navAgent.isStopped = true;
                }
                return;
            }
        }
        else
        {
            // Trong tầm nhìn: Quét mối đe dọa nhanh (0.1 giây mỗi lần)
            if (Time.time >= _nextFleeScanTime)
            {
                _nextFleeScanTime = Time.time + 0.1f;
                ScanForThreats();
            }

            // Kiểm tra hết thời gian hoảng loạn chạy trốn
            if (_isFleeing && Time.time >= _fleeEndTime)
            {
                _isFleeing = false;
                if (navAgent != null && navAgent.enabled)
                {
                    navAgent.speed = _walkSpeed;
                }
            }

            // Khi hiển thị trở lại, khôi phục di chuyển nếu trước đó bị đóng băng
            if (navAgent != null && navAgent.enabled && navAgent.isStopped && currentState == CombatState.Moving)
            {
                navAgent.isStopped = false;
            }
        }

        base.Update();
    }

    protected override void HandleIdleState()
    {
        if (_isFleeing)
        {
            HandleFleeMovement();
            return;
        }

        // Di chuyển lang thang định kỳ
        if (Time.time >= _nextWanderTime)
        {
            PickRandomWanderTarget();
        }
    }

    protected override void HandleMovingState()
    {
        if (_isFleeing)
        {
            HandleFleeMovement();
            return;
        }

        if (IsNavAgentReady())
        {
            if (!navAgent.pathPending && navAgent.remainingDistance <= navAgent.stoppingDistance)
            {
                ChangeState(CombatState.Idle);
                _nextWanderTime = Time.time + Random.Range(4f, 8f);
            }
        }
    }

    protected override void HandleChasingState()
    {
        // Thú hoang không truy đuổi ai, lập tức về Idle
        ClearCurrentTargetAndIdle();
    }

    protected override void HandleAttackingState()
    {
        // Thú hoang không tấn công ai, lập tức về Idle
        ClearCurrentTargetAndIdle();
    }

    /// <summary>
    /// Quét tìm các đơn vị phe Player hoặc Enemy trong bán kính _fleeRange.
    /// </summary>
    private void ScanForThreats()
    {
        // Sử dụng mảng cache tĩnh của BaseCombatUnitController để tối ưu hóa rác GC
        int count = Physics.OverlapSphereNonAlloc(transform.position, _fleeRange, s_overlapCache);
        Transform closestThreat = null;
        float minDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            Collider col = s_overlapCache[i];
            if (col == null) continue;

            BaseCombatUnitController unit = col.GetComponentInParent<BaseCombatUnitController>();
            if (unit != null && unit != this && unit.currentState != CombatState.Dead)
            {
                // Chạy trốn cả người chơi (Player) và quái vật (Enemy)
                if (unit.faction == UnitFaction.Player || unit.faction == UnitFaction.Enemy)
                {
                    float dist = Vector3.Distance(transform.position, unit.transform.position);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        closestThreat = unit.transform;
                    }
                }
            }
        }

        System.Array.Clear(s_overlapCache, 0, count);

        if (closestThreat != null)
        {
            _currentThreat = closestThreat;
            _isFleeing = true;
            _fleeEndTime = Time.time + 3f; // Hoảng loạn trong 3 giây
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.speed = _fleeSpeed;
            }
            HandleFleeMovement();
        }
    }

    /// <summary>
    /// Tính toán hướng chạy trốn ngược lại với mối đe dọa.
    /// </summary>
    private void HandleFleeMovement()
    {
        if (_currentThreat == null || !IsNavAgentReady()) return;

        Vector3 fleeDirection = (transform.position - _currentThreat.position).normalized;
        Vector3 targetPos = transform.position + fleeDirection * _fleeDistance;

        // Thử tìm vị trí hợp lệ trên NavMesh theo hướng chạy trốn
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(hit.position);
            ChangeState(CombatState.Moving);
        }
    }

    protected override void OnDamagedBy(BaseCombatUnitController attacker)
    {
        base.OnDamagedBy(attacker);
        if (attacker != null && currentState != CombatState.Dead)
        {
            _currentThreat = attacker.transform;
            _isFleeing = true;
            _fleeEndTime = Time.time + 3f; // Hoảng loạn trong 3 giây
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.speed = _fleeSpeed;
            }
            HandleFleeMovement();
        }
    }

    /// <summary>
    /// Chọn ngẫu nhiên một điểm di chuyển lang thang.
    /// </summary>
    private void PickRandomWanderTarget()
    {
        if (!IsNavAgentReady()) return;

        Vector3 randomDirection = Random.insideUnitSphere * _wanderRadius;
        randomDirection += transform.position;

        if (NavMesh.SamplePosition(randomDirection, out NavMeshHit hit, _wanderRadius, NavMesh.AllAreas))
        {
            _wanderTarget = hit.position;
            navAgent.isStopped = false;
            navAgent.SetDestination(_wanderTarget);
            ChangeState(CombatState.Moving);
        }
        else
        {
            // Thử lại sau 1 giây nếu không chọn được điểm
            _nextWanderTime = Time.time + 1f;
        }
    }

    protected override void OnDeath()
    {
        if (WildlifeManager.Instance != null && WildlifeManager.Instance.FoodPrefab != null)
        {
            GridSystem grid = FindAnyObjectByType<GridSystem>();
            GridCell cell = null;
            if (grid != null)
            {
                cell = grid.GetCell(transform.position);
            }

            // Sinh ra mỏ tài nguyên thức ăn
            GameObject foodObj = Instantiate(WildlifeManager.Instance.FoodPrefab, transform.position, Quaternion.identity);
            foodObj.name = $"Food_Carcass_{cell?.x ?? 0}_{cell?.z ?? 0}";

            // Bỏ qua NavMesh để dân không bị kẹt khi đi xuyên qua
            NavMeshModifier modifier = foodObj.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = foodObj.AddComponent<NavMeshModifier>();
            }
            modifier.ignoreFromBuild = true;

            ResourceNode node = foodObj.GetComponent<ResourceNode>();
            if (node == null)
            {
                node = foodObj.AddComponent<ResourceNode>();
            }

            // Gán thông tin ô lưới
            if (cell != null)
            {
                cell.hasResource = true;
                cell.resourceType = ResourceType.Food;
                cell.resourceObject = foodObj;
                cell.isWalkable = false;
                cell.isBuildable = false;
            }

            node.Initialize(ResourceType.Food, _foodAmount, cell);
            Debug.Log($"[Wildlife] {unitName} chết, sinh ra mỏ Food trữ lượng {_foodAmount} tại {transform.position}");
        }
        else
        {
            Debug.LogWarning("[Wildlife] Không thể sinh mỏ Food vì thiếu WildlifeManager hoặc FoodPrefab.");
        }

        // Hủy đăng ký khỏi manager
        if (WildlifeManager.Instance != null)
        {
            WildlifeManager.Instance.RegisterDeath(this);
        }

        base.OnDeath();
    }

    protected override void UpdateAnimationState()
    {
        if (animator != null)
        {
            float targetVert = 0f;
            float targetState = 0f;

            // Xác định xem động vật có đang di chuyển thực tế trên NavMesh không
            bool isMoving = navAgent != null && navAgent.enabled && navAgent.velocity.sqrMagnitude > 0.01f;

            if (isMoving)
            {
                targetVert = 1f;
                // Nếu đang trong trạng thái Fleeing (chạy trốn), chuyển sang chạy (State = 1), ngược lại đi bộ (State = 0)
                targetState = _isFleeing ? 1f : 0f;
            }

            // Nội suy mượt mà các tham số Vert và State giống hệt cơ chế của CreatureMover
            float transitionSpeed = 4.5f;
            _animVert = Mathf.MoveTowards(_animVert, targetVert, Time.deltaTime * transitionSpeed);
            _animState = Mathf.MoveTowards(_animState, targetState, Time.deltaTime * transitionSpeed);

            animator.SetFloat("Vert", _animVert);
            animator.SetFloat("State", _animState);
        }
    }

    public void OnSpawnedFromPool()
    {
        RestoreBaseStats();
        returnToPoolOnDeath = true;
        faction = UnitFaction.Neutral;

        currentHealth = maxHealth;
        currentTarget = null;
        lastAttackTime = 0f;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;

        _isFleeing = false;
        _currentThreat = null;
        _animVert = 0f;
        _animState = 0f;

        // Kích hoạt lại NavMeshAgent
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
            navAgent.speed = _walkSpeed;
            navAgent.stoppingDistance = 0.2f;
            navAgent.ResetPath();
        }

        // Kích hoạt lại Colliders
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = true;
            }
        }

        // Thiết lập lại Rigidbody
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        // Khởi tạo lại Animator
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        ChangeState(CombatState.Idle);
        _nextWanderTime = Time.time + Random.Range(1f, 3f);

        // Đăng ký lại với WildlifeManager
        if (WildlifeManager.Instance != null)
        {
            WildlifeManager.Instance.RegisterAnimal(this);
        }
    }

    public void OnReturnedToPool()
    {
        currentTarget = null;
        isStunned = false;
        blockedTimer = 0f;
        isManualMoveCommand = false;
        _isFleeing = false;
        _currentThreat = null;
    }
}
