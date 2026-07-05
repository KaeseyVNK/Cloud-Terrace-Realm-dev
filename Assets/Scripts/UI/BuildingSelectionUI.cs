using UnityEngine;
using System.Collections.Generic;

public class BuildingSelectionUI : MonoBehaviour
{
    public static BuildingSelectionUI Instance { get; private set; }

    [Header("UI Customization")]
    [UnityEngine.Serialization.FormerlySerializedAs("panelBgColor")]
    [SerializeField] private Color _panelBgColor = new Color(0.08f, 0.09f, 0.12f, 0.92f);
    
    [UnityEngine.Serialization.FormerlySerializedAs("cardNormalBg")]
    [SerializeField] private Color _cardNormalBg = new Color(0.12f, 0.14f, 0.18f, 0.95f);
    
    [UnityEngine.Serialization.FormerlySerializedAs("cardNormalBorder")]
    [SerializeField] private Color _cardNormalBorder = new Color(0.25f, 0.28f, 0.35f, 0.8f);

    [UnityEngine.Serialization.FormerlySerializedAs("cardHoverBg")]
    [SerializeField] private Color _cardHoverBg = new Color(0.14f, 0.18f, 0.26f, 0.98f);
    
    [UnityEngine.Serialization.FormerlySerializedAs("cardHoverBorder")]
    [SerializeField] private Color _cardHoverBorder = new Color(0.0f, 0.75f, 1.0f, 1.0f); // Vibrant Cyan

    [UnityEngine.Serialization.FormerlySerializedAs("cardSelectedBg")]
    [SerializeField] private Color _cardSelectedBg = new Color(0.15f, 0.22f, 0.18f, 0.98f);
    
    [UnityEngine.Serialization.FormerlySerializedAs("cardSelectedBorder")]
    [SerializeField] private Color _cardSelectedBorder = new Color(0.2f, 0.9f, 0.4f, 1.0f); // Vibrant Green

    [UnityEngine.Serialization.FormerlySerializedAs("cardLockedBg")]
    [SerializeField] private Color _cardLockedBg = new Color(0.16f, 0.12f, 0.12f, 0.95f);
    
    [UnityEngine.Serialization.FormerlySerializedAs("cardLockedBorder")]
    [SerializeField] private Color _cardLockedBorder = new Color(0.55f, 0.15f, 0.15f, 0.8f); // Dark Red

    [Header("Demolish Button Colors")]
    [SerializeField] private Color _demolishBtnColor = new Color(0.6f, 0.15f, 0.15f, 0.9f);
    [SerializeField] private Color _demolishBtnActiveColor = new Color(0.9f, 0.2f, 0.2f, 1.0f);
    [SerializeField] private Color _demolishBtnBorder = new Color(0.8f, 0.3f, 0.3f, 1.0f);

    // Procedural textures
    private Texture2D _panelBgTex;
    private Texture2D _cardNormalTex;
    private Texture2D _cardHoverTex;
    private Texture2D _cardSelectedTex;
    private Texture2D _cardLockedTex;
    private Texture2D _cardLockedOverlayTex;
    private Texture2D _demolishNormalTex;
    private Texture2D _demolishActiveTex;

    // Custom GUIStyles
    private GUIStyle _titleStyle;
    private GUIStyle _subtitleStyle;
    private GUIStyle _cardNameStyle;
    private GUIStyle _cardNameSelectedStyle;
    private GUIStyle _cardNameLockedStyle;
    private GUIStyle _statsStyle;
    private GUIStyle _costStyle;
    private GUIStyle _costLockedStyle;
    private GUIStyle _emptyStyle;
    private GUIStyle _demolishBtnStyle;
    private GUIStyle _demolishBtnActiveStyle;
    private GUIStyle _tooltipDescStyle;

    // Optimized cached Box/Card Styles
    private GUIStyle _panelStyle;
    private GUIStyle _cardNormalStyle;
    private GUIStyle _cardHoverStyle;
    private GUIStyle _cardSelectedStyle;
    private GUIStyle _cardLockedStyle;
    private GUIStyle _cardLockedOverlayStyle;

    private bool _stylesInitialized = false;
    private Vector2 _buildingScrollPosition;

    private static readonly Rect BuildPanelRect = new Rect(300, 805, 1320, 250);
    private static readonly Rect BuildCardViewportRect = new Rect(330, 875, 1260, 150);
    private const int BuildCardWidth = 185;
    private const int BuildCardHeight = 125;
    private const int BuildCardSpacing = 18;

