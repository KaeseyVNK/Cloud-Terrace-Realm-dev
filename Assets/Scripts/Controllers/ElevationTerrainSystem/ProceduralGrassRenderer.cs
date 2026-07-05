using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using FischlWorks_FogWar;

/// <summary>
/// Manages procedural grass rendering using a Compute Shader for culling and bending,
/// and draws grass blades via GPU instanced indirect rendering.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
public class ProceduralGrassRenderer : MonoBehaviour
{
    private struct GrassBladeData
    {
        public Vector3 position;      // 12 bytes
        public float rotation;        // 4 bytes
        public Vector2 size;          // 8 bytes
        public float bend;            // 4 bytes
        public float windEffect;      // 4 bytes
        public Vector3 bendDirection; // 12 bytes
    }

    [System.Serializable]
    public struct GrassType
    {
        public string name;
        public Material material;
        [Range(0f, 1f)]
        public float weight;
    }

    [Header("Assets & Shaders")]
    [Tooltip("Fallback material using Custom/ProceduralGrass shader if no types are configured.")]
    [SerializeField] private Material _grassMaterial;
    [Tooltip("Compute shader for grass culling and deformation.")]
    [SerializeField] private ComputeShader _computeShader;
    [Tooltip("Multiple grass materials and their weights.")]
    [SerializeField] private List<GrassType> _grassTypes = new List<GrassType>();

    [Header("Multi-Material Distribution")]
    [Tooltip("Distribute grass types in natural patches using noise instead of pure random mixing.")]
    [SerializeField] private bool _patchyDistribution = true;
    [Tooltip("Noise scale for patchy distribution. Lower values create larger patches.")]
    [SerializeField] private float _patchNoiseScale = 0.05f;


    [Header("Grass Spawning Settings")]
    [Tooltip("Grass blades per square meter.")]
    [SerializeField] private float _densityPerUnit = 3.5f;
    [Tooltip("Width range (min, max) of grass blades.")]
    [SerializeField] private Vector2 _minSize = new Vector2(0.3f, 0.45f);
    [Tooltip("Height range (min, max) of grass blades.")]
    [SerializeField] private Vector2 _maxSize = new Vector2(0.5f, 1.1f);
    [Tooltip("Random base bending range.")]
    [SerializeField] private float _minBend = 0.1f;
    [SerializeField] private float _maxBend = 0.45f;
    [Tooltip("Maximum allowed terrain slope for grass growth.")]
    [SerializeField] private float _maxSlope = 30f;
    [Tooltip("Maximum allowed normalized terrain height (0.0 to 1.0) for grass growth.")]
    [SerializeField] private float _maxHeightScale = 0.75f;
    [Tooltip("Exclude the starting safe zone around the base from grass spawning.")]
    [SerializeField] private bool _excludeSafeZone = true;
    [Tooltip("Automatically generate grass on Start using active terrain.")]
    [SerializeField] private bool _generateOnStart = true;

    [Header("Grass Coverage Variation")]
    [Tooltip("Use procedural noise so grass grows in natural patches instead of covering the whole terrain.")]
    [SerializeField] private bool _useGrassCoverageNoise = true;
    [Tooltip("Lower values create larger grass/no-grass patches.")]
    [SerializeField] private float _coverageNoiseScale = 0.035f;
    [Tooltip("Higher values create more bare ground.")]
    [Range(0f, 1f)]
    [SerializeField] private float _grassCoverageThreshold = 0.35f;
    [Tooltip("Adds smaller patch variation on top of the main coverage noise.")]
    [Range(0f, 1f)]
    [SerializeField] private float _secondaryCoverageNoiseStrength = 0.25f;

    [Header("Building Exclusion")]
    [Tooltip("Do not spawn procedural grass on cells occupied by buildings.")]
    [SerializeField] private bool _excludeBuildingFootprints = true;
    [Tooltip("Extra grid cells cleared around each building footprint.")]
    [Min(0)]
    [SerializeField] private int _buildingGrassPaddingCells = 0;

