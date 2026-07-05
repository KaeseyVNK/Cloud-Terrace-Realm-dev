using UnityEngine;
using TMPro;
using System.Collections.Generic;

namespace CloudTerraceRealm.UI
{
    /// <summary>
    /// Script điều khiển HUD hiển thị số lượng dân rảnh rỗi trên mobile.
    /// Cho phép click để nhảy camera đến dân làng rảnh rỗi tiếp theo.
    /// </summary>
    public class IdleVillagerUIController : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _countText;

        [Header("Settings")]
        [SerializeField] private float _refreshInterval = 0.5f;

        private float _nextRefreshTime;
        private int _lastIdleCount = -1;
        private int _currentFocusIndex = 0;

        private void Update()
        {
            if (Time.time >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.time + _refreshInterval;
                UpdateIdleCount();
            }
        }

        private void UpdateIdleCount()
        {
            int idleCount = GetIdleVillagerCount();
            if (idleCount != _lastIdleCount)
            {
                _lastIdleCount = idleCount;
                if (_countText != null)
                {
                    _countText.text = idleCount.ToString();
                }

                // Ẩn nút nếu không có dân làng nào rảnh rỗi
                gameObject.SetActive(idleCount > 0);
            }
        }

        private int GetIdleVillagerCount()
        {
            int count = 0;
            if (VillagerController.SpawnedVillagers != null)
            {
                foreach (var villager in VillagerController.SpawnedVillagers)
                {
                    if (villager != null && villager.CurrentState == VillagerState.Idle)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>
        /// Được gọi khi người chơi bấm vào nút Idle Villager trên HUD.
        /// Tìm dân rảnh rỗi tiếp theo và focus camera vào họ.
        /// </summary>
        public void FocusNextIdleVillager()
        {
            if (VillagerController.SpawnedVillagers == null || VillagerController.SpawnedVillagers.Count == 0)
            {
                return;
            }

            // Thu thập toàn bộ dân làng rảnh rỗi
            List<VillagerController> idleVillagers = new List<VillagerController>();
            foreach (var villager in VillagerController.SpawnedVillagers)
            {
                if (villager != null && villager.CurrentState == VillagerState.Idle)
                {
                    idleVillagers.Add(villager);
                }
            }

            if (idleVillagers.Count == 0)
            {
                return;
            }

            // Đảm bảo index nằm trong khoảng hợp lệ
            if (_currentFocusIndex >= idleVillagers.Count)
            {
                _currentFocusIndex = 0;
            }

            var targetVillager = idleVillagers[_currentFocusIndex];
            if (targetVillager != null)
            {
                var cameraControls = FindAnyObjectByType<CameraControls>();
                if (cameraControls != null)
                {
                    cameraControls.FocusOnPosition(targetVillager.transform.position);
                    
                    // Tự động quét chọn luôn dân làng này để tiện điều khiển
                    if (UnitSelectionManager.Instance != null)
                    {
                        var selectable = targetVillager.GetComponent<SelectableUnit>();
                        if (selectable != null)
                        {
                            UnitSelectionManager.Instance.selectedUnits.Clear();
                            UnitSelectionManager.Instance.selectedUnits.Add(selectable);
                        }
                    }
                    
                    GameLog.Log($"[IdleVillagerUI] Đã focus và chọn dân làng rảnh rỗi: {targetVillager.name}");
                }
            }

            // Tăng index cho lần nhấn tiếp theo
            _currentFocusIndex = (_currentFocusIndex + 1) % idleVillagers.Count;
        }
    }
}