    private int _heldCardIndex = -1;
    private float _holdStartTime = 0f;
    private bool _isTooltipShown = false;
    private const float HoldThreshold = 0.4f; // 400ms threshold

    void Awake()
    {
        Instance = this;

        // Tạo các Texture động bằng code (Plug and Play - không cần gán ảnh từ Inspector)
        _panelBgTex = MakeSolidTex(1200, 200, _panelBgColor);
        _cardNormalTex = MakeBorderTex(220, 130, _cardNormalBg, _cardNormalBorder, 1);
        _cardHoverTex = MakeBorderTex(220, 130, _cardHoverBg, _cardHoverBorder, 2);
        _cardSelectedTex = MakeBorderTex(220, 130, _cardSelectedBg, _cardSelectedBorder, 3);
        _cardLockedTex = MakeBorderTex(220, 130, _cardLockedBg, _cardLockedBorder, 1);
        _cardLockedOverlayTex = MakeSolidTex(1, 1, new Color(0.1f, 0.05f, 0.05f, 0.6f));
    }

    private void OnDestroy()
    {
        // Giải phóng tài nguyên texture tránh rò rỉ bộ nhớ VRAM
        if (_panelBgTex != null) Destroy(_panelBgTex);
        if (_cardNormalTex != null) Destroy(_cardNormalTex);
        if (_cardHoverTex != null) Destroy(_cardHoverTex);
        if (_cardSelectedTex != null) Destroy(_cardSelectedTex);
        if (_cardLockedTex != null) Destroy(_cardLockedTex);
        if (_cardLockedOverlayTex != null) Destroy(_cardLockedOverlayTex);
        if (_demolishNormalTex != null) Destroy(_demolishNormalTex);
        if (_demolishActiveTex != null) Destroy(_demolishActiveTex);
    }

    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        _titleStyle = new GUIStyle();
        _titleStyle.fontSize = 20;
        _titleStyle.fontStyle = FontStyle.Bold;
        _titleStyle.alignment = TextAnchor.UpperLeft;
        _titleStyle.normal.textColor = new Color(0.95f, 0.8f, 0.3f, 1.0f); // Gold color

