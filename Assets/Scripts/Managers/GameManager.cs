using System;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private static GameManager s_instance;
    public static GameManager Instance => s_instance;

    private static Transform _villagersContainer;
    public static Transform VillagersContainer
    {
        get
        {
            if (_villagersContainer == null)
            {
                GameObject container = GameObject.Find("Villagers");
                if (container == null)
                {
                    container = new GameObject("Villagers");
                }
                _villagersContainer = container.transform;
            }
            return _villagersContainer;
        }
    }

    private static Transform _combatUnitsContainer;
    public static Transform CombatUnitsContainer
    {
        get
        {
            if (_combatUnitsContainer == null)
            {
                GameObject container = GameObject.Find("CombatUnits");
                if (container == null)
                {
                    container = new GameObject("CombatUnits");
                }
                _combatUnitsContainer = container.transform;
            }
            return _combatUnitsContainer;
        }
    }

    private GridSystem _gridSystem;
    private BuildingManager _buildingManager;

    [Header("Starting Entities")]
    [UnityEngine.Serialization.FormerlySerializedAs("villagerPrefab")]
    [SerializeField] private GameObject _villagerPrefab;
    
    [UnityEngine.Serialization.FormerlySerializedAs("startingVillagers")]
    [SerializeField] private int _startingVillagers = 3;
    
    [UnityEngine.Serialization.FormerlySerializedAs("flatAreaRadius")]
    [SerializeField] private int _flatAreaRadius = 5;

    [Header("Market Settings")]
    [SerializeField] private BuildingData _neutralMarketData;

    [Header("Cursor Settings")]
    [SerializeField] private Texture2D _customCursorTexture;
    [SerializeField] private Vector2 _cursorHotspot = Vector2.zero;

    [Header("Wildlife Settings")]
    [SerializeField] private GameObject _chickenPrefab;
    [SerializeField] private GameObject _deerPrefab;
    [SerializeField] private GameObject _foodPrefab;

    [Header("Ancient Ruins Settings")]
    [SerializeField] private GameObject[] _ancientRuinsPrefabs;

    [Header("Map Event Settings")]
    [SerializeField] private GameObject _rewardKnightPrefab;
    [SerializeField] private GameObject _rewardArcherPrefab;
    [SerializeField] private GameObject _voidPortalPrefab;
    [SerializeField] private GameObject _merchantCaravanPrefab;

    public GameObject RewardKnightPrefab => _rewardKnightPrefab;
    public GameObject RewardArcherPrefab => _rewardArcherPrefab;
    public GameObject VoidPortalPrefab => _voidPortalPrefab;
    public GameObject MerchantCaravanPrefab => _merchantCaravanPrefab;

    public enum GameState { Playing, Victory, Defeat }

    [Header("Game State Settings")]
    [SerializeField] private int _targetSurvivalDays = 15;

    public GameState CurrentState { get; private set; } = GameState.Playing;

    void Awake()
    {
        if (s_instance == null)
        {
            s_instance = this; 
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        } 

        _gridSystem = FindAnyObjectByType<GridSystem>();
        _buildingManager = FindAnyObjectByType<BuildingManager>();

        // Dynamically setup WildlifeManager
        WildlifeManager wildlife = gameObject.GetComponent<WildlifeManager>();
        if (wildlife == null)
        {
            wildlife = gameObject.AddComponent<WildlifeManager>();
        }
        wildlife.SetPrefabs(_chickenPrefab, _deerPrefab, _foodPrefab);

        // Dynamically setup CursorManager
        CursorManager cursorManager = gameObject.GetComponent<CursorManager>();
        if (cursorManager == null)
        {
            cursorManager = gameObject.AddComponent<CursorManager>();
        }

        // Dynamically setup AncientRuinsSpawner
        AncientRuinsSpawner ruinsSpawner = gameObject.GetComponent<AncientRuinsSpawner>();
        if (ruinsSpawner == null)
        {
            ruinsSpawner = gameObject.AddComponent<AncientRuinsSpawner>();
        }
        ruinsSpawner.SetPrefabs(_ancientRuinsPrefabs);

        // Dynamically setup MapEventManager
        MapEventManager eventManager = gameObject.GetComponent<MapEventManager>();
        if (eventManager == null)
        {
            eventManager = gameObject.AddComponent<MapEventManager>();
        }
        eventManager.SetupPrefabs(_voidPortalPrefab, _merchantCaravanPrefab);
    }

    void Start()
    {
        InitGame();
        SetupCursor();

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
        }
    }

    void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
        }
    }

    private void HandleDayChanged(int newDay)
    {
        CheckWinCondition();
    }

    public void CheckWinCondition()
    {
        if (CurrentState != GameState.Playing) return;

        int currentDay = TimeManager.Instance != null ? TimeManager.Instance.dayCount : 1;
        if (currentDay < _targetSurvivalDays + 1)
        {
            return;
        }

        VoidPortal[] portals = FindObjectsByType<VoidPortal>(FindObjectsInactive.Include);
        int activePortals = 0;
        foreach (var portal in portals)
        {
            if (portal != null && portal.currentState != CombatState.Dead)
            {
                activePortals++;
            }
        }

        if (activePortals == 0)
        {
            TriggerVictory();
        }
    }

    public void TriggerVictory()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Victory;
        Debug.Log("[GAME OVER] CHIEN THANG! Ban da tieu diet sach cong hu vo va song sot qua 15 ngay!");

        Time.timeScale = 0f;

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowVictoryScreen();
        }
    }

    public void TriggerDefeat()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState = GameState.Defeat;
        Debug.Log("[GAME OVER] THAT BAI! Nha chinh cua ban da bi tieu diet!");

        Time.timeScale = 0f;

        if (HUDManager.Instance != null)
        {
            HUDManager.Instance.ShowDefeatScreen();
        }
    }

    private void SetupCursor()
    {
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.SetCursorType(CursorType.Default);
        }
        else if (_customCursorTexture != null)
        {
            Cursor.SetCursor(_customCursorTexture, _cursorHotspot, CursorMode.Auto);
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            if (Cursor.lockState != CursorLockMode.Confined)
            {
                Cursor.lockState = CursorLockMode.Confined;
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_customCursorTexture == null)
        {
            _customCursorTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdAssets/StoneCursorWenrexa/PNG/01.png");
            if (_customCursorTexture != null)
            {
                Debug.Log("[GameManager] Tu dong gan texture chuot tuy chinh tu StoneCursorWenrexa.");
            }
        }

        if (_chickenPrefab == null)
        {
            _chickenPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Resource/Chicken_001.prefab");
        }
        if (_deerPrefab == null)
        {
            _deerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Resource/Deer_001.prefab");
        }
        if (_foodPrefab == null)
        {
            _foodPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ResourceCaple/Food.prefab");
        }

        if (_ancientRuinsPrefabs == null || _ancientRuinsPrefabs.Length == 0)
        {
            _ancientRuinsPrefabs = new GameObject[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/AncientRuins_Rock1.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/AncientRuins_Rock2.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/AncientRuins_Rock3.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/AncientRuins_Rock4.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/AncientRuins_Shell.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/RuinsSword.prefab")
            };
        }

        if (_rewardKnightPrefab == null)
        {
            _rewardKnightPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Player/Knight.prefab");
        }
        if (_rewardArcherPrefab == null)
        {
            _rewardArcherPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Unit/Player/PlayerArcher.prefab");
        }
        if (_voidPortalPrefab == null)
        {
            _voidPortalPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/VoidPortalPrefab.prefab");
        }
        if (_merchantCaravanPrefab == null)
        {
            _merchantCaravanPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Event/MerchantCaravan.prefab");
        }
    }
