using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Trưởng nhóm điều khiển cư dân (Villager), quản lý FSM (Finite State Machine),
/// di chuyển, khai thác tài nguyên và xây dựng công trình.
/// Tuân thủ nghiêm ngặt các quy tắc Unity 6.2 của dự án.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class VillagerController : MonoBehaviour
{
    #region Serialized Fields

    [Header("Trạng thái")]
    [Tooltip("Trạng thái hiện tại của dân làng")]
    [SerializeField] private VillagerState _currentState = VillagerState.Idle;

    [Header("Xây dựng (Construction)")]
    [Tooltip("Công trình đang nhắm tới để xây dựng")]
    [SerializeField] private ConstructibleBuilding _targetBuilding;

    [Tooltip("Hệ số tốc độ xây dựng (1 = Bình thường, >1 = Xây nhanh hơn)")]
    [SerializeField] private float _buildSpeedMultiplier = 1f;

    [Header("Công cụ làm việc (Tools)")]
    [Tooltip("Búa hoặc rìu chặt gỗ")]
    [SerializeField] private GameObject _woodTool;

    [Tooltip("Cuốc đào đá")]
    [SerializeField] private GameObject _miningTool;

    [Tooltip("Cây cung đi săn")]
    [SerializeField] private GameObject _bowTool;

    [Header("Chuỗi Cung Ứng (Inventory)")]
    [Tooltip("Sức chứa tối đa của dân làng")]
    [SerializeField] private int _maxCarryCapacity = 5;

    [Tooltip("Thời gian để hoàn thành 1 chu kỳ khai thác (giây)")]
    [SerializeField] private float _timeToGather = 3f;

    [Tooltip("Số lượng tài nguyên khai thác mỗi chu kỳ")]
    [SerializeField] private int _gatherAmountPerTick = 1;

    [Header("Chuỗi Cung Ứng - Nộp Tài Nguyên")]
    [Tooltip("Thời gian để nộp tài nguyên vào kho (giây)")]
    [SerializeField] private float _timeToDeposit = 2f;

    [Header("Auto Work After Construction")]
    [Tooltip("Radius used to find nearby resources after finishing a matching resource storage building.")]
    [SerializeField] private float _autoGatherAfterBuildRadius = 40f;

    [Header("Crowd Control")]
    [Tooltip("NavMeshAgent radius used at runtime. Keep this smaller than the visual collider so villagers can pass in groups.")]
    [SerializeField] private float _runtimeAgentRadius = 0.32f;

    [Tooltip("Only disable avoidance when the villager is very close to its exact work position.")]
    [SerializeField] private float _disableAvoidanceNearTargetDistance = 0.9f;

    [Tooltip("Minimum spacing between villagers standing around the same construction site.")]
    [SerializeField] private float _builderSlotSpacing = 1.25f;

    [Header("Bắn cung đi săn")]
    [Tooltip("Tầm bắn cung khi đi săn thú/quái")]
    [SerializeField] private float _huntRange = 8f;

    [Tooltip("Thời gian nạp/ngắm bắn cung")]
    [SerializeField] private float _aimDuration = 1.2f;

    [Tooltip("Prefab mũi tên bắn đi")]
    [SerializeField] private GameObject _arrowPrefab;

    [Tooltip("Điểm xuất phát bắn tên trên người dân làng")]
    [SerializeField] private Transform _bowFirePoint;

    [Tooltip("Sát thương mỗi lần bắn trúng khi đi săn")]
    [SerializeField] private int _huntDamage = 15;

    [Tooltip("Hệ số nhân số lượng tài nguyên khai thác mỗi chu kỳ khi thu hoạch xác thú hoang dã (so với khai thác gỗ/đá thông thường)")]
    [SerializeField] private int _huntGatherMultiplier = 3;

    #endregion

    #region Private Fields

    private GridSystem _gridSystem;
    private NavMeshAgent _navAgent;
    private Animator _animator;
    private float _baseAgentSpeed = 3.5f;
    private bool _isHungry = false;
    private VillagerCarryVisuals _carryVisuals;

    private Job _currentJob;
    private ResourceType _targetResource;
    private readonly Dictionary<ResourceType, int> _inventory = new();
    private int _totalCarryAmount = 0;

    private float _gatherTimer = 0f;
    private float _depositTimer = 0f;
    private Vector3 _storageDropoffTarget = Vector3.zero;
    private Vector3 _buildTargetPos = Vector3.zero;
    private ResourceNode _reservedNode;
    private int _avoidancePriorityOffset;
    private readonly List<Vector3> _failedResourcePositions = new List<Vector3>();
    private ConstructibleBuilding _autoGatherBuildingAfterDeposit;
    private int _assignedBuildSlotIndex = -1;
    private BaseCombatUnitController _repairTarget;
    private float _repairResourceTimer = 0f;
    private bool _isManualMove = false;

    // Cache Animator parameter status
    private bool _hasBuildingParam;
    private bool _hasGatheringParam;
    private bool _hasDepositingParam;
    private bool _hasIdleParam;
    private bool _hasMovingParam;
    private bool _hasInteractTypeParam;
    private bool _hasIsAimingParam;
    private bool _hasAttackTriggerParam;

    private WildAnimalController _huntTarget;
    private bool _wasFarmingWildAnimals = false;

    private int _pathRetryCount = 0;
    private float _stuckTimer = 0f;

    private const int MaxPathRetries = 5;

    private HouseShelter _assignedShelter;
    private WatchTowerGarrison _assignedGarrison;
    private bool _overrideShelter = false;
    private float _shelterSearchTimer = 0f;
    private const float ShelterSearchCooldown = 1.5f;

    // Cache to restore previous work
    private VillagerState _preShelterState = VillagerState.Idle;
    private Job _preShelterJob;
    private ConstructibleBuilding _preShelterBuilding;
    private ResourceType _preShelterResource;

    // Bộ sưu tập tĩnh được tối ưu hóa để tránh allocations bộ nhớ trong runtime
    private static readonly List<VillagerController> s_tempBuilders = new(32);
    private static readonly List<VillagerController> s_tempGatherers = new(32);
    private static readonly List<VillagerController> s_tempIndexBuilders = new(32);
    private static readonly List<VillagerController> s_tempIndexGatherers = new(32);
    private static readonly List<VillagerController> s_tempCompletedBuilders = new(32);

    public static readonly List<VillagerController> AllVillagers = new List<VillagerController>();

    #endregion

    #region Public Properties

    /// <summary>
    /// Trạng thái hiện tại của dân làng.
    /// </summary>
    public VillagerState CurrentState => _currentState;

    /// <summary>
    /// Cho biết dân làng có đang bị đói hay không.
    /// </summary>
    public bool IsHungry => _isHungry;

    /// <summary>
    /// Công trình đang nhắm tới để xây dựng.
    /// </summary>
    public ConstructibleBuilding TargetBuilding
    {
        get => _targetBuilding;
        set
        {
            if (_targetBuilding == value) return;

            _targetBuilding = value;
            if (_targetBuilding == null)
            {
                _assignedBuildSlotIndex = -1;
            }
            else
            {
                _assignedBuildSlotIndex = AssignFreeBuildSlot(_targetBuilding);
                _buildTargetPos = CalculateTargetBuildPosition(_targetBuilding, _assignedBuildSlotIndex);
            }
        }
    }

    /// <summary>
    /// Hệ số tốc độ xây dựng.
    /// </summary>
    public float BuildSpeedMultiplier => _buildSpeedMultiplier;

    /// <summary>
    /// Búa hoặc rìu chặt gỗ.
    /// </summary>
    public GameObject WoodTool => _woodTool;

    /// <summary>
    /// Cuốc đào đá.
    /// </summary>
    public GameObject MiningTool => _miningTool;

    /// <summary>
    /// Công việc hiện tại đang được gán.
    /// </summary>
    public Job CurrentJob => _currentJob;

    /// <summary>
    /// Loại tài nguyên đang nhắm tới.
    /// </summary>
    public ResourceType TargetResource => _targetResource;

    /// <summary>
    /// Túi đồ hiện tại của dân làng.
    /// </summary>
    public IReadOnlyDictionary<ResourceType, int> Inventory => _inventory;

    /// <summary>
    /// Sức chứa tối đa của dân làng.
    /// </summary>
    public int MaxCarryCapacity => EffectiveMaxCarryCapacity;

    /// <summary>
    /// Thời gian khai thác mỗi chu kỳ.
    /// </summary>
    public float TimeToGather => GetEffectiveTimeToGather(_targetResource);

    /// <summary>
    /// Số lượng tài nguyên khai thác mỗi chu kỳ.
    /// </summary>
    public int GatherAmountPerTick => _gatherAmountPerTick;

    /// <summary>
    /// Thời gian nộp tài nguyên.
    /// </summary>
    public float TimeToDeposit => _timeToDeposit;

    /// <summary>
    /// Vị trí đứng xây dựng được phân bổ riêng.
    /// </summary>
    public Vector3 BuildTargetPos => _buildTargetPos;

    #endregion

    #region Backward Compatibility Properties (for JobBroker)

    /// <summary>
    /// Thuộc tính cũ phục vụ tương thích ngược với JobBroker.
    /// </summary>
    public VillagerState currentState
    {
        get => _currentState;
        set => ChangeState(value);
    }

    /// <summary>
    /// Thuộc tính cũ phục vụ tương thích ngược với JobBroker.
    /// </summary>
    public Job currentJob
    {
        get => _currentJob;
        set => _currentJob = value;
    }

    /// <summary>
    /// Thuộc tính cũ phục vụ tương thích ngược với JobBroker.
    /// </summary>
    public ConstructibleBuilding targetBuilding
    {
        get => TargetBuilding;
        set => TargetBuilding = value;
    }

    /// <summary>
    /// Thuộc tính cũ phục vụ tương thích ngược với JobBroker.
    /// </summary>
    public ResourceType targetResource
    {
        get => _targetResource;
        set => _targetResource = value;
    }

    #endregion

    #region Unity Lifecycle Methods

    private void OnEnable()
    {
        AllVillagers.Add(this);
    }

    private void OnDisable()
    {
        AllVillagers.Remove(this);
        if (TechnologyManager.HasInstance)
        {
            TechnologyManager.Instance.OnTechnologyUnlocked -= HandleTechnologyUnlocked;
        }
        if (_carryVisuals != null)
        {
            _carryVisuals.Hide();
        }
    }

    private void Awake()
    {
        _gridSystem = FindAnyObjectByType<GridSystem>();
        

        // Tạo một offset ngẫu nhiên cố định từ 0 đến 14 dựa trên HashCode độc nhất
        _avoidancePriorityOffset = Mathf.Abs(GetHashCode()) % 15;

        // Khởi tạo NavMesh Agent
        if (TryGetComponent(out _navAgent))
        {
            _navAgent.radius = Mathf.Max(0.05f, _runtimeAgentRadius);
            _navAgent.stoppingDistance = 0.25f;
            _navAgent.avoidancePriority = 50 + _avoidancePriorityOffset;
            _navAgent.autoBraking = true;
            _baseAgentSpeed = _navAgent.speed;
        }

        // BẬT isTrigger cho Collider của dân làng để tránh kẹt cứng vật lý
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        // Tự động thêm VillagerCombatTarget để kẻ địch có thể tấn công dân làng
        if (GetComponent<VillagerCombatTarget>() == null)
        {
            gameObject.AddComponent<VillagerCombatTarget>();
        }

        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _hasBuildingParam = HasParameter("IsBuilding");
            _hasGatheringParam = HasParameter("IsGathering");
            _hasDepositingParam = HasParameter("IsDepositing");
            _hasIdleParam = HasParameter("IsIdle");
            _hasMovingParam = HasParameter("IsMoving");
            _hasInteractTypeParam = HasParameter("InteractType");
            _hasIsAimingParam = HasParameter("isAiming");
            _hasAttackTriggerParam = HasParameter("Attack");
        }

        _carryVisuals = GetComponent<VillagerCarryVisuals>();
        if (_carryVisuals == null)
        {
            _carryVisuals = gameObject.AddComponent<VillagerCarryVisuals>();
        }
    }

    private void Start()
    {
        UpdateAnimationState();
        UpdateActiveTools();
        UpdateCarryVisuals();

        // Tự động gắn đèn cho dân làng
        if (gameObject.GetComponent<UnitLightController>() == null)
        {
            gameObject.AddComponent<UnitLightController>();
        }

        if (TechnologyManager.HasInstance)
        {
            TechnologyManager.Instance.OnTechnologyUnlocked += HandleTechnologyUnlocked;
        }
        ApplyVillagerTechnologyStats();
    }

    public void ClearAssignedShelter()
    {
        _assignedShelter = null;
    }

    /// <summary>
    /// Dọn dẹp biến tham chiếu tháp canh đồn trú khi dân làng đã vào bên trong.
    /// </summary>
    public void ClearAssignedGarrison()
    {
        _assignedGarrison = null;
    }

    private void CancelAssignedGarrison()
    {
        if (_assignedGarrison == null)
        {
            return;
        }

        SelectableUnit selectable = GetComponent<SelectableUnit>();
        if (selectable != null)
        {
            _assignedGarrison.CancelReservation(selectable);
        }

        _assignedGarrison = null;
    }

    public void SetOverrideShelter(bool value)
    {
        _overrideShelter = value;
    }

    /// <summary>
    /// Khôi phục lại công việc trước khi đi trú ẩn.
    /// </summary>
    public void ResumePostShelterState()
    {
        if (_navAgent != null && !_navAgent.enabled)
        {
            _navAgent.enabled = true;
        }

        if (_preShelterBuilding != null && !_preShelterBuilding.IsCompleted)
        {
            Debug.Log($"[Villager] Khôi phục xây dựng công trình: {_preShelterBuilding.name}");
            CommandBuild(_preShelterBuilding);
        }
        else if (_preShelterJob != null)
        {
            Debug.Log($"[Villager] Khôi phục khai thác tài nguyên: {_preShelterResource}");
            AssignJob(_preShelterJob);
        }
        else if (GetTotalCarryAmount() > 0)
        {
            if (BuildingManager.Instance != null)
            {
                _targetResource = _preShelterResource;
                _storageDropoffTarget = BuildingManager.Instance.FindNearestDropoff(transform.position, _targetResource);
                if (_storageDropoffTarget != Vector3.zero && SetPathToTarget(_storageDropoffTarget))
                {
                    ChangeState(VillagerState.Moving);
                }
                else
                {
                    ChangeState(VillagerState.Idle);
                }
            }
            else
            {
                ChangeState(VillagerState.Idle);
            }
        }
        else
        {
            ChangeState(VillagerState.Idle);
        }

        // Xóa bộ nhớ cache
        _preShelterState = VillagerState.Idle;
        _preShelterJob = null;
        _preShelterBuilding = null;
    }

    private HouseShelter FindClosestAvailableShelter()
    {
        HouseShelter[] shelters = FindObjectsByType<HouseShelter>(FindObjectsInactive.Exclude);
        HouseShelter closest = null;
        float closestDistSqr = float.MaxValue;

        foreach (HouseShelter shelter in shelters)
        {
            if (shelter == null || !shelter.IsOperational() || !shelter.HasSpace) continue;

            float distSqr = (transform.position - shelter.transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = shelter;
            }
        }

        return closest;
    }

    private WatchTowerGarrison FindClosestAvailableGarrison()
    {
        WatchTowerGarrison[] garrisons = FindObjectsByType<WatchTowerGarrison>(FindObjectsInactive.Exclude);
        WatchTowerGarrison closest = null;
        float closestDistSqr = float.MaxValue;

        foreach (WatchTowerGarrison garrison in garrisons)
        {
            if (garrison == null || !garrison.IsOperational() || !garrison.HasSpace) continue;

            float distSqr = (transform.position - garrison.transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = garrison;
            }
        }

        return closest;
    }

    private void CachePreShelterWorkIfNeeded()
    {
        if (_preShelterState != VillagerState.Idle)
        {
            return;
        }

        _preShelterState = _currentState;
        _preShelterJob = _currentJob;
        _preShelterBuilding = _targetBuilding;
        _preShelterResource = _targetResource;
    }

    private void Update()
    {
        // Kiểm tra nhu cầu trú ẩn (mưa, đêm, hoặc lệnh khẩn cấp)
        bool needsShelter = HouseShelter.IsEmergencyShelterActive || 
                            (!_overrideShelter && (
                                (TimeManager.Instance != null && TimeManager.Instance.IsNight) || 
                                (WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain)
                            ));

        // Reset cờ override khi thời tiết và thời gian đã trở lại bình thường
        bool isEnvironmentShelterNeeded = (TimeManager.Instance != null && TimeManager.Instance.IsNight) || 
                                          (WeatherManager.Instance != null && WeatherManager.Instance.CurrentWeather == WeatherState.Rain);
        if (!isEnvironmentShelterNeeded)
        {
            _overrideShelter = false;
        }

        if (needsShelter && _currentState != VillagerState.Sheltered)
        {
            bool isEmergencyShelter = HouseShelter.IsEmergencyShelterActive;
            bool shouldUseHouseShelter = true;

            if (isEmergencyShelter)
            {
                SelectableUnit selectable = GetComponent<SelectableUnit>();
                if (_assignedGarrison != null && (!_assignedGarrison.IsOperational() || !_assignedGarrison.IsTrackingUnit(selectable)))
                {
                    _assignedGarrison = null;
                }

                shouldUseHouseShelter = _assignedGarrison == null;
                if (_assignedGarrison == null)
                {
                    _shelterSearchTimer += Time.deltaTime;
                    if (_shelterSearchTimer >= ShelterSearchCooldown || _shelterSearchTimer <= Time.deltaTime)
                    {
                        _shelterSearchTimer = 0f;
                        WatchTowerGarrison closestGarrison = FindClosestAvailableGarrison();
                        if (closestGarrison != null && selectable != null)
                        {
                            CachePreShelterWorkIfNeeded();
                            if (closestGarrison.TrySendToGarrison(selectable))
                            {
                                _assignedGarrison = closestGarrison;
                                shouldUseHouseShelter = false;
                            }
                        }
                    }
                }
            }

            if (shouldUseHouseShelter && (_assignedShelter == null || !_assignedShelter.HasSpace || !_assignedShelter.IsOperational()))
            {
                if (_assignedShelter != null)
                {
                    _assignedShelter.CancelReservation(this);
                    _assignedShelter = null;
                }

                _shelterSearchTimer += Time.deltaTime;
                if (_shelterSearchTimer >= ShelterSearchCooldown || _shelterSearchTimer <= Time.deltaTime)
                {
                    _shelterSearchTimer = 0f;
                    HouseShelter closestShelter = FindClosestAvailableShelter();
                    if (closestShelter != null)
                    {
                        CachePreShelterWorkIfNeeded();
                        _assignedShelter = closestShelter;
                        _assignedShelter.TryReserveSpot(this);
                        if (SetPathToTarget(_assignedShelter.transform.position))
                        {
                            ChangeState(VillagerState.Moving);
                        }
                    }
                }
            }
            else if (shouldUseHouseShelter)
            {
                if (_currentState != VillagerState.Moving)
                {
                    if (SetPathToTarget(_assignedShelter.transform.position))
                    {
                        ChangeState(VillagerState.Moving);
                    }
                }
            }
        }
        else if (!needsShelter && _assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
            ResumePostShelterState();
        }
        else if (!needsShelter && _assignedGarrison != null)
        {
            CancelAssignedGarrison();
            ResumePostShelterState();
        }

        switch (_currentState)
        {
            case VillagerState.Idle:
                break;
            case VillagerState.Moving:
                HandleMovement();
                break;
            case VillagerState.Gathering:
                HandleGathering();
                break;
            case VillagerState.Depositing:
                HandleDepositing();
                break;
            case VillagerState.Building:
                HandleBuilding();
                break;
            case VillagerState.Sheltered:
                break;
        }
    }

    private void OnDestroy()
    {
        ReleaseReservedSlot();
        TargetBuilding = null;
    }

    #endregion

    #region FSM Management

    /// <summary>
    /// Chuyển đổi trạng thái FSM hiện tại của cư dân và thiết lập các thông số NavMesh tương ứng.
    /// </summary>
    public void ChangeState(VillagerState newState)
    {
        _currentState = newState;
        _stuckTimer = 0f;

        if (_currentState != VillagerState.Gathering)
        {
            if (_animator != null && _hasIsAimingParam)
            {
                _animator.SetBool("isAiming", false);
            }
        }

        if (_currentState == VillagerState.Gathering)
        {
            _failedResourcePositions.Clear();
        }

        // Giữ slot khai thác khi đang đi tới mỏ hoặc đang khai thác; thả ra khi rời job đó.
        if (_currentState == VillagerState.Idle ||
            _currentState == VillagerState.Depositing ||
            _currentState == VillagerState.Building ||
            _currentState == VillagerState.Sheltered ||
            (_currentState == VillagerState.Moving && GetTotalCarryAmount() > 0))
        {
            ReleaseReservedSlot();
        }

        // Đảm bảo dọn dẹp liên kết công trình khi không còn xây dựng/di chuyển
        if (_currentState != VillagerState.Moving && _currentState != VillagerState.Building)
        {
            TargetBuilding = null;
        }

        if (_navAgent != null)
        {
            if (_currentState != VillagerState.Sheltered && !_navAgent.enabled)
            {
                _navAgent.enabled = true;
            }

            bool isAgentActiveOnNavMesh = _navAgent.enabled && _navAgent.isOnNavMesh;
            int uniqueOffset = _avoidancePriorityOffset % 10;

            if (_currentState == VillagerState.Idle)
            {
                if (isAgentActiveOnNavMesh)
                {
                    _navAgent.isStopped = true;
                    _navAgent.ResetPath();
                }
                // Dân nhàn rỗi: Ưu tiên thấp nhất (80-89) để dễ dàng nhường đường cho mọi người đang làm việc
                _navAgent.avoidancePriority = 80 + uniqueOffset;
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }
            else if (_currentState == VillagerState.Gathering)
            {
                if (isAgentActiveOnNavMesh)
                {
                    _navAgent.isStopped = true;
                    _navAgent.ResetPath();
                    _navAgent.velocity = Vector3.zero;
                }
                // Dân đang khai thác đứng tại chỗ: Ưu tiên cực cao (10-19), người khác đi chuyển phải né họ ra
                _navAgent.avoidancePriority = 10 + uniqueOffset;
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }
            else if (_currentState == VillagerState.Moving)
            {
                if (isAgentActiveOnNavMesh)
                {
                    if (_navAgent.isStopped) _navAgent.isStopped = false;
                }
                if (GetTotalCarryAmount() > 0)
                {
                    // Dân đang vận chuyển tài nguyên về kho: Ưu tiên cao nhất trong nhóm di chuyển (30-39)
                    _navAgent.avoidancePriority = 30 + uniqueOffset;
                }
                else if (_targetBuilding != null)
                {
                    // Dân đang đi xây dựng công trình: Ưu tiên trung bình (40-49)
                    _navAgent.avoidancePriority = 40 + uniqueOffset;
                }
                else if (_currentJob != null)
                {
                    // Dân đang đi tới mỏ (tay không): Ưu tiên thấp hơn (50-59) để phải né tránh dân mang đồ về
                    _navAgent.avoidancePriority = 50 + uniqueOffset;
                }
                else
                {
                    // Di chuyển tự do/khác: Ưu tiên thấp (70-79)
                    _navAgent.avoidancePriority = 70 + uniqueOffset;
                }
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            }
            else if (_currentState == VillagerState.Depositing)
            {
                if (isAgentActiveOnNavMesh)
                {
                    _navAgent.isStopped = true;
                    _navAgent.ResetPath();
                    _navAgent.velocity = Vector3.zero;
                }
                // Đang đứng tại kho nộp đồ: Nhường đường tốt (80-89) để tránh chắn lối vào kho
                _navAgent.avoidancePriority = 80 + uniqueOffset;
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }
            else if (_currentState == VillagerState.Building)
            {
                if (isAgentActiveOnNavMesh)
                {
                    _navAgent.isStopped = true;
                    _navAgent.ResetPath();
                    _navAgent.velocity = Vector3.zero;
                }
                // Dân đang xây công trình tại chỗ: Ưu tiên cực cao (10-19) để đứng yên làm việc vững vàng
                _navAgent.avoidancePriority = 10 + uniqueOffset;
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }
            else if (_currentState == VillagerState.Sheltered)
            {
                if (isAgentActiveOnNavMesh)
                {
                    _navAgent.isStopped = true;
                    if (_navAgent.hasPath) _navAgent.ResetPath();
                    _navAgent.velocity = Vector3.zero;
                }
                _navAgent.enabled = false;
            }
        }

        UpdateAnimationState();
        UpdateActiveTools();
    }

    #endregion

    #region Movement Handling & Pathfinding

    private void HandleMovement()
    {
        if (_navAgent == null) return;

        if (_huntTarget != null)
        {
            if (_huntTarget.currentState == CombatState.Dead)
            {
                _navAgent.ResetPath();
                OnReachedDestination();
                return;
            }

            // Nếu đang đi săn và đã lọt vào tầm bắn _huntRange, cho dừng lại và bắn
            float distToTarget = Vector3.Distance(transform.position, _huntTarget.transform.position);
            if (distToTarget <= _huntRange)
            {
                _navAgent.ResetPath();
                OnReachedDestination();
                return;
            }

            float distToTargetDest = Vector3.Distance(_navAgent.destination, _huntTarget.transform.position);
            if (distToTargetDest > 0.5f)
            {
                SetPathToTarget(_huntTarget.transform.position);
            }
        }

        // Stuck Solver (AOE/SC2 Style)
        if (_navAgent.hasPath && _navAgent.velocity.sqrMagnitude < 0.05f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer > 1.2f)
            {
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }
        }
        else
        {
            _stuckTimer = 0f;

            // Khi gần đến đích làm việc, tạm thời tắt né tránh để cập bến hoàn hảo
            bool isNearWorkTarget = false;
            float disableAvoidanceDistanceSqr = _disableAvoidanceNearTargetDistance * _disableAvoidanceNearTargetDistance;
            if (_targetBuilding != null)
            {
                float distToDestSqr = (transform.position - _buildTargetPos).sqrMagnitude;
                if (distToDestSqr <= disableAvoidanceDistanceSqr)
                {
                    isNearWorkTarget = true;
                }
            }
            else if (_currentJob != null)
            {
                float distToJobSqr = (transform.position - _currentJob.position).sqrMagnitude;
                if (distToJobSqr <= disableAvoidanceDistanceSqr)
                {
                    isNearWorkTarget = true;
                }
            }

            if (isNearWorkTarget)
            {
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
            }
            else if (_navAgent.obstacleAvoidanceType == ObstacleAvoidanceType.NoObstacleAvoidance && _currentState == VillagerState.Moving)
            {
                _navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            }
        }

        // Kiểm tra đích đến
        if (!_navAgent.pathPending)
        {
            float sqrDist = (transform.position - _navAgent.destination).sqrMagnitude;
            
            // Nếu đi làm việc (mỏ hoặc công trình), nới lỏng khoảng cách dừng lên 0.65m để tránh kẹt do chen lấn
            float stopDist = _navAgent.stoppingDistance;
            if (_currentJob != null || _targetBuilding != null)
            {
                stopDist = Mathf.Max(stopDist, 0.65f);
            }
            float sqrStoppingDistance = stopDist * stopDist;

            if (sqrDist <= sqrStoppingDistance)
            {
                // Chấp nhận vận tốc nhỏ < 0.25f (do chen lấn hoặc đang giảm tốc) thay vì bắt buộc bằng 0 tuyệt đối
                if (!_navAgent.hasPath || _navAgent.velocity.sqrMagnitude < 0.25f)
                {
                    OnReachedDestination();
                }
            }
        }
    }

    private bool SetPathToTarget(Vector3 targetPos)
    {
        if (_navAgent == null) return false;

        // Bật agent lên nếu nó đang bị tắt (ví dụ sau khi thoát khỏi trạng thái Sheltered)
        if (!_navAgent.enabled)
        {
            _navAgent.enabled = true;
        }

        // Nếu Agent bị văng ra khỏi NavMesh, Warp quay lại
        if (!_navAgent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit warpHit, 3.0f, NavMesh.AllAreas))
            {
                _navAgent.Warp(warpHit.position);
            }
            else
            {
                Debug.LogWarning($"[NavMesh] {gameObject.name} rời NavMesh và không thể Warp!");
                return false;
            }
        }

        _navAgent.stoppingDistance = 0.25f;

        NavMeshPath path = new NavMeshPath();
        _navAgent.CalculatePath(targetPos, path);

        if (path.status == NavMeshPathStatus.PathComplete || path.status == NavMeshPathStatus.PathPartial)
        {
            Vector3 finalTarget = targetPos;
            if (path.corners.Length > 0)
            {
                finalTarget = path.corners[path.corners.Length - 1];
            }

            _navAgent.SetDestination(finalTarget);
            _navAgent.isStopped = false;
            return true;
        }

        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, 20.0f, NavMesh.AllAreas))
        {
            _navAgent.SetDestination(hit.position);
            _navAgent.isStopped = false;
            return true;
        }

        Debug.LogWarning($"[NavMesh] Không tìm được đường đi đến {targetPos}!");
        return false;
    }

    private void OnReachedDestination()
    {
        if (_isManualMove)
        {
            _isManualMove = false;
            _pathRetryCount = 0;
            ChangeState(VillagerState.Idle);
            return;
        }

        if (_huntTarget != null)
        {
            _pathRetryCount = 0;
            _gatherTimer = 0f;
            ChangeState(VillagerState.Gathering);
            return;
        }

        // Tình huống 4: Đến nhà trú ẩn (Phải được kiểm tra ĐẦU TIÊN để tránh bị đè bởi các trạng thái khác)
        if (_assignedShelter != null)
        {
            _pathRetryCount = 0;
            _assignedShelter.EnterShelter(this);
            return;
        }

        // Tình huống 1: Khai thác tài nguyên
        if (GetTotalCarryAmount() < MaxCarryCapacity && _currentJob != null && _targetBuilding == null)
        {
            float distToJobSqr = (transform.position - _currentJob.position).sqrMagnitude;
            if (distToJobSqr > 42.25f) // Tăng lên 6.5m để hoàn toàn tương thích với cự ly đứng của slot xếp hàng vòng ngoài (4.0m) và sai số NavMesh
            {
                if (_pathRetryCount >= MaxPathRetries)
                {
                    Debug.LogWarning($"[Logistics] {gameObject.name} không thể tiếp cận mỏ tài nguyên tại {_currentJob.position}. Thêm vào danh sách mỏ kẹt và tìm mỏ thay thế...");
                    _pathRetryCount = 0;
                    _failedResourcePositions.Add(_currentJob.position);

                    ResourceNode nextNode = FindNearbyResource(_currentJob.position, _targetResource, 50.0f, _failedResourcePositions);
                    if (nextNode != null)
                    {
                        Debug.Log($"[Logistics] {gameObject.name} tìm thấy mỏ tài nguyên thay thế tại {nextNode.transform.position}. Di chuyển...");
                        _currentJob.position = nextNode.transform.position;
                        if (SetPathToTarget(GetHarvestPositionAroundNode(_currentJob.position)))
                        {
                            ChangeState(VillagerState.Moving);
                            return;
                        }
                    }

                    Debug.LogWarning($"[Logistics] {gameObject.name} không tìm thấy mỏ tài nguyên thay thế nào khác. Huỷ.");
                    if (JobBroker.Instance != null && _currentJob != null) JobBroker.Instance.RemoveJob(_currentJob);
                    _currentJob = null;
                    ChangeState(VillagerState.Idle);
                    return;
                }
                _pathRetryCount++;
                SetPathToTarget(GetHarvestPositionAroundNode(_currentJob.position));
                return;
            }

            _pathRetryCount = 0;
            _gatherTimer = 0f;
            ChangeState(VillagerState.Gathering);
        }
        // Tình huống 2: Nộp tài nguyên vào kho
        else if (GetTotalCarryAmount() > 0 && _targetBuilding == null)
        {
            if (_storageDropoffTarget == Vector3.zero)
            {
                // Xác định loại tài nguyên đang mang theo người
                ResourceType carriedType = ResourceType.Wood;
                foreach (var kvp in _inventory)
                {
                    if (kvp.Value > 0)
                    {
                        carriedType = kvp.Key;
                        break;
                    }
                }
                _targetResource = carriedType;
                _storageDropoffTarget = BuildingManager.Instance != null 
                    ? BuildingManager.Instance.FindNearestDropoff(transform.position, _targetResource)
                    : Vector3.zero;
            }

            if (_storageDropoffTarget == Vector3.zero)
            {
                // Vẫn không tìm thấy kho nào hợp lệ -> Đứng yên
                _pathRetryCount = 0;
                ChangeState(VillagerState.Idle);
                return;
            }

            float distToStorageSqr = (transform.position - _storageDropoffTarget).sqrMagnitude;
            if (distToStorageSqr > 12.25f)
            {
                if (_pathRetryCount >= MaxPathRetries)
                {
                    Debug.LogWarning($"[Logistics] {gameObject.name} không thể tiếp cận nhà kho. Huỷ.");
                    _pathRetryCount = 0;
                    ChangeState(VillagerState.Idle);
                    return;
                }
                _pathRetryCount++;
                SetPathToTarget(_storageDropoffTarget);
                return;
            }

            _pathRetryCount = 0;
            _depositTimer = 0f;
            ChangeState(VillagerState.Depositing);
        }
        // Tình huống 3: Đến công trình xây dựng
        else if (_targetBuilding != null && !_targetBuilding.IsCompleted)
        {
            if (_navAgent != null)
            {
                float distToDestSqr = (transform.position - _buildTargetPos).sqrMagnitude;
                bool reachedSlot = distToDestSqr <= 4.0f; // 2.0m * 2.0m = 4f

                bool reachedBuildingPerimeter = false;
                Vector3 buildingPos = _targetBuilding.transform.position;
                BuildingData data = null;
                if (BuildingManager.Instance != null && BuildingManager.Instance.BuildingDataMap.TryGetValue(_targetBuilding.gameObject, out var bData))
                {
                    data = bData;
                }

                float halfWidth = 1.5f;
                float halfLength = 1.5f;
                if (data != null && _gridSystem != null)
                {
                    float cellSize = _gridSystem.GetCellSize();
                    halfWidth = (data.buildingSize.x * cellSize) / 2f;
                    halfLength = (data.buildingSize.y * cellSize) / 2f;
                }

                float minX = buildingPos.x - halfWidth;
                float maxX = buildingPos.x + halfWidth;
                float minZ = buildingPos.z - halfLength;
                float maxZ = buildingPos.z + halfLength;

                float closestX = Mathf.Clamp(transform.position.x, minX, maxX);
                float closestZ = Mathf.Clamp(transform.position.z, minZ, maxZ);
                Vector3 closestPointOnEdge = new Vector3(closestX, transform.position.y, closestZ);

                float distToBuilding = Vector3.Distance(transform.position, closestPointOnEdge);
                if (distToBuilding <= 2.2f)
                {
                    reachedBuildingPerimeter = true;
                }

                if (!reachedSlot && !reachedBuildingPerimeter)
                {
                    if (_pathRetryCount >= MaxPathRetries)
                    {
                        Debug.LogWarning($"[Logistics] {gameObject.name} không thể tiếp cận công trình. Huỷ.");
                        _pathRetryCount = 0;
                        TargetBuilding = null;
                        ChangeState(VillagerState.Idle);
                        return;
                    }
                    _pathRetryCount++;
                    SetPathToTarget(_buildTargetPos);
                    return;
                }
            }

            _pathRetryCount = 0;
            ChangeState(VillagerState.Building);
        }
        // Tình huống 3.2: Đến công trình sửa chữa
        else if (_repairTarget != null && _repairTarget.currentHealth < _repairTarget.maxHealth)
        {
            if (_navAgent != null)
            {
                Vector3 buildingPos = _repairTarget.transform.position;
                Collider col = _repairTarget.GetComponent<Collider>();
                float boundsRadius = 2.0f;
                if (col != null)
                {
                    boundsRadius = Mathf.Max(boundsRadius, col.bounds.extents.x + 1.2f);
                }

                float distToBuilding = Vector3.Distance(transform.position, buildingPos);
                if (distToBuilding > boundsRadius + 0.5f)
                {
                    if (_pathRetryCount >= MaxPathRetries)
                    {
                        Debug.LogWarning($"[Logistics] {gameObject.name} không thể tiếp cận công trình để sửa chữa. Huỷ.");
                        _pathRetryCount = 0;
                        _repairTarget = null;
                        ChangeState(VillagerState.Idle);
                        return;
                    }
                    _pathRetryCount++;
                    SetPathToTarget(buildingPos);
                    return;
                }
            }

            _pathRetryCount = 0;
            ChangeState(VillagerState.Building);
        }
        else
        {
            _pathRetryCount = 0;
            ChangeState(VillagerState.Idle);
        }
    }

    #endregion

    #region Job Execution: Gathering

    private void HandleGathering()
    {
        if (_huntTarget != null)
        {
            if (_huntTarget.currentState == CombatState.Dead)
            {
                if (_animator != null && _hasIsAimingParam)
                {
                    _animator.SetBool("isAiming", false);
                }

                // Thú hoang đã chết! Đánh dấu chế độ săn bắn để tự động tìm con thú tiếp theo.
                _wasFarmingWildAnimals = true;

                // Tìm mỏ Food vừa rơi ra gần đó để khai thác.
                ResourceNode spawnedFood = null;
                float minD = float.MaxValue;
                Vector3 targetDeathPos = _huntTarget.transform.position;
                ResourceNode[] nodes = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None);
                foreach (var n in nodes)
                {
                    if (n.ResourceType == ResourceType.Food)
                    {
                        float d = Vector3.Distance(targetDeathPos, n.transform.position);
                        if (d < 4f && d < minD)
                        {
                            minD = d;
                            spawnedFood = n;
                        }
                    }
                }
                _huntTarget = null;
                if (spawnedFood != null)
                {
                    CommandGather(spawnedFood, null);
                }
                else
                {
                    // Không tìm thấy xác thú, thử tìm thú sống tiếp trong tầm
                    TryContinueHuntingWildAnimals();
                }
                return;
            }

            // Đảm bảo agent dừng đứng yên khi ngắm bắn
            if (_navAgent != null && _navAgent.enabled && !_navAgent.isStopped)
            {
                _navAgent.isStopped = true;
                _navAgent.velocity = Vector3.zero;
            }

            // Quay mặt về phía con thú
            Vector3 dirToBeast = (_huntTarget.transform.position - transform.position).normalized;
            dirToBeast.y = 0;
            if (dirToBeast.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToBeast), Time.deltaTime * _faceTargetSpeed);
            }

            // Kiểm tra khoảng cách để săn
            float dist = Vector3.Distance(transform.position, _huntTarget.transform.position);
            if (dist > _huntRange + 2.0f)
            {
                if (_animator != null && _hasIsAimingParam)
                {
                    _animator.SetBool("isAiming", false);
                }

                // Đuổi theo nếu thú chạy trốn ra ngoài tầm bắn
                if (_navAgent != null && _navAgent.enabled)
                {
                    _navAgent.isStopped = false;
                    _navAgent.SetDestination(_huntTarget.transform.position);
                }
                ChangeState(VillagerState.Moving);
                return;
            }

            // Kích hoạt ngắm bắn
            if (_animator != null && _hasIsAimingParam)
            {
                _animator.SetBool("isAiming", true);
            }

            // Săn/tấn công thú hoang bằng cung tên
            float progressFactor = Time.deltaTime;
            if (_isHungry) progressFactor *= 0.7f;
            _gatherTimer += progressFactor;

            if (_gatherTimer >= _aimDuration)
            {
                _gatherTimer = 0f;

                if (_animator != null && _hasAttackTriggerParam)
                {
                    _animator.SetTrigger("Attack");
                }

                // Sinh mũi tên bay tới mục tiêu
                Vector3 firePos = _bowFirePoint != null ? _bowFirePoint.position : transform.position + Vector3.up * 1.4f;
                ArrowProjectile arrowProj = null;
                if (_arrowPrefab != null)
                {
                    arrowProj = _arrowPrefab.GetComponent<ArrowProjectile>();
                }
                if (arrowProj == null)
                {
                    var defaultArrow = LoadDefaultArrowProjectile();
                    if (defaultArrow != null)
                    {
                        arrowProj = defaultArrow.GetComponent<ArrowProjectile>();
                    }
                }

                if (arrowProj != null)
                {
                    ArrowProjectile spawnedArrow = PoolManager.Instance.Spawn(arrowProj, firePos, Quaternion.identity);
                    if (spawnedArrow != null)
                    {
                        BaseCombatUnitController combatCtrl = GetComponent<VillagerCombatTarget>();
                        spawnedArrow.LaunchAtPosition(_huntTarget, _huntTarget.transform.position, _huntDamage, combatCtrl);
                    }
                }
                else
                {
                    BaseCombatUnitController combatCtrl = GetComponent<VillagerCombatTarget>();
                    _huntTarget.TakeDamage(_huntDamage, combatCtrl);
                }
            }
            return;
        }

        if (_currentJob != null)
        {
            Vector3 direction = (_currentJob.position - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }

        float progress = Time.deltaTime;
        if (_isHungry) progress *= 0.7f;
        _gatherTimer += progress;
        
        if (_gatherTimer >= TimeToGather)
        {
            _gatherTimer = 0f;

            _gridSystem.GetXY(_currentJob.position, out int x, out int z);
            GridCell targetCell = _gridSystem.GetCell(x, z);

            ResourceNode node = null;
            if (targetCell != null && targetCell.hasResource && targetCell.resourceObject != null)
            {
                node = targetCell.resourceObject.GetComponent<ResourceNode>();
            }

            if (node != null)
            {
                int spaceLeft = MaxCarryCapacity - _totalCarryAmount;
                // Khi thu hoạch xác thú hoang dã (Food Carcass), nhân hệ số _huntGatherMultiplier
                int effectiveGatherAmount = _gatherAmountPerTick;
                if (_wasFarmingWildAnimals && node.ResourceType == ResourceType.Food)
                {
                    effectiveGatherAmount = _gatherAmountPerTick * _huntGatherMultiplier;
                }
                int toExtract = Mathf.Min(spaceLeft, effectiveGatherAmount);
                int extracted = node.ExtractResource(toExtract);
                
                if (extracted > 0)
                {
                    if (_inventory.ContainsKey(node.ResourceType))
                        _inventory[node.ResourceType] += extracted;
                    else
                        _inventory[node.ResourceType] = extracted;
                        
                    _totalCarryAmount += extracted; // Tải trọng lưu trữ cục bộ (Không phân bổ bộ nhớ)
                    UpdateCarryVisuals();

                    // Kiểm tra và thu hoạch tài nguyên phụ (ví dụ: lương thực từ cây gỗ)
                    if (node.HasSecondaryResource)
                    {
                        int secondaryAmount = Mathf.RoundToInt(extracted * node.SecondaryYieldRatio);
                        int remainingSpace = MaxCarryCapacity - _totalCarryAmount;
                        secondaryAmount = Mathf.Min(secondaryAmount, remainingSpace);

                        if (secondaryAmount > 0)
                        {
                            if (_inventory.ContainsKey(node.SecondaryResourceType))
                                _inventory[node.SecondaryResourceType] += secondaryAmount;
                            else
                                _inventory[node.SecondaryResourceType] = secondaryAmount;

                            _totalCarryAmount += secondaryAmount;
                            UpdateCarryVisuals();
                            Debug.Log($"[Logistics] Thu hoạch thêm {secondaryAmount} {node.SecondaryResourceType} (Tài nguyên phụ từ {node.ResourceType}).");
                        }
                    }
                }

                Debug.Log($"[Logistics] Chặt/khai thác {extracted} {node.ResourceType}. Giỏ đồ: {_totalCarryAmount}/{MaxCarryCapacity}.");
                node.TriggerBounceEffect();
            }
            else
            {
                Debug.LogWarning("[Logistics] Tài nguyên đã biến mất!");
            }

            _gridSystem.GetXY(_currentJob.position, out int cx, out int cz);
            GridCell checkCell = _gridSystem.GetCell(cx, cz);
            bool isCurrentResourceDepleted = (checkCell == null || !checkCell.hasResource || checkCell.resourceObject == null);

            if (isCurrentResourceDepleted)
            {
                _pathRetryCount = 0;
                ResourceNode nextNode = FindNearbyResource(_currentJob.position, _targetResource, 50.0f);
                if (nextNode != null)
                {
                    _currentJob.position = nextNode.transform.position;
                }
                else
                {
                    if (JobBroker.Instance != null) JobBroker.Instance.RemoveJob(_currentJob);
                    _currentJob = null;
                }
            }

            // Quyết định hành vi sau khi khai thác
            if (_totalCarryAmount >= MaxCarryCapacity)
            {
                _pathRetryCount = 0;
                if (BuildingManager.Instance != null)
                {
                    _storageDropoffTarget = BuildingManager.Instance.FindNearestDropoff(transform.position, _targetResource);
                }

                if (_storageDropoffTarget != Vector3.zero && SetPathToTarget(_storageDropoffTarget))
                {
                    ChangeState(VillagerState.Moving);
                }
                else
                {
                    ChangeState(VillagerState.Idle);
                }
            }
            else
            {
                if (_currentJob != null)
                {
                    if (isCurrentResourceDepleted)
                    {
                        // Mỏ hiện tại cạn kiệt: nếu đang ở chế độ săn thú và không còn xác thú khác, tìm thú sống tiếp
                        if (_wasFarmingWildAnimals && _currentJob == null)
                        {
                            TryContinueHuntingWildAnimals();
                        }
                        else
                        {
                            _pathRetryCount = 0;
                            if (SetPathToTarget(GetHarvestPositionAroundNode(_currentJob.position))) 
                                ChangeState(VillagerState.Moving);
                            else 
                                ChangeState(VillagerState.Idle);
                        }
                    }
                }
                else
                {
                    // Không còn job nào (mỏ cũng cạn): thử tiếp tục đi săn nếu đang ở chế độ săn thú
                    if (_wasFarmingWildAnimals)
                    {
                        if (_totalCarryAmount > 0)
                        {
                            // Mang thịt về kho trước rồi mới đi săn tiếp
                            _pathRetryCount = 0;
                            if (BuildingManager.Instance != null)
                            {
                                _storageDropoffTarget = BuildingManager.Instance.FindNearestDropoff(transform.position, _targetResource);
                            }
                            if (_storageDropoffTarget != Vector3.zero && SetPathToTarget(_storageDropoffTarget))
                                ChangeState(VillagerState.Moving);
                            else
                                TryContinueHuntingWildAnimals();
                        }
                        else
                        {
                            TryContinueHuntingWildAnimals();
                        }
                    }
                    else if (_totalCarryAmount > 0)
                    {
                        _pathRetryCount = 0;
                        if (BuildingManager.Instance != null)
                        {
                            _storageDropoffTarget = BuildingManager.Instance.FindNearestDropoff(transform.position, _targetResource);
                        }

                        if (_storageDropoffTarget != Vector3.zero && SetPathToTarget(_storageDropoffTarget)) 
                            ChangeState(VillagerState.Moving);
                        else 
                            ChangeState(VillagerState.Idle);
                    }
                    else
                    {
                        ChangeState(VillagerState.Idle);
                    }
                }
            }
        }
    }

    private ResourceNode FindNearbyResource(Vector3 searchCenter, ResourceType type, float radius, List<Vector3> ignorePositions = null, Vector3? ignorePosition = null)
    {
        if (_gridSystem == null) return null;

        _gridSystem.GetXY(searchCenter, out int centerX, out int centerZ);
        float cellSize = _gridSystem.GetCellSize();
        if (cellSize <= 0f) cellSize = 2f;

        int cellRadius = Mathf.CeilToInt(radius / cellSize) + 1;

        ResourceNode closestNode = null;
        float closestDist = float.MaxValue;

        int width = _gridSystem.GetWidth();
        int length = _gridSystem.GetLength();

        for (int x = centerX - cellRadius; x <= centerX + cellRadius; x++)
        {
            for (int z = centerZ - cellRadius; z <= centerZ + cellRadius; z++)
            {
                if (x >= 0 && x < width && z >= 0 && z < length)
                {
                    GridCell cell = _gridSystem.GetCell(x, z);
                    if (cell != null && cell.hasResource && cell.resourceObject != null)
                    {
                        ResourceNode node = cell.resourceObject.GetComponent<ResourceNode>();
                        if (node != null && node.ResourceType == type && node.CanHarvest)
                        {
                            if (IsResourceHiddenByFog(node))
                            {
                                continue;
                            }

                            if (ignorePosition.HasValue && Vector3.Distance(node.transform.position, ignorePosition.Value) < 0.5f)
                            {
                                continue;
                            }

                            if (ignorePositions != null)
                            {
                                bool shouldIgnore = false;
                                foreach (var pos in ignorePositions)
                                {
                                    if (Vector3.Distance(node.transform.position, pos) < 0.5f)
                                    {
                                        shouldIgnore = true;
                                        break;
                                    }
                                }
                                if (shouldIgnore) continue;
                            }

                            float sqrDist = (searchCenter - node.transform.position).sqrMagnitude;
                            float sqrRadius = radius * radius;
                            if (sqrDist <= sqrRadius && sqrDist < closestDist)
                            {
                                closestDist = sqrDist;
                                closestNode = node;
                            }
                        }
                    }
                }
            }
        }
        return closestNode;
    }

    private bool IsResourceHiddenByFog(ResourceNode node)
    {
        if (node == null)
        {
            return false;
        }

        FogVisibilityTarget visibilityTarget = node.GetComponentInParent<FogVisibilityTarget>();
        return visibilityTarget != null && !visibilityTarget.IsVisible;
    }

    /// <summary>
    /// Tìm kiếm động vật hoang dã gần nhất còn sống trong bán kính cho trước.
    /// </summary>
    private WildAnimalController FindNearbyWildAnimal(Vector3 searchCenter, float radius)
    {
        WildAnimalController closest = null;
        float closestDistSqr = radius * radius;

        WildAnimalController[] animals = FindObjectsByType<WildAnimalController>(FindObjectsSortMode.None);
        foreach (var animal in animals)
        {
            if (animal == null || animal.currentState == CombatState.Dead) continue;

            float distSqr = (searchCenter - animal.transform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                closest = animal;
            }
        }

        return closest;
    }

    /// <summary>
    /// Thử tự động tìm và săn bắn con thú hoang dã tiếp theo trong tầm.
    /// Nếu không tìm thấy thú nào, chuyển về trạng thái nhàn rỗi.
    /// </summary>
    private void TryContinueHuntingWildAnimals()
    {
        if (!_wasFarmingWildAnimals)
        {
            ChangeState(VillagerState.Idle);
            return;
        }

        WildAnimalController nextAnimal = FindNearbyWildAnimal(transform.position, _autoGatherAfterBuildRadius);
        if (nextAnimal != null)
        {
            Debug.Log($"[Villager] {name} tự động đi săn con thú tiếp theo: {nextAnimal.unitName}");
            CommandHunt(nextAnimal);
        }
        else
        {
            _wasFarmingWildAnimals = false;
            ChangeState(VillagerState.Idle);
        }
    }

    private void TryAutoGatherNearCompletedBuilding(ConstructibleBuilding completedBuilding)
    {
        if (completedBuilding == null || BuildingManager.Instance == null || _gridSystem == null) return;
        if (GetTotalCarryAmount() > 0)
        {
            _autoGatherBuildingAfterDeposit = completedBuilding;
            // Tự động đi cất tài nguyên đang mang theo người trước
            if (BuildingManager.Instance != null)
            {
                // Xác định loại tài nguyên đang mang theo người
                ResourceType carriedType = ResourceType.Wood;
                foreach (var kvp in _inventory)
                {
                    if (kvp.Value > 0)
                    {
                        carriedType = kvp.Key;
                        break;
                    }
                }
                _targetResource = carriedType;
                _storageDropoffTarget = BuildingManager.Instance.FindNearestDropoff(transform.position, _targetResource);
                if (_storageDropoffTarget != Vector3.zero && SetPathToTarget(_storageDropoffTarget))
                {
                    ChangeState(VillagerState.Moving);
                }
                else
                {
                    ChangeState(VillagerState.Idle);
                }
            }
            return;
        }

        if (!BuildingManager.Instance.BuildingDataMap.TryGetValue(completedBuilding.gameObject, out BuildingData data))
        {
            return;
        }

        bool isResourceCamp = false;
        List<ResourceType> searchTypes = new List<ResourceType>();

        if (data != null)
        {
            if (data.isStorage)
            {
                isResourceCamp = true;
                if (data.acceptedResources != null && data.acceptedResources.Count > 0)
                {
                    searchTypes.AddRange(data.acceptedResources);
                }
            }

            // Fallback dựa trên tên nếu danh sách trống hoặc không đánh dấu isStorage nhưng có tên liên quan
            string nameLower = (data.buildingName ?? "").ToLower();
            if (searchTypes.Count == 0)
            {
                if (nameLower.Contains("gỗ") || nameLower.Contains("wood") || nameLower.Contains("camp"))
                {
                    searchTypes.Add(ResourceType.Wood);
                    isResourceCamp = true;
                }
                if (nameLower.Contains("khoáng") || nameLower.Contains("quặng") || nameLower.Contains("đá") || nameLower.Contains("stone") || nameLower.Contains("gold") || nameLower.Contains("mineral") || nameLower.Contains("mine"))
                {
                    searchTypes.Add(ResourceType.Stone);
                    searchTypes.Add(ResourceType.Gold);
                    isResourceCamp = true;
                }
            }
        }

        if (!isResourceCamp || searchTypes.Count == 0)
        {
            return;
        }

        ResourceNode bestNode = null;
        GridCell bestCell = null;
        float bestDistance = float.MaxValue;
        Vector3 searchCenter = completedBuilding.transform.position;

        foreach (ResourceType resourceType in searchTypes)
        {
            ResourceNode node = FindNearbyResource(searchCenter, resourceType, _autoGatherAfterBuildRadius);
            if (node == null) continue;

            float sqrDistance = (node.transform.position - searchCenter).sqrMagnitude;
            if (sqrDistance < bestDistance)
            {
                _gridSystem.GetXY(node.transform.position, out int x, out int z);
                GridCell cell = _gridSystem.GetCell(x, z);
                if (cell == null) continue;

                bestDistance = sqrDistance;
                bestNode = node;
                bestCell = cell;
            }
        }

        if (bestNode == null || bestCell == null) return;

        CommandGather(bestNode, bestCell);
        Debug.Log($"[Villager] {name} auto-gathering {bestNode.ResourceType} sau khi hoàn thành {data.buildingName}.");
    }

    private static void CollectBuildersAssignedTo(ConstructibleBuilding building, List<VillagerController> builders)
    {
        builders.Clear();
        if (building == null) return;

        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        foreach (VillagerController villager in allVillagers)
        {
            if (villager != null && villager._targetBuilding == building)
            {
                builders.Add(villager);
            }
        }
    }

    private static void TryAutoGatherForCompletedBuilders(ConstructibleBuilding completedBuilding)
    {
        if (completedBuilding == null) return;

        CollectBuildersAssignedTo(completedBuilding, s_tempCompletedBuilders);
        foreach (VillagerController builder in s_tempCompletedBuilders)
        {
            if (builder == null) continue;

            builder.TargetBuilding = null;
            builder.ChangeState(VillagerState.Idle);
            builder.TryAutoGatherNearCompletedBuilding(completedBuilding);
        }

        s_tempCompletedBuilders.Clear();
    }

    #endregion

    #region Job Execution: Depositing

    private void HandleDepositing()
    {
        if (_storageDropoffTarget != Vector3.zero)
        {
            Vector3 direction = (_storageDropoffTarget - transform.position).normalized;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 5f);
            }
        }

        float progress = Time.deltaTime;
        if (_isHungry) progress *= 0.7f;
        _depositTimer += progress;
        
        if (_depositTimer >= _timeToDeposit)
        {
            _depositTimer = 0f;
            DepositResource();
        }
    }

    private void DepositResource()
    {
        if (ResourceManager.Instance != null)
        {
            foreach (var kvp in _inventory)
            {
                if (kvp.Value > 0)
                {
                    ResourceManager.Instance.AddResource(kvp.Key, kvp.Value);
                    Debug.Log($"[Logistics] Villager đã nộp {kvp.Value} {kvp.Key} vào kho lúc {Time.time}");
                }
            }
        }

        _inventory.Clear();
        _totalCarryAmount = 0; // Đưa tải trọng về 0 (Zero Allocations)
        UpdateCarryVisuals();

        if (_autoGatherBuildingAfterDeposit != null)
        {
            ConstructibleBuilding targetCamp = _autoGatherBuildingAfterDeposit;
            _autoGatherBuildingAfterDeposit = null;
            ChangeState(VillagerState.Idle);
            TryAutoGatherNearCompletedBuilding(targetCamp);
            return;
        }

        if (_currentJob == null)
        {
            // Không có job cũ: nếu đang ở chế độ săn thú, tìm thú hoang tiếp theo
            if (_wasFarmingWildAnimals)
            {
                TryContinueHuntingWildAnimals();
            }
            else
            {
                ChangeState(VillagerState.Idle);
            }
            return;
        }

        _gridSystem.GetXY(_currentJob.position, out int cx, out int cz);
        GridCell jobCell = _gridSystem.GetCell(cx, cz);
        
        if (jobCell == null || !jobCell.hasResource)
        {
            ResourceNode nextNode = FindNearbyResource(_currentJob.position, _targetResource, 50.0f);
            if (nextNode != null)
            {
                _currentJob.position = nextNode.transform.position;
                _pathRetryCount = 0;
                if (SetPathToTarget(GetHarvestPositionAroundNode(_currentJob.position))) 
                    ChangeState(VillagerState.Moving);
                else 
                    ChangeState(VillagerState.Idle);
            }
            else
            {
                if (JobBroker.Instance != null) JobBroker.Instance.RemoveJob(_currentJob);
                _currentJob = null;
                // Mỏ cạn kiệt và không tìm thấy mỏ thay thế: thử đi săn tiếp nếu đang ở chế độ săn thú
                if (_wasFarmingWildAnimals)
                {
                    TryContinueHuntingWildAnimals();
                }
                else
                {
                    ChangeState(VillagerState.Idle);
                }
            }
        }
        else
        {
            _pathRetryCount = 0;
            if (SetPathToTarget(GetHarvestPositionAroundNode(_currentJob.position)))
            {
                ChangeState(VillagerState.Moving);
            }
            else
            {
                _currentJob = null;
                ChangeState(VillagerState.Idle);
            }
        }
    }

    #endregion

    #region Job Execution: Building

    private void HandleBuilding()
    {
        // 1. Xử lý logic sửa chữa công trình
        if (_repairTarget != null)
        {
            if (_repairTarget.currentHealth >= _repairTarget.maxHealth)
            {
                // Đã sửa xong đầy máu
                _repairTarget = null;
                _repairResourceTimer = 0f;
                ChangeState(VillagerState.Idle);
                return;
            }

            Collider col = _repairTarget.GetComponent<Collider>();
            float boundsRadius = 2.0f;
            if (col != null)
            {
                boundsRadius = Mathf.Max(boundsRadius, col.bounds.extents.x + 1.2f);
            }

            float distToBuilding = Vector3.Distance(transform.position, _repairTarget.transform.position);
            if (distToBuilding > boundsRadius + 0.8f)
            {
                SetPathToTarget(_repairTarget.transform.position);
                ChangeState(VillagerState.Moving);
                return;
            }

            Vector3 repairTargetDir = _repairTarget.transform.position - transform.position;
            repairTargetDir.y = 0f;
            if (repairTargetDir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(repairTargetDir), Time.deltaTime * 10f);
            }

            float repairProgress = Time.deltaTime;
            if (_isHungry) repairProgress *= 0.7f; // Giảm hiệu suất sửa chữa khi đói

            // Kiểm tra tài nguyên Wood
            if (ResourceManager.Instance != null && ResourceManager.Instance.GetResourceAmount(ResourceType.Wood) > 0)
            {
                float repairRate = 15f; // Hồi phục 15 HP/giây
                float hpToRestore = repairRate * repairProgress;
                float currentHp = _repairTarget.currentHealth;
                float newHp = Mathf.Min(_repairTarget.maxHealth, currentHp + hpToRestore);
                _repairTarget.currentHealth = Mathf.RoundToInt(newHp);

                // Khấu trừ tài nguyên Wood: cứ tích lũy 1 giây sửa chữa thì khấu trừ 1 Wood
                _repairResourceTimer += repairProgress;
                if (_repairResourceTimer >= 1.0f)
                {
                    _repairResourceTimer = 0f;
                    ResourceManager.Instance.TryConsumeResource(ResourceType.Wood, 1);
                }
            }
            else
            {
                // Không đủ gỗ
                Debug.LogWarning($"[Repair] {gameObject.name} dừng sửa chữa do thiếu Gỗ!");
                _repairTarget = null;
                _repairResourceTimer = 0f;
                ChangeState(VillagerState.Idle);
            }
            return;
        }

        // 2. Xử lý logic xây dựng công trình dở dang
        if (_targetBuilding == null)
        {
            ChangeState(VillagerState.Idle);
            return;
        }

        if (_targetBuilding.IsCompleted)
        {
            ConstructibleBuilding oldBuilding = _targetBuilding;
            TryAutoGatherForCompletedBuilders(oldBuilding);
            return;
        }

        float distToDestSqr = (transform.position - _buildTargetPos).sqrMagnitude;
        if (distToDestSqr > 4.0f)
        {
            SetPathToTarget(_buildTargetPos);
            ChangeState(VillagerState.Moving);
            return;
        }

        Vector3 targetDir = _targetBuilding.transform.position - transform.position;
        targetDir.y = 0f;
        if (targetDir.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * 10f);
        }

        float progress = Time.deltaTime;
        if (_isHungry) progress *= 0.7f;
        float progressContribution = (1f / _targetBuilding.TotalBuildTime) * _buildSpeedMultiplier * progress;
        _targetBuilding.Construct(progressContribution);

        if (_targetBuilding.IsCompleted)
        {
            ConstructibleBuilding oldBuilding = _targetBuilding;
            TryAutoGatherForCompletedBuilders(oldBuilding);
        }
    }

    #endregion

    #region RTS Command Interface

    /// <summary>
    /// Cưỡng chế dân di chuyển đến một tọa độ tự do.
    /// </summary>
    public void CommandMoveTo(Vector3 destination)
    {
        _overrideShelter = true;
        CancelAssignedGarrison();
        if (_assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
        }

        _autoGatherBuildingAfterDeposit = null;
        _pathRetryCount = 0;
        ReleaseReservedSlot();
        
        TargetBuilding = null;
        _currentJob = null;
        _repairTarget = null;
        _huntTarget = null;
        _isManualMove = true;
        _wasFarmingWildAnimals = false;
        
        if (SetPathToTarget(destination))
        {
            ChangeState(VillagerState.Moving);
        }
    }

    /// <summary>
    /// Ra lệnh đi khai thác mỏ tài nguyên.
    /// </summary>
    public void CommandGather(ResourceNode node, GridCell cell)
    {
        _overrideShelter = true;
        CancelAssignedGarrison();
        if (_assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
        }

        _autoGatherBuildingAfterDeposit = null;
        _failedResourcePositions.Clear();
        _pathRetryCount = 0;
        
        TargetBuilding = null;
        _repairTarget = null;
        _huntTarget = null;
        _isManualMove = false;

        // Giữ cờ _wasFarmingWildAnimals nếu đang thu hoạch xác thú (Food Carcass)
        if (node.ResourceType == ResourceType.Food && node.gameObject.name.Contains("Carcass"))
        {
            _wasFarmingWildAnimals = true;
        }
        else if (!_wasFarmingWildAnimals)
        {
            _wasFarmingWildAnimals = false;
        }
        
        _currentJob = new Job(ZoneType.None, node.ResourceType, node.transform.position)
        {
            isAssigned = true
        };
        _targetResource = node.ResourceType;
        
        if (SetPathToTarget(GetHarvestPositionAroundNode(node.transform.position)))
        {
            ChangeState(VillagerState.Moving);
        }
    }

    /// <summary>
    /// Ra lệnh đi săn một con thú hoang cụ thể.
    /// </summary>
    public void CommandHunt(WildAnimalController animal)
    {
        if (animal == null || animal.currentState == CombatState.Dead) return;

        _overrideShelter = true;
        CancelAssignedGarrison();
        if (_assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
        }

        _autoGatherBuildingAfterDeposit = null;
        _failedResourcePositions.Clear();
        _pathRetryCount = 0;
        ReleaseReservedSlot();

        TargetBuilding = null;
        _repairTarget = null;
        _huntTarget = animal;
        _currentJob = null;
        _isManualMove = false;
        _wasFarmingWildAnimals = true;

        _targetResource = ResourceType.Food;

        if (SetPathToTarget(animal.transform.position))
        {
            ChangeState(VillagerState.Moving);
        }
        else
        {
            ChangeState(VillagerState.Idle);
        }
    }

    /// <summary>
    /// Ra lệnh đi xây dựng một công trình cụ thể.
    /// </summary>
    public void CommandBuild(ConstructibleBuilding building)
    {
        if (building == null || building.IsCompleted) return;

        _overrideShelter = true;
        CancelAssignedGarrison();
        if (_assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
        }

        _autoGatherBuildingAfterDeposit = null;
        _pathRetryCount = 0;
        ReleaseReservedSlot();
        TargetBuilding = building;
        _currentJob = null; // Hủy job khai thác cũ
        _repairTarget = null;
        _huntTarget = null;
        _isManualMove = false;
        _wasFarmingWildAnimals = false;

        if (SetPathToTarget(_buildTargetPos))
        {
            ChangeState(VillagerState.Moving);
        }
        else
        {
            ChangeState(VillagerState.Idle);
        }
    }

    /// <summary>
    /// Ra lệnh đi sửa chữa một công trình bị hư hại.
    /// </summary>
    public void CommandRepair(BaseCombatUnitController target)
    {
        if (target == null) return;

        _overrideShelter = true;
        CancelAssignedGarrison();
        if (_assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
        }

        _autoGatherBuildingAfterDeposit = null;
        _pathRetryCount = 0;
        ReleaseReservedSlot();
        
        TargetBuilding = null;
        _currentJob = null;
        _repairTarget = target;
        _repairResourceTimer = 0f;
        _huntTarget = null;
        _isManualMove = false;
        _wasFarmingWildAnimals = false;

        if (SetPathToTarget(target.transform.position))
        {
            ChangeState(VillagerState.Moving);
        }
        else
        {
            ChangeState(VillagerState.Idle);
        }
    }

    #endregion

    #region Job Assignment

    /// <summary>
    /// Nhận một công việc (Job) tự động phân phối từ JobBroker.
    /// </summary>
    public void AssignJob(Job newJob)
    {
        CancelAssignedGarrison();
        if (_assignedShelter != null)
        {
            _assignedShelter.CancelReservation(this);
            _assignedShelter = null;
        }

        _autoGatherBuildingAfterDeposit = null;
        _failedResourcePositions.Clear();
        _pathRetryCount = 0;
        
        TargetBuilding = null;
        _repairTarget = null;
        _wasFarmingWildAnimals = false;
        
        _currentJob = newJob;
        _isManualMove = false;
        
        if (_currentJob.zoneType == ZoneType.Logging) _targetResource = ResourceType.Wood;
        else if (_currentJob.zoneType == ZoneType.Mining) _targetResource = ResourceType.Stone;
        else if (_currentJob.zoneType == ZoneType.Farming) _targetResource = ResourceType.Food;

        if (_gridSystem != null)
        {
            _gridSystem.GetXY(_currentJob.position, out int x, out int z);
            GridCell targetCell = _gridSystem.GetCell(x, z);
            if (targetCell != null && targetCell.hasResource)
            {
                _targetResource = targetCell.resourceType;
            }
        }

        if (SetPathToTarget(GetHarvestPositionAroundNode(_currentJob.position)))
        {
            ChangeState(VillagerState.Moving);
        }
        else
        {
            if (JobBroker.Instance != null) JobBroker.Instance.RemoveJob(newJob);
            _currentJob = null;
            ChangeState(VillagerState.Idle);
        }
    }

    #endregion

    #region Grid & Layout Algorithms

    /// <summary>
    /// Tính vị trí đứng khai thác tối ưu xung quanh tài nguyên (360 độ polar).
    /// </summary>
    public Vector3 GetHarvestPositionAroundNode(Vector3 nodeCenter)
    {
        ReleaseReservedSlot();

        ResourceNode node = null;
        if (_gridSystem != null)
        {
            _gridSystem.GetXY(nodeCenter, out int cx, out int cz);
            GridCell cell = _gridSystem.GetCell(cx, cz);
            if (cell != null && cell.resourceObject != null)
            {
                node = cell.resourceObject.GetComponent<ResourceNode>();
            }
        }

        if (node == null)
        {
            Collider[] colliders = Physics.OverlapSphere(nodeCenter, 0.5f);
            foreach (var col in colliders)
            {
                node = col.GetComponentInParent<ResourceNode>();
                if (node != null) break;
            }
        }

        if (node != null)
        {
            _reservedNode = node;
            node.ReserveSlot(GetHashCode(), transform.position, out Vector3 slotPos);
            return slotPos;
        }

        Vector3 dir = (transform.position - nodeCenter).normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
        return nodeCenter + dir * 3.2f;
    }

    /// <summary>
    /// Tính điểm đứng xây dựng được phân bổ riêng biệt bao quanh công trình.
    /// </summary>
    public Vector3 CalculateTargetBuildPosition(ConstructibleBuilding building, int slotIndex)
    {
        Vector3 buildingPos = building.transform.position;
        BuildingData data = null;

        if (BuildingManager.Instance != null && BuildingManager.Instance.BuildingDataMap.TryGetValue(building.gameObject, out var bData))
        {
            data = bData;
        }

        float halfWidth = 1.5f;
        float halfLength = 1.5f;

        if (data != null)
        {
            float cellSize = _gridSystem != null ? _gridSystem.GetCellSize() : 2f;
            halfWidth = (data.buildingSize.x * cellSize) / 2f;
            halfLength = (data.buildingSize.y * cellSize) / 2f;
        }

        // Dùng số lượng slot cố định xung quanh vòng tròn (mặc định 8 slot)
        const int numSlots = 8;
        float radius = Mathf.Max(halfWidth, halfLength) + 1.8f;

        float angle = (slotIndex * (360f / numSlots)) * Mathf.Deg2Rad;

        float targetX = buildingPos.x + Mathf.Cos(angle) * radius;
        float targetZ = buildingPos.z + Mathf.Sin(angle) * radius;

        Vector3 targetPosition = new Vector3(targetX, buildingPos.y, targetZ);
        if (NavMesh.SamplePosition(targetPosition, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            targetPosition = hit.position;
        }

        return targetPosition;
    }

    /// <summary>
    /// Tìm một slot xây dựng trống đầu tiên cho công trình này.
    /// </summary>
    private int AssignFreeBuildSlot(ConstructibleBuilding building)
    {
        if (building == null) return -1;

        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        HashSet<int> occupiedSlots = new HashSet<int>();
        foreach (var v in allVillagers)
        {
            if (v != null && v != this && v._targetBuilding == building && v._assignedBuildSlotIndex >= 0)
            {
                occupiedSlots.Add(v._assignedBuildSlotIndex);
            }
        }

        int slot = 0;
        while (occupiedSlots.Contains(slot))
        {
            slot++;
        }
        return slot;
    }

    /// <summary>
    /// Giải phóng slot đứng khai thác hiện tại nếu đang giữ.
    /// </summary>
    public void ReleaseReservedSlot()
    {
        if (_reservedNode != null)
        {
            _reservedNode.ReleaseSlot(GetHashCode());
            _reservedNode = null;
        }
    }

    #endregion

    #region Visuals & Animation Controllers

    /// <summary>
    /// Cập nhật hiển thị công cụ làm việc dựa trên công việc đang xử lý.
    /// </summary>
    public void UpdateActiveTools()
    {
        if (_woodTool != null) _woodTool.SetActive(false);
        if (_miningTool != null) _miningTool.SetActive(false);
        if (_bowTool != null) _bowTool.SetActive(false);

        if (_currentState == VillagerState.Building || (_currentState == VillagerState.Moving && _targetBuilding != null))
        {
            if (_woodTool != null) _woodTool.SetActive(true);
            return;
        }

        if (_currentState == VillagerState.Gathering || (_currentState == VillagerState.Moving && _currentJob != null && GetTotalCarryAmount() == 0))
        {
            if (_targetResource == ResourceType.Wood)
            {
                if (_woodTool != null) _woodTool.SetActive(true);
            }
            else if (_targetResource == ResourceType.Stone || _targetResource == ResourceType.Gold)
            {
                if (_miningTool != null) _miningTool.SetActive(true);
            }
        }

        if (_huntTarget != null)
        {
            if (_currentState == VillagerState.Gathering || (_currentState == VillagerState.Moving && GetTotalCarryAmount() == 0))
            {
                if (_bowTool != null) _bowTool.SetActive(true);
            }
        }
    }

    /// <summary>
    /// Cập nhật hiển thị tài nguyên đang mang trên người dân làng.
    /// </summary>
    public void UpdateCarryVisuals()
    {
        if (_carryVisuals == null) return;

        if (_totalCarryAmount <= 0)
        {
            _carryVisuals.Hide();
            return;
        }

        ResourceType carriedType = ResourceType.Wood;
        bool hasCarriedResource = false;
        foreach (var kvp in _inventory)
        {
            if (kvp.Value > 0)
            {
                carriedType = kvp.Key;
                hasCarriedResource = true;
                break;
            }
        }

        if (hasCarriedResource)
        {
            _carryVisuals.SetCarry(carriedType, _totalCarryAmount);
        }
        else
        {
            _carryVisuals.Hide();
        }
    }

    private void UpdateAnimationState()
    {
        if (_animator == null) return;

        if (_hasIdleParam) _animator.SetBool("IsIdle", _currentState == VillagerState.Idle);
        if (_hasMovingParam) _animator.SetBool("IsMoving", _currentState == VillagerState.Moving);
        
        bool isBuildingState = _currentState == VillagerState.Building;

        if (_hasBuildingParam)
        {
            _animator.SetBool("IsBuilding", isBuildingState);
            if (_hasGatheringParam) _animator.SetBool("IsGathering", _currentState == VillagerState.Gathering);
        }
        else
        {
            if (_hasGatheringParam) _animator.SetBool("IsGathering", _currentState == VillagerState.Gathering || isBuildingState);
        }

        if (_hasDepositingParam)
        {
            _animator.SetBool("IsDepositing", _currentState == VillagerState.Depositing);
        }
        else
        {
            if (_hasGatheringParam) _animator.SetBool("IsGathering", _currentState == VillagerState.Gathering || _currentState == VillagerState.Depositing || isBuildingState);
        }

        if (_hasIsAimingParam)
        {
            _animator.SetBool("isAiming", _currentState == VillagerState.Gathering && _huntTarget != null);
        }

        // Cập nhật InteractType động
        if (_hasInteractTypeParam)
        {
            int typeVal = 0; // Mặc định: Chặt gỗ
            if (isBuildingState || _repairTarget != null)
            {
                typeVal = 3; // Xây dựng / Sửa chữa
            }
            else if (_currentState == VillagerState.Gathering || _currentState == VillagerState.Moving)
            {
                if (_huntTarget != null)
                {
                    typeVal = 2; // Thu hoạch / Săn bắn
                }
                else
                {
                    switch (_targetResource)
                    {
                        case ResourceType.Wood:
                            typeVal = 0;
                            break;
                        case ResourceType.Stone:
                        case ResourceType.Gold:
                        case ResourceType.AncientRelic:
                            typeVal = 1;
                            break;
                        case ResourceType.Food:
                        case ResourceType.Water:
                            typeVal = 2;
                            break;
                    }
                }
            }
            _animator.SetInteger("InteractType", typeVal);
        }
    }

    private bool HasParameter(string paramName)
    {
        if (_animator == null) return false;
        foreach (AnimatorControllerParameter param in _animator.parameters)
        {
            if (param.name == paramName) return true;
        }
        return false;
    }

    #endregion

    #region Performance Optimized Static Indexes

    private static int GetBuilderIndex(VillagerController villager, ConstructibleBuilding building)
    {
        if (building == null) return 0;
        
        s_tempIndexBuilders.Clear();
        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        foreach (var v in allVillagers)
        {
            if (v != null && v._targetBuilding == building)
            {
                s_tempIndexBuilders.Add(v);
            }
        }
        
        s_tempIndexBuilders.Sort((a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
        int index = s_tempIndexBuilders.IndexOf(villager);
        s_tempIndexBuilders.Clear();
        return index;
    }

    private static int GetGathererIndex(VillagerController villager, Vector3 resourcePosition)
    {
        s_tempIndexGatherers.Clear();
        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        foreach (var v in allVillagers)
        {
            if (v != null && v._currentJob != null && v._currentJob.position == resourcePosition)
            {
                s_tempIndexGatherers.Add(v);
            }
        }

        s_tempIndexGatherers.Sort((a, b) => a.GetHashCode().CompareTo(b.GetHashCode()));
        int index = s_tempIndexGatherers.IndexOf(villager);
        s_tempIndexGatherers.Clear();
        return index;
    }

    #endregion

    #region Public Carry Amount Helper

    /// <summary>
    /// Trả về tổng lượng tài nguyên mà dân làng đang giữ (Độ phức tạp O(1) - Không allocations).
    /// </summary>
    public int GetTotalCarryAmount()
    {
        return _totalCarryAmount;
    }

    #endregion

    #region Hunger System & Combat Notification

    /// <summary>
    /// Nhận thông báo khi bị tấn công để tự động chạy trốn về nhà trú ẩn gần nhất hoặc nhà chính.
    /// </summary>
    public void NotifyUnderAttack(BaseCombatUnitController attacker)
    {
        if (attacker == null || _currentState == VillagerState.Sheltered) return;
        
        HouseShelter shelter = FindClosestAvailableShelter();
        Vector3 escapeTarget = shelter != null ? shelter.transform.position : 
            (BuildingManager.Instance != null && BuildingManager.Instance.MainBuildingInstance != null ? 
             BuildingManager.Instance.MainBuildingInstance.transform.position : Vector3.zero);
             
        if (escapeTarget != Vector3.zero)
        {
            CommandMoveTo(escapeTarget);
        }
    }

    /// <summary>
    /// Thiết lập trạng thái đói cho dân làng.
    /// </summary>
    public void SetHungry(bool hungry)
    {
        if (_isHungry == hungry) return;
        _isHungry = hungry;
        ApplyVillagerTechnologyStats();
    }

    /// <summary>
    /// Áp dụng các chỉ số công nghệ và hiệu ứng đói lên dân làng.
    /// </summary>
    public void ApplyVillagerTechnologyStats()
    {
        if (_navAgent != null)
        {
            float speedMultiplier = TechnologyManager.HasInstance ? TechnologyManager.Instance.VillagerMoveSpeedMultiplier : 1f;
            float hungerMultiplier = _isHungry ? 0.7f : 1f;
            _navAgent.speed = _baseAgentSpeed * speedMultiplier * hungerMultiplier;
        }
    }

    private void HandleTechnologyUnlocked(TechnologyData tech)
    {
        ApplyVillagerTechnologyStats();
    }

    /// <summary>
    /// Sức mang vác hiệu dụng sau nâng cấp công nghệ.
    /// </summary>
    public int EffectiveMaxCarryCapacity
    {
        get
        {
            int capacity = _maxCarryCapacity;
            if (TechnologyManager.HasInstance)
            {
                capacity += TechnologyManager.Instance.VillagerCarryCapacityBonus;
            }
            return capacity;
        }
    }

    /// <summary>
    /// Thời gian khai thác hiệu dụng sau nâng cấp công nghệ.
    /// </summary>
    public float GetEffectiveTimeToGather(ResourceType resourceType)
    {
        float baseTime = _timeToGather;
        float techMultiplier = TechnologyManager.HasInstance ? TechnologyManager.Instance.GetVillagerGatherSpeedMultiplier(resourceType) : 1f;
        if (techMultiplier > 0f)
        {
            baseTime /= techMultiplier;
        }
        return baseTime;
    }

    [Header("Ranged Hunt Rotation")]
    [SerializeField] private float _faceTargetSpeed = 10f;

    private ArrowProjectile LoadDefaultArrowProjectile()
    {
    #if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<ArrowProjectile>("Assets/Prefabs/Projectile/ArrowProjectile.prefab");
    #else
        return null;
    #endif
    }

    #endregion
}
