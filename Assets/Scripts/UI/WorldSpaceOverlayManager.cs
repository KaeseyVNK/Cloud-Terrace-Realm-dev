using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// Quản lý giao diện hiển thị thông tin thế giới (Thanh máu Unit/Công trình và số lượng dân/lính trong nhà/tháp canh).
/// Tự động khởi tạo và chạy không cần gán thủ công vào Scene (Plug and Play), đồng thời hỗ trợ kéo thả Prefabs tùy biến giao diện từ Inspector.
/// </summary>
public class WorldSpaceOverlayManager : MonoBehaviour
{
    // Cấu trúc quản lý đối tượng trong Pool thanh máu
    private class HealthBarElement
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Image backgroundImage;
        public Image fillImage;
        public TextMeshProUGUI hpText;
        public bool isUsed;

        public void SetActive(bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }

    // Cấu trúc quản lý đối tượng trong Pool Badge hiển thị số lượng/trạng thái
    private class BadgeElement
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Image backgroundImage;
        public TextMeshProUGUI text;
        public Outline outline;
        public bool isUsed;

        public void SetActive(bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }

    // Cấu trúc quản lý đối tượng Tooltip tài nguyên
    private class TooltipElement
    {
        public GameObject gameObject;
        public RectTransform rectTransform;
        public Image backgroundImage;
        public Image iconImage; // Image component hiển thị icon tài nguyên
        public TextMeshProUGUI text;
        public Outline outline;

        public void SetActive(bool active)
        {
            if (gameObject.activeSelf != active)
            {
                gameObject.SetActive(active);
            }
        }
    }

    [Header("UI Prefabs Tùy biến (Kéo thả từ Designer)")]
    [Tooltip("Prefab thanh máu. Cần có cấu trúc con tên 'Background', 'Fill' (Image) và component TextMeshProUGUI.")]
    [SerializeField] private GameObject _healthBarPrefab;

    [Tooltip("Prefab Badge đồn trú / đói ăn. Cần có component Image và TextMeshProUGUI con.")]
    [SerializeField] private GameObject _badgePrefab;

    [Tooltip("Prefab Tooltip tài nguyên. Cần có component Image con (tên là 'Icon' hoặc 'ResourceIcon') và TextMeshProUGUI con.")]
    [SerializeField] private GameObject _tooltipPrefab;

    [Header("Resource Sprites (Hình ảnh icon cho Tooltip)")]
    [SerializeField] private Sprite _woodSprite;
    [SerializeField] private Sprite _stoneSprite;
    [SerializeField] private Sprite _foodSprite;
    [SerializeField] private Sprite _goldSprite;

    private Camera _mainCamera;
    private float _nextRefreshTime = 0f;
    private ResourceNode _hoveredResourceNode;
    private BuildingMenuUI _buildingMenuUI;
    
    // Canvas hiển thị UGUI
    private Canvas _canvas;
    private RectTransform _canvasTransform;

    // Pools
    private readonly List<HealthBarElement> _healthBarPool = new List<HealthBarElement>();
    private int _activeHealthBarsCount = 0;

    private readonly List<BadgeElement> _badgePool = new List<BadgeElement>();
    private int _activeBadgesCount = 0;

    private TooltipElement _tooltipElement;

    // Cache các UI managers
    private MainBuildingUI _mainUI;
    private WatchTowerGarrisonUI _watchUI;
    private TestProductionUI _prodUI;

    // Cache chiều cao vật lý của đối tượng để tránh gọi GetComponent<Collider>() mỗi frame
    private readonly Dictionary<GameObject, float> _cachedHeights = new Dictionary<GameObject, float>();
    
    // Cache BaseCombatUnitController trên SelectableUnit để tránh GetComponent mỗi frame
    private readonly Dictionary<SelectableUnit, BaseCombatUnitController> _cachedCombatControllers = new Dictionary<SelectableUnit, BaseCombatUnitController>();

    private readonly List<GameObject> _keysToRemove = new List<GameObject>();
    private readonly List<SelectableUnit> _unitsToRemove = new List<SelectableUnit>();

