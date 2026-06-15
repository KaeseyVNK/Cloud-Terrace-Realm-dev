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

    [Header("Cursor Settings")]
    [SerializeField] private Texture2D _customCursorTexture;
    [SerializeField] private Vector2 _cursorHotspot = Vector2.zero;

    [Header("Wildlife Settings")]
    [SerializeField] private GameObject _chickenPrefab;
    [SerializeField] private GameObject _deerPrefab;
    [SerializeField] private GameObject _foodPrefab;

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
    }

    void Start()
    {
        InitGame();
        SetupCursor();
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
}
