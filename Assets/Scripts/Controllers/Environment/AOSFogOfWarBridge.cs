using System.Collections.Generic;
using System.Reflection;
using FischlWorks_FogWar;
using UnityEngine;

[RequireComponent(typeof(csFogWar))]
public class AOSFogOfWarBridge : MonoBehaviour
{
    public static AOSFogOfWarBridge Instance { get; private set; }

    public bool AutoAddVisibilityTargets => autoAddVisibilityTargets;
    public bool HideResourcesOutsideVision => hideResourcesOutsideVision;
    public bool HideEnemiesOutsideVision => hideEnemiesOutsideVision;
    public bool HideNonPlayerCombatTargetsOutsideVision => hideNonPlayerCombatTargetsOutsideVision;

    [Header("Activation")]
    [SerializeField] private bool onlyAtNight = false;
    [SerializeField] private bool alwaysNightForDebug = false;
    [SerializeField] private bool hideRuntimeFogPlaneDuringDay = false;

    [Header("Fog")]
    [SerializeField] private Material fogPlaneMaterial;
    [SerializeField] private Color fogColor = new Color32(0, 2, 8, 255);
    [SerializeField] private float fogPlaneAlpha = 0.75f;
    [SerializeField] private Color dayFogColor = new Color32(120, 145, 155, 130);
    [SerializeField] private float dayFogPlaneAlpha = 0.16f;
    [SerializeField] private float fogPlaneHeight = 0.5f;
    [SerializeField] private float fogRefreshRate = 4f;
    [SerializeField] private float fogLerpSpeed = 3f;

