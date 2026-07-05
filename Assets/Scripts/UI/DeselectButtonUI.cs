using UnityEngine;
using UnityEngine.UI;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Component xử lý sự kiện click bỏ chọn tất cả đơn vị đang được chọn.
    /// Thích hợp dùng cho giao diện di động.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class DeselectButtonUI : MonoBehaviour
    {
        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (UnitSelectionManager.Instance != null)
            {
                UnitSelectionManager.Instance.DeselectAll();
                GameLog.Log("[DeselectButtonUI] Đã bỏ chọn tất cả đơn vị thành công.");
            }
        }
    }
}