    // Cache trạng thái lựa chọn công trình để so sánh tham chiếu cực nhanh
    private BaseCombatUnitController _selectedMainBuildingCombat;
    private BaseCombatUnitController _selectedWatchTowerCombat;
    private BaseCombatUnitController _selectedProductionCombat;
    private BaseCombatUnitController _selectedResearchCombat;
    private int _lastSelectionCacheFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Nếu Designer đã chủ động kéo WorldSpaceOverlayManager vào Scene, bỏ qua việc tự tạo mới
        if (FindAnyObjectByType<WorldSpaceOverlayManager>() != null)
        {
            Debug.Log("[WorldSpaceOverlayManager] Tìm thấy instance có sẵn trong Scene. Dùng cấu hình tùy biến của Designer.");
            return;
        }

        GameObject go = new GameObject("WorldSpaceOverlayManager");
        go.AddComponent<WorldSpaceOverlayManager>();
        DontDestroyOnLoad(go);
        Debug.Log("[WorldSpaceOverlayManager] Không thấy có sẵn trong Scene. Đã tự động tạo phiên bản mặc định.");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string sheetPath = "Assets/ThirdAssets/fantasy_ui_asset_sheet_transparent_clean.png";
        if (System.IO.File.Exists(sheetPath))
        {
            var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(sheetPath);
            foreach (var asset in subAssets)
            {
                if (asset is Sprite sprite)
                {
                    if (sprite.name == "fantasy_ui_asset_sheet_transparent_clean_2") _woodSprite = sprite;
                    else if (sprite.name == "fantasy_ui_asset_sheet_transparent_clean_4") _stoneSprite = sprite;
                    else if (sprite.name == "fantasy_ui_asset_sheet_transparent_clean_3") _foodSprite = sprite;
                    else if (sprite.name == "fantasy_ui_asset_sheet_transparent_clean_5") _goldSprite = sprite;
                }
            }
        }
    }