    [Header("Map")]
    [SerializeField] private bool autoUseActiveTerrainBounds = true;
    [SerializeField] private Vector2Int levelDimensions = new Vector2Int(100, 100);
    [Tooltip("World size of one AOS fog tile. Auto terrain mode may raise this value so the 128x128 AOS limit still covers the whole terrain.")]
    [SerializeField] private float unitScale = 2f;
    [SerializeField] private bool autoFitUnitScaleToTerrain = true;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("Default Vision")]
    [Tooltip("Default villager sight radius in world units.")]
    [SerializeField] private float villagerVisionRadius = 10f;
    [Tooltip("Default combat unit sight radius in world units.")]
    [SerializeField] private float combatUnitVisionRadius = 14f;
    [Tooltip("Default building sight radius in world units.")]
    [SerializeField] private float buildingVisionRadius = 16f;
    [Tooltip("Default main building sight radius in world units.")]
    [SerializeField] private float mainBuildingVisionRadius = 24f;
    [SerializeField] private float revealerRefreshInterval = 1f;

    [Header("Visibility Targets")]
    [SerializeField] private bool autoAddVisibilityTargets = true;
    [SerializeField] private bool hideEnemiesOutsideVision = true;
    [SerializeField] private bool hideResourcesOutsideVision = true;
    [SerializeField] private bool hideNonPlayerCombatTargetsOutsideVision = true;
    [SerializeField] private bool maskTerrainDetailsOutsideVision = false;

    private csFogWar fogWar;
    private float revealerRefreshTimer;
    private GameObject runtimeFogPlane;
    private readonly HashSet<Object> revealedObjects = new HashSet<Object>();
    private readonly Dictionary<string, FieldInfo> _cachedFields = new Dictionary<string, FieldInfo>();

    private void Awake()
    {
        Instance = this;
        fogWar = GetComponent<csFogWar>();
        ConfigureFogWar();
    }

    private void Start()
    {
        RefreshRevealers();
        EnsureVisibilityTargets();
        ApplyNightState();
    }

    private void Update()
    {
        ApplyNightState();

        if (fogWar == null || !fogWar.enabled)
        {
            return;
        }

        revealerRefreshTimer += Time.deltaTime;
        if (revealerRefreshTimer < revealerRefreshInterval)
        {
            return;
        }

        revealerRefreshTimer = 0f;
        RefreshRevealers();
        EnsureVisibilityTargets();
    }

    private void ConfigureFogWar()
    {
        if (autoUseActiveTerrainBounds && Terrain.activeTerrain != null)
        {
            Terrain terrain = Terrain.activeTerrain;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            transform.position = new Vector3(
                terrainPosition.x + terrainSize.x * 0.5f,
                terrainPosition.y,
                terrainPosition.z + terrainSize.z * 0.5f);

            if (autoFitUnitScaleToTerrain)
            {
                float minScaleX = terrainSize.x / 128f;
                float minScaleZ = terrainSize.z / 128f;
                unitScale = Mathf.Max(0.1f, unitScale, minScaleX, minScaleZ);
            }

            levelDimensions = new Vector2Int(
                Mathf.Clamp(Mathf.CeilToInt(terrainSize.x / unitScale), 1, 128),
                Mathf.Clamp(Mathf.CeilToInt(terrainSize.z / unitScale), 1, 128));
        }

        if (fogPlaneMaterial == null)
        {
            fogPlaneMaterial = Resources.Load<Material>("FogPlane");
        }

        if (fogPlaneMaterial == null)
        {
            fogPlaneMaterial = LoadAOSFogMaterialFromProject();
        }

        SetPrivateField("fogRevealers", new List<csFogWar.FogRevealer>());
        SetPrivateField("levelMidPoint", transform);
        SetPrivateField("FogRefreshRate", fogRefreshRate);
        SetPrivateField("fogPlaneHeight", fogPlaneHeight);
        SetPrivateField("fogPlaneMaterial", fogPlaneMaterial);
        SetPrivateField("fogLerpSpeed", fogLerpSpeed);
        SetPrivateField("levelDimensionX", Mathf.Clamp(levelDimensions.x, 1, 128));
        SetPrivateField("levelDimensionY", Mathf.Clamp(levelDimensions.y, 1, 128));
        SetPrivateField("unitScale", Mathf.Max(0.1f, unitScale));
        SetPrivateField("scanSpacingPerUnit", 0.25f);
        SetPrivateField("rayStartHeight", 20f);
        SetPrivateField("rayMaxDistance", 60f);
        SetPrivateField("obstacleLayers", obstacleLayers);
        SetPrivateField("ignoreTriggers", true);
        SetPrivateField("saveDataOnScan", false);
        SetPrivateField("LevelDataToLoad", null);

        fogWar.keepRevealedTiles = false;
        ApplyFogAppearance();
    }

    private void EnsureTerrainDetailFogMask()
    {
        if (!maskTerrainDetailsOutsideVision)
        {
            return;
        }

        if (GetComponent<TerrainDetailFogMask>() == null)
        {
            gameObject.AddComponent<TerrainDetailFogMask>();
        }
    }

    private void ApplyNightState()
    {
        bool shouldRun = alwaysNightForDebug || !onlyAtNight || (TimeManager.Instance != null && TimeManager.Instance.IsNight);
        if (fogWar != null)
        {
            fogWar.enabled = shouldRun;
        }

        if (shouldRun)
        {
            ApplyFogAppearance();
        }

        if (!hideRuntimeFogPlaneDuringDay)
        {
            return;
        }

        GameObject fogPlane = GetRuntimeFogPlane();
        if (fogPlane != null && fogPlane.activeSelf != shouldRun)
        {
            fogPlane.SetActive(shouldRun);
        }
    }

    private void ApplyFogAppearance()
    {
        float nightBlend = GetNightBlend();
        fogWar.FogColor = Color.Lerp(dayFogColor, fogColor, nightBlend);
        fogWar.FogPlaneAlpha = Mathf.Lerp(dayFogPlaneAlpha, fogPlaneAlpha, nightBlend);
    }

    private float GetNightBlend()
    {
        if (alwaysNightForDebug)
        {
            return 1f;
        }

        if (TimeManager.Instance == null)
        {
            return 0f;
        }

        float timeRatio = TimeManager.Instance.GetTimeRatio();
        return Mathf.Clamp01(Mathf.Sin((timeRatio - 0.5f) * Mathf.PI * 2f));
    }

    private void RefreshRevealers()
    {
        List<csFogWar.FogRevealer> revealers = new List<csFogWar.FogRevealer>();
        revealedObjects.Clear();

        AddExplicitVisionSources(revealers);
        AddDefaultPlayerUnits(revealers);
        AddDefaultBuildings(revealers);

        fogWar.ReplaceFogRevealerList(revealers);
    }

    private void EnsureVisibilityTargets()
    {
        // Tối ưu hóa hiệu năng: Các đối tượng (Tài nguyên, Kẻ địch, Chợ trung lập)
        // tự động đăng ký FogVisibilityTarget khi khởi chạy (Start/Awake).
        // Tránh vòng lặp duyệt hàng nghìn đối tượng mỗi giây trong Update() gây sụt giảm FPS định kỳ.
    }

    private static void AddVisibilityTargetIfMissing(GameObject target)
    {
        if (target == null || target.GetComponent<FogVisibilityTarget>() != null)
        {
            return;
        }

        target.AddComponent<FogVisibilityTarget>();
    }

    private void AddExplicitVisionSources(List<csFogWar.FogRevealer> revealers)
    {
        for (int i = 0; i < VisionSource.Registry.Count; i++)
        {
            VisionSource source = VisionSource.Registry[i];
            if (source == null || !source.CanReveal())
            {
                continue;
            }

            revealers.Add(new csFogWar.FogRevealer(source.transform, ToSightRange(source.VisionRadius), false));
            revealedObjects.Add(source.gameObject);
        }
    }

    private void AddDefaultPlayerUnits(List<csFogWar.FogRevealer> revealers)
    {
        for (int i = 0; i < BaseCombatUnitController.Registry.Count; i++)
        {
            BaseCombatUnitController unit = BaseCombatUnitController.Registry[i];
            if (unit == null || revealedObjects.Contains(unit.gameObject) || unit.faction != UnitFaction.Player || unit.currentState == CombatState.Dead)
            {
                continue;
            }

            ConstructibleBuilding building = unit.GetComponent<ConstructibleBuilding>();
            if (building != null && !building.IsCompleted)
            {
                continue;
            }

            revealers.Add(new csFogWar.FogRevealer(unit.transform, GetDefaultUnitVision(unit), false));
            revealedObjects.Add(unit.gameObject);
        }
    }

    private void AddDefaultBuildings(List<csFogWar.FogRevealer> revealers)
    {
        for (int i = 0; i < ConstructibleBuilding.Registry.Count; i++)
        {
            ConstructibleBuilding building = ConstructibleBuilding.Registry[i];
            if (building == null || revealedObjects.Contains(building.gameObject) || !building.IsCompleted)
            {
                continue;
            }

            // Chặn các công trình trung lập (Neutral Market) tự mở sương mù xung quanh nó
            MarketController mc = building.GetComponent<MarketController>();
            if (mc == null) mc = building.GetComponentInChildren<MarketController>();
            if (mc != null && mc.isNeutral)
            {
                continue;
            }

            revealers.Add(new csFogWar.FogRevealer(building.transform, ToSightRange(buildingVisionRadius), false));
            revealedObjects.Add(building.gameObject);
        }
    }

    private int GetDefaultUnitVision(BaseCombatUnitController unit)
    {
        if (unit.GetComponent<MainBuildingCombatTarget>() != null)
        {
            return ToSightRange(mainBuildingVisionRadius);
        }

        if (unit.GetComponent<BuildingCombatTarget>() != null)
        {
            WatchTowerGarrison watchTower = unit.GetComponent<WatchTowerGarrison>();
            if (watchTower != null)
            {
                return ToSightRange(watchTower.VisionRadius);
            }

            return ToSightRange(buildingVisionRadius);
        }

        if (unit.GetComponent<VillagerCombatTarget>() != null || unit.GetComponent<VillagerController>() != null)
        {
            return ToSightRange(villagerVisionRadius);
        }

        return ToSightRange(combatUnitVisionRadius);
    }

    private static int ToSightRange(float worldRadius)
    {
        return Mathf.Max(1, Mathf.RoundToInt(worldRadius));
    }

    private GameObject GetRuntimeFogPlane()
    {
        if (runtimeFogPlane != null)
        {
            return runtimeFogPlane;
        }

        Transform child = transform.Find("[RUNTIME] Fog_Plane");
        if (child != null)
        {
            runtimeFogPlane = child.gameObject;
            return runtimeFogPlane;
        }

        return runtimeFogPlane;
    }

    private void SetPrivateField(string fieldName, object value)
    {
        if (!_cachedFields.TryGetValue(fieldName, out FieldInfo field))
        {
            field = typeof(csFogWar).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                Debug.LogWarning("AOS FogWar field not found: " + fieldName);
                return;
            }
            _cachedFields[fieldName] = field;
        }

        field.SetValue(fogWar, value);
    }

    private static Material LoadAOSFogMaterialFromProject()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/AOSFogWar/FogPlane.mat");
#else
        return null;
#endif
    }
}
