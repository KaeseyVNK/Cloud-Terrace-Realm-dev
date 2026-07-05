using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý Panel HUD hiển thị các Thẻ Nâng Cấp đang hoạt động.
/// Lắng nghe sự kiện từ CardManager để tự động cập nhật danh sách Icon.
/// </summary>
public class ActiveCardHUDController : MonoBehaviour
{
    [Header("UI Configurations")]
    [Tooltip("Prefab của ô hiển thị thẻ nâng cấp (chứa component ActiveCardSlotUI)")]
    [SerializeField] private GameObject _iconSlotPrefab;

    [Tooltip("Container chứa danh sách các ô icon (thường có HorizontalLayoutGroup)")]
    [SerializeField] private Transform _container;

    private readonly List<GameObject> _activeSlots = new List<GameObject>();

    private void Start()
    {
        // 1. Xác định container (content) và viewport (khung che)
        if (_container == null)
        {
            _container = transform;
        }

        RectTransform viewportRT = null;
        if (_container != transform)
        {
            viewportRT = _container.parent as RectTransform;
        }
        
        if (viewportRT == null)
        {
            viewportRT = transform as RectTransform;
        }

        // 2. Thiết lập ScrollRect trên Panel cha để hỗ trợ cuộn dọc khi tràn chiều cao
        UnityEngine.UI.ScrollRect scrollRect = GetComponent<UnityEngine.UI.ScrollRect>();
        if (scrollRect == null)
        {
            scrollRect = gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        }
        scrollRect.enabled = true; // Đảm bảo ScrollRect hoạt động
        scrollRect.content = _container as RectTransform;
        scrollRect.viewport = viewportRT; // Viewport là Container con hoặc chính nó để clip chuẩn xác
        scrollRect.horizontal = false; // Chỉ cuộn dọc
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 25f; // Độ nhạy cuộn
        scrollRect.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;

        // 3. Thiết lập RectMask2D trên Viewport để clip phần tràn
        if (viewportRT != (transform as RectTransform))
        {
            // Xóa RectMask2D trên Panel cha nếu có để tránh xung đột
            UnityEngine.UI.RectMask2D parentMask = GetComponent<UnityEngine.UI.RectMask2D>();
            if (parentMask != null)
            {
                Destroy(parentMask);
            }

            // Đảm bảo có RectMask2D trên Viewport con
            UnityEngine.UI.RectMask2D viewportMask = viewportRT.GetComponent<UnityEngine.UI.RectMask2D>();
            if (viewportMask == null)
            {
                viewportMask = viewportRT.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            }
            viewportMask.enabled = true;
        }
        else
        {
            UnityEngine.UI.RectMask2D mask = GetComponent<UnityEngine.UI.RectMask2D>();
            if (mask == null)
            {
                mask = gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            }
            mask.enabled = true;
        }

        // 4. Loại bỏ các component thừa/xung đột trên Container con (_container)
        if (_container != transform)
        {
            UnityEngine.UI.ScrollRect containerScroll = _container.GetComponent<UnityEngine.UI.ScrollRect>();
            if (containerScroll != null)
            {
                Destroy(containerScroll);
            }

            UnityEngine.UI.RectMask2D containerMask = _container.GetComponent<UnityEngine.UI.RectMask2D>();
            if (containerMask != null)
            {
                Destroy(containerMask);
            }

            ActiveCardHUDController containerController = _container.GetComponent<ActiveCardHUDController>();
            if (containerController != null && containerController != this)
            {
                Destroy(containerController);
            }

            SlidingPanelUI containerSliding = _container.GetComponent<SlidingPanelUI>();
            if (containerSliding != null)
            {
                Destroy(containerSliding);
            }
        }

        // 5. Cấu hình Container sử dụng GridLayoutGroup kết hợp ContentSizeFitter chiều dọc
        if (_container != null)
        {
            // Xóa HorizontalLayoutGroup cũ nếu có
            UnityEngine.UI.HorizontalLayoutGroup oldHorizontal = _container.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            if (oldHorizontal != null)
            {
                Destroy(oldHorizontal);
            }

            // Cấu hình hoặc gán mới GridLayoutGroup
            UnityEngine.UI.GridLayoutGroup grid = _container.GetComponent<UnityEngine.UI.GridLayoutGroup>();
            if (grid == null)
            {
                grid = _container.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
                grid.cellSize = new Vector2(65f, 65f);
                grid.spacing = new Vector2(8f, 8f);
                grid.startCorner = UnityEngine.UI.GridLayoutGroup.Corner.UpperLeft;
                grid.startAxis = UnityEngine.UI.GridLayoutGroup.Axis.Horizontal;
                grid.childAlignment = TextAnchor.UpperLeft;
                grid.constraint = UnityEngine.UI.GridLayoutGroup.Constraint.Flexible;
            }

            // Đảm bảo có ContentSizeFitter để tự động tính toán chiều cao cho ScrollRect hoạt động
            UnityEngine.UI.ContentSizeFitter fitter = _container.GetComponent<UnityEngine.UI.ContentSizeFitter>();
            if (fitter == null)
            {
                fitter = _container.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>();
            }
            fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained; // Theo chiều ngang của cha
            fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;   // Tự phình chiều cao theo lưới

            // Cấu hình kéo giãn chiều ngang, neo ở cạnh trên (Top) của Container con
            RectTransform containerRT = _container as RectTransform;
            if (containerRT != null)
            {
                containerRT.anchorMin = new Vector2(0f, 1f); // Stretch ngang, neo Top dọc
                containerRT.anchorMax = new Vector2(1f, 1f);
                containerRT.pivot = new Vector2(0.5f, 1f);   // Pivot ở đỉnh trên
                
                // Căn lề an toàn: Trái = 20px, Phải = 40px (tránh nút kéo), Trên = 20px
                containerRT.offsetMin = new Vector2(20f, containerRT.offsetMin.y);
                containerRT.offsetMax = new Vector2(-40f, -20f);
            }
        }

        // 6. Lắng nghe sự kiện thay đổi danh sách thẻ từ CardManager
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardStateChanged += RefreshUI;
        }