        _subtitleStyle = new GUIStyle();
        _subtitleStyle.fontSize = 13;
        _subtitleStyle.fontStyle = FontStyle.Italic;
        _subtitleStyle.alignment = TextAnchor.UpperRight;
        _subtitleStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f, 1.0f); // Light gray

        _cardNameStyle = new GUIStyle();
        _cardNameStyle.fontSize = 15;
        _cardNameStyle.fontStyle = FontStyle.Bold;
        _cardNameStyle.alignment = TextAnchor.MiddleCenter;
        _cardNameStyle.normal.textColor = new Color(0.9f, 0.95f, 1.0f, 1.0f); // Crisp white-cyan

        _cardNameSelectedStyle = new GUIStyle(_cardNameStyle);
        _cardNameSelectedStyle.normal.textColor = new Color(0.4f, 1.0f, 0.5f, 1.0f); // Bright green

        _cardNameLockedStyle = new GUIStyle(_cardNameStyle);
        _cardNameLockedStyle.normal.textColor = new Color(0.7f, 0.4f, 0.4f, 1.0f); // Red-gray

        _statsStyle = new GUIStyle();
        _statsStyle.fontSize = 12;
        _statsStyle.alignment = TextAnchor.MiddleCenter;
        _statsStyle.normal.textColor = new Color(0.3f, 0.75f, 0.95f, 1.0f); // Cyan

        _costStyle = new GUIStyle();
        _costStyle.fontSize = 11;
        _costStyle.fontStyle = FontStyle.Bold;
        _costStyle.alignment = TextAnchor.UpperLeft;
        _costStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f, 1.0f);

        _costLockedStyle = new GUIStyle(_costStyle);
        _costLockedStyle.normal.textColor = new Color(0.6f, 0.4f, 0.4f, 1.0f);

        _emptyStyle = new GUIStyle();

        _tooltipDescStyle = new GUIStyle();
        _tooltipDescStyle.fontSize = 13;
        _tooltipDescStyle.wordWrap = true;
        _tooltipDescStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        _tooltipDescStyle.alignment = TextAnchor.UpperLeft;

        _demolishNormalTex = MakeBorderTex(180, 35, _demolishBtnColor, _demolishBtnBorder, 1);
        _demolishActiveTex = MakeBorderTex(180, 35, _demolishBtnActiveColor, Color.white, 2);

        _demolishBtnStyle = new GUIStyle();
        _demolishBtnStyle.normal.background = _demolishNormalTex;
        _demolishBtnStyle.alignment = TextAnchor.MiddleCenter;
        _demolishBtnStyle.fontSize = 13;
        _demolishBtnStyle.fontStyle = FontStyle.Bold;
        _demolishBtnStyle.normal.textColor = Color.white;

        _demolishBtnActiveStyle = new GUIStyle(_demolishBtnStyle);
        _demolishBtnActiveStyle.normal.background = _demolishActiveTex;
        _demolishBtnActiveStyle.normal.textColor = Color.yellow;

        // Khởi tạo các Box style sử dụng texture đã được pre-cache
        _panelStyle = new GUIStyle();
        _panelStyle.normal.background = _panelBgTex;

        _cardNormalStyle = new GUIStyle();
        _cardNormalStyle.normal.background = _cardNormalTex;

        _cardHoverStyle = new GUIStyle();
        _cardHoverStyle.normal.background = _cardHoverTex;

        _cardSelectedStyle = new GUIStyle();
        _cardSelectedStyle.normal.background = _cardSelectedTex;

        _cardLockedStyle = new GUIStyle();
        _cardLockedStyle.normal.background = _cardLockedTex;

        _cardLockedOverlayStyle = new GUIStyle();
        _cardLockedOverlayStyle.normal.background = _cardLockedOverlayTex;

        _stylesInitialized = true;
    }

    void OnGUI()
    {
        // CHỈ HIỆN UI KHI ĐANG BẬT CHẾ ĐỘ XÂY DỰNG HOẶC PHÁ HỦY
        if (BuildingManager.Instance == null || (!BuildingManager.Instance.IsBuildMode && !BuildingManager.Instance.IsDeleteMode))
        {
            return;
        }

        InitializeStyles();

        // THIẾT LẬP MA TRẬN TỰ ĐỘNG SCALE CHO ĐỘ PHÂN GIẢI 1920 x 1080
        Vector3 scale = new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);

        // Khung điều khiển chính ở dưới đáy màn hình.
        GUI.Box(BuildPanelRect, "", _panelStyle);

        bool isDeleteMode = BuildingManager.Instance.IsDeleteMode;

        // Tiêu đề của Menu
        if (isDeleteMode)
        {
            GUI.Label(new Rect(330, 822, 600, 30), "💣 DEMOLISH MODE", _titleStyle);
            GUI.Label(new Rect(950, 828, 620, 30), "Tap on any structure to demolish | B Key: Close", _subtitleStyle);
        }
        else
        {
            GUI.Label(new Rect(330, 822, 600, 30), "🛠️ BUILD MODE", _titleStyle);
            GUI.Label(new Rect(950, 828, 620, 30), "R Key: Rotate Structure | B Key: Close Menu", _subtitleStyle);
        }

        // Nút Demolish Mode
        Rect demolishBtnRect = new Rect(1420, 815, 180, 35);
        GUIStyle currentDemolishStyle = isDeleteMode ? _demolishBtnActiveStyle : _demolishBtnStyle;
        if (GUI.Button(demolishBtnRect, isDeleteMode ? "💣 DEMOLISHING" : "💣 DEMOLISH", currentDemolishStyle))
        {
            BuildingManager.Instance.ToggleDeleteMode();
        }

        // Nếu ở chế độ xóa, không vẽ danh sách thẻ xây dựng
        if (isDeleteMode)
        {
            if (BuildPanelRect.Contains(Event.current.mousePosition)
                && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp))
            {
                Event.current.Use();
            }
            return;
        }

        // Danh sách công trình có sẵn
        List<BuildingData> buildings = BuildingManager.Instance.AvailableBuildings;
        if (buildings == null || buildings.Count == 0)
        {
            GUI.Label(new Rect(BuildCardViewportRect.x, BuildCardViewportRect.y + 30, BuildCardViewportRect.width, 50), "No building data found!", _cardNameLockedStyle);
            return;
        }

        int totalWidth = Mathf.Max((buildings.Count * BuildCardWidth) + ((buildings.Count - 1) * BuildCardSpacing), Mathf.RoundToInt(BuildCardViewportRect.width));
        Rect contentRect = new Rect(0, 0, totalWidth, BuildCardViewportRect.height - 20);
        _buildingScrollPosition = GUI.BeginScrollView(BuildCardViewportRect, _buildingScrollPosition, contentRect, false, false);

        Vector2 mousePos = Event.current.mousePosition;
        int hoveredCardIndex = -1;

        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingData data = buildings[i];
            if (data == null) continue;

            Rect cardRect = new Rect(i * (BuildCardWidth + BuildCardSpacing), 0, BuildCardWidth, BuildCardHeight);
            bool isInsideCard = cardRect.Contains(mousePos);
            bool isSelected = (BuildingManager.Instance.CurrentSelectedBuilding == data);
            bool isUnlocked = IsBuildingUnlocked(data);

            if (isInsideCard && isUnlocked)
            {
                hoveredCardIndex = i;
            }

            GUIStyle currentBoxStyle = _cardNormalStyle;
            GUIStyle currentNameStyle = _cardNameStyle;

            if (!isUnlocked)
            {
                currentBoxStyle = _cardLockedStyle;
                currentNameStyle = _cardNameLockedStyle;
            }
            else if (isSelected)
            {
                currentBoxStyle = _cardSelectedStyle;
                currentNameStyle = _cardNameSelectedStyle;
            }
            else if (isInsideCard)
            {
                currentBoxStyle = _cardHoverStyle;
            }

            // Vẽ thẻ công trình
            GUI.Box(cardRect, "", currentBoxStyle);

            // Vẽ Tên công trình
            GUI.Label(new Rect(cardRect.x + 6, cardRect.y + 8, cardRect.width - 12, 25), data.buildingName.ToUpper(), currentNameStyle);

            // Vẽ kích thước ô
            GUI.Label(new Rect(cardRect.x, cardRect.y + 35, cardRect.width, 20), $"Size: [{data.buildingSize.x} x {data.buildingSize.y}]", _statsStyle);

            // Vẽ tài nguyên yêu cầu
            if (data.buildCosts != null && data.buildCosts.Count > 0)
            {
                int costX = (int)cardRect.x + 16;
                int costY = (int)cardRect.y + 60;
                GUIStyle currentCostStyle = isUnlocked ? _costStyle : _costLockedStyle;

                foreach (var cost in data.buildCosts)
                {
                    string resourceIcon = GetResourceIcon(cost.resourceType);
                    GUI.Label(new Rect(costX, costY, cardRect.width - 40, 20), $"{resourceIcon} {cost.amount}", currentCostStyle);
                    costY += 18;
                }
            }

            // Nếu công trình bị khóa, vẽ đè nhãn báo khóa
            if (!isUnlocked)
            {
                GUI.Box(new Rect(cardRect.x + 5, cardRect.y + 5, cardRect.width - 10, cardRect.height - 10), "", _cardLockedOverlayStyle);
                string reqName = data.requiredBuildings != null && data.requiredBuildings.Count > 0 ? data.requiredBuildings[0].buildingName : "Locked";
                GUI.Label(new Rect(cardRect.x, cardRect.y + BuildCardHeight / 2 - 10, cardRect.width, 25), $"🔒 Requires: {reqName}", _cardNameLockedStyle);
            }
        }

        GUI.EndScrollView();

        // Xử lý click và nhấn giữ (Hold to Tooltip, Click to Build)
        if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            if (hoveredCardIndex != -1)
            {
                _heldCardIndex = hoveredCardIndex;
                _holdStartTime = Time.time;
                _isTooltipShown = false;
            }
        }
        else if (Event.current.type == EventType.MouseDrag)
        {
            if (_heldCardIndex != -1 && hoveredCardIndex != _heldCardIndex)
            {
                _heldCardIndex = -1;
                _isTooltipShown = false;
            }
        }
        else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            if (_heldCardIndex != -1)
            {
                float holdDuration = Time.time - _holdStartTime;
                if (holdDuration < HoldThreshold)
                {
                    // TAP / CLICK: Chọn đặt móng xây công trình
                    BuildingManager.Instance.SelectBuilding(buildings[_heldCardIndex]);
                    GameLog.Log($"[UI] Tap/Click: Select building {buildings[_heldCardIndex].buildingName}");
                }
                _heldCardIndex = -1;
                _isTooltipShown = false;
            }
        }

        // Kiểm tra xem có đang kích hoạt Tooltip hay không
        if (_heldCardIndex != -1 && !_isTooltipShown)
        {
            if (Time.time - _holdStartTime >= HoldThreshold)
            {
                _isTooltipShown = true;
            }
        }

        // Vẽ Tooltip nổi tuyệt đẹp ở phía trên danh sách công trình
        if (_isTooltipShown && _heldCardIndex != -1 && _heldCardIndex < buildings.Count)
        {
            BuildingData heldData = buildings[_heldCardIndex];
            if (heldData != null)
            {
                float cardX = _heldCardIndex * (BuildCardWidth + BuildCardSpacing) - _buildingScrollPosition.x;
                float screenX = 330f + cardX;
                
                float tooltipX = Mathf.Clamp(screenX + BuildCardWidth / 2f - 160f, 310f, 1590f);
                Rect tooltipRect = new Rect(tooltipX, 670f, 320f, 120f);

                GUI.Box(tooltipRect, "", _cardSelectedStyle);

                GUI.Label(new Rect(tooltipRect.x + 12, tooltipRect.y + 8, tooltipRect.width - 24, 25), heldData.buildingName.ToUpper(), _titleStyle);
                
                string descText = string.IsNullOrEmpty(heldData.description) ? "No description available." : heldData.description;
                GUI.Label(new Rect(tooltipRect.x + 12, tooltipRect.y + 35, tooltipRect.width - 24, 75), descText, _tooltipDescStyle);
            }
        }

        if (BuildPanelRect.Contains(Event.current.mousePosition)
            && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp))
        {
            Event.current.Use();
        }
    }

    private bool IsBuildingUnlocked(BuildingData data)
    {
        if (BuildingManager.Instance == null) return false;
        if (data.requiredBuildings == null || data.requiredBuildings.Count == 0) return true;

        foreach (var reqBuilding in data.requiredBuildings)
        {
            if (!BuildingManager.Instance.BuiltBuildingCounts.ContainsKey(reqBuilding) || 
                BuildingManager.Instance.BuiltBuildingCounts[reqBuilding] <= 0)
            {
                return false;
            }
        }
        return true;
    }

    private string GetResourceIcon(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood: return "🪵 Wood:";
            case ResourceType.Stone: return "🪨 Stone:";
            case ResourceType.Food: return "🌾 Food:";
            case ResourceType.Gold: return "🪙 Gold:";
            default: return "📦 Item:";
        }
    }

    // --- TIỆN ÍCH TẠO TEXTURE PROCEDURAL BẰNG CODE ---

    private Texture2D MakeSolidTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; ++i)
        {
            pix[i] = col;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private Texture2D MakeBorderTex(int width, int height, Color bgColor, Color borderColor, int borderThickness)
    {
        Color[] pix = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBorder = (x < borderThickness || x >= width - borderThickness || 
                                 y < borderThickness || y >= height - borderThickness);
                pix[y * width + x] = isBorder ? borderColor : bgColor;
            }
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    /// <summary>
    /// Kiểm tra xem con trỏ chuột/chạm có đang nằm đè lên khu vực bảng chọn công trình (UI panel) hay không.
    /// </summary>
    public bool IsMouseOverUI()
    {
        if (BuildingManager.Instance == null || (!BuildingManager.Instance.IsBuildMode && !BuildingManager.Instance.IsDeleteMode))
        {
            return false;
        }

        // Tính tỉ lệ co giãn thực tế theo độ phân giải màn hình hiện tại
        float scaleX = Screen.width / 1920f;
        float scaleY = Screen.height / 1080f;

        float actualX = BuildPanelRect.x * scaleX;
        float actualY = BuildPanelRect.y * scaleY;
        float actualW = BuildPanelRect.width * scaleX;
        float actualH = BuildPanelRect.height * scaleY;

        Vector2 inputPos = Vector2.zero;
        if (Input.touchCount > 0)
        {
            inputPos = Input.GetTouch(0).position;
        }
        else
        {
            inputPos = Input.mousePosition;
        }

        float guiInputX = inputPos.x;
        float guiInputY = Screen.height - inputPos.y;

        Rect panelRect = new Rect(actualX, actualY, actualW, actualH);
        return panelRect.Contains(new Vector2(guiInputX, guiInputY));
    }
}
