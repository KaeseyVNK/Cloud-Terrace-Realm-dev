using UnityEngine;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Tự động co giãn RectTransform để khớp với Safe Area của các dòng điện thoại có tai thỏ, nốt ruồi.
    /// Gắn Script này vào một Panel cha bao bọc toàn bộ UI của màn chơi.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2Int _lastScreenSize = new Vector2Int(0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.Unknown;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            Refresh();
        }

        private void Update()
        {
            // Kiểm tra và cập nhật lại nếu xoay màn hình hoặc thay đổi độ phân giải lúc runtime
            if (_lastSafeArea != Screen.safeArea || 
                _lastScreenSize.x != Screen.width || 
                _lastScreenSize.y != Screen.height || 
                _lastOrientation != Screen.orientation)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            _lastOrientation = Screen.orientation;

            ApplySafeArea(_lastSafeArea);
        }

        private void ApplySafeArea(Rect r)
        {
            // Chuyển đổi tọa độ Safe Area (pixel) sang tọa độ Anchors (0 đến 1)
            Vector2 anchorMin = r.position;
            Vector2 anchorMax = r.position + r.size;

            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
        }
    }
}