#endif

    private void Start()
    {
        _mainCamera = Camera.main;
        CreateOverlayCanvas();
        
        // Thử tìm nạp các Prefab tùy biến mặc định từ Resources nếu Designer chưa gán trực tiếp
        if (_healthBarPrefab == null) _healthBarPrefab = Resources.Load<GameObject>("UI/HealthBarPrefab");
        if (_badgePrefab == null) _badgePrefab = Resources.Load<GameObject>("UI/BadgePrefab");
        if (_tooltipPrefab == null) _tooltipPrefab = Resources.Load<GameObject>("UI/TooltipPrefab");

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
        // Bỏ qua nếu con trỏ đang đè lên phần tử UI (EventSystem) để tránh đè lấp hiển thị tooltip trong menu xây dựng
        if (IsPointerOverUI())
        {
            _hoveredResourceNode = null;
        }
        else if (_mainCamera != null && UnityEngine.InputSystem.Mouse.current != null)
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

    private void LateUpdate()
    {
        if (_mainCamera == null || _canvasTransform == null) return;

        ResetPools();
        CacheSelectionsIfNeeded();

        DrawSelectedUnitsHealthUGUI();
        DrawBuildingsHealthUGUI();
        DrawShelterOccupancyUGUI();
        DrawWatchTowerOccupancyUGUI();
        DrawHungryVillagersBadgeUGUI();
        DrawHoveredResourceTooltipUGUI();

        DeactivateUnusedPoolElements();
    }

    /// <summary>
    /// Tạo Canvas động để chứa các UI overlay.
    /// </summary>
    private void CreateOverlayCanvas()
    {
        GameObject canvasGO = new GameObject("WorldSpaceOverlayCanvas");
        _canvasTransform = canvasGO.AddComponent<RectTransform>();
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = -1; // Hiển thị phía dưới các UI Canvas chính để tránh đè lên UI canvas

        canvasGO.AddComponent<CanvasScaler>();
        DontDestroyOnLoad(canvasGO);
    }

    /// <summary>
    /// Lấy hoặc khởi tạo thanh máu từ Pool.
    /// </summary>
    private HealthBarElement GetHealthBarFromPool()
    {
        HealthBarElement element;
        if (_activeHealthBarsCount < _healthBarPool.Count)
        {
            element = _healthBarPool[_activeHealthBarsCount];
            element.isUsed = true;
            _activeHealthBarsCount++;
            element.SetActive(true);
        }
        else
        {
            element = CreateNewHealthBar();
            element.isUsed = true;
            _healthBarPool.Add(element);
            _activeHealthBarsCount++;
            element.SetActive(true);
        }

        // Tắt Outline mặc định
        var outline = element.gameObject.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;

        return element;
    }

    /// <summary>
    /// Lấy hoặc khởi tạo Badge từ Pool.
    /// </summary>
    private BadgeElement GetBadgeFromPool()
    {
        BadgeElement element;
        if (_activeBadgesCount < _badgePool.Count)
        {
            element = _badgePool[_activeBadgesCount];
            element.isUsed = true;
            _activeBadgesCount++;
            element.SetActive(true);
        }
        else
        {
            element = CreateNewBadge();
            element.isUsed = true;
            _badgePool.Add(element);
            _activeBadgesCount++;
            element.SetActive(true);
        }
        return element;
    }

    private void ResetPools()
    {
        _activeHealthBarsCount = 0;
        _activeBadgesCount = 0;
    }

    private void DeactivateUnusedPoolElements()
    {
        for (int i = _activeHealthBarsCount; i < _healthBarPool.Count; i++)
        {
            _healthBarPool[i].SetActive(false);
        }
        for (int i = _activeBadgesCount; i < _badgePool.Count; i++)
        {
            _badgePool[i].SetActive(false);
        }
    }

    /// <summary>
    /// Tạo mới GameObject thanh máu động (Từ Prefab hoặc Procedural).
    /// </summary>
    private HealthBarElement CreateNewHealthBar()
    {
        if (_healthBarPrefab != null)
        {
            GameObject go = Instantiate(_healthBarPrefab, _canvasTransform, false);
            
            // Tìm kiếm các component tương ứng
            Image[] images = go.GetComponentsInChildren<Image>(true);
            Image bgImg = null;
            Image fillImg = null;
            foreach (var img in images)
            {
                if (img.gameObject.name == "Background") bgImg = img;
                else if (img.gameObject.name == "Fill") fillImg = img;
            }
            if (bgImg == null && images.Length > 0) bgImg = images[0];
            if (fillImg == null && images.Length > 1) fillImg = images[1];

            TextMeshProUGUI hpText = go.GetComponentInChildren<TextMeshProUGUI>(true);

            go.SetActive(false);
            return new HealthBarElement
            {
                gameObject = go,
                rectTransform = go.GetComponent<RectTransform>(),
                backgroundImage = bgImg,
                fillImage = fillImg,
                hpText = hpText
            };
        }
        else
        {
            GameObject go = new GameObject("HealthBar", typeof(RectTransform));
            go.transform.SetParent(_canvasTransform, false);
            
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(80f, 10f);

            // Background Image
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            RectTransform bgRect = bgGo.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
            bgImg.raycastTarget = false;

            // Fill Area
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one; 
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.sizeDelta = Vector2.zero;
            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.color = new Color(0.2f, 0.9f, 0.4f);
            fillImg.raycastTarget = false;

            // HP Text
            GameObject textGo = new GameObject("HPText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.anchoredPosition = new Vector2(0f, 4f);
            textRect.sizeDelta = new Vector2(150f, 15f);
            TextMeshProUGUI hpText = textGo.GetComponent<TextMeshProUGUI>();
            hpText.fontSize = 10f;
            hpText.alignment = TextAlignmentOptions.Center;
            hpText.color = Color.white;
            hpText.fontStyle = FontStyles.Bold;
            hpText.raycastTarget = false;

            go.SetActive(false);

            return new HealthBarElement
            {
                gameObject = go,
                rectTransform = rect,
                backgroundImage = bgImg,
                fillImage = fillImg,
                hpText = hpText
            };
        }
    }

    /// <summary>
    /// Tạo mới GameObject Badge động (Từ Prefab hoặc Procedural).
    /// </summary>
    private BadgeElement CreateNewBadge()
    {
        if (_badgePrefab != null)
        {
            GameObject go = Instantiate(_badgePrefab, _canvasTransform, false);
            Image bgImg = go.GetComponent<Image>();
            if (bgImg == null) bgImg = go.GetComponentInChildren<Image>(true);
            TextMeshProUGUI badgeText = go.GetComponentInChildren<TextMeshProUGUI>(true);
            
            Outline outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.GetComponentInChildren<Outline>(true);

            go.SetActive(false);
            return new BadgeElement
            {
                gameObject = go,
                rectTransform = go.GetComponent<RectTransform>(),
                backgroundImage = bgImg,
                text = badgeText,
                outline = outline
            };
        }
        else
        {
            GameObject go = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvasTransform, false);
            
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(65f, 20f);

            Image bgImg = go.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);
            bgImg.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.75f, 1.0f, 0.6f);
            outline.effectDistance = new Vector2(1f, 1f);

            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            TextMeshProUGUI badgeText = textGo.GetComponent<TextMeshProUGUI>();
            badgeText.fontSize = 11f;
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.alignment = TextAlignmentOptions.Center;
            badgeText.color = Color.white;
            badgeText.raycastTarget = false;

            go.SetActive(false);

            return new BadgeElement
            {
                gameObject = go,
                rectTransform = rect,
                backgroundImage = bgImg,
                text = badgeText,
                outline = outline
            };
        }
    }

    /// <summary>
    /// Tạo mới GameObject Tooltip tài nguyên (Từ Prefab hoặc Procedural).
    /// </summary>
    private TooltipElement CreateNewTooltip()
    {
        if (_tooltipPrefab != null)
        {
            GameObject go = Instantiate(_tooltipPrefab, _canvasTransform, false);
            
            // Tìm Image icon hiển thị (tên là Icon hoặc ResourceIcon)
            Image iconImg = null;
            Image[] images = go.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject.name == "Icon" || img.gameObject.name == "ResourceIcon" || img.gameObject.name == "ResourceIconImage")
                {
                    iconImg = img;
                    break;
                }
            }
            // Nếu không tìm thấy bằng tên, lấy Image con đầu tiên không trùng với Root Background Image
            if (iconImg == null)
            {
                Image rootImg = go.GetComponent<Image>();
                foreach (var img in images)
                {
                    if (img != rootImg)
                    {
                        iconImg = img;
                        break;
                    }
                }
            }

            TextMeshProUGUI tooltipText = go.GetComponentInChildren<TextMeshProUGUI>(true);
            Outline outline = go.GetComponent<Outline>();
            if (outline == null) outline = go.GetComponentInChildren<Outline>(true);

            go.SetActive(false);
            return new TooltipElement
            {
                gameObject = go,
                rectTransform = go.GetComponent<RectTransform>(),
                backgroundImage = go.GetComponent<Image>(),
                iconImage = iconImg,
                text = tooltipText,
                outline = outline
            };
        }
        else
        {
            GameObject go = new GameObject("ResourceTooltip", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_canvasTransform, false);
            
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(100f, 22f);

            Image bgImg = go.GetComponent<Image>();
            bgImg.color = new Color(0.08f, 0.09f, 0.12f, 0.9f);
            bgImg.raycastTarget = false;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.3f, 0.9f, 1.0f, 0.6f);
            outline.effectDistance = new Vector2(1f, 1f);

            // Tạo Icon Image con ở bên trái
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            RectTransform iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(6f, 0f);
            iconRect.sizeDelta = new Vector2(14f, 14f);
            Image iconImg = iconGo.GetComponent<Image>();
            iconImg.raycastTarget = false;

            // Tạo Text con ở bên phải của Icon
            GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(24f, 0f); // Dịch lề trái sang phải 24px để chừa chỗ cho Icon
            
            TextMeshProUGUI tooltipText = textGo.GetComponent<TextMeshProUGUI>();
            tooltipText.fontSize = 11f;
            tooltipText.fontStyle = FontStyles.Bold;
            tooltipText.alignment = TextAlignmentOptions.Left; // Căn trái để nằm cạnh Icon
            tooltipText.color = Color.white;
            tooltipText.raycastTarget = false;

            return new TooltipElement
            {
                gameObject = go,
                rectTransform = rect,
                backgroundImage = bgImg,
                iconImage = iconImg,
                text = tooltipText,
                outline = outline
            };
        }
    }

    private void DrawSelectedUnitsHealthUGUI()
    {
        if (SelectableUnit.AllUnits == null) return;

        for (int i = 0; i < SelectableUnit.AllUnits.Count; i++)
        {
            var unit = SelectableUnit.AllUnits[i];
            if (unit == null || !unit.gameObject.activeInHierarchy || !unit.IsSelected)
                continue;

            var combat = GetCachedCombatController(unit);
            if (combat == null || combat.currentState == CombatState.Dead)
                continue;

            DrawUnitHealthBarUGUI(unit, combat);
        }
    }

    private void DrawUnitHealthBarUGUI(SelectableUnit unit, BaseCombatUnitController combat)
    {
        float height = GetCachedHeight(unit.gameObject, 2.0f);
        Vector3 worldPos = unit.transform.position + Vector3.up * (height + 0.25f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        var bar = GetHealthBarFromPool();
        bar.rectTransform.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
        
        if (_healthBarPrefab == null)
        {
            bar.rectTransform.sizeDelta = new Vector2(60f, 6f);
        }

        float healthPercent = Mathf.Clamp01((float)combat.currentHealth / combat.maxHealth);
        if (bar.fillImage != null)
        {
            bar.fillImage.rectTransform.anchorMax = new Vector2(healthPercent, 1f);

            Color healthColor = new Color(0.2f, 0.9f, 0.4f);
            if (healthPercent < 0.2f)
                healthColor = new Color(1.0f, 0.2f, 0.2f);
            else if (healthPercent < 0.5f)
                healthColor = new Color(1.0f, 0.6f, 0.0f);

            bar.fillImage.color = healthColor;
        }

        if (bar.hpText != null)
        {
            bar.hpText.text = $"{combat.currentHealth}/{combat.maxHealth}";
            if (_healthBarPrefab == null)
            {
                bar.hpText.fontSize = 9f;
                bar.hpText.rectTransform.anchoredPosition = new Vector2(0f, 4f);
            }
        }
    }

    private void DrawBuildingsHealthUGUI()
    {
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
                    DrawBuildingHealthBarUGUI(combat, isSelected);
                }
            }
        }
    }

    private void DrawBuildingHealthBarUGUI(BaseCombatUnitController combat, bool isSelected)
    {
        float height = GetCachedHeight(combat.gameObject, 3.5f);
        Vector3 worldPos = combat.transform.position + Vector3.up * (height + 0.25f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        var bar = GetHealthBarFromPool();
        bar.rectTransform.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
        
        if (_healthBarPrefab == null)
        {
            bar.rectTransform.sizeDelta = new Vector2(100f, 8f);
        }

        float healthPercent = Mathf.Clamp01((float)combat.currentHealth / combat.maxHealth);
        if (bar.fillImage != null)
        {
            bar.fillImage.rectTransform.anchorMax = new Vector2(healthPercent, 1f);

            Color healthColor = new Color(0.2f, 0.9f, 0.4f);
            if (healthPercent < 0.2f)
                healthColor = new Color(1.0f, 0.2f, 0.2f);
            else if (healthPercent < 0.5f)
                healthColor = new Color(1.0f, 0.6f, 0.0f);

            bar.fillImage.color = healthColor;
        }

        if (bar.hpText != null)
        {
            string nameText = string.IsNullOrEmpty(combat.unitName) ? "Building" : combat.unitName;
            bar.hpText.text = $"{nameText}: {combat.currentHealth}/{combat.maxHealth}";
            if (_healthBarPrefab == null)
            {
                bar.hpText.fontSize = 10f;
                bar.hpText.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            }
        }

        var outline = bar.gameObject.GetComponent<Outline>();
        if (outline != null)
        {
            outline.enabled = isSelected;
            if (isSelected)
            {
                outline.effectColor = new Color(0.25f, 0.75f, 1.0f, 0.95f);
                outline.effectDistance = new Vector2(1f, 1f);
            }
        }
    }

    private void DrawShelterOccupancyUGUI()
    {
        for (int i = 0; i < HouseShelter.Registry.Count; i++)
        {
            var shelter = HouseShelter.Registry[i];
            if (shelter == null || !shelter.gameObject.activeInHierarchy)
                continue;

            int occupants = shelter.OccupantCount;
            if (occupants <= 0)
                continue;

            DrawBadgeUGUI(
                shelter.transform.position,
                GetCachedHeight(shelter.gameObject, 3.5f) + 0.95f,
                $"🏠 {occupants} / {shelter.Capacity}",
                new Color(0.08f, 0.09f, 0.12f, 0.88f),
                new Color(0.25f, 0.75f, 1.0f, 0.6f)
            );
        }
    }

    private void DrawWatchTowerOccupancyUGUI()
    {
        for (int i = 0; i < WatchTowerGarrison.Registry.Count; i++)
        {
            var tower = WatchTowerGarrison.Registry[i];
            if (tower == null || !tower.gameObject.activeInHierarchy)
                continue;

            int occupants = tower.OccupantCount;
            if (occupants <= 0)
                continue;

            DrawBadgeUGUI(
                tower.transform.position,
                GetCachedHeight(tower.gameObject, 5.0f) + 0.95f,
                $"🏹 {occupants} / {tower.Capacity}",
                new Color(0.15f, 0.11f, 0.08f, 0.9f),
                new Color(1.0f, 0.7f, 0.1f, 0.7f)
            );
        }
    }

    private void DrawHungryVillagersBadgeUGUI()
    {
        if (VillagerController.AllVillagers == null) return;

        for (int i = 0; i < VillagerController.AllVillagers.Count; i++)
        {
            var villager = VillagerController.AllVillagers[i];
            if (villager == null || !villager.gameObject.activeInHierarchy || villager.CurrentState == VillagerState.Sheltered)
                continue;

            if (villager.IsHungry)
            {
                DrawBadgeUGUI(
                    villager.transform.position,
                    GetCachedHeight(villager.gameObject, 2.0f) + 0.65f,
                    "🍽️ Đói",
                    new Color(0.15f, 0.05f, 0.05f, 0.9f),
                    new Color(1.0f, 0.3f, 0.3f, 0.7f)
                );
            }
        }
    }

    private void DrawBadgeUGUI(Vector3 position, float heightOffset, string text, Color bgColor, Color borderColor)
    {
        Vector3 worldPos = position + Vector3.up * heightOffset;
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0) return;

        var badge = GetBadgeFromPool();
        badge.rectTransform.anchoredPosition = new Vector2(screenPos.x, screenPos.y);
        
        if (badge.backgroundImage != null && _badgePrefab == null)
        {
            badge.backgroundImage.color = bgColor;
        }

        if (badge.outline != null && _badgePrefab == null)
        {
            badge.outline.effectColor = borderColor;
        }

        if (badge.text != null)
        {
            badge.text.text = text;
        }

        if (_badgePrefab == null)
        {
            badge.rectTransform.sizeDelta = new Vector2(70f, 20f);
        }
    }

    private void DrawHoveredResourceTooltipUGUI()
    {
        if (_hoveredResourceNode == null || !_hoveredResourceNode.CanHarvest || _mainCamera == null || IsPointerOverUI())
        {
            if (_tooltipElement != null) _tooltipElement.SetActive(false);
            return;
        }

        float height = GetCachedHeight(_hoveredResourceNode.gameObject, 2.0f);
        Vector3 worldPos = _hoveredResourceNode.transform.position + Vector3.up * (height + 0.5f);
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0)
        {
            if (_tooltipElement != null) _tooltipElement.SetActive(false);
            return;
        }

        if (_tooltipElement == null)
        {
            _tooltipElement = CreateNewTooltip();
        }

        _tooltipElement.SetActive(true);
        _tooltipElement.rectTransform.anchoredPosition = new Vector2(screenPos.x, screenPos.y);

        Sprite resourceSprite = null;
        string resourceName = "";
        switch (_hoveredResourceNode.ResourceType)
        {
            case ResourceType.Wood: 
                resourceSprite = _woodSprite; 
                resourceName = "Gỗ";
                break;
            case ResourceType.Stone: 
                resourceSprite = _stoneSprite; 
                resourceName = "Đá";
                break;
            case ResourceType.Food: 
                resourceSprite = _foodSprite; 
                resourceName = "Lương thực";
                break;
            case ResourceType.Gold: 
                resourceSprite = _goldSprite; 
                resourceName = "Vàng";
                break;
        }

        // Cập nhật Sprite cho Image Icon
        if (_tooltipElement.iconImage != null)
        {
            _tooltipElement.iconImage.sprite = resourceSprite;
            _tooltipElement.iconImage.gameObject.SetActive(resourceSprite != null);
        }

        // Cập nhật text số lượng
        if (_tooltipElement.text != null)
        {
            if (resourceSprite != null)
            {
                // Nếu có hình ảnh biểu thị tài nguyên, chỉ hiển thị số lượng để tránh rối mắt
                _tooltipElement.text.text = $": {_hoveredResourceNode.CurrentQuantity}";
            }
            else
            {
                // Fallback nếu không có sprite hình ảnh: Hiển thị chữ kèm số lượng
                _tooltipElement.text.text = $"{resourceName}: {_hoveredResourceNode.CurrentQuantity}";
            }
        }

        if (_tooltipPrefab == null)
        {
            _tooltipElement.rectTransform.sizeDelta = new Vector2(100f, 22f);
        }
    }

    private bool IsPointerOverUI()
    {
        if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
        {
            return true;
        }

        if (_buildingMenuUI != null && _buildingMenuUI.gameObject.activeInHierarchy)
        {
            RectTransform menuRect = _buildingMenuUI.GetComponent<RectTransform>();
            if (menuRect != null && UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                if (RectTransformUtility.RectangleContainsScreenPoint(menuRect, mousePos, null))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void RefreshCachedReferences()
    {
        _mainUI = FindAnyObjectByType<MainBuildingUI>();
        _watchUI = FindAnyObjectByType<WatchTowerGarrisonUI>();
        _prodUI = FindAnyObjectByType<TestProductionUI>();
        _buildingMenuUI = FindAnyObjectByType<BuildingMenuUI>(FindObjectsInactive.Include);

        CleanDestroyedObjectsFromCache();
    }

    private void CleanDestroyedObjectsFromCache()
    {
        _keysToRemove.Clear();
        foreach (var key in _cachedHeights.Keys)
        {
            if (key == null) _keysToRemove.Add(key);
        }
        for (int i = 0; i < _keysToRemove.Count; i++)
        {
            _cachedHeights.Remove(_keysToRemove[i]);
        }

        _unitsToRemove.Clear();
        foreach (var key in _cachedCombatControllers.Keys)
        {
            if (key == null) _unitsToRemove.Add(key);
        }
        for (int i = 0; i < _unitsToRemove.Count; i++)
        {
            _cachedCombatControllers.Remove(_unitsToRemove[i]);
        }
    }

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

    private float GetCachedHeight(GameObject go, float defaultHeight)
    {
        if (go == null) return defaultHeight;

        if (_cachedHeights.TryGetValue(go, out float height))
        {
            return height;
        }

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

    private bool IsBuildingSelected(BaseCombatUnitController combat)
    {
        if (combat == null) return false;

        return combat == _selectedMainBuildingCombat ||
               combat == _selectedWatchTowerCombat ||
               combat == _selectedProductionCombat ||
               combat == _selectedResearchCombat;
    }
}