    [Header("Rendering & Layer Settings")]
    [Tooltip("Maximum distance to draw grass.")]
    [SerializeField] private float _cullDistance = 250f;
    [Tooltip("Enable frustum and distance culling on the GPU.")]
    [SerializeField] private bool _enableCulling = true;
    [Tooltip("Layer index to render the grass on.")]
    [SerializeField] private int _layer = 0;

    [Header("Fog of War")]
    [Tooltip("Hide grass blades under AOS Fog of War. Requires csFogWar in the scene.")]
    [SerializeField] private bool _enableFogCulling = true;
    [Tooltip("Fog alpha threshold at which blades are fully culled (0.0 to 1.0).")]
    [Range(0.1f, 1f)]
    [SerializeField] private float _fogCullThreshold = 0.85f;

    [Header("Wind Settings")]
    [SerializeField] private Vector4 _windDirection = new Vector4(1f, 0.3f, 0f, 0f);
    [SerializeField] private float _windSpeed = 1.2f;
    [SerializeField] private float _windFrequency = 0.08f;
    [SerializeField] private float _windStrength = 0.35f;

    private class RuntimeGrassTypeData
    {
        public Material material;
        public float weight;
        public ComputeBuffer sourceBuffer;
        public ComputeBuffer culledBuffer;
        public ComputeBuffer argsBuffer;
        public int totalSourceBlades;
        public List<GrassBladeData> bladesTempList = new List<GrassBladeData>();
    }

    private List<RuntimeGrassTypeData> _runtimeGrassTypes = new List<RuntimeGrassTypeData>();

    private Mesh _bladeMesh;
    private MaterialPropertyBlock _propertyBlock;
    private Bounds _bounds;
    private int _totalSourceBlades;
    private bool _isInitialized;
    private int _diagnosticFrameCount;

    // Fog of War cached references
    private csFogWar _fogWar;
    private bool _fogAvailable;

    // Cached arrays to avoid allocations in LateUpdate
    private readonly Vector4[] _frustumPlanes = new Vector4[6];
    private readonly Vector4[] _interactorData = new Vector4[128];
    private readonly Vector4[] _buildingExclusionZones = new Vector4[256];
    private readonly uint[] _argsDataBuffer = new uint[5];
    private readonly HashSet<Vector2Int> _buildingBlockedCells = new HashSet<Vector2Int>();

    private void Start()
    {
        if (_generateOnStart)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                terrain = FindAnyObjectByType<Terrain>();
            }

