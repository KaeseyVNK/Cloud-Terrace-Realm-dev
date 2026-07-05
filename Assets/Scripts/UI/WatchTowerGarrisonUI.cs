using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles selection of the WatchTower via right click (when no units are selected)
/// and displays the Garrison Exit UI.
/// </summary>
public class WatchTowerGarrisonUI : MonoBehaviour
{
    private static readonly Rect PanelRect = new Rect(10, 10, 320, 160);
    private const string PanelName = "WatchTowerGarrisonPanel";
    private const string TitleName = "WatchTowerTitleText";
    private const string OccupancyName = "WatchTowerOccupancyText";
    private const string EjectButtonName = "WatchTowerEjectAllButton";
    private const string CloseButtonName = "WatchTowerCloseButton";

    [Header("UGUI Panel")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _ejectAllButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _titleTMP;
    [SerializeField] private TMP_Text _occupancyTMP;

    private CanvasGroup _panelCanvasGroup;
    private WatchTowerGarrison selectedWatchTower;

    public WatchTowerGarrison SelectedWatchTower => selectedWatchTower;

    private void Awake()
    {
        BindPanelReferences();
        SetPanelVisible(false);
    }

    private void OnDestroy()
    {
        if (_ejectAllButton != null)
        {
            _ejectAllButton.onClick.RemoveListener(EjectSelectedWatchTower);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(DeselectWatchTower);
        }
    }

    void Update()
    {
        RefreshPanel();

        // Kiểm tra xem chuột có đang đè lên bất kỳ phần tử UI UGUI nào không (sử dụng cả EventSystem và UnitSelectionManager để có độ chính xác cao nhất ở mọi độ phân giải)
        bool isMouseOverUI = false;
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            isMouseOverUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }
        if (!isMouseOverUI && UnitSelectionManager.Instance != null)
        {
            isMouseOverUI = UnitSelectionManager.Instance.IsPointerOverUI(Input.mousePosition);
        }

        // Nếu chuột đang trỏ vào UI, bỏ qua mọi logic click/chọn/bỏ chọn thế giới 3D dưới nền để tránh lỗi chớp chớp
        if (isMouseOverUI)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            HandleLeftClickDeselect();
        }

        // Chọn tháp canh bằng chuột phải, hoặc bằng chuột trái/chạm khi không có unit nào đang được chọn.
        bool isSelectTriggered = Input.GetMouseButtonDown(1) || 
                                 (Input.GetMouseButtonDown(0) && UnitSelectionManager.Instance != null && UnitSelectionManager.Instance.selectedUnits.Count == 0);

        if (isSelectTriggered)
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

        // Avoid deselecting when clicking on UGUI elements
        if (UnityEngine.EventSystems.EventSystem.current != null && 
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
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
        SetPanelVisible(selectedWatchTower != null);
        RefreshPanel();

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
        SetPanelVisible(false);
    }

    private bool IsMouseOverPanel()
    {
        if (_panelRoot != null && _panelRoot.activeInHierarchy)
        {
            RectTransform panelRect = _panelRoot.transform as RectTransform;
            if (panelRect != null)
            {
                return RectTransformUtility.RectangleContainsScreenPoint(panelRect, Input.mousePosition);
            }
        }

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

    private void BindPanelReferences()
    {
        if (_panelRoot == null)
        {
            GameObject panel = GameObject.Find(PanelName);
            if (panel != null)
            {
                _panelRoot = panel;
            }
        }

        if (_panelRoot == null)
        {
            return;
        }

        if (_panelCanvasGroup == null)
        {
            _panelCanvasGroup = _panelRoot.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null && _panelRoot == gameObject)
            {
                _panelCanvasGroup = _panelRoot.AddComponent<CanvasGroup>();
            }
        }

        if (_titleTMP == null)
        {
            Transform title = _panelRoot.transform.Find(TitleName);
            if (title != null)
            {
                _titleTMP = title.GetComponent<TMP_Text>();
            }
        }

        if (_occupancyTMP == null)
        {
            Transform occupancy = _panelRoot.transform.Find(OccupancyName);
            if (occupancy != null)
            {
                _occupancyTMP = occupancy.GetComponent<TMP_Text>();
            }
        }

        if (_ejectAllButton == null)
        {
            Transform eject = _panelRoot.transform.Find(EjectButtonName);
            if (eject != null)
            {
                _ejectAllButton = eject.GetComponent<Button>();
            }
        }

        if (_closeButton == null)
        {
            Transform close = _panelRoot.transform.Find(CloseButtonName);
            if (close != null)
            {
                _closeButton = close.GetComponent<Button>();
            }
        }

        if (_ejectAllButton != null)
        {
            _ejectAllButton.onClick.RemoveListener(EjectSelectedWatchTower);
            _ejectAllButton.onClick.AddListener(EjectSelectedWatchTower);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(DeselectWatchTower);
            _closeButton.onClick.AddListener(DeselectWatchTower);
        }
    }

    private void RefreshPanel()
    {
        if (_panelRoot == null)
        {
            BindPanelReferences();
        }

        if (_panelRoot == null || selectedWatchTower == null)
        {
            SetPanelVisible(false);
            return;
        }

        SetPanelVisible(true);
        SetText(_titleTMP, selectedWatchTower.gameObject.name);
        SetText(_occupancyTMP, $"Garrison: {selectedWatchTower.OccupantCount}/{selectedWatchTower.Capacity}");

        if (_ejectAllButton != null)
        {
            _ejectAllButton.interactable = selectedWatchTower.OccupantCount > 0;
        }
    }

    private void EjectSelectedWatchTower()
    {
        if (selectedWatchTower == null)
        {
            return;
        }

        selectedWatchTower.EjectAll();
        RefreshPanel();
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panelRoot == gameObject)
        {
            if (_panelCanvasGroup == null)
            {
                _panelCanvasGroup = _panelRoot.GetComponent<CanvasGroup>();
                if (_panelCanvasGroup == null)
                {
                    _panelCanvasGroup = _panelRoot.AddComponent<CanvasGroup>();
                }
            }

            _panelCanvasGroup.alpha = visible ? 1f : 0f;
            _panelCanvasGroup.interactable = visible;
            _panelCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (_panelRoot != null && _panelRoot.activeSelf != visible)
        {
            _panelRoot.SetActive(visible);
        }
    }

    private void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    void OnGUI()
    {
        // Legacy IMGUI rendering is disabled.
    }
}
