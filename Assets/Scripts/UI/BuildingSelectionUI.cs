using UnityEngine;
using System.Collections.Generic;

public class BuildingSelectionUI : MonoBehaviour
{
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

    // Procedural textures
    private Texture2D _panelBgTex;
    private Texture2D _cardNormalTex;
    private Texture2D _cardHoverTex;
    private Texture2D _cardSelectedTex;
    private Texture2D _cardLockedTex;
    private Texture2D _cardLockedOverlayTex;

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

    void Awake()
    {
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
        // CHỈ HIỆN UI KHI ĐANG BẬT CHẾ ĐỘ XÂY DỰNG
        if (BuildingManager.Instance == null || !BuildingManager.Instance.IsBuildMode)
        {
            return;
        }

        InitializeStyles();

        // THIẾT LẬP MA TRẬN TỰ ĐỘNG SCALE CHO ĐỘ PHÂN GIẢI 1920 x 1080
        // Dù người chơi dùng màn hình to hay nhỏ, UI sẽ tự động co giãn cực kỳ nét và chuẩn tỉ lệ!
        Vector3 scale = new Vector3(Screen.width / 1920f, Screen.height / 1080f, 1f);
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);

        // Khung điều khiển chính ở dưới đáy màn hình.
        GUI.Box(BuildPanelRect, "", _panelStyle);

        // Tiêu đề của Menu
        GUI.Label(new Rect(330, 822, 600, 30), "🛠️ CHẾ ĐỘ XÂY DỰNG (BUILD MODE)", _titleStyle);
        GUI.Label(new Rect(950, 828, 620, 30), "Phím R: Xoay Công Trình | Phím B: Đóng Menu", _subtitleStyle);

        // Danh sách công trình có sẵn
        List<BuildingData> buildings = BuildingManager.Instance.AvailableBuildings;
        if (buildings == null || buildings.Count == 0)
        {
            GUI.Label(new Rect(BuildCardViewportRect.x, BuildCardViewportRect.y + 30, BuildCardViewportRect.width, 50), "Không tìm thấy dữ liệu công trình nào để xây dựng!", _cardNameLockedStyle);
            return;
        }

        int totalWidth = Mathf.Max((buildings.Count * BuildCardWidth) + ((buildings.Count - 1) * BuildCardSpacing), Mathf.RoundToInt(BuildCardViewportRect.width));
        Rect contentRect = new Rect(0, 0, totalWidth, BuildCardViewportRect.height - 20);
        _buildingScrollPosition = GUI.BeginScrollView(BuildCardViewportRect, _buildingScrollPosition, contentRect, false, false);

        Vector2 mousePos = Event.current.mousePosition;

        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingData data = buildings[i];
            if (data == null) continue;

            Rect cardRect = new Rect(i * (BuildCardWidth + BuildCardSpacing), 0, BuildCardWidth, BuildCardHeight);
            
            bool isSelected = (BuildingManager.Instance.CurrentSelectedBuilding == data);
            bool isUnlocked = IsBuildingUnlocked(data);
            bool isHovered = cardRect.Contains(mousePos);

            // Chọn Style tương ứng dựa theo trạng thái (hoàn toàn không new GUIStyle() ở đây!)
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
            else if (isHovered)
            {
                currentBoxStyle = _cardHoverStyle;
            }

            // Vẽ thẻ công trình (Card)
            GUI.Box(cardRect, "", currentBoxStyle);

            // Vẽ Tên công trình
            GUI.Label(new Rect(cardRect.x + 6, cardRect.y + 8, cardRect.width - 12, 25), data.buildingName.ToUpper(), currentNameStyle);

            // Vẽ kích thước ô (Footprint Size)
            GUI.Label(new Rect(cardRect.x, cardRect.y + 35, cardRect.width, 20), $"Kích thước: [{data.buildingSize.x} x {data.buildingSize.y}]", _statsStyle);

            // Vẽ tài nguyên yêu cầu (Build Costs)
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
                
                string reqName = data.requiredBuildings != null && data.requiredBuildings.Count > 0 ? data.requiredBuildings[0].buildingName : "Khóa";
                GUI.Label(new Rect(cardRect.x, cardRect.y + BuildCardHeight / 2 - 10, cardRect.width, 25), $"🔒 Cần: {reqName}", _cardNameLockedStyle);
            }

            // Xử lý click trực tiếp trên card. GUI.Button hiểu đúng tọa độ bên trong ScrollView.
            GUI.enabled = isUnlocked;
            if (GUI.Button(cardRect, "", _emptyStyle))
            {
                BuildingManager.Instance.SelectBuilding(data);
                Debug.Log($"[UI] Đã chọn công trình: {data.buildingName}");
            }
            GUI.enabled = true;
        }

        GUI.EndScrollView();

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
            case ResourceType.Wood: return "🪵 Gỗ:";
            case ResourceType.Stone: return "🪨 Đá:";
            case ResourceType.Food: return "🌾 Thức ăn:";
            case ResourceType.Gold: return "🪙 Vàng:";
            default: return "📦 Vật phẩm:";
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
    /// Kiểm tra xem con trỏ chuột có đang nằm đè lên khu vực bảng chọn công trình (UI panel) hay không.
    /// </summary>
    public bool IsMouseOverUI()
    {
        if (BuildingManager.Instance == null || !BuildingManager.Instance.IsBuildMode)
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

        if (UnityEngine.InputSystem.Mouse.current == null) return false;
        
        // Hệ tọa độ chuột của Input System có gốc (0,0) ở góc DƯỚI bên trái.
        // Chuyển đổi tọa độ Y sang hệ OnGUI (gốc (0,0) ở góc TRÊN bên trái)
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
        float guiMouseX = mousePos.x;
        float guiMouseY = Screen.height - mousePos.y;

        Rect panelRect = new Rect(actualX, actualY, actualW, actualH);
        return panelRect.Contains(new Vector2(guiMouseX, guiMouseY));
    }
}
