using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý giao diện bảng lệnh hành động động góc trái dưới, hỗ trợ đổi icon hình ảnh cho dân/lính.
/// </summary>
public class ActionCommandPanelUI : MonoBehaviour
{
    public static ActionCommandPanelUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _buildButton;        // Slot 1
    [SerializeField] private Button _garrisonButton;     // Slot 2
    [SerializeField] private Button _repairButton;       // Slot 3
    [SerializeField] private Button _returnCargoButton;  // Slot 4
    [SerializeField] private Button _stopButton;         // Slot 5

    [Header("Icon Sprites (Kéo thả ảnh vào đây)")]
    [SerializeField] private Sprite _buildIcon;          // Icon Xây dựng (B)
    [SerializeField] private Sprite _garrisonIcon;       // Icon Đồn trú (G)
    [SerializeField] private Sprite _repairIcon;         // Icon Sửa chữa (R)
    [SerializeField] private Sprite _returnCargoIcon;    // Icon Cất tài nguyên (C)
    [SerializeField] private Sprite _stopIcon;           // Icon Stop (S)
    [SerializeField] private Sprite _attackIcon;         // Icon Tấn công (A)
    [SerializeField] private Sprite _holdPositionIcon;   // Icon Giữ vị trí (H)

    private TextMeshProUGUI _btn1Text;
    private TextMeshProUGUI _btn2Text;
    private TextMeshProUGUI _btn3Text;
    private TextMeshProUGUI _btn4Text;
    private TextMeshProUGUI _btn5Text;

    private Sprite _defaultBtnSprite;
    private Color _defaultBtnColor;
    private bool _isVillagerMode = true;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (_panelRoot == null) _panelRoot = gameObject;

        // Lưu trữ tham chiếu tới Text hiển thị
        if (_buildButton != null) _btn1Text = _buildButton.GetComponentInChildren<TextMeshProUGUI>();
        if (_garrisonButton != null) _btn2Text = _garrisonButton.GetComponentInChildren<TextMeshProUGUI>();
        if (_repairButton != null) _btn3Text = _repairButton.GetComponentInChildren<TextMeshProUGUI>();
        if (_returnCargoButton != null) _btn4Text = _returnCargoButton.GetComponentInChildren<TextMeshProUGUI>();
        if (_stopButton != null) _btn5Text = _stopButton.GetComponentInChildren<TextMeshProUGUI>();

        // Cache sprite và màu nền nút mặc định để fallback
        if (_buildButton != null)
        {
            Image img = _buildButton.GetComponent<Image>();
            if (img != null)
            {
                _defaultBtnSprite = img.sprite;
                _defaultBtnColor = img.color;
            }
        }

        // Đăng ký lắng nghe sự kiện thay đổi lựa chọn chọn đơn vị
        if (UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.OnSelectionChanged += Refresh;
        }

        // Gán sự kiện click nút bấm
        if (_buildButton != null) _buildButton.onClick.AddListener(OnButton1Clicked);
        if (_garrisonButton != null) _garrisonButton.onClick.AddListener(OnButton2Clicked);
        if (_repairButton != null) _repairButton.onClick.AddListener(OnButton3Clicked);
        if (_returnCargoButton != null) _returnCargoButton.onClick.AddListener(OnButton4Clicked);
        if (_stopButton != null) _stopButton.onClick.AddListener(OnButton5Clicked);