            if (terrain != null)
            {
                InitializeRenderer();
                GenerateGrass(terrain);
            }
            else
            {
                GameLog.LogWarning("[ProceduralGrassRenderer] No Terrain found in scene to generate grass on!");
            }
        }

        InitializeFogOfWar();
    }

    private void InitializeFogOfWar()
    {
        if (!_enableFogCulling)
        {
            _fogAvailable = false;
            return;
        }

        _fogWar = FindAnyObjectByType<csFogWar>();
        if (_fogWar == null)
        {
            GameLog.LogWarning("[ProceduralGrassRenderer] csFogWar not found. Fog culling disabled.");
            _fogAvailable = false;
            return;
        }

        _fogAvailable = true;
        GameLog.Log("[ProceduralGrassRenderer] Fog of War integration initialized successfully.");
    }

    private void InitializeRenderer()
    {
        if (_isInitialized) return;

        // Ép cỏ render trên layer "Grass" để tối ưu hóa culling mask của ánh sáng ban đêm
        int grassLayer = LayerMask.NameToLayer("Grass");
        if (grassLayer != -1)
        {
            _layer = grassLayer;
        }

        bool hasMaterials = _grassMaterial != null;
        if (_grassTypes != null)
        {
            foreach (var type in _grassTypes)
            {
                if (type.material != null)
                {
                    hasMaterials = true;
                    break;
                }
            }
        }

        if (!hasMaterials)
        {
            GameLog.LogError("[ProceduralGrassRenderer] No grass materials are assigned in the Inspector!");
            return;
        }

        if (_computeShader == null)
        {
            GameLog.LogError("[ProceduralGrassRenderer] Compute Shader is not assigned in the Inspector!");
            return;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            GameLog.LogError("[ProceduralGrassRenderer] Compute shaders are not supported on this graphics card or platform!");
            return;
        }

        _bladeMesh = CreateGrassBladeMesh();
        _propertyBlock = new MaterialPropertyBlock();
        _isInitialized = true;
        GameLog.Log("[ProceduralGrassRenderer] Renderer successfully initialized.");
    }

    public void GenerateGrass(Terrain terrain)
    {
        InitializeRenderer();

        if (terrain == null)
        {
            GameLog.LogError("[ProceduralGrassRenderer] Cannot generate grass: Terrain argument is null!");
            return;
        }

        if (terrain.terrainData == null)
        {
            GameLog.LogWarning("[ProceduralGrassRenderer] Cannot generate grass: TerrainData is missing.");
            return;
        }

        if (!_isInitialized)
        {
            GameLog.LogError("[ProceduralGrassRenderer] Cannot generate grass: Initialization failed!");
            return;
        }

        ReleaseBuffers();

        List<RuntimeGrassTypeData> activeTypes = new List<RuntimeGrassTypeData>();
        if (_grassTypes != null && _grassTypes.Count > 0)
        {
            foreach (var gt in _grassTypes)
            {
                if (gt.material != null)
                {
                    var data = new RuntimeGrassTypeData();
                    data.material = gt.material;
                    data.weight = Mathf.Max(0f, gt.weight);
                    activeTypes.Add(data);
                }
            }
        }

        if (activeTypes.Count == 0 && _grassMaterial != null)
        {
            var data = new RuntimeGrassTypeData();
            data.material = _grassMaterial;
            data.weight = 1.0f;
            activeTypes.Add(data);
        }

        if (activeTypes.Count == 0)
        {
            GameLog.LogError("[ProceduralGrassRenderer] Cannot generate grass: No valid materials assigned!");
            return;
        }

        _runtimeGrassTypes = activeTypes;

        TerrainData tData = terrain.terrainData;
        Vector3 terrainSize = tData.size;
        Vector3 terrainPos = terrain.transform.position;

        _bounds = new Bounds(
            terrainPos + terrainSize * 0.5f,
            new Vector3(terrainSize.x, terrainSize.y + 100f, terrainSize.z)
        );

        float step = 1f / Mathf.Sqrt(_densityPerUnit);
        step = Mathf.Max(0.25f, step);

        int stepsX = Mathf.FloorToInt(terrainSize.x / step);
        int stepsZ = Mathf.FloorToInt(terrainSize.z / step);

        System.Random rand = new System.Random(777);
        GridSystem grid = FindAnyObjectByType<GridSystem>();
        CacheBuildingBlockedCells();

        float activeMaxHeightScale = (terrainSize.y <= 5f) ? 2.0f : _maxHeightScale;

        for (int x = 0; x < stepsX; x++)
        {
            for (int z = 0; z < stepsZ; z++)
            {
                float jitterX = ((float)rand.NextDouble() * 2f - 1f) * step * 0.45f;
                float jitterZ = ((float)rand.NextDouble() * 2f - 1f) * step * 0.45f;

                float localX = x * step + jitterX;
                float localZ = z * step + jitterZ;

                if (localX < 0 || localX >= terrainSize.x || localZ < 0 || localZ >= terrainSize.z) continue;

                float worldX = terrainPos.x + localX;
                float worldZ = terrainPos.z + localZ;
                
                int gridX = 0; int gridZ = 0; bool hasGridPosition = false;
                if (grid != null) 
                { 
                    float cellSize = grid.GetCellSize(); 
                    gridX = Mathf.RoundToInt(worldX / cellSize); 
                    gridZ = Mathf.RoundToInt(worldZ / cellSize); 
                    hasGridPosition = true; 
                }

                if (_excludeSafeZone && hasGridPosition && grid.IsInsideStartingSafeZonePublic(gridX, gridZ)) continue;
                if (_excludeBuildingFootprints && hasGridPosition && _buildingBlockedCells.Contains(new Vector2Int(gridX, gridZ))) continue;
                if (_useGrassCoverageNoise && !PassesGrassCoverage(worldX, worldZ)) continue;

                float height = tData.GetInterpolatedHeight(localX / terrainSize.x, localZ / terrainSize.z);
                if (height / terrainSize.y > activeMaxHeightScale) continue;
                if (Vector3.Angle(tData.GetInterpolatedNormal(localX / terrainSize.x, localZ / terrainSize.z), Vector3.up) > _maxSlope) continue;

                GrassBladeData blade = new GrassBladeData();
                blade.position = new Vector3(worldX, height + terrainPos.y, worldZ);
                blade.rotation = (float)(rand.NextDouble() * Mathf.PI * 2.0);
                blade.size = new Vector2(Mathf.Lerp(_minSize.x, _minSize.y, (float)rand.NextDouble()), Mathf.Lerp(_maxSize.x, _maxSize.y, (float)rand.NextDouble()));
                blade.bend = Mathf.Lerp(_minBend, _maxBend, (float)rand.NextDouble());
                blade.windEffect = Mathf.Lerp(0.6f, 1.2f, (float)rand.NextDouble());
                blade.bendDirection = Vector3.zero;

                int selectedTypeIndex = ChooseGrassTypeIndex(rand, worldX, worldZ);
                _runtimeGrassTypes[selectedTypeIndex].bladesTempList.Add(blade);
            }
        }

        _totalSourceBlades = 0;
        foreach (var typeData in _runtimeGrassTypes)
        {
            int bladeCount = typeData.bladesTempList.Count;
            typeData.totalSourceBlades = bladeCount;
            _totalSourceBlades += bladeCount;

            if (bladeCount == 0) continue;

            typeData.sourceBuffer = new ComputeBuffer(bladeCount, 44, ComputeBufferType.Default);
            typeData.sourceBuffer.SetData(typeData.bladesTempList);
            typeData.culledBuffer = new ComputeBuffer(bladeCount, 44, ComputeBufferType.Append);
            typeData.argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            uint[] args = { _bladeMesh.GetIndexCount(0), 0, 0, 0, 0 };
            typeData.argsBuffer.SetData(args);
            typeData.bladesTempList.Clear();
        }
    }

    private bool PassesGrassCoverage(float worldX, float worldZ)
    {
        float noiseScale = Mathf.Max(0.0001f, _coverageNoiseScale);
        float primary = Mathf.PerlinNoise(worldX * noiseScale, worldZ * noiseScale);
        float secondary = Mathf.PerlinNoise((worldX + 913.7f) * noiseScale * 2.7f, (worldZ - 271.3f) * noiseScale * 2.7f);
        float coverage = Mathf.Lerp(primary, primary * secondary, _secondaryCoverageNoiseStrength);
        return coverage >= _grassCoverageThreshold;
    }

    private int ChooseGrassTypeIndex(System.Random rand, float worldX, float worldZ)
    {
        if (_runtimeGrassTypes.Count <= 1) return 0;

        float totalWeight = 0f;
        for (int i = 0; i < _runtimeGrassTypes.Count; i++)
        {
            totalWeight += _runtimeGrassTypes[i].weight;
        }

        float roll = (float)rand.NextDouble();
        if (_patchyDistribution)
        {
            float noiseScale = Mathf.Max(0.0001f, _patchNoiseScale);
            roll = Mathf.PerlinNoise(worldX * noiseScale, worldZ * noiseScale);
        }

        if (totalWeight <= 0f)
        {
            return Mathf.Clamp(Mathf.FloorToInt(roll * _runtimeGrassTypes.Count), 0, _runtimeGrassTypes.Count - 1);
        }

        float targetWeight = roll * totalWeight;
        float sum = 0f;
        for (int i = 0; i < _runtimeGrassTypes.Count; i++)
        {
            sum += _runtimeGrassTypes[i].weight;
            if (targetWeight <= sum) return i;
        }
        return _runtimeGrassTypes.Count - 1;
    }

    public void RegenerateForCurrentTerrain()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            terrain = FindAnyObjectByType<Terrain>();
        }

        if (terrain == null)
        {
            GameLog.LogWarning("[ProceduralGrassRenderer] Cannot regenerate grass: no Terrain found.");
            return;
        }

        InitializeRenderer();
        GenerateGrass(terrain);
    }

    private void CacheBuildingBlockedCells()
    {
        _buildingBlockedCells.Clear();
        if (!_excludeBuildingFootprints) return;
        BuildingManager bm = BuildingManager.Instance ?? FindAnyObjectByType<BuildingManager>();
        if (bm != null) bm.CollectOccupiedBuildingCells(_buildingBlockedCells, _buildingGrassPaddingCells);
    }

    private Mesh CreateGrassBladeMesh()
    {
        Mesh mesh = new Mesh { name = "GrassBlade" };
        mesh.vertices = new Vector3[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(-0.35f, 0.5f, 0), new Vector3(0.35f, 0.5f, 0), new Vector3(0, 1.0f, 0) };
        mesh.uv = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0.5f, 1.0f) };
        mesh.triangles = new int[] { 0, 2, 1, 1, 2, 3, 2, 4, 3 };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private void LateUpdate()
    {
        if (!_isInitialized || _runtimeGrassTypes == null || _runtimeGrassTypes.Count == 0 || _totalSourceBlades == 0) return;
        Camera cam = Camera.main ?? Camera.current;
        if (cam == null) return;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        for (int i = 0; i < 6; i++) _frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);

        int interactorCount = 0;
        var activeInteractors = GrassInteractor.ActiveInteractors;
        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < activeInteractors.Count && interactorCount < 128; i++)
        {
            var inter = activeInteractors[i];
            if (inter != null && Vector3.SqrMagnitude(inter.transform.position - camPos) < _cullDistance * _cullDistance)
                _interactorData[interactorCount++] = new Vector4(inter.transform.position.x, inter.transform.position.y, inter.transform.position.z, inter.Radius);
        }
        for (int i = interactorCount; i < 128; i++) _interactorData[i] = Vector4.zero;

        int kernel = _computeShader.FindKernel("CullAndProcess");
        _computeShader.SetVector("_CameraPosition", camPos);
        _computeShader.SetFloat("_CullDistance", _cullDistance);
        _computeShader.SetVectorArray("_FrustumPlanes", _frustumPlanes);
        _computeShader.SetInt("_EnableCulling", _enableCulling ? 1 : 0);
        _computeShader.SetInt("_NumInteractors", interactorCount);
        _computeShader.SetVectorArray("_Interactors", _interactorData);
        _computeShader.SetInt("_NumBuildingExclusionZones", CollectBuildingExclusionZones());
        _computeShader.SetVectorArray("_BuildingExclusionZones", _buildingExclusionZones);

        SetupFogComputeData(kernel);

        bool logDiagnostics = (++_diagnosticFrameCount >= 100);
        if (logDiagnostics) _diagnosticFrameCount = 0;
        int totalRenderedBlades = 0;

        foreach (var typeData in _runtimeGrassTypes)
        {
            if (typeData.totalSourceBlades == 0 || typeData.sourceBuffer == null) continue;
            typeData.culledBuffer.SetCounterValue(0);
            _computeShader.SetBuffer(kernel, "_SourceBuffer", typeData.sourceBuffer);
            _computeShader.SetBuffer(kernel, "_CulledBuffer", typeData.culledBuffer);
            _computeShader.SetInt("_NumSourceBlades", typeData.totalSourceBlades);
            _computeShader.Dispatch(kernel, Mathf.CeilToInt(typeData.totalSourceBlades / 64f), 1, 1);
            ComputeBuffer.CopyCount(typeData.culledBuffer, typeData.argsBuffer, sizeof(uint));
            if (logDiagnostics) { typeData.argsBuffer.GetData(_argsDataBuffer); totalRenderedBlades += (int)_argsDataBuffer[1]; }
            _propertyBlock.Clear();
            _propertyBlock.SetBuffer("_CulledBuffer", typeData.culledBuffer);
            _propertyBlock.SetVector("_WindDirection", _windDirection);
            _propertyBlock.SetFloat("_WindSpeed", _windSpeed);
            _propertyBlock.SetFloat("_WindFrequency", _windFrequency);
            _propertyBlock.SetFloat("_WindStrength", _windStrength);
            Graphics.DrawMeshInstancedIndirect(_bladeMesh, 0, typeData.material, _bounds, typeData.argsBuffer, 0, _propertyBlock, UnityEngine.Rendering.ShadowCastingMode.Off, true, _layer);
        }
        if (logDiagnostics) GameLog.Log($"[ProceduralGrassRenderer Debug] GPU rendering: {totalRenderedBlades} / {_totalSourceBlades} total blades visible.");
    }

    private int CollectBuildingExclusionZones()
    {
        for (int i = 0; i < _buildingExclusionZones.Length; i++) _buildingExclusionZones[i] = Vector4.zero;
        if (!_excludeBuildingFootprints) return 0;
        BuildingManager bm = BuildingManager.Instance ?? FindAnyObjectByType<BuildingManager>();
        return bm != null ? bm.CollectGrassExclusionZones(_buildingExclusionZones, _buildingGrassPaddingCells) : 0;
    }

    private void SetupFogComputeData(int kernel)
    {
        _computeShader.SetTexture(kernel, "_FogTexture", Texture2D.blackTexture);

        if (!_enableFogCulling || !_fogAvailable || _fogWar == null || !_fogWar.enabled)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        Texture2D fogTexture = _fogWar.FogPlaneTextureLerpBuffer;
        if (fogTexture == null)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        Transform levelMidPoint = _fogWar._LevelMidPoint;
        int dimX = _fogWar.LevelDimensionX;
        int dimY = _fogWar.LevelDimensionY;
        float fogUnitScale = _fogWar._UnitScale;

        if (levelMidPoint == null)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        Vector2 fogCenter = new Vector2(levelMidPoint.position.x, levelMidPoint.position.z);
        Vector2 fogSize = new Vector2(dimX * fogUnitScale, dimY * fogUnitScale);

        float currentFogPlaneAlpha = _fogWar.FogPlaneAlpha;
        if (currentFogPlaneAlpha < 0.01f)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        _computeShader.SetTexture(kernel, "_FogTexture", fogTexture);
        _computeShader.SetVector("_FogWorldCenter", new Vector4(fogCenter.x, fogCenter.y, 0f, 0f));
        _computeShader.SetVector("_FogWorldSize", new Vector4(fogSize.x, fogSize.y, 0f, 0f));
        _computeShader.SetInt("_EnableFogCulling", 1);
        _computeShader.SetFloat("_FogPlaneAlpha", currentFogPlaneAlpha);
        _computeShader.SetFloat("_FogCullThreshold", _fogCullThreshold);
    }

    private void ReleaseBuffers()
    {
        if (_runtimeGrassTypes != null)
        {
            foreach (var typeData in _runtimeGrassTypes)
            {
                if (typeData.sourceBuffer != null)
                {
                    typeData.sourceBuffer.Release();
                    typeData.sourceBuffer = null;
                }
                if (typeData.culledBuffer != null)
                {
                    typeData.culledBuffer.Release();
                    typeData.culledBuffer = null;
                }
                if (typeData.argsBuffer != null)
                {
                    typeData.argsBuffer.Release();
                    typeData.argsBuffer = null;
                }
            }
        }
    }

    private void OnDisable()
    {
        ReleaseBuffers();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
    }
}