        // 7. Cập nhật UI lần đầu tiên
        RefreshUI();
    }

    private void OnDestroy()
    {
        // Hủy đăng ký lắng nghe để tránh memory leak
        if (CardManager.Instance != null)
        {
            CardManager.Instance.OnCardStateChanged -= RefreshUI;
        }
    }

    /// <summary>
    /// Vẽ lại toàn bộ danh sách các Icon thẻ nâng cấp đang hoạt động.
    /// </summary>
    public void RefreshUI()
    {
        // 1. Dọn dẹp các Icon cũ đang hiển thị
        ClearSlots();

        if (CardManager.Instance == null || _iconSlotPrefab == null || _container == null)
        {
            return;
        }

        // 2. Lấy danh sách thẻ đã kích hoạt
        IReadOnlyList<UpgradeCardData> unlockedCards = CardManager.Instance.UnlockedCards;
        if (unlockedCards == null) return;

        // 3. Duyệt qua từng thẻ nâng cấp và tạo Icon tương ứng
        foreach (var card in unlockedCards)
        {
            if (card == null) continue;

            // Chỉ hiển thị các thẻ dạng Buff hoặc Unlock (thẻ Instant không có hiệu ứng duy trì lâu dài)
            if (card.cardType == UpgradeCardType.Instant) continue;

            GameObject slotObj = Instantiate(_iconSlotPrefab, _container);
            if (slotObj != null)
            {
                ActiveCardSlotUI slotUI = slotObj.GetComponent<ActiveCardSlotUI>();
                if (slotUI != null)
                {
                    slotUI.Setup(card);
                    _activeSlots.Add(slotObj);
                }
                else
                {
                    GameLog.LogWarning($"[ActiveCardHUDController] Prefab '{_iconSlotPrefab.name}' thiếu component ActiveCardSlotUI!");
                    Destroy(slotObj);
                }
            }
        }
    }

    private void ClearSlots()
    {
        // Xóa danh sách lưu trữ nội bộ
        foreach (var slot in _activeSlots)
        {
            if (slot != null)
            {
                Destroy(slot);
            }
        }
        _activeSlots.Clear();

        // Dự phòng dọn dẹp tất cả con trực tiếp trong container (để tránh rác UI khi tải lại scene)
        if (_container != null)
        {
            foreach (Transform child in _container)
            {
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}
