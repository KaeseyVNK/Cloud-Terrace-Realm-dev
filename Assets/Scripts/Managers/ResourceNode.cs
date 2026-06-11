using UnityEngine;
using System;

public class ResourceNode : MonoBehaviour
{
    [UnityEngine.Serialization.FormerlySerializedAs("resourceType")]
    [SerializeField] private ResourceType _resourceType;
    public ResourceType ResourceType
    {
        get => _resourceType;
        set => _resourceType = value;
    }

    [UnityEngine.Serialization.FormerlySerializedAs("currentQuantity")]
    [SerializeField] private int _currentQuantity = 20;
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
    private int[] _reservedSlots = new int[6]; // Lưu trữ ID (HashCode) của dân làng đang chiếm giữ 6 slot đứng xung quanh mỏ
    private Transform _visualTarget; // Đối tượng visual thực tế được áp dụng hiệu ứng scale (không thay đổi Collider ở root)

    private void Awake()
    {
        SetupVisualTarget();
        CacheScale();
    }

    private void SetupVisualTarget()
    {
        if (_visualTarget != null) return;

        // 1. Tìm con có tên "Visual" hoặc "Model"
        Transform modelChild = transform.Find("Visual");
        if (modelChild == null) modelChild = transform.Find("Model");

        // 2. Nếu không tìm thấy, lấy con đầu tiên có Renderer (không phải root)
        if (modelChild == null)
        {
            Renderer[] childRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in childRenderers)
            {
                if (r.transform != transform)
                {
                    modelChild = r.transform;
                    break;
                }
            }
        }

        // 3. Nếu vẫn không có con nào có mesh, kiểm tra xem có mesh trực tiếp trên Root không.
        // Nếu có, tự tạo một Visual Container và di chuyển Mesh/Renderer từ root xuống đó.
        if (modelChild == null)
        {
            MeshFilter rootFilter = GetComponent<MeshFilter>();
            MeshRenderer rootRenderer = GetComponent<MeshRenderer>();
            if (rootFilter != null && rootRenderer != null)
            {
                GameObject container = new GameObject("VisualContainer");
                container.transform.SetParent(transform);
                container.transform.localPosition = Vector3.zero;
                container.transform.localRotation = Quaternion.identity;
                container.transform.localScale = Vector3.one;

                MeshFilter copyFilter = container.AddComponent<MeshFilter>();
                copyFilter.sharedMesh = rootFilter.sharedMesh;

                MeshRenderer copyRenderer = container.AddComponent<MeshRenderer>();
                copyRenderer.sharedMaterials = rootRenderer.sharedMaterials;

                DestroyComponentSafely(rootRenderer);
                DestroyComponentSafely(rootFilter);

                modelChild = container.transform;
            }
        }

        // 4. Fallback cuối cùng: dùng chính root transform
        _visualTarget = (modelChild != null) ? modelChild : transform;
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
        Debug.Log($"Tài nguyên {ResourceType} đã cạn kiệt, đang biến mất...");
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
    /// Trả về index của slot đã đặt thành công (0 đến 5), hoặc -1 nếu đã full slot.
    /// </summary>
    public int ReserveSlot(int villagerId, Vector3 villagerPos, out Vector3 slotPosition)
    {
        // Giải phóng slot cũ của dân làng này trên mỏ (tránh bị trùng)
        ReleaseSlot(villagerId);

        Vector3 nodeCenter = transform.position;
        Vector3 dir = (villagerPos - nodeCenter).normalized;
        dir.y = 0;
        if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;

        int bestSlot = -1;
        float minDistance = float.MaxValue;
        Vector3 bestSlotPos = nodeCenter;

        // Quét tìm slot trống gần nhất với hướng đi của dân làng
        for (int i = 0; i < 6; i++)
        {
            if (_reservedSlots[i] == 0)
            {
                // Phân bổ cố định đều 360 độ xung quanh mỏ tài nguyên (60 độ mỗi slot) trong không gian thế giới (không bị xoay theo góc tiếp cận)
                float angle = (i * 60f) * Mathf.Deg2Rad;
                Vector3 rotatedDir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 candidatePos = nodeCenter + rotatedDir * 3.2f; // Đứng xa tâm 3.2m để tạo khoảng trống cực kỳ rộng rãi thoáng đãng

                float dist = Vector3.Distance(villagerPos, candidatePos);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestSlot = i;
                    bestSlotPos = candidatePos;
                }
            }
        }

        // Đăng ký thành công slot
        if (bestSlot != -1)
        {
            _reservedSlots[bestSlot] = villagerId;
            slotPosition = bestSlotPos;
            return bestSlot;
        }

        // Hết slot (full 6 con): Đứng lùi ra ngoài 4.0 mét ở góc tiếp cận xếp hàng rộng rãi
        slotPosition = nodeCenter + dir * 4.0f;
        return -1;
    }

    /// <summary>
    /// Giải phóng slot đứng khi dân làng ngưng khai thác mỏ này.
    /// </summary>
    public void ReleaseSlot(int villagerId)
    {
        for (int i = 0; i < 6; i++)
        {
            if (_reservedSlots[i] == villagerId)
            {
                _reservedSlots[i] = 0;
            }
        }
    }
}
