using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Quản lý giao diện hiển thị thông tin thế giới (Thanh máu Unit/Công trình và số lượng dân/lính trong nhà/tháp canh).
/// Tự động khởi tạo và chạy không cần gán thủ công vào Scene (Plug and Play).
/// Đã được tối ưu hóa hiệu năng triệt để (Không GC Allocations, không gọi GetComponent/Find trong OnGUI).
/// </summary>
public class WorldSpaceOverlayManager : MonoBehaviour
{
    private Camera _mainCamera;
    private float _nextRefreshTime = 0f;
    private ResourceNode _hoveredResourceNode;
    
    // Cache các UI managers
    private MainBuildingUI _mainUI;
    private WatchTowerGarrisonUI _watchUI;
    private TestProductionUI _prodUI;

    // Cache các GUIStyle để tránh cấp phát bộ nhớ (GC Alloc) gây sụt FPS
    private GUIStyle _unitHpTextStyle;
    private GUIStyle _buildingHpTextStyle;
    private GUIStyle _shelterTextStyle;
    private GUIStyle _watchTowerTextStyle;
    private GUIStyle _resourceTooltipStyle;
    private GUIStyle _hungerTextStyle;
    private bool _stylesInitialized = false;

    // Cache GUIContent để tái sử dụng, tránh new GUIContent mỗi frame
    private readonly GUIContent _tempContent = new GUIContent();

    // Cache chiều cao vật lý của đối tượng để tránh gọi GetComponent<Collider>() mỗi frame
    private readonly Dictionary<GameObject, float> _cachedHeights = new Dictionary<GameObject, float>();
    
    // Cache BaseCombatUnitController trên SelectableUnit để tránh GetComponent mỗi frame
    private readonly Dictionary<SelectableUnit, BaseCombatUnitController> _cachedCombatControllers = new Dictionary<SelectableUnit, BaseCombatUnitController>();

