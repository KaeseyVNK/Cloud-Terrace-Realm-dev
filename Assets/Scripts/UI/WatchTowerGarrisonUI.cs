using UnityEngine;

/// <summary>
/// Handles selection of the WatchTower via right click (when no units are selected)
/// and displays the Garrison Exit UI.
/// </summary>
public class WatchTowerGarrisonUI : MonoBehaviour
{
    private static readonly Rect PanelRect = new Rect(10, 10, 320, 160);
    private WatchTowerGarrison selectedWatchTower;

    public WatchTowerGarrison SelectedWatchTower => selectedWatchTower;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Nhấn chuột phải để chọn tháp canh khi không có unit nào đang được chọn.
        if (Input.GetMouseButtonDown(1))
        {
            if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count > 0)
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (TryGetWatchTowerFromHit(hit, out WatchTowerGarrison wt, out GameObject clickedBuilding))
                {
                    ConstructibleBuilding cb = clickedBuilding.GetComponent<ConstructibleBuilding>();
                    if (cb != null && !cb.IsCompleted)
                    {
                        Debug.LogWarning("Không thể chọn: Tháp canh này đang được xây dựng chưa hoàn thành!");
                        DeselectWatchTower();
                        return;
                    }

                    SelectWatchTower(wt);
                    Debug.Log("Đã chọn tháp canh: " + clickedBuilding.name);
                    return;
                }
            }
        }
    }

    private void HandleLeftClickDeselect()
    {
        if (selectedWatchTower == null || IsMouseOverPanel())
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && TryGetWatchTowerFromHit(hit, out _, out _))
        {
            return;
        }

        DeselectWatchTower();
    }

    public void SelectWatchTower(WatchTowerGarrison watchTower)
    {
        selectedWatchTower = watchTower;

        // Deselect any production buildings to avoid overlapping UIs
        TestProductionUI productionUI = FindAnyObjectByType<TestProductionUI>();
        if (productionUI != null)
        {
            productionUI.DeselectProduction();
        }
    }

    public void DeselectWatchTower()
    {
        selectedWatchTower = null;
    }

    private bool IsMouseOverPanel()
    {
        Vector2 mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        return PanelRect.Contains(mouse);
    }

    private bool TryGetWatchTowerFromHit(RaycastHit hit, out WatchTowerGarrison watchTower, out GameObject building)
    {
        watchTower = hit.collider.GetComponentInParent<WatchTowerGarrison>();
        building = watchTower != null ? watchTower.gameObject : null;

        if (watchTower != null)
        {
            return true;
        }

        GridSystem grid = FindAnyObjectByType<GridSystem>();
        if (grid == null || BuildingManager.Instance == null)
        {
            return false;
        }

        grid.GetXY(hit.point, out int gridX, out int gridZ);
        GridCell cell = grid.GetCell(gridX, gridZ);
        if (cell == null)
        {
            return false;
        }

        building = BuildingManager.Instance.GetBuildingAtCell(cell);
        if (building == null)
        {
            return false;
        }

        watchTower = building.GetComponent<WatchTowerGarrison>();
        if (watchTower == null)
        {
            watchTower = building.GetComponentInChildren<WatchTowerGarrison>();
        }

        return watchTower != null;
    }

    void OnGUI()
    {
        if (selectedWatchTower == null)
        {
            return;
        }

        GUI.Box(PanelRect, "Tháp Canh Phòng Thủ");

        string countText = $"Đơn vị đóng quân: {selectedWatchTower.OccupantCount} / {selectedWatchTower.Capacity}";
        GUI.Label(new Rect(20, 45, 280, 25), countText);

        if (selectedWatchTower.OccupantCount > 0)
        {
            if (GUI.Button(new Rect(20, 85, 280, 40), "Cho toàn bộ đi ra (Eject All)"))
            {
                selectedWatchTower.EjectAll();
                Debug.Log("Đã cho toàn bộ đơn vị ra khỏi tháp canh.");
            }
        }
        else
        {
            GUI.Label(new Rect(20, 85, 280, 40), "(Không có đơn vị nào trong tháp)");
        }
    }
}
