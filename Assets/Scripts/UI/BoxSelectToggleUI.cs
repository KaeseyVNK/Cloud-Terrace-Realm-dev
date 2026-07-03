using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script điều khiển nút bật/tắt chế độ quét chọn lính (Box Select) trên mobile.
    /// Tránh xung đột giữa vuốt màn hình cuộn camera và vuốt quét chọn unit.
    /// </summary>
    public class BoxSelectToggleUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image _buttonImage;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Visual Settings")]
        [SerializeField] private Color _activeColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Xanh lá
        [SerializeField] private Color _inactiveColor = new Color(0.5f, 0.5f, 0.5f, 1f); // Xám

        private void OnEnable()
        {
            UnitSelectionManager.OnBoxSelectModeChanged += HandleBoxSelectModeChanged;
        }

        private void OnDisable()
        {
            UnitSelectionManager.OnBoxSelectModeChanged -= HandleBoxSelectModeChanged;
        }

        private void Start()
        {
            // Thiết lập trạng thái mặc định (thường là OFF để pan camera)
            UnitSelectionManager.IsBoxSelectMode = false;
            UpdateVisuals();
        }

        /// <summary>
        /// Được gọi khi người chơi click vào nút Toggle trên màn hình.
        /// </summary>
        public void ToggleBoxSelectMode()
        {
            UnitSelectionManager.IsBoxSelectMode = !UnitSelectionManager.IsBoxSelectMode;
            UpdateVisuals();
            Debug.Log($"[BoxSelectToggleUI] Chuyển đổi BoxSelectMode: {UnitSelectionManager.IsBoxSelectMode}");
        }

        private void HandleBoxSelectModeChanged(bool isOn)
        {
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            bool isOn = UnitSelectionManager.IsBoxSelectMode;

            if (_statusText != null)
            {
                _statusText.text = isOn ? "Quét Chọn: BẬT" : "Quét Chọn: TẮT";
            }

            if (_buttonImage != null)
            {
                _buttonImage.color = isOn ? _activeColor : _inactiveColor;
            }
        }
    }
}