        Refresh();
    }

    private void OnDestroy()
    {
        if (UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.OnSelectionChanged -= Refresh;
        }
    }

    private void Update()
    {
        if (_panelRoot == null || !_panelRoot.activeSelf) return;

        // Bắt phím tắt nhanh dựa trên chế độ hiển thị
        if (_isVillagerMode)
        {
            if (Input.GetKeyDown(KeyCode.B)) OnButton1Clicked();
            if (Input.GetKeyDown(KeyCode.G)) OnButton2Clicked();
            if (Input.GetKeyDown(KeyCode.R)) OnButton3Clicked();
            if (Input.GetKeyDown(KeyCode.C)) OnButton4Clicked();
            if (Input.GetKeyDown(KeyCode.S)) OnButton5Clicked();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.A)) OnButton1Clicked();
            if (Input.GetKeyDown(KeyCode.H)) OnButton2Clicked();
            if (Input.GetKeyDown(KeyCode.S)) OnButton3Clicked();
        }
    }

    public void Refresh()
    {
        if (UnitSelectionManager.Instance == null || UnitSelectionManager.Instance.selectedUnits.Count == 0)
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
            return;
        }

        bool hasVillagers = false;
        bool hasCombatUnits = false;

        foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
        {
            if (unit != null)
            {
                if (unit.GetComponent<VillagerController>() != null)
                {
                    hasVillagers = true;
                }
                else if (unit.GetComponent<BaseCombatUnitController>() != null)
                {
                    hasCombatUnits = true;
                }
            }
        }

        if (hasVillagers)
        {
            _isVillagerMode = true;
            if (_panelRoot != null) _panelRoot.SetActive(true);

            // Cấu hình các nút hành động của Dân làng
            SetupButton(_buildButton, _btn1Text, _buildIcon, "🔨 B", "B");
            SetupButton(_garrisonButton, _btn2Text, _garrisonIcon, "🚪 G", "G");
            SetupButton(_repairButton, _btn3Text, _repairIcon, "🔧 R", "R");
            
            // Tính toán nút Cất tài nguyên
            bool canReturnCargo = false;
            foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
            {
                if (unit != null)
                {
                    var villager = unit.GetComponent<VillagerController>();
                    if (villager != null && villager.GetTotalCarryAmount() > 0)
                    {
                        canReturnCargo = true;
                        break;
                    }
                }
            }
            SetupButton(_returnCargoButton, _btn4Text, _returnCargoIcon, "📦 C", "C");
            if (_returnCargoButton != null) _returnCargoButton.interactable = canReturnCargo;

            SetupButton(_stopButton, _btn5Text, _stopIcon, "🛑 S", "S");
        }
        else if (hasCombatUnits)
        {
            _isVillagerMode = false;
            if (_panelRoot != null) _panelRoot.SetActive(true);

            // Cấu hình các nút hành động của Binh lính (Attack, Hold Position, Stop)
            SetupButton(_buildButton, _btn1Text, _attackIcon, "⚔️ A", "A");
            SetupButton(_garrisonButton, _btn2Text, _holdPositionIcon, "🛡️ H", "H");
            SetupButton(_repairButton, _btn3Text, _stopIcon, "🛑 S", "S");

            if (_returnCargoButton != null) _returnCargoButton.gameObject.SetActive(false);
            if (_stopButton != null) _stopButton.gameObject.SetActive(false);
        }
        else
        {
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }
    }

    /// <summary>
    /// Hàm thiết lập linh hoạt nút bấm: sử dụng ảnh icon (nếu kéo thả trong Inspector) hoặc nhãn chữ mặc định (nếu chưa gán).
    /// </summary>
    private void SetupButton(Button btn, TextMeshProUGUI txt, Sprite icon, string fallbackText, string hotkey)
    {
        if (btn == null) return;
        btn.gameObject.SetActive(true);
        btn.interactable = true;

        Image img = btn.GetComponent<Image>();

        if (icon != null)
        {
            // Thiết lập ảnh icon riêng
            if (img != null)
            {
                img.sprite = icon;
                img.color = Color.white; // Giữ nguyên màu thật của icon
            }

            // Đưa phím tắt nhanh về góc dưới bên phải nút cho giống phong cách RTS chuyên nghiệp
            if (txt != null)
            {
                txt.text = hotkey;
                RectTransform textRT = txt.GetComponent<RectTransform>();
                if (textRT != null)
                {
                    textRT.anchorMin = new Vector2(0.6f, 0f);
                    textRT.anchorMax = new Vector2(1f, 0.4f);
                    textRT.offsetMin = Vector2.zero;
                    textRT.offsetMax = Vector2.zero;
                    txt.alignment = TextAlignmentOptions.BottomRight;
                }
            }
        }
        else
        {
            // Fallback nếu chưa kéo thả ảnh icon trong Inspector
            if (img != null)
            {
                img.sprite = _defaultBtnSprite;
                img.color = _defaultBtnColor;
            }

            if (txt != null)
            {
                txt.text = fallbackText;
                RectTransform textRT = txt.GetComponent<RectTransform>();
                if (textRT != null)
                {
                    textRT.anchorMin = Vector2.zero;
                    textRT.anchorMax = Vector2.one;
                    textRT.offsetMin = Vector2.zero;
                    textRT.offsetMax = Vector2.zero;
                    txt.alignment = TextAlignmentOptions.Center;
                }
            }
        }
    }

    private void OnButton1Clicked()
    {
        if (_isVillagerMode)
        {
            OpenBuildingMenu();
        }
        else
        {
            if (UnitSelectionManager.Instance != null) UnitSelectionManager.Instance.StartAttackMode();
        }
    }

    private void OpenBuildingMenu()
    {
        BuildingManager buildingManager = BuildingManager.Instance;
        if (buildingManager != null)
        {
            if (!buildingManager.IsBuildMode)
            {
                buildingManager.ToggleBuildMode();
            }

            return;
        }

        if (UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.StartBuildMode();
        }
    }

    private void OnButton2Clicked()
    {
        if (_isVillagerMode)
        {
            foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
            {
                if (unit != null)
                {
                    var villager = unit.GetComponent<VillagerController>();
                    if (villager != null)
                    {
                        WatchTowerGarrison closest = null;
                        float closestDist = float.MaxValue;
                        for (int i = 0; i < WatchTowerGarrison.Registry.Count; i++)
                        {
                            var wt = WatchTowerGarrison.Registry[i];
                            if (wt != null && wt.gameObject.activeInHierarchy && wt.HasSpace)
                            {
                                float dist = Vector3.Distance(villager.transform.position, wt.transform.position);
                                if (dist < closestDist)
                                {
                                    closestDist = dist;
                                    closest = wt;
                                }
                            }
                        }
                        if (closest != null) closest.TrySendToGarrison(unit);
                    }
                }
            }
        }
        else
        {
            foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
            {
                if (unit != null)
                {
                    var combatUnit = unit.GetComponent<BaseCombatUnitController>();
                    if (combatUnit != null) combatUnit.CommandHoldPosition();
                }
            }
        }
    }

    private void OnButton3Clicked()
    {
        if (_isVillagerMode)
        {
            if (UnitSelectionManager.Instance != null) UnitSelectionManager.Instance.StartGatherMode();
        }
        else
        {
            foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
            {
                if (unit != null)
                {
                    var combatUnit = unit.GetComponent<BaseCombatUnitController>();
                    if (combatUnit != null) combatUnit.CommandStop();
                }
            }
        }
    }

    private void OnButton4Clicked()
    {
        if (_isVillagerMode)
        {
            foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
            {
                if (unit != null)
                {
                    var villager = unit.GetComponent<VillagerController>();
                    if (villager != null) villager.CommandReturnCargo();
                }
            }
        }
    }

    private void OnButton5Clicked()
    {
        if (_isVillagerMode)
        {
            foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
            {
                if (unit != null)
                {
                    var villager = unit.GetComponent<VillagerController>();
                    if (villager != null) villager.GoIdle();
                }
            }
        }
    }
}
