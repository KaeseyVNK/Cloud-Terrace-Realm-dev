using UnityEngine;
using System;

public class ResourceNode : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<ResourceNode> Registry = new System.Collections.Generic.List<ResourceNode>();

    [UnityEngine.Serialization.FormerlySerializedAs("resourceType")]
    [SerializeField] private ResourceType _resourceType;
    public ResourceType ResourceType
    {
        get => _resourceType;
        set => _resourceType = value;
    }

    [UnityEngine.Serialization.FormerlySerializedAs("currentQuantity")]
    [SerializeField] private int _currentQuantity = 250;
    public int CurrentQuantity
    {
        get => _currentQuantity;
        set => _currentQuantity = value;
    }

    [UnityEngine.Serialization.FormerlySerializedAs("occupiedCell")]
    [SerializeField] private GridCell _occupiedCell;
    public GridCell OccupiedCell
    {
        get => _occupiedCell;
        set => _occupiedCell = value;
    }

    [Header("Secondary Resource Yield")]
    [Tooltip("Enable secondary resource yield (e.g. food when gathering wood)")]
    [SerializeField] private bool _hasSecondaryResource = false;
    
    [Tooltip("The type of the secondary resource")]
    [SerializeField] private ResourceType _secondaryResourceType = ResourceType.Food;
    
    [Tooltip("Multiplier for the secondary resource yield (relative to extracted primary amount)")]
    [Range(0f, 5f)]
    [SerializeField] private float _secondaryYieldRatio = 1.0f;

    public bool HasSecondaryResource
    {
        get => _resourceType == ResourceType.Wood || _hasSecondaryResource;
        set => _hasSecondaryResource = value;
    }

    public ResourceType SecondaryResourceType
    {
        get => _resourceType == ResourceType.Wood ? ResourceType.Food : _secondaryResourceType;
        set => _secondaryResourceType = value;
    }

    public float SecondaryYieldRatio
    {
        get => _secondaryYieldRatio;
        set => _secondaryYieldRatio = value;
    }

    public bool CanHarvest
    {
        get
        {
            if (CurrentQuantity <= 0) return false;
            
            ConstructibleBuilding building = GetComponent<ConstructibleBuilding>();
            if (building == null) building = GetComponentInChildren<ConstructibleBuilding>();
            if (building == null) building = GetComponentInParent<ConstructibleBuilding>();
            
            if (building != null && !building.IsCompleted) return false;
            
            return true;
        }
    }

    [Obsolete("Use ResourceType instead")]
    public ResourceType resourceType { get => ResourceType; set => ResourceType = value; }

    [Obsolete("Use CurrentQuantity instead")]
    public int currentQuantity { get => CurrentQuantity; set => CurrentQuantity = value; }

    [Obsolete("Use OccupiedCell instead")]
    public GridCell occupiedCell { get => OccupiedCell; set => OccupiedCell = value; }

    public event Action<ResourceNode> OnDepleted;
    
    private Vector3 _originalScale;  
    private bool _isScaleCached = false;
    [SerializeField] private int _maxHarvestSlots = 8;
    public int MaxHarvestSlots
    {
        get => _maxHarvestSlots;
        set => _maxHarvestSlots = value;
    }
    private const float DefaultHarvestSlotRadius = 3.4f;
    private const float OverflowHarvestSlotRadius = 4.6f;
    private const float HarvestSlotColliderClearance = 0.85f;
    private int[] _reservedSlots; // Khởi tạo động trong Awake dựa trên _maxHarvestSlots
    private Transform _visualTarget; // Đối tượng visual thực tế được áp dụng hiệu ứng scale (không thay đổi Collider ở root)

    private void OnEnable()
    {
        Registry.Add(this);
    }

    private void OnDisable()
    {
        Registry.Remove(this);
    }

    private void Awake()
    {
        gameObject.isStatic = false; // Tắt static để có thể thực hiện hiệu ứng scale/bounce!
        if (_maxHarvestSlots <= 0) _maxHarvestSlots = 8;
        _reservedSlots = new int[_maxHarvestSlots];
        SetupVisualTarget();
        CacheScale();
    }

    private void Start()
    {
        // Tự động gắn FogVisibilityTarget cho tài nguyên để phục vụ Sương mù chiến trận (Fog of War)
        if (AOSFogOfWarBridge.Instance != null && 
            AOSFogOfWarBridge.Instance.AutoAddVisibilityTargets && 
            AOSFogOfWarBridge.Instance.HideResourcesOutsideVision)
        {
            if (GetComponent<FogVisibilityTarget>() == null)
            {
                gameObject.AddComponent<FogVisibilityTarget>();
            }
        }
    }

    private void SetupVisualTarget()
    {
        _visualTarget = transform;
    }

    private void DestroyComponentSafely(Component component)
    {
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
        }
    }

    private void CacheScale()
    {
        if (!_isScaleCached)
        {
            SetupVisualTarget();
            if (_visualTarget != null)
            {
                _originalScale = _visualTarget.localScale;
                _isScaleCached = true;
            }
        }
    }

    public void Initialize(ResourceType type, int amount, GridCell cell)
    {
        this.ResourceType = type;
        this.CurrentQuantity = amount;
        this.OccupiedCell = cell;
        SetupVisualTarget();
        CacheScale();
    }

    // Villager gọi hàm này để lấy tài nguyên
    public int ExtractResource(int amountToGather)
    {
        int gathered = Mathf.Min(amountToGather, CurrentQuantity);
        CurrentQuantity -= gathered;

        if (CurrentQuantity <= 0)
        {
            Deplete();
        }

        return gathered;
    }

    private void Deplete()
    {
        // 1. Trả lại trạng thái cho Ô lưới
        if (OccupiedCell != null)
        {
            OccupiedCell.hasResource = false;
            OccupiedCell.resourceObject = null;
            
            OccupiedCell.isBuildable = true; 
            OccupiedCell.isWalkable = true; 
        }

        // 2. Báo hiệu cho các hệ thống khác
        OnDepleted?.Invoke(this);

        // 3. Xóa Cây
        GameLog.Log($"Tài nguyên {ResourceType} đã cạn kiệt, đang biến mất...");
        Destroy(gameObject);
    }

    public void TriggerBounceEffect()
    {
        StopAllCoroutines(); // Dừng các hiệu ứng bounce cũ nếu click liên tục
        StartCoroutine(BounceRoutine());
    }

    private System.Collections.IEnumerator BounceRoutine()
    {
        CacheScale();
        Vector3 targetScale = _originalScale * 1.3f;
        float duration = 0.1f;
        float elapsed = 0f;

        // Phóng to
        while (elapsed < duration)
        {
            if (_visualTarget != null)
                _visualTarget.localScale = Vector3.Lerp(_originalScale, targetScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        elapsed = 0f;
        // Thu nhỏ lại
        while (elapsed < duration)
        {
            if (_visualTarget != null)
                _visualTarget.localScale = Vector3.Lerp(targetScale, _originalScale, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (_visualTarget != null)
            _visualTarget.localScale = _originalScale;
    }

    /// <summary>
    /// Đăng ký một vị trí (slot) đứng khai thác xung quanh mỏ tài nguyên (chuẩn AOE).
    /// Trả về index của slot đã đặt thành công, hoặc -1 nếu đã full slot.
    /// </summary>
    public int ReserveSlot(int villagerId, Vector3 villagerPos, out Vector3 slotPosition)
    {
        if (_reservedSlots == null || _reservedSlots.Length != _maxHarvestSlots)
        {
            if (_maxHarvestSlots <= 0) _maxHarvestSlots = 8;
            _reservedSlots = new int[_maxHarvestSlots];
        }
        ReleaseSlot(villagerId);

        Vector3 nodeCenter = transform.position;
        Collider harvestCollider = GetHarvestCollider();
        float slotRadius = GetHarvestSlotRadius(harvestCollider);
        int bestSlot = -1;
        float minDistance = float.MaxValue;
        Vector3 bestSlotPos = nodeCenter;
        Vector3 fallbackSlotPos = nodeCenter;
        bool hasFallback = false;

        for (int i = 0; i < _maxHarvestSlots; i++)
        {
            if (_reservedSlots[i] != 0) continue;

            Vector3 candidatePos = GetSlotPosition(nodeCenter, i, slotRadius);
            candidatePos = PushOutsideHarvestCollider(candidatePos, harvestCollider);
            Vector3 validPos = GetNearestNavMeshPosition(candidatePos, 1.5f, out bool hasNavMeshPos);
            if (!hasFallback)
            {
                fallbackSlotPos = hasNavMeshPos ? validPos : candidatePos;
                hasFallback = true;
            }

            if (!hasNavMeshPos) continue;

            float dist = (villagerPos - validPos).sqrMagnitude;
            if (dist < minDistance)
            {
                minDistance = dist;
                bestSlot = i;
                bestSlotPos = validPos;
            }
        }

        if (bestSlot != -1)
        {
            _reservedSlots[bestSlot] = villagerId;
            slotPosition = bestSlotPos;
            return bestSlot;
        }

        if (hasFallback)
        {
            slotPosition = fallbackSlotPos;
            return -1;
        }

        int overflowSlot = Mathf.Abs(villagerId) % _maxHarvestSlots;
        Vector3 overflowPos = GetSlotPosition(nodeCenter, overflowSlot, OverflowHarvestSlotRadius);
        overflowPos = PushOutsideHarvestCollider(overflowPos, harvestCollider);
        slotPosition = GetNearestNavMeshPosition(overflowPos, 2.0f, out _);
        return -1;
    }

    private Collider GetHarvestCollider()
    {
        return GetComponentInChildren<Collider>();
    }

    private float GetHarvestSlotRadius(Collider col)
    {
        if (col != null)
        {
            Vector3 extents = col.bounds.extents;
            float horizontalSize = Mathf.Max(extents.x, extents.z);
            return Mathf.Clamp(horizontalSize + HarvestSlotColliderClearance, 1.8f, 5.5f);
        }

        switch (ResourceType)
        {
            case ResourceType.Food:
                return 3.5f;
            case ResourceType.Stone:
            case ResourceType.Gold:
                return 3.2f;
            default:
                return DefaultHarvestSlotRadius;
        }
    }

    private Vector3 GetSlotPosition(Vector3 center, int slotIndex, float radius)
    {
        float angle = (slotIndex * (360f / _maxHarvestSlots)) * Mathf.Deg2Rad;
        return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
    }

    private Vector3 PushOutsideHarvestCollider(Vector3 position, Collider col)
    {
        if (col == null)
        {
            return position;
        }

        Bounds bounds = col.bounds;
        Vector3 fromCenter = position - bounds.center;
        fromCenter.y = 0f;

        if (fromCenter.sqrMagnitude < 0.0001f)
        {
            fromCenter = transform.forward;
            fromCenter.y = 0f;
        }

        float minRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) + HarvestSlotColliderClearance;
        if (fromCenter.magnitude >= minRadius)
        {
            return position;
        }

        Vector3 pushed = bounds.center + fromCenter.normalized * minRadius;
        pushed.y = position.y;
        return pushed;
    }

    private Vector3 GetNearestNavMeshPosition(Vector3 position, float maxDistance, out bool found)
    {
        if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, maxDistance, ~2))
        {
            found = true;
            return hit.position;
        }

        found = false;
        return position;
    }

    /// <summary>
    /// Giải phóng slot đứng khi dân làng ngưng khai thác mỏ này.
    /// </summary>
    public void ReleaseSlot(int villagerId)
    {
        for (int i = 0; i < _reservedSlots.Length; i++)
        {
            if (_reservedSlots[i] == villagerId)
            {
                _reservedSlots[i] = 0;
            }
        }
    }
}
