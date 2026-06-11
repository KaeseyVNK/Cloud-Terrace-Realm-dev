using UnityEngine;

public class TestProductionUI : MonoBehaviour
{
    private static readonly Rect ProductionPanelRect = new Rect(10, 10, 320, 300);
    private BuildingProduction selectedProduction;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Nhấn chuột phải để chọn công trình hoặc đặt rally point khi không có unit nào đang được chọn.
        if (Input.GetMouseButtonDown(1))
        {
            if (UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count > 0)
            {
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (TryGetProductionFromHit(hit, out BuildingProduction prod, out GameObject clickedBuilding))
                {
                    ConstructibleBuilding cb = clickedBuilding.GetComponent<ConstructibleBuilding>();
                    if (cb != null && !cb.IsCompleted)
                    {
                        Debug.LogWarning("Không thể chọn: Công trình này đang được xây dựng chưa hoàn thành!");
                        DeselectProduction();
                        return;
                    }

                    SelectProduction(prod);
                    Debug.Log("Đã chọn công trình để sản xuất: " + clickedBuilding.name);
                    return;
                }

                if (selectedProduction != null)
                {
                    selectedProduction.SetRallyFromHit(hit);
                    return;
                }

                Debug.Log("Không có công trình sản xuất nào đang được chọn để đặt rally point.");
            }
            else
            {
                Debug.Log("Chuột phải không trúng bất kỳ Collider nào!");
            }
        }
    }

    private void HandleLeftClickDeselect()
    {
        if (selectedProduction == null || IsMouseOverProductionPanel())
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (TryGetProductionFromHit(hit, out _, out _))
            {
                return;
            }

            // Cũng bỏ qua bỏ chọn nếu bấm trúng tháp canh phòng thủ
            if (hit.collider.GetComponentInParent<WatchTowerGarrison>() != null)
            {
                return;
            }
        }

        DeselectProduction();
    }

    private void SelectProduction(BuildingProduction production)
    {
        if (selectedProduction != null && selectedProduction != production)
        {
            selectedProduction.SetRallyFlagVisible(false);
        }

        selectedProduction = production;
        if (selectedProduction != null)
        {
            selectedProduction.SetRallyFlagVisible(true);

            // Bỏ chọn tháp canh để tránh chồng chéo UI
            WatchTowerGarrisonUI watchTowerUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
            if (watchTowerUI != null)
            {
                watchTowerUI.DeselectWatchTower();
            }
        }
    }

    public void DeselectProduction()
    {
        if (selectedProduction != null)
        {
            selectedProduction.SetRallyFlagVisible(false);
        }

        selectedProduction = null;
    }

    private bool IsMouseOverProductionPanel()
    {
        Vector2 mouse = Input.mousePosition;
        mouse.y = Screen.height - mouse.y;
        return ProductionPanelRect.Contains(mouse);
    }

    private bool TryGetProductionFromHit(RaycastHit hit, out BuildingProduction production, out GameObject building)
    {
        production = hit.collider.GetComponentInParent<BuildingProduction>();
        building = production != null ? production.gameObject : null;

        if (production != null)
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

        production = building.GetComponent<BuildingProduction>();
        if (production == null)
        {
            production = building.GetComponentInChildren<BuildingProduction>();
        }

        return production != null;
    }

    void OnGUI()
    {
        if (selectedProduction == null)
        {
            GUI.Label(new Rect(10, 10, 300, 20), "Bấm CHUỘT PHẢI vào một công trình để sản xuất.");
            return;
        }

        if (selectedProduction.BuildingData == null || selectedProduction.BuildingData.producibleUnits == null)
        {
            GUI.Label(new Rect(10, 10, 300, 20), "Công trình này không có dữ liệu sản xuất.");
            return;
        }

        GUI.Box(ProductionPanelRect, "Sản Xuất: " + selectedProduction.BuildingData.buildingName);
        
        int yPos = 40;
        foreach (var unit in selectedProduction.BuildingData.producibleUnits)
        {
            if (unit == null) continue;

            string costText = "";
            if (unit.productionCosts != null)
            {
                foreach (var cost in unit.productionCosts)
                {
                    costText += $"{cost.amount} {cost.resourceType} ";
                }
            }

            if (GUI.Button(new Rect(20, yPos, 200, 30), $"Mua {unit.unitName}"))
            {
                selectedProduction.RequestProduceUnit(unit);
            }
            
            GUI.Label(new Rect(230, yPos, 200, 30), costText);
            yPos += 40;
        }

        yPos += 20;

        if (selectedProduction.CurrentProducingUnit != null)
        {
            GUI.Label(new Rect(20, yPos, 300, 20), "Đang tạo: " + selectedProduction.CurrentProducingUnit.unitName);
            yPos += 20;
            
            float progress = 1f - (selectedProduction.CurrentProductionTimer / selectedProduction.CurrentProducingUnit.productionTime);
            GUI.HorizontalScrollbar(new Rect(20, yPos, 200, 20), 0, progress, 0, 1);
            GUI.Label(new Rect(230, yPos, 100, 20), (progress * 100).ToString("F0") + "%");
        }
    }
}