    // Cache trạng thái lựa chọn công trình để so sánh tham chiếu cực nhanh
    private BaseCombatUnitController _selectedMainBuildingCombat;
    private BaseCombatUnitController _selectedWatchTowerCombat;
    private BaseCombatUnitController _selectedProductionCombat;
    private BaseCombatUnitController _selectedResearchCombat;
    private int _lastSelectionCacheFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        GameObject go = new GameObject("WorldSpaceOverlayManager");
        go.AddComponent<WorldSpaceOverlayManager>();
        DontDestroyOnLoad(go);
        Debug.Log("[WorldSpaceOverlayManager] Đã tự động khởi chạy hệ thống Giao diện Overlay tối ưu hóa cao.");
    }

    private void Start()
    {
        _mainCamera = Camera.main;
        RefreshCachedReferences();
    }

    private void Update()
    {
        // Liên tục cập nhật Camera chính nếu có thay đổi
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        // CHỈ quét tìm các đối tượng và UI mỗi giây một lần để tiết kiệm CPU tối đa
        if (Time.time >= _nextRefreshTime)
        {
            _nextRefreshTime = Time.time + 1.0f;
            RefreshCachedReferences();
        }

        // Bắn tia Raycast phát hiện ResourceNode dưới chuột (lọc tài nguyên trong sương mù)
        if (_mainCamera != null && UnityEngine.InputSystem.Mouse.current != null)
        {
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Ray ray = _mainCamera.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, 200f))
            {
                ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
                if (node != null)
                {
                    FogVisibilityTarget visibilityTarget = node.GetComponentInParent<FogVisibilityTarget>();
                    if (visibilityTarget != null && !visibilityTarget.IsVisible)
                    {
                        _hoveredResourceNode = null;
                    }
                    else
                    {
                        _hoveredResourceNode = node;
                    }
                }
                else
                {
                    _hoveredResourceNode = null;
                }
            }
            else
            {
                _hoveredResourceNode = null;
            }
        }
        else
        {
            _hoveredResourceNode = null;
        }
    }

    /// <summary>
    /// Thực hiện quét tìm các UI Manager và đối tượng trong màn chơi (Chỉ gọi 1 giây / lần).
    /// </summary>
    private void RefreshCachedReferences()
    {
        _mainUI = FindAnyObjectByType<MainBuildingUI>();
        _watchUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
        _prodUI = FindAnyObjectByType<TestProductionUI>();

        // Dọn dẹp cache để tránh rò rỉ bộ nhớ (memory leak) khi đối tượng bị hủy
        CleanDestroyedObjectsFromCache();
    }

    /// <summary>
    /// Dọn dẹp các đối tượng đã bị hủy khỏi bộ đệm Cache.
    /// </summary>
    private void CleanDestroyedObjectsFromCache()
    {
        // Dọn dẹp cache chiều cao
        var keysToRemove = new List<GameObject>();
        foreach (var key in _cachedHeights.Keys)
        {
            if (key == null)
            {
                keysToRemove.Add(key);
            }
        }
        for (int i = 0; i < keysToRemove.Count; i++)
        {
            _cachedHeights.Remove(keysToRemove[i]);
        }

        // Dọn dẹp cache bộ điều khiển chiến đấu
        var unitsToRemove = new List<SelectableUnit>();
        foreach (var key in _cachedCombatControllers.Keys)
        {
            if (key == null)
            {
                unitsToRemove.Add(key);
            }
        }
        for (int i = 0; i < unitsToRemove.Count; i++)
        {
            _cachedCombatControllers.Remove(unitsToRemove[i]);
        }
    }

    /// <summary>
    /// Khởi tạo các Style hiển thị tĩnh một lần duy nhất.
    /// </summary>
    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        _unitHpTextStyle = new GUIStyle();
        _unitHpTextStyle.fontSize = 10;
        _unitHpTextStyle.alignment = TextAnchor.MiddleCenter;
        _unitHpTextStyle.fontStyle = FontStyle.Bold;
        _unitHpTextStyle.normal.textColor = Color.white;

        _buildingHpTextStyle = new GUIStyle();
        _buildingHpTextStyle.fontSize = 10;
        _buildingHpTextStyle.alignment = TextAnchor.MiddleCenter;
        _buildingHpTextStyle.fontStyle = FontStyle.Bold;
        _buildingHpTextStyle.normal.textColor = Color.white;

        _shelterTextStyle = new GUIStyle();
        _shelterTextStyle.alignment = TextAnchor.MiddleCenter;
        _shelterTextStyle.normal.textColor = new Color(0.95f, 0.95f, 1f);
        _shelterTextStyle.fontSize = 11;
        _shelterTextStyle.fontStyle = FontStyle.Bold;

        _watchTowerTextStyle = new GUIStyle();
        _watchTowerTextStyle.alignment = TextAnchor.MiddleCenter;
        _watchTowerTextStyle.normal.textColor = new Color(1.0f, 0.95f, 0.85f);
        _watchTowerTextStyle.fontSize = 11;
        _watchTowerTextStyle.fontStyle = FontStyle.Bold;

        _resourceTooltipStyle = new GUIStyle();
        _resourceTooltipStyle.alignment = TextAnchor.MiddleCenter;
        _resourceTooltipStyle.normal.textColor = new Color(0.3f, 0.9f, 1.0f); // Bright Cyan
        _resourceTooltipStyle.fontSize = 11;
        _resourceTooltipStyle.fontStyle = FontStyle.Bold;

        _hungerTextStyle = new GUIStyle();
        _hungerTextStyle.alignment = TextAnchor.MiddleCenter;
        _hungerTextStyle.normal.textColor = new Color(1.0f, 0.3f, 0.3f); // Đỏ sáng
        _hungerTextStyle.fontSize = 11;
        _hungerTextStyle.fontStyle = FontStyle.Bold;

        _stylesInitialized = true;
    }

    /// <summary>
    /// Cache các công trình đang được chọn một lần mỗi frame (OnGUI chạy nhiều lần/frame).
    /// </summary>
    private void CacheSelectionsIfNeeded()
    {
        if (Time.frameCount == _lastSelectionCacheFrame) return;
        _lastSelectionCacheFrame = Time.frameCount;

        _selectedMainBuildingCombat = (_mainUI != null) ? _mainUI.SelectedMainBuilding : null;
        
        _selectedWatchTowerCombat = null;
        if (_watchUI != null && _watchUI.SelectedWatchTower != null)
        {
            _selectedWatchTowerCombat = _watchUI.SelectedWatchTower.GetComponent<BaseCombatUnitController>();
        }

        _selectedProductionCombat = null;
        if (_prodUI != null && _prodUI.SelectedProduction != null)
        {
            _selectedProductionCombat = _prodUI.SelectedProduction.GetComponent<BaseCombatUnitController>();
        }

        _selectedResearchCombat = null;
        if (_prodUI != null && _prodUI.SelectedResearch != null)
        {
            _selectedResearchCombat = _prodUI.SelectedResearch.GetComponent<BaseCombatUnitController>();
        }
    }

    /// <summary>
    /// Lấy hoặc tính toán chiều cao của đối tượng thế giới và lưu vào bộ đệm cache.
    /// </summary>
    private float GetCachedHeight(GameObject go, float defaultHeight)
    {
        if (go == null) return defaultHeight;

        if (_cachedHeights.TryGetValue(go, out float height))
        {
            return height;
        }

        // Tính toán chiều cao thực tế dựa trên Collider hoặc Mesh Renderer
        height = defaultHeight;
        var col = go.GetComponent<Collider>();
        if (col != null)
        {
            height = col.bounds.size.y;
        }
        else
        {
            var ren = go.GetComponentInChildren<Renderer>();
            if (ren != null)
            {
                height = ren.bounds.size.y;
            }
        }

        _cachedHeights[go] = height;
        return height;
    }

    /// <summary>
    /// Lấy hoặc cache BaseCombatUnitController trên SelectableUnit.
    /// </summary>
    private BaseCombatUnitController GetCachedCombatController(SelectableUnit unit)
    {
        if (unit == null) return null;

        if (_cachedCombatControllers.TryGetValue(unit, out var controller))
        {
            return controller;
        }

        controller = unit.GetComponent<BaseCombatUnitController>();
        _cachedCombatControllers[unit] = controller;
        return controller;
    }

    private void OnGUI()
    {
        if (_mainCamera == null) return;

        InitializeStyles();
        CacheSelectionsIfNeeded();

        DrawSelectedUnitsHealth();
        DrawBuildingsHealth();
        DrawShelterOccupancy();
        DrawWatchTowerOccupancy();
        DrawHoveredResourceTooltip();
        DrawHungryVillagersBadge();
    }

    /// <summary>
    /// Kiểm tra xem một công trình có đang được người chơi chọn hay không thông qua so sánh tham chiếu cực nhanh.
    /// </summary>
    private bool IsBuildingSelected(BaseCombatUnitController combat)
    {
        if (combat == null) return false;

        return combat == _selectedMainBuildingCombat ||
               combat == _selectedWatchTowerCombat ||
               combat == _selectedProductionCombat ||
               combat == _selectedResearchCombat;
    }

    /// <summary>
    /// Vẽ thanh máu cho các đơn vị đang được chọn.
    /// </summary>
    private void DrawSelectedUnitsHealth()
    {
        if (SelectableUnit.AllUnits == null) return;

        Color originalColor = GUI.color;

        for (int i = 0; i < SelectableUnit.AllUnits.Count; i++)
        {
            var unit = SelectableUnit.AllUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || !unit.IsSelected)
                continue;

            var combat = GetCachedCombatController(unit);
            if (combat == null || combat.currentState == CombatState.Dead)
                continue;

            DrawUnitHealthBar(unit, combat);
        }

        GUI.color = originalColor;
    }

    /// <summary>
    /// Vẽ thanh máu cho các công trình (khi được chọn hoặc khi bị mất máu/tấn công).
    /// </summary>
    private void DrawBuildingsHealth()
    {
        Color originalColor = GUI.color;

        for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
        {
            var combat = BaseCombatUnitController.Registry[i];
            if (combat == null || !combat.gameObject.activeInHierarchy || combat.currentState == CombatState.Dead)
                continue;

            bool isMainBuilding = combat is MainBuildingCombatTarget;
            bool isBuilding = combat is BuildingCombatTarget;

            if (isMainBuilding || isBuilding)
            {
                bool isSelected = IsBuildingSelected(combat);
                bool isDamaged = combat.currentHealth < combat.maxHealth;

                if (isSelected || isDamaged)
                {
                    DrawBuildingHealthBar(combat, isSelected);
                }
            }
        }

        GUI.color = originalColor;
    }

    /// <summary>
    /// Vẽ thanh máu cụ thể phía trên một đơn vị di động.
    /// </summary>
    private void DrawUnitHealthBar(SelectableUnit unit, BaseCombatUnitController combat)
    {
        float height = GetCachedHeight(unit.gameObject, 2.0f);

        Vector3 worldPos = unit.transform.position + Vector3.up * (height + 0.25f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        float barWidth = 60f;
        float barHeight = 6f;

        Rect bgRect = new Rect(guiX - barWidth / 2f, guiY - barHeight / 2f, barWidth, barHeight);
        Rect borderRect = new Rect(bgRect.x - 1, bgRect.y - 1, bgRect.width + 2, bgRect.height + 2);

        float healthPercent = Mathf.Clamp01((float)combat.currentHealth / combat.maxHealth);

        Color healthColor = new Color(0.2f, 0.9f, 0.4f); // Xanh lục neon
        if (healthPercent < 0.2f)
            healthColor = new Color(1.0f, 0.2f, 0.2f); // Đỏ
        else if (healthPercent < 0.5f)
            healthColor = new Color(1.0f, 0.6f, 0.0f); // Cam

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(borderRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.9f);
        GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

        Rect fillRect = new Rect(bgRect.x, bgRect.y, bgRect.width * healthPercent, bgRect.height);
        GUI.color = healthColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        string healthText = $"{combat.currentHealth}/{combat.maxHealth}";

        Rect textRect = new Rect(guiX - 30f, guiY - barHeight / 2f - 14f, 60f, 12f);
        GUI.Label(textRect, healthText, _unitHpTextStyle);
    }

    /// <summary>
    /// Vẽ thanh máu lớn phía trên một công trình kiến trúc.
    /// </summary>
    private void DrawBuildingHealthBar(BaseCombatUnitController combat, bool isSelected)
    {
        float height = GetCachedHeight(combat.gameObject, 3.5f);

        Vector3 worldPos = combat.transform.position + Vector3.up * (height + 0.25f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        float barWidth = 100f;
        float barHeight = 8f;

        Rect bgRect = new Rect(guiX - barWidth / 2f, guiY - barHeight / 2f, barWidth, barHeight);
        Rect borderRect = new Rect(bgRect.x - 1, bgRect.y - 1, bgRect.width + 2, bgRect.height + 2);

        float healthPercent = Mathf.Clamp01((float)combat.currentHealth / combat.maxHealth);

        Color healthColor = new Color(0.2f, 0.9f, 0.4f); // Xanh lục
        if (healthPercent < 0.2f)
            healthColor = new Color(1.0f, 0.2f, 0.2f); // Đỏ
        else if (healthPercent < 0.5f)
            healthColor = new Color(1.0f, 0.6f, 0.0f); // Cam

        // Nổi bật viền nếu công trình đang được chọn
        if (isSelected)
            GUI.color = new Color(0.25f, 0.75f, 1.0f, 0.95f); // Viền xanh dương sáng
        else
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            
        GUI.DrawTexture(borderRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

        Rect fillRect = new Rect(bgRect.x, bgRect.y, bgRect.width * healthPercent, bgRect.height);
        GUI.color = healthColor;
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;
        string nameText = string.IsNullOrEmpty(combat.unitName) ? "Building" : combat.unitName;
        string healthText = $"{nameText}: {combat.currentHealth}/{combat.maxHealth}";

        Rect textRect = new Rect(guiX - 100f, guiY - barHeight / 2f - 14f, 200f, 12f);
        GUI.Label(textRect, healthText, _buildingHpTextStyle);
    }

    /// <summary>
    /// Vẽ số lượng cư dân cho toàn bộ nhà dân.
    /// </summary>
    private void DrawShelterOccupancy()
    {
        Color originalColor = GUI.color;

        for (int i = 0; i < HouseShelter.Registry.Count; i++)
        {
            var shelter = HouseShelter.Registry[i];
            if (shelter == null || !shelter.gameObject.activeInHierarchy)
                continue;

            int occupants = shelter.OccupantCount;
            if (occupants <= 0)
                continue;

            DrawShelterBadge(shelter, occupants);
        }

        GUI.color = originalColor;
    }

    /// <summary>
    /// Vẽ nhãn số lượng cư dân cụ thể phía trên một nhà dân (Xanh dương).
    /// </summary>
    private void DrawShelterBadge(HouseShelter shelter, int occupants)
    {
        float height = GetCachedHeight(shelter.gameObject, 3.5f);

        Vector3 worldPos = shelter.transform.position + Vector3.up * (height + 0.95f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        string text = $"🏠 {occupants} / {shelter.Capacity}";
        _tempContent.text = text;

        Vector2 size = _shelterTextStyle.CalcSize(_tempContent);
        float paddingX = 8f;
        float paddingY = 4f;
        float rectWidth = size.x + paddingX * 2;
        float rectHeight = size.y + paddingY * 2;

        Rect badgeRect = new Rect(guiX - rectWidth / 2f, guiY - rectHeight / 2f, rectWidth, rectHeight);

        GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.88f);
        GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);

        Rect borderRect = new Rect(badgeRect.x - 1, badgeRect.y - 1, badgeRect.width + 2, badgeRect.height + 2);
        GUI.color = new Color(0.25f, 0.75f, 1.0f, 0.6f); // Viền Cyan
        
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        Rect textRect = new Rect(badgeRect.x + paddingX, badgeRect.y + paddingY, size.x, size.y);
        GUI.Label(textRect, _tempContent, _shelterTextStyle);
    }

    /// <summary>
    /// Vẽ số lượng đơn vị đồn trú cho các tháp canh phòng thủ.
    /// </summary>
    private void DrawWatchTowerOccupancy()
    {
        Color originalColor = GUI.color;

        for (int i = 0; i < WatchTowerGarrison.Registry.Count; i++)
        {
            var tower = WatchTowerGarrison.Registry[i];
            if (tower == null || !tower.gameObject.activeInHierarchy)
                continue;

            int occupants = tower.OccupantCount;
            if (occupants <= 0)
                continue;

            DrawWatchTowerBadge(tower, occupants);
        }

        GUI.color = originalColor;
    }

    /// <summary>
    /// Vẽ nhãn số lượng quân đồn trú cụ thể phía trên Tháp canh (Vàng/Cam phòng thủ).
    /// </summary>
    private void DrawWatchTowerBadge(WatchTowerGarrison tower, int occupants)
    {
        float height = GetCachedHeight(tower.gameObject, 5.0f);

        Vector3 worldPos = tower.transform.position + Vector3.up * (height + 0.95f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        string text = $"🏹 {occupants} / {tower.Capacity}";
        _tempContent.text = text;

        Vector2 size = _watchTowerTextStyle.CalcSize(_tempContent);
        float paddingX = 8f;
        float paddingY = 4f;
        float rectWidth = size.x + paddingX * 2;
        float rectHeight = size.y + paddingY * 2;

        Rect badgeRect = new Rect(guiX - rectWidth / 2f, guiY - rectHeight / 2f, rectWidth, rectHeight);

        GUI.color = new Color(0.15f, 0.11f, 0.08f, 0.9f); // Nền cam-vàng tối
        GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);

        Rect borderRect = new Rect(badgeRect.x - 1, badgeRect.y - 1, badgeRect.width + 2, badgeRect.height + 2);
        GUI.color = new Color(1.0f, 0.7f, 0.1f, 0.7f); // Viền vàng-cam
        
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        Rect textRect = new Rect(badgeRect.x + paddingX, badgeRect.y + paddingY, size.x, size.y);
        GUI.Label(textRect, _tempContent, _watchTowerTextStyle);
    }

    /// <summary>
    /// Vẽ tooltip hiển thị trữ lượng tài nguyên còn lại khi di chuột qua.
    /// </summary>
    private void DrawHoveredResourceTooltip()
    {
        if (_hoveredResourceNode == null || !_hoveredResourceNode.CanHarvest) return;

        // Lấy chiều cao thực tế của tài nguyên
        float height = GetCachedHeight(_hoveredResourceNode.gameObject, 2.0f);

        Vector3 worldPos = _hoveredResourceNode.transform.position + Vector3.up * (height + 0.5f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        string resourceIcon = "";
        switch (_hoveredResourceNode.ResourceType)
        {
            case ResourceType.Wood: resourceIcon = "🪵 Gỗ"; break;
            case ResourceType.Stone: resourceIcon = "🪨 Đá"; break;
            case ResourceType.Food: resourceIcon = "🌾 Lương thực"; break;
            case ResourceType.Gold: resourceIcon = "🪙 Vàng"; break;
            default: resourceIcon = "📦 Vật phẩm"; break;
        }

        string text = $"{resourceIcon}: {_hoveredResourceNode.CurrentQuantity}";
        _tempContent.text = text;

        Vector2 size = _resourceTooltipStyle.CalcSize(_tempContent);
        float paddingX = 8f;
        float paddingY = 4f;
        float rectWidth = size.x + paddingX * 2;
        float rectHeight = size.y + paddingY * 2;

        Rect badgeRect = new Rect(guiX - rectWidth / 2f, guiY - rectHeight / 2f, rectWidth, rectHeight);

        Color originalColor = GUI.color;

        // Nền kính mờ tối
        GUI.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);
        GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);

        // Viền màu xanh biển nhẹ nhàng
        Rect borderRect = new Rect(badgeRect.x - 1, badgeRect.y - 1, badgeRect.width + 2, badgeRect.height + 2);
        GUI.color = new Color(0.3f, 0.9f, 1.0f, 0.6f);
        
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        Rect textRect = new Rect(badgeRect.x + paddingX, badgeRect.y + paddingY, size.x, size.y);
        GUI.Label(textRect, _tempContent, _resourceTooltipStyle);

        GUI.color = originalColor;
    }

    /// <summary>
    /// Vẽ nhãn đói cho cư dân bị thiếu lương thực.
    /// </summary>
    private void DrawHungryVillagersBadge()
    {
        if (VillagerController.AllVillagers == null) return;

        Color originalColor = GUI.color;

        for (int i = 0; i < VillagerController.AllVillagers.Count; i++)
        {
            var villager = VillagerController.AllVillagers[i];
            if (villager == null || !villager.gameObject.activeInHierarchy || villager.CurrentState == VillagerState.Sheltered)
                continue;

            if (villager.IsHungry)
            {
                DrawHungryBadge(villager);
            }
        }

        GUI.color = originalColor;
    }

    private void DrawHungryBadge(VillagerController villager)
    {
        float height = GetCachedHeight(villager.gameObject, 2.0f);

        // Đặt ở trên đầu villager, cao hơn thanh máu.
        Vector3 worldPos = villager.transform.position + Vector3.up * (height + 0.65f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        float guiX = screenPos.x;
        float guiY = Screen.height - screenPos.y;

        string text = "🍽️ Đói";
        _tempContent.text = text;

        Vector2 size = _hungerTextStyle.CalcSize(_tempContent);
        float paddingX = 6f;
        float paddingY = 2f;
        float rectWidth = size.x + paddingX * 2;
        float rectHeight = size.y + paddingY * 2;

        Rect badgeRect = new Rect(guiX - rectWidth / 2f, guiY - rectHeight / 2f, rectWidth, rectHeight);

        // Nền tối
        GUI.color = new Color(0.15f, 0.05f, 0.05f, 0.9f);
        GUI.DrawTexture(badgeRect, Texture2D.whiteTexture);

        // Viền đỏ
        Rect borderRect = new Rect(badgeRect.x - 1, badgeRect.y - 1, badgeRect.width + 2, badgeRect.height + 2);
        GUI.color = new Color(1.0f, 0.3f, 0.3f, 0.7f);
        
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.yMax - 1, borderRect.width, 1), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.x, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(borderRect.xMax - 1, borderRect.y, 1, borderRect.height), Texture2D.whiteTexture);

        GUI.color = Color.white;
        Rect textRect = new Rect(badgeRect.x + paddingX, badgeRect.y + paddingY, size.x, size.y);
        GUI.Label(textRect, _tempContent, _hungerTextStyle);
    }
}
