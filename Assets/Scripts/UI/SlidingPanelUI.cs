using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Điều khiển hiệu ứng trượt ra/vào (Slide Drawer) cho Panel hiển thị Thẻ Nâng Cấp.
/// Khi click nút mũi tên, panel sẽ trượt ra ngoài màn hình.
/// Khi click chuột ra ngoài vùng Panel, nó sẽ tự động thu gọn lại.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SlidingPanelUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Panel Settings")]
    [Tooltip("RectTransform của Panel chính cần trượt")]
    [SerializeField] private RectTransform _panelRect;

    [Tooltip("Tốc độ trượt (giây)")]
    [SerializeField] private float _slideDuration = 0.2f;

    [Header("Positions")]
    [Tooltip("Vị trí khi panel đang thu gọn (đóng)")]
    [SerializeField] private Vector2 _collapsedPosition = new Vector2(-400f, 380f);

    [Tooltip("Vị trí khi panel đang mở ra (hiển thị)")]
    [SerializeField] private Vector2 _expandedPosition = new Vector2(0f, 380f);

    [Header("UI Visuals")]
    [Tooltip("Hình ảnh mũi tên (để xoay chiều khi đóng/mở)")]
    [SerializeField] private RectTransform _arrowIconRect;

    private bool _isOpen = false;
    private Coroutine _slideCoroutine;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        if (_panelRect == null)
        {
            _panelRect = GetComponent<RectTransform>();
        }

        // Thiết lập trạng thái đóng ban đầu
        _panelRect.anchoredPosition = _collapsedPosition;
        _isOpen = false;

        if (_arrowIconRect != null)
        {
            // Ban đầu đóng: Mũi tên hướng sang phải (Rotation Z = 0)
            _arrowIconRect.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    /// <summary>
    /// Kích hoạt mở panel khi di chuột vào vùng UI
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        OpenPanel();
    }

    /// <summary>
    /// Kích hoạt thu gọn panel khi di chuột ra khỏi vùng UI
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        ClosePanel();
    }

    /// <summary>
    /// Mở panel ra (trượt sang vị trí Expanded).
    /// </summary>
    public void OpenPanel()
    {
        if (_isOpen) return;

        _isOpen = true;

        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideRoutine(_expandedPosition));

        // Xoay mũi tên quay ngược lại (Z = 180) để chỉ hướng đóng
        if (_arrowIconRect != null)
        {
            _arrowIconRect.localRotation = Quaternion.Euler(0f, 0f, 180f);
        }
    }

    /// <summary>
    /// Thu gọn panel lại (trượt sang vị trí Collapsed).
    /// </summary>
    public void ClosePanel()
    {
        if (!_isOpen) return;

        _isOpen = false;

        if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
        _slideCoroutine = StartCoroutine(SlideRoutine(_collapsedPosition));

        // Xoay mũi tên quay lại hướng ban đầu (Z = 0)
        if (_arrowIconRect != null)
        {
            _arrowIconRect.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }

    private IEnumerator SlideRoutine(Vector2 targetPosition)
    {
        Vector2 startPosition = _panelRect.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < _slideDuration)
        {
            elapsed += Time.unscaledDeltaTime; // Sử dụng unscaledDeltaTime để chạy mượt cả khi game Pause
            float t = elapsed / _slideDuration;
            // Áp dụng SmoothStep để chuyển động trơn tru ở hai đầu
            t = Mathf.SmoothStep(0f, 1f, t);

            _panelRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        _panelRect.anchoredPosition = targetPosition;
        _slideCoroutine = null;
    }
}