#endif

    public void InitGame()
    {
        if (_gridSystem == null || _buildingManager == null) return;

        int centerX = _gridSystem.GetWidth() / 2;
        int centerZ = _gridSystem.GetLength() / 2;

        // San phẳng địa hình xung quanh trước khi xây và sinh dân (bán kính 5 ô)
        GridCell centerCell = _gridSystem.GetCell(centerX, centerZ);
        if (centerCell != null)
        {
            _gridSystem.FlattenArea(centerX, centerZ, _flatAreaRadius, 0);
        }

        // Xây nhà chính
        _buildingManager.SpawnMainBuildingAt(centerX, centerZ);

        // Đặt camera vào giữa
        CameraControls camControl = FindAnyObjectByType<CameraControls>();
        if (camControl != null && centerCell != null)
        {
            Vector3 targetPos = _gridSystem.GetWorldPosition(centerX, centerZ, centerCell.elevation);
            camControl.transform.position = new Vector3(targetPos.x, camControl.transform.position.y, targetPos.z);
        }

        // Spawn dân làng
        SpawnVillagers(centerX, centerZ);

        // Spawn chợ trung lập ngẫu nhiên
        SpawnNeutralMarkets();

        // Spawn phế tích cổ ngẫu nhiên khởi tạo
        if (AncientRuinsSpawner.Instance != null)
        {
            AncientRuinsSpawner.Instance.SpawnInitialRuins();
        }
    }

    private void SpawnNeutralMarkets()
    {
        if (_gridSystem == null || _buildingManager == null || _neutralMarketData == null || _neutralMarketData.buildingPrefab == null)
        {
            Debug.LogWarning("[GameManager] Không thể sinh chợ trung lập: Thiếu dữ liệu hoặc prefab!");
            return;
        }

        int mapWidth = _gridSystem.GetWidth();
        int mapLength = _gridSystem.GetLength();
        int centerX = mapWidth / 2;
        int centerZ = mapLength / 2;

        // Khởi tạo bộ sinh số ngẫu nhiên deterministic dựa trên seed của GridSystem (salt = 88)
        System.Random prng = _gridSystem.CreateDeterministicRandom(88);

        int numMarkets = prng.Next(1, 3); // Sinh từ 1 đến 2 chợ trung lập
        int spawnedMarkets = 0;
        int maxAttempts = 150;

        Vector2Int size = _neutralMarketData.buildingSize;

        for (int attempt = 0; attempt < maxAttempts && spawnedMarkets < numMarkets; attempt++)
        {
            int startX = prng.Next(5, mapWidth - 5 - size.x);
            int startZ = prng.Next(5, mapLength - 5 - size.y);

            // Đo khoảng cách đến nhà chính (tâm map) xem có xa hơn 15 ô không
            float distToCenter = Vector2.Distance(new Vector2(startX + size.x / 2f, startZ + size.y / 2f), new Vector2(centerX, centerZ));
            if (distToCenter < 15f)
            {
                continue;
            }

            bool canPlace = true;
            int targetElevation = -1;
            List<GridCell> cellsToOccupy = new List<GridCell>();

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.y; z++)
                {
                    GridCell cell = _gridSystem.GetCell(startX + x, startZ + z);
                    if (cell == null || !cell.isBuildable || cell.hasResource || cell.elevation < 0)
                    {
                        canPlace = false;
                        break;
                    }

                    if (targetElevation == -1)
                    {
                        targetElevation = cell.elevation;
                    }
                    cellsToOccupy.Add(cell);
                }
                if (!canPlace) break;
            }

            if (canPlace)
            {
                // Ủi phẳng địa hình khu vực chợ trung lập
                _gridSystem.FlattenRectArea(startX, startZ, size.x, size.y);

                float cellSize = _gridSystem.GetCellSize();
                Vector3 startPos = _gridSystem.GetWorldPosition(startX, startZ, targetElevation);
                float offsetX = (size.x - 1) * cellSize / 2f;
                float offsetZ = (size.y - 1) * cellSize / 2f;
                Vector3 centerPos = startPos + new Vector3(offsetX, 0f, offsetZ);

                // Sinh GameObject chợ trung lập
                GameObject marketObj = Instantiate(_neutralMarketData.buildingPrefab, centerPos, Quaternion.identity);
                marketObj.name = $"MarketNeutral_{spawnedMarkets + 1}";

                // Đăng ký với BuildingManager
                _buildingManager.RegisterSpawnedBuilding(marketObj, _neutralMarketData, startX, startZ);

                spawnedMarkets++;
                Debug.Log($"[GameManager] Đã tự động sinh chợ trung lập {marketObj.name} tại [{startX}, {startZ}]");
            }
        }
    }


    private void SpawnVillagers(int centerX, int centerZ)
    {
        if (_villagerPrefab == null)
        {
            Debug.LogWarning("Chưa gắn prefab Villager trong GameManager!");
            return;
        }

        int spawnedCount = 0;
        int radius = 5; 

        for (int x = centerX - radius; x <= centerX + radius && spawnedCount < _startingVillagers; x++)
        {
            for (int z = centerZ - radius; z <= centerZ + radius && spawnedCount < _startingVillagers; z++)
            {
                // Bỏ qua nếu quá gần nhà chính
                if (Mathf.Abs(x - centerX) <= 1 && Mathf.Abs(z - centerZ) <= 1) continue;

                GridCell cell = _gridSystem.GetCell(x, z);
                // Cần đảm bảo ô đó đi được và không có vật cản
                if (cell != null && cell.isWalkable && !cell.hasResource) 
                {
                    Vector3 spawnPos = _gridSystem.GetWorldPosition(x, z);
                    // Dân làng có Pivot ở dưới chân nên không cần nâng Y
                    
                    GameObject villagerObj = Instantiate(_villagerPrefab, spawnPos, Quaternion.identity);
                    if (villagerObj != null)
                    {
                        villagerObj.transform.SetParent(VillagersContainer);
                    }
                    spawnedCount++;
                }
            }
        }
    }

    public void AddVillager(Vector3 position)
    {
        if (_villagerPrefab != null)
        {
            GameObject villagerObj = Instantiate(_villagerPrefab, position, Quaternion.identity);
            if (villagerObj != null)
            {
                villagerObj.transform.SetParent(VillagersContainer);
            }
        }
        else
        {
            Debug.LogWarning("[GameManager] Villager prefab is null!");
        }
    }

    public void RestartGame()
    {
        Debug.Log("[GameManager] Dang khoi dong lai game, dang don dep cac doi tuong DontDestroyOnLoad...");

        // Reset timescale to prevent frozen game on reload
        Time.timeScale = 1f;

        // Tao mot do tuong tam thoi de lay reference den DontDestroyOnLoad scene
        GameObject tempObj = new GameObject();
        DontDestroyOnLoad(tempObj);
        UnityEngine.SceneManagement.Scene dontDestroyScene = tempObj.scene;
        Destroy(tempObj);

        // Huy tat ca cac root objects trong DontDestroyOnLoad scene de reset hoan toan cac singleton
        GameObject[] rootObjects = dontDestroyScene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            if (rootObjects[i] != null)
            {
                Destroy(rootObjects[i]);
            }
        }

        // Load lai scene hien tai
        string activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        UnityEngine.SceneManagement.SceneManager.LoadScene(activeSceneName);
    }
}
