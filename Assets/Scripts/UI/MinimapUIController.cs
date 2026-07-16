using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller nâng cấp toàn diện hệ thống Minimap.
/// Hỗ trợ Fog of War, Icon theo loại, Camera Frustum chuẩn, Ping Alert nhấp nháy,
/// Selection Outline, Filter hiển thị, và Right-click để di chuyển lính.
/// </summary>
public class MinimapUIController : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public static MinimapUIController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private RectTransform _mapRect;
    [SerializeField] private RectTransform _markerRoot;
    [SerializeField] private CameraControls _cameraControls;
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Camera _minimapCamera;

    [Header("Fog of War")]
    [SerializeField] private RawImage _fogOverlay;

    [Header("Icons/Sprites (Kéo thả trong Inspector)")]
    [SerializeField] private Sprite _townHallSprite;
    [SerializeField] private Sprite _barrackSprite;
    [SerializeField] private Sprite _blacksmithSprite;
    [SerializeField] private Sprite _marketSprite;
    [SerializeField] private Sprite _watchTowerSprite;
    [SerializeField] private Sprite _defaultBuildingSprite;
    [SerializeField] private Sprite _soldierSprite;
    [SerializeField] private Sprite _villagerSprite;
    [SerializeField] private Sprite _portalSprite;
    [SerializeField] private Sprite _resourceSprite;
    [SerializeField] private Sprite _selectionRingSprite;
    [SerializeField] private Sprite _pingCircleSprite;

    [Header("Map Bounds")]
    [SerializeField] private bool _useActiveTerrainBounds = true;
    [SerializeField] private Vector2 _worldMin = new Vector2(-100f, -100f);
    [SerializeField] private Vector2 _worldMax = new Vector2(100f, 100f);

    [Header("Update")]
    [SerializeField] private float _markerRefreshInterval = 0.15f;

    [Header("Marker Style")]
    [SerializeField] private Color _playerUnitColor = new Color32(70, 210, 255, 255);
    [SerializeField] private Color _playerBuildingColor = new Color32(75, 230, 120, 255);
    [SerializeField] private Color _enemyColor = new Color32(240, 70, 70, 255);
    [SerializeField] private Color _neutralColor = new Color32(235, 210, 90, 255);
    [SerializeField] private Color _portalColor = new Color32(190, 85, 255, 255);
    [SerializeField] private Color _resourceColor = new Color32(245, 205, 90, 255);

    // --- Bộ lọc hiển thị (Filters) ---
    private bool _showResources = false;
    private bool _showEnemies = true;
    private bool _showBuildings = true;
    private bool _showPlayerUnits = true;

    // --- Cấu trúc dữ liệu Ping ---
    private class MinimapPing
    {
        public Vector3 worldPosition;
        public float startTime;
        public float duration;
        public Color color;
        public Image pingMarker;
    }

    private readonly List<MinimapPing> _activePings = new List<MinimapPing>();
    private readonly List<Image> _markerPool = new List<Image>();
    private readonly HashSet<Transform> _drawnTargets = new HashSet<Transform>();
    private float _nextMarkerRefreshTime;
    private bool _fogBoundsRefreshed;

    // --- Camera Frustum lines (4 cạnh khung nhìn) ---
    private RectTransform _frustumRoot;
    private Image _lineBottom;
    private Image _lineLeft;
    private Image _lineTop;
    private Image _lineRight;

    private void Awake()
    {
        Instance = this;
        AutoBindReferences();
        RefreshMapBounds();
        SetupFogOverlay();
        SetupCameraFrustumFrame();
    }

    private void OnEnable()
    {
        RefreshMarkers();
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextMarkerRefreshTime)
        {
            RefreshMarkers();
            _nextMarkerRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, _markerRefreshInterval);
        }

        UpdateCameraFrustum();
        UpdatePings();
        UpdateFogOverlayTexture();
    }

    // --- MOUSE CLICK/DRAG TƯƠNG TÁC ---
    public void OnPointerDown(PointerEventData eventData)
    {
        HandlePointerInteraction(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        HandlePointerInteraction(eventData);
    }

    private void HandlePointerInteraction(PointerEventData eventData)
    {
        if (_mapRect == null) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _mapRect,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        if (!_mapRect.rect.Contains(localPoint))
        {
            return;
        }

        // Chuột trái hoặc chạm ngón tay trên mobile -> Di chuyển Camera
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            MoveCameraTargetToMinimapPoint(localPoint);
        }
        // Chuột phải trên PC -> Ra lệnh di chuyển cho lính
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            Vector3 targetWorldPos = MinimapToWorldPosition(localPoint);
            IssueMoveCommand(targetWorldPos);
        }
    }

    // --- ĐIỀU BINH QUA MINIMAP ---
    private void IssueMoveCommand(Vector3 targetWorldPos)
    {
        if (UnitSelectionManager.Instance == null || UnitSelectionManager.Instance.selectedUnits.Count == 0)
        {
            return;
        }

        // Phát âm thanh
        if (MyGame.Audio.AudioManager.Instance != null)
        {
            MyGame.Audio.AudioManager.Instance.PlayVillagerMove(targetWorldPos);
        }

        // Spawn Indicator trong thế giới 3D
        MyGame.UI.MoveIndicator.Spawn(targetWorldPos, Vector3.up, new Color(0.2f, 0.8f, 0.2f, 1.0f), 1.2f, 0.4f);

        int moveIndex = 0;
        foreach (var unit in UnitSelectionManager.Instance.selectedUnits)
        {
            if (unit == null) continue;

            var villager = unit.GetComponent<VillagerController>();
            var combatUnit = unit.GetComponent<BaseCombatUnitController>();

            if (villager != null)
            {
                // Gọi hàm GetFormationOffset đã chuyển thành public
                Vector3 offsetPos = targetWorldPos + UnitSelectionManager.Instance.GetFormationOffset(moveIndex, 1.2f);
                if (Terrain.activeTerrain != null)
                {
                    offsetPos.y = Terrain.activeTerrain.SampleHeight(offsetPos) + Terrain.activeTerrain.transform.position.y;
                }
                villager.CommandMoveTo(offsetPos);
                moveIndex++;
            }
            else if (combatUnit != null)
            {
                Vector3 offsetPos = targetWorldPos + UnitSelectionManager.Instance.GetFormationOffset(moveIndex, 1.5f);
                if (Terrain.activeTerrain != null)
                {
                    offsetPos.y = Terrain.activeTerrain.SampleHeight(offsetPos) + Terrain.activeTerrain.transform.position.y;
                }
                combatUnit.CommandMove(offsetPos);
                moveIndex++;
            }
        }

        GameLog.Log($"[MinimapUI] Đã ra lệnh di chuyển cho {moveIndex} đơn vị tới {targetWorldPos}");
    }

    // --- PUBLIC FILTERS INTERFACE ---
    public void ToggleShowResources(bool show) { _showResources = show; RefreshMarkers(); }
    public void ToggleShowEnemies(bool show) { _showEnemies = show; RefreshMarkers(); }
    public void ToggleShowBuildings(bool show) { _showBuildings = show; RefreshMarkers(); }
    public void ToggleShowPlayerUnits(bool show) { _showPlayerUnits = show; RefreshMarkers(); }

    // --- PING SYSTEM (ALERT) ---
    /// <summary>
    /// Phát cảnh báo nhấp nháy tại một vị trí thế giới 3D trên Minimap.
    /// </summary>
    public void ShowPing(Vector3 worldPos, Color color, float duration = 2.0f)
    {
        // Lấy một Image marker tạm thời cho ping
        Image pingImg = GetMarker(_markerPool.Count);
        
        // Xóa khỏi pool thông thường để tránh đè lấn
        _markerPool.Remove(pingImg);

        pingImg.sprite = _pingCircleSprite;
        pingImg.color = color;
        pingImg.rectTransform.anchoredPosition = WorldToMinimapPosition(worldPos);
        pingImg.rectTransform.sizeDelta = new Vector2(16f, 16f); // Bắt đầu từ kích thước nhỏ
        pingImg.gameObject.SetActive(true);

        MinimapPing ping = new MinimapPing
        {
            worldPosition = worldPos,
            startTime = Time.time,
            duration = duration,
            color = color,
            pingMarker = pingImg
        };
        _activePings.Add(ping);
    }

    private void UpdatePings()
    {
        for (int i = _activePings.Count - 1; i >= 0; i--)
        {
            var ping = _activePings[i];
            float elapsed = Time.time - ping.startTime;
            if (elapsed >= ping.duration || ping.pingMarker == null)
            {
                if (ping.pingMarker != null)
                {
                    Destroy(ping.pingMarker.gameObject);
                }
                _activePings.RemoveAt(i);
                continue;
            }

            float progress = elapsed / ping.duration;
            // Vòng tròn ping loang to ra và mờ dần
            float currentScale = Mathf.Lerp(1.0f, 3.5f, progress);
            ping.pingMarker.transform.localScale = new Vector3(currentScale, currentScale, 1f);

            Color col = ping.color;
            col.a = Mathf.Lerp(1.0f, 0.0f, progress);
            ping.pingMarker.color = col;
        }
    }

    // --- SETUP HÌNH THANG CAMERA VIEWPORT (FRUSTUM) ---
    private void SetupCameraFrustumFrame()
    {
        var rootGo = new GameObject("FrustumFrameRoot", typeof(RectTransform));
        rootGo.transform.SetParent(_mapRect, false);
        _frustumRoot = rootGo.GetComponent<RectTransform>();
        _frustumRoot.anchorMin = Vector2.zero;
        _frustumRoot.anchorMax = Vector2.one;
        _frustumRoot.sizeDelta = Vector2.zero;

        _lineBottom = CreateLineObject("LineBottom");
        _lineLeft = CreateLineObject("LineLeft");
        _lineTop = CreateLineObject("LineTop");
        _lineRight = CreateLineObject("LineRight");
    }

    private Image CreateLineObject(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(_frustumRoot, false);
        var img = go.GetComponent<Image>();
        img.color = Color.yellow;
        img.raycastTarget = false;

        var rect = img.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    private void UpdateCameraFrustum()
    {
        if (_mainCamera == null || _frustumRoot == null || !_frustumRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        // Bắn 4 góc Viewport xuống mặt phẳng Terrain
        Vector3 pBottomLeft = GetViewportWorldPoint(new Vector3(0, 0, 0));
        Vector3 pTopLeft = GetViewportWorldPoint(new Vector3(0, 1, 0));
        Vector3 pTopRight = GetViewportWorldPoint(new Vector3(1, 1, 0));
        Vector3 pBottomRight = GetViewportWorldPoint(new Vector3(1, 0, 0));

        // Chuyển đổi sang Minimap UI
        Vector2 m0 = WorldToMinimapPosition(pBottomLeft);
        Vector2 m1 = WorldToMinimapPosition(pTopLeft);
        Vector2 m2 = WorldToMinimapPosition(pTopRight);
        Vector2 m3 = WorldToMinimapPosition(pBottomRight);

        // Vẽ 4 cạnh nối các góc
        DrawFrustumLine(_lineBottom, m0, m3);
        DrawFrustumLine(_lineLeft, m0, m1);
        DrawFrustumLine(_lineTop, m1, m2);
        DrawFrustumLine(_lineRight, m2, m3);
    }

    private Vector3 GetViewportWorldPoint(Vector3 viewportPoint)
    {
        Ray ray = _mainCamera.ViewportPointToRay(viewportPoint);
        float terrainHeight = Terrain.activeTerrain != null ? Terrain.activeTerrain.transform.position.y : 0f;
        Plane groundPlane = new Plane(Vector3.up, new Vector3(0, terrainHeight, 0));

        if (groundPlane.Raycast(ray, out float enter))
        {
            return ray.GetPoint(enter);
        }
        return ray.GetPoint(100f);
    }

    private void DrawFrustumLine(Image line, Vector2 start, Vector2 end)
    {
        Vector2 diff = end - start;
        line.rectTransform.anchoredPosition = start + diff * 0.5f;
        line.rectTransform.sizeDelta = new Vector2(diff.magnitude, 2.5f); // Chiều rộng 2.5px
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;
        line.rectTransform.localEulerAngles = new Vector3(0, 0, angle);
    }

    // --- SETUP FOG OF WAR OVERLAY ---
    private void SetupFogOverlay()
    {
        if (_fogOverlay == null)
        {
            var fogGo = new GameObject("FogOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            fogGo.transform.SetParent(_markerRoot.parent, false);
            
            // Đặt ở vị trí đầu tiên để vẽ dưới Markers nhưng trên bản đồ nền
            fogGo.transform.SetAsFirstSibling(); 
            
            _fogOverlay = fogGo.GetComponent<RawImage>();
            _fogOverlay.color = new Color(0.05f, 0.05f, 0.1f, 0.82f); // Màu tối phủ sương mù
            
            var r = _fogOverlay.rectTransform;
            r.anchorMin = Vector2.zero; // Stretch
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            // Lật ngược trục Y vì RawImage Render Texture của AOS bị lật ngược tọa độ UV
            r.localScale = new Vector3(1f, -1f, 1f); 
        }
    }

    private void UpdateFogOverlayTexture()
    {
        if (_fogOverlay != null && AOSFogOfWarBridge.Instance != null)
        {
            var fogWar = AOSFogOfWarBridge.Instance.GetComponent<FischlWorks_FogWar.csFogWar>();
            if (fogWar != null && fogWar.FogPlaneTextureLerpBuffer != null)
            {
                _fogOverlay.texture = fogWar.FogPlaneTextureLerpBuffer;
            }
        }
    }

    // --- RENDER MARKERS (ICONS & SELECTION HIGHLIGHT) ---
    private void RefreshMarkers()
    {
        if (_mapRect == null || _markerRoot == null)
        {
            return;
        }

        RefreshMapBounds();
        _drawnTargets.Clear();

        int usedMarkerCount = 0;
        DrawCombatMarkers(ref usedMarkerCount);
        
        if (_showBuildings)
        {
            DrawBuildingMarkers(ref usedMarkerCount);
        }
        if (_showResources)
        {
            DrawResourceMarkers(ref usedMarkerCount);
        }

        // Hủy kích hoạt các marker thừa trong pool
        for (int i = usedMarkerCount; i < _markerPool.Count; i++)
        {
            _markerPool[i].gameObject.SetActive(false);
        }
    }

    private void DrawCombatMarkers(ref int usedMarkerCount)
    {
        for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
        {
            BaseCombatUnitController unit = BaseCombatUnitController.Registry[i];
            if (unit == null || unit.currentHealth <= 0 || unit.transform == null)
            {
                continue;
            }

            bool isPortal = unit is VoidPortal;
            bool isBuilding = unit.GetComponent<BuildingCombatTarget>() != null ||
                              unit.GetComponent<MainBuildingCombatTarget>() != null ||
                              unit.GetComponent<ConstructibleBuilding>() != null;

            // Lọc Faction Enemy
            if (unit.faction == UnitFaction.Enemy && !_showEnemies)
            {
                continue;
            }
            // Lọc Faction Player Unit
            if (unit.faction == UnitFaction.Player && !isBuilding && !_showPlayerUnits)
            {
                continue;
            }
            // Lọc Faction Player Building
            if (isBuilding && !_showBuildings)
            {
                continue;
            }

            // Không vẽ nếu nằm ngoài tầm sương mù của phe địch
            if (unit.faction != UnitFaction.Player && !IsVisibleOnMinimap(unit.gameObject))
            {
                continue;
            }

            // Phân loại Sprite Icon và Màu sắc
            Color color = GetCombatColor(unit, isPortal, isBuilding);
            Sprite sprite = GetCombatSprite(unit, isPortal, isBuilding);
            float size = isPortal ? 14f : isBuilding ? 12f : 7f;

            // Kiểm tra xem đối tượng có đang được chọn không (Selection Highlight)
            bool isSelected = IsTargetSelected(unit.gameObject, isBuilding);

            DrawMarker(unit.transform, color, size, sprite, isSelected, ref usedMarkerCount);
        }
    }

    private void DrawBuildingMarkers(ref int usedMarkerCount)
    {
        for (int i = 0; i < ConstructibleBuilding.Registry.Count; i++)
        {
            ConstructibleBuilding building = ConstructibleBuilding.Registry[i];
            if (building == null || building.transform == null)
            {
                continue;
            }

            if (_drawnTargets.Contains(building.transform))
            {
                continue;
            }

            // Kiểm tra xem có đang được chọn không
            bool isSelected = IsTargetSelected(building.gameObject, true);
            Sprite sprite = GetBuildingSprite(building.gameObject);

            DrawMarker(building.transform, _playerBuildingColor, 12f, sprite, isSelected, ref usedMarkerCount);
        }
    }

    private void DrawResourceMarkers(ref int usedMarkerCount)
    {
        for (int i = 0; i < ResourceNode.Registry.Count; i++)
        {
            ResourceNode resource = ResourceNode.Registry[i];
            if (resource == null || !resource.CanHarvest || resource.transform == null)
            {
                continue;
            }

            if (!IsVisibleOnMinimap(resource.gameObject))
            {
                continue;
            }

            DrawMarker(resource.transform, _resourceColor, 6f, _resourceSprite, false, ref usedMarkerCount);
        }
    }

    private bool IsTargetSelected(GameObject target, bool isBuilding)
    {
        if (isBuilding)
        {
            // Kiểm tra các bảng UI logic đang giữ tham chiếu công trình được chọn
            var mbUI = FindAnyObjectByType<MainBuildingUI>();
            if (mbUI != null && mbUI.SelectedMainBuilding != null && mbUI.SelectedMainBuilding.gameObject == target)
            {
                return true;
            }
            
            var production = target.GetComponent<BuildingProduction>();
            if (production == null) production = target.GetComponentInChildren<BuildingProduction>();
            if (production != null && TestProductionUI.Instance != null && TestProductionUI.Instance.SelectedProduction == production)
            {
                return true;
            }

            var blacksmith = target.GetComponent<BlacksmithResearch>();
            if (blacksmith == null) blacksmith = target.GetComponentInChildren<BlacksmithResearch>();
            if (blacksmith != null && TestProductionUI.Instance != null && TestProductionUI.Instance.SelectedResearch == blacksmith)
            {
                return true;
            }

            var market = target.GetComponent<MarketController>();
            if (market == null) market = target.GetComponentInChildren<MarketController>();
            var mUI = FindAnyObjectByType<MarketUI>();
            if (market != null && mUI != null && mUI.SelectedMarket == market)
            {
                return true;
            }
        }
        else
        {
            // Kiểm tra trong Unit Selection Manager (đối với Unit)
            if (UnitSelectionManager.Instance != null)
            {
                var selectable = target.GetComponent<SelectableUnit>();
                if (selectable != null && UnitSelectionManager.Instance.selectedUnits.Contains(selectable))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private Sprite GetCombatSprite(BaseCombatUnitController unit, bool isPortal, bool isBuilding)
    {
        if (isPortal) return _portalSprite;
        if (isBuilding) return GetBuildingSprite(unit.gameObject);
        
        // Player Unit
        if (unit.faction == UnitFaction.Player)
        {
            if (unit.GetComponent<VillagerController>() != null)
            {
                return _villagerSprite;
            }
            return _soldierSprite;
        }
        // Enemy Unit
        return _soldierSprite;
    }

    private Sprite GetBuildingSprite(GameObject go)
    {
        if (go.GetComponent<MainBuildingCombatTarget>() != null) return _townHallSprite;
        if (go.GetComponent<WatchTowerGarrison>() != null) return _watchTowerSprite;
        if (go.GetComponent<BuildingProduction>() != null || go.name.Contains("Barrack")) return _barrackSprite;
        if (go.GetComponent<BlacksmithResearch>() != null || go.name.Contains("blacksmith")) return _blacksmithSprite;
        if (go.GetComponent<MarketController>() != null) return _marketSprite;

        return _defaultBuildingSprite;
    }

    private Color GetCombatColor(BaseCombatUnitController unit, bool isPortal, bool isBuilding)
    {
        if (isPortal) return _portalColor;
        if (unit.faction == UnitFaction.Player) return isBuilding ? _playerBuildingColor : _playerUnitColor;
        if (unit.faction == UnitFaction.Enemy) return _enemyColor;
        return _neutralColor;
    }

    private void DrawMarker(Transform target, Color color, float size, Sprite sprite, bool isSelected, ref int usedMarkerCount)
    {
        if (target == null || _drawnTargets.Contains(target))
        {
            return;
        }

        // Nếu đang được chọn -> Vẽ vòng sáng màu trắng bên dưới
        if (isSelected && _selectionRingSprite != null)
        {
            Image ring = GetMarker(usedMarkerCount++);
            ring.sprite = _selectionRingSprite;
            ring.color = Color.white;
            ring.rectTransform.anchoredPosition = WorldToMinimapPosition(target.position);
            ring.rectTransform.sizeDelta = new Vector2(size * 1.6f, size * 1.6f);
            ring.gameObject.SetActive(true);
        }

        Image marker = GetMarker(usedMarkerCount++);
        marker.sprite = sprite;
        marker.color = color;
        marker.rectTransform.anchoredPosition = WorldToMinimapPosition(target.position);
        marker.rectTransform.sizeDelta = new Vector2(size, size);
        marker.rectTransform.localEulerAngles = Vector3.zero;
        marker.gameObject.SetActive(true);
        
        _drawnTargets.Add(target);
    }

    private Image GetMarker(int index)
    {
        while (_markerPool.Count <= index)
        {
            GameObject markerObject = new GameObject("Marker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            markerObject.layer = gameObject.layer;
            markerObject.transform.SetParent(_markerRoot, false);
            Image marker = markerObject.GetComponent<Image>();
            marker.raycastTarget = false;

            RectTransform rect = marker.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            _markerPool.Add(marker);
        }

        return _markerPool[index];
    }

    // --- CÁC HÀM TÍNH TOÁN TỌA ĐỘ BẢN ĐỒ ---
    private void AutoBindReferences()
    {
        if (_mapRect == null) _mapRect = transform as RectTransform;
        if (_markerRoot == null)
        {
            Transform markerRoot = transform.Find("MarkerRoot");
            _markerRoot = markerRoot != null ? markerRoot as RectTransform : _mapRect;
        }
        if (_mainCamera == null) _mainCamera = Camera.main;
        if (_cameraControls == null) _cameraControls = FindAnyObjectByType<CameraControls>();
        EnsureMinimapCamera();
    }

    private void RefreshMapBounds()
    {
        if (!_useActiveTerrainBounds || Terrain.activeTerrain == null)
        {
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        _worldMin = new Vector2(terrainPosition.x, terrainPosition.z);
        _worldMax = new Vector2(terrainPosition.x + terrainSize.x, terrainPosition.z + terrainSize.z);

        SyncMinimapCamera(terrainPosition, terrainSize);

        if (!_fogBoundsRefreshed && AOSFogOfWarBridge.Instance != null)
        {
            AOSFogOfWarBridge.Instance.RefreshFogBounds();
            _fogBoundsRefreshed = true;
        }
    }

    private void EnsureMinimapCamera()
    {
        if (_minimapCamera != null)
        {
            return;
        }

        GameObject minimapCameraObject = GameObject.Find("Minimap Camera");
        if (minimapCameraObject != null)
        {
            _minimapCamera = minimapCameraObject.GetComponent<Camera>();
        }
    }

    private void SyncMinimapCamera(Vector3 terrainPosition, Vector3 terrainSize)
    {
        EnsureMinimapCamera();
        if (_minimapCamera == null)
        {
            return;
        }

        float centerX = terrainPosition.x + terrainSize.x * 0.5f;
        float centerZ = terrainPosition.z + terrainSize.z * 0.5f;
        Vector3 cameraPosition = _minimapCamera.transform.position;
        _minimapCamera.transform.position = new Vector3(centerX, cameraPosition.y, centerZ);
        _minimapCamera.orthographicSize = terrainSize.z * 0.5f;
    }

    private Vector2 WorldToMapLocalPosition(Vector3 worldPosition)
    {
        if (_mapRect == null)
        {
            return Vector2.zero;
        }

        Rect rect = _mapRect.rect;
        float normalizedX = Mathf.InverseLerp(_worldMin.x, _worldMax.x, worldPosition.x);
        float normalizedY = Mathf.InverseLerp(_worldMin.y, _worldMax.y, worldPosition.z);

        return new Vector2(
            Mathf.Lerp(rect.xMin, rect.xMax, normalizedX),
            Mathf.Lerp(rect.yMin, rect.yMax, normalizedY));
    }

    private Vector2 WorldToMinimapPosition(Vector3 worldPosition)
    {
        Vector2 mapLocal = WorldToMapLocalPosition(worldPosition);
        if (_markerRoot != null && _mapRect != null && _markerRoot != _mapRect)
        {
            Vector3 worldPoint = _mapRect.TransformPoint(mapLocal);
            return _markerRoot.InverseTransformPoint(worldPoint);
        }

        return mapLocal;
    }

    private Vector3 MinimapToWorldPosition(Vector2 localPoint)
    {
        Rect rect = _mapRect.rect;
        float normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        Vector3 worldPosition = new Vector3(
            Mathf.Lerp(_worldMin.x, _worldMax.x, normalizedX),
            0f,
            Mathf.Lerp(_worldMin.y, _worldMax.y, normalizedY));

        if (Terrain.activeTerrain != null)
        {
            worldPosition.y = Terrain.activeTerrain.SampleHeight(worldPosition) + Terrain.activeTerrain.transform.position.y;
        }

        return worldPosition;
    }

    private void MoveCameraTargetToMinimapPoint(Vector2 localPoint)
    {
        if (_cameraControls == null) return;
        Vector3 worldPosition = MinimapToWorldPosition(localPoint);
        Transform cameraTarget = _cameraControls.transform;
        Vector3 currentPosition = cameraTarget.position;
        cameraTarget.position = new Vector3(worldPosition.x, currentPosition.y, worldPosition.z);
    }

    private bool IsVisibleOnMinimap(GameObject target)
    {
        FogVisibilityTarget visibilityTarget = target.GetComponentInParent<FogVisibilityTarget>();
        return visibilityTarget == null || visibilityTarget.IsVisible;
    }
}
