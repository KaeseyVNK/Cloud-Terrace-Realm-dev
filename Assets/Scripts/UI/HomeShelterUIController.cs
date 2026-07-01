using UnityEngine;
using TMPro;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script điều khiển bảng thông tin Nhà Dân (HouseShelter) trên Canvas UGUI.
    /// Hiển thị thông số cư dân trú ngụ và cho phép đóng bảng.
    /// </summary>
    public class HomeShelterUIController : MonoBehaviour
    {
        public static HomeShelterUIController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject _panelParent;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _infoText;

        private HouseShelter _selectedShelter;

        private void Awake()
        {
            Instance = this;
            if (_panelParent != null)
            {
                _panelParent.SetActive(false); // Mặc định ẩn
            }
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count == 0)
                {
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        var shelter = hit.collider.GetComponentInParent<HouseShelter>();
                        if (shelter != null)
                        {
                            SelectShelter(shelter);
                            return;
                        }
                    }

                    if (_selectedShelter != null)
                    {
                        Deselect();
                    }
                }
            }

            if (_selectedShelter != null && _panelParent != null && _panelParent.activeSelf)
            {
                // Cập nhật thông số cư dân liên tục
                if (_infoText != null)
                {
                    _infoText.text = $"Cư Dân Trú Ngụ: {_selectedShelter.OccupantCount} / {_selectedShelter.Capacity}\n\nTrạng thái: " + 
                                     (_selectedShelter.OccupantCount == _selectedShelter.Capacity ? "Đầy dung lượng" : "Còn chỗ trống");
                }
            }
        }

        /// <summary>
        /// Được gọi khi click chọn một công trình Nhà Dân.
        /// </summary>
        public void SelectShelter(HouseShelter shelter)
        {
            if (shelter == null) return;

            // Đóng các UI công trình khác để tránh đè nhau
            DeselectAllOtherUIs();

            _selectedShelter = shelter;
            
            if (_panelParent != null)
            {
                _panelParent.SetActive(true);
            }

            if (_titleText != null)
            {
                _titleText.text = shelter.gameObject.name.Replace("(Clone)", "");
            }
        }

        /// <summary>
        /// Tắt bảng thông tin.
        /// </summary>
        public void Deselect()
        {
            _selectedShelter = null;
            if (_panelParent != null)
            {
                _panelParent.SetActive(false);
            }
        }

        private void DeselectAllOtherUIs()
        {
            // Tắt các bảng UI công trình khác
            if (MainBuildingUIController.Instance != null) MainBuildingUIController.Instance.SetProduction(null);
            if (ProductionUIController.Instance != null) ProductionUIController.Instance.SetProduction(null);
            if (MarketUIController.Instance != null) MarketUIController.Instance.CloseMenu();

            if (StorageUIController.Instance != null) StorageUIController.Instance.Deselect();

            var watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
            if (watchTowerUI != null) watchTowerUI.DeselectWatchTower();
        }
    }
}
