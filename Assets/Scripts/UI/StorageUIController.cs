using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script điều khiển bảng thông tin Kho Chứa (Storage / Food Storage) trên Canvas UGUI.
    /// Hiển thị tổng tài nguyên quốc gia và các loại tài nguyên mà kho chấp nhận chứa.
    /// </summary>
    public class StorageUIController : MonoBehaviour
    {
        public static StorageUIController Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject _panelParent;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _infoText;
        [SerializeField] private bool _enableStoragePanel = false;

        private GameObject _selectedStorageBuilding;
        private BuildingData _selectedBuildingData;

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
            if (!_enableStoragePanel)
            {
                return;
            }

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
                        if (TryGetBuildingData(hit.collider.gameObject, out GameObject rootBuilding, out BuildingData data))
                        {
                            if (data != null && data.isStorage)
                            {
                                SelectStorage(rootBuilding, data);
                                return;
                            }
                        }
                    }

                    if (_selectedStorageBuilding != null)
                    {
                        Deselect();
                    }
                }
            }

            if (_selectedStorageBuilding != null && _panelParent != null && _panelParent.activeSelf)
            {
                UpdateStorageInfo();
            }
        }

        private bool TryGetBuildingData(GameObject obj, out GameObject rootBuilding, out BuildingData data)
        {
            rootBuilding = null;
            data = null;
            if (BuildingManager.Instance == null) return false;

            Transform curr = obj.transform;
            while (curr != null)
            {
                if (BuildingManager.Instance.BuildingDataMap.TryGetValue(curr.gameObject, out data))
                {
                    rootBuilding = curr.gameObject;
                    return true;
                }
                curr = curr.parent;
            }
            return false;
        }

        /// <summary>
        /// Được gọi khi click chọn một công trình Kho Chứa.
        /// </summary>
        public void SelectStorage(GameObject building, BuildingData data)
        {
            if (!_enableStoragePanel)
            {
                Deselect();
                return;
            }

            if (building == null || data == null) return;

            // Đóng các UI công trình khác để tránh đè nhau
            DeselectAllOtherUIs();

            _selectedStorageBuilding = building;
            _selectedBuildingData = data;

            if (_panelParent != null)
            {
                _panelParent.SetActive(true);
            }

            if (_titleText != null)
            {
                _titleText.text = data.buildingName;
            }

            UpdateStorageInfo();
        }

        /// <summary>
        /// Tắt bảng thông tin.
        /// </summary>
        public void Deselect()
        {
            _selectedStorageBuilding = null;
            _selectedBuildingData = null;
            if (_panelParent != null)
            {
                _panelParent.SetActive(false);
            }
        }

        private void UpdateStorageInfo()
        {
            if (ResourceManager.Instance == null || _selectedBuildingData == null) return;

            int wood = ResourceManager.Instance.GetResourceAmount(ResourceType.Wood);
            int food = ResourceManager.Instance.GetResourceAmount(ResourceType.Food);
            int stone = ResourceManager.Instance.GetResourceAmount(ResourceType.Stone);
            int gold = ResourceManager.Instance.GetResourceAmount(ResourceType.Gold);

            // Xây dựng danh sách tài nguyên chấp nhận chứa
            string acceptedStr = "";
            if (_selectedBuildingData.acceptedResources == null || _selectedBuildingData.acceptedResources.Count == 0)
            {
                acceptedStr = "All types";
            }
            else
            {
                List<string> names = new List<string>();
                foreach (var res in _selectedBuildingData.acceptedResources)
                {
                    names.Add(res.ToString());
                }
                acceptedStr = string.Join(", ", names);
            }

            if (_infoText != null)
            {
                _infoText.text = $"<b>Global Stockpile:</b>\n" +
                                 $"- Wood: {wood}\n" +
                                 $"- Food: {food}\n" +
                                 $"- Stone: {stone}\n" +
                                 $"- Gold: {gold}\n\n" +
                                 $"<b>Accepted Resources:</b>\n{acceptedStr}";
            }
        }

        private void DeselectAllOtherUIs()
        {
            if (MainBuildingUIController.Instance != null) MainBuildingUIController.Instance.SetProduction(null);
            if (ProductionUIController.Instance != null) ProductionUIController.Instance.SetProduction(null);
            if (MarketUIController.Instance != null) MarketUIController.Instance.CloseMenu();

            if (HomeShelterUIController.Instance != null) HomeShelterUIController.Instance.Deselect();

            var watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
            if (watchTowerUI != null) watchTowerUI.DeselectWatchTower();
        }
    }
}
