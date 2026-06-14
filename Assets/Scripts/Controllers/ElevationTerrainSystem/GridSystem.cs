using UnityEngine;
using System.Collections.Generic;
using Unity.AI.Navigation;

public class GridSystem : MonoBehaviour
{
    [Header("Grid Settings")]
    [UnityEngine.Serialization.FormerlySerializedAs("cellSize")]
    [SerializeField] private float _cellSize = 2f; 
    
    private int _width;
    private int _length;

    [Header("Procedural Map")]
    [SerializeField] private bool _useProceduralSeed = true;
    [SerializeField] private int _mapSeed = 12345;
    [SerializeField] private bool _resizeTerrainOnGenerate = true;
    [SerializeField] private Vector2Int _mapWorldSize = new Vector2Int(512, 512);
    [SerializeField] private int _startingSafeRadiusCells = 14;

    [Header("Navigation Bake")]
    [SerializeField] private bool _bakeNavMeshBeforeResources = true;
    [SerializeField] private NavMeshSurface _navMeshSurface;

    [Header("Terrain Details")]
    [SerializeField] private bool _generateTerrainDetails = true;
    [SerializeField] private bool _useComputeShaderGrass = true;
    [SerializeField] private Texture2D _detailTexture;
    [SerializeField, Range(0, 255)] private int _detailDensity = 128;
    [SerializeField] private bool _disableDetailBillboards = true;
    [SerializeField] private float _detailNoiseScale = 0.08f;
    [SerializeField] private float _detailDrawDistance = 160f;
    [SerializeField, Range(0f, 1f)] private float _terrainDetailObjectDensity = 1f;
    [SerializeField] private Vector2 _detailWidthRange = new Vector2(1.2f, 2.2f);
    [SerializeField] private Vector2 _detailHeightRange = new Vector2(1.0f, 2.0f);
    [SerializeField] private int _detailBrushStampCount = 260;
    [SerializeField] private bool _detailBrushUseTerrainSize = true;
    [SerializeField] private Vector2 _detailBrushRadiusRange = new Vector2(10f, 24f);
    [SerializeField, Range(0.1f, 1f)] private float _detailBrushHardness = 0.65f;
    [SerializeField, Range(0f, 0.95f)] private float _detailBrushCoreRadius = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _detailBrushNoiseStrength = 0.15f;
    [SerializeField, Range(0f, 90f)] private float _maxDetailSlope = 35f;
    [SerializeField, Range(0f, 1f)] private float _maxDetailHeight01 = 0.75f;

    [Header("Terrain Generation (Shape)")]
    [UnityEngine.Serialization.FormerlySerializedAs("terrainHeightMultiplier")]
    [SerializeField] private float _terrainHeightMultiplier = 30f;
    [UnityEngine.Serialization.FormerlySerializedAs("terrainNoiseScale")]
    [SerializeField] private float _terrainNoiseScale = 0.03f;
    [UnityEngine.Serialization.FormerlySerializedAs("octaves")]
    [SerializeField] private int _octaves = 4;
    [UnityEngine.Serialization.FormerlySerializedAs("persistance")]
    [Range(0, 1)] [SerializeField] private float _persistance = 0.5f;
    [UnityEngine.Serialization.FormerlySerializedAs("lacunarity")]
    [SerializeField] private float _lacunarity = 2f;
    [UnityEngine.Serialization.FormerlySerializedAs("heightCurve")]
    [SerializeField] private AnimationCurve _heightCurve = AnimationCurve.Linear(0, 0, 1, 1);
    
    [Header("Rivers")]
    [UnityEngine.Serialization.FormerlySerializedAs("generateRivers")]
    [SerializeField] private bool _generateRivers = true;
    [UnityEngine.Serialization.FormerlySerializedAs("riverFrequency")]
    [SerializeField] private float _riverFrequency = 0.05f;
    [UnityEngine.Serialization.FormerlySerializedAs("riverWidth")]
    [Range(0, 1)] [SerializeField] private float _riverWidth = 0.02f;
    [UnityEngine.Serialization.FormerlySerializedAs("riverDepth")]
    [SerializeField] private float _riverDepth = 0.2f;

    [Header("Resource Spawning")]
    [UnityEngine.Serialization.FormerlySerializedAs("treePrefab")]
    [SerializeField] private GameObject _treePrefab; 
    [UnityEngine.Serialization.FormerlySerializedAs("rockPrefab")]
    [SerializeField] private GameObject _rockPrefab; 
    [UnityEngine.Serialization.FormerlySerializedAs("bushPrefab")]
    [SerializeField] private GameObject _bushPrefab; 
    [UnityEngine.Serialization.FormerlySerializedAs("goldPrefab")]
    [SerializeField] private GameObject _goldPrefab;

    [Header("Resource Node Amounts")]
    [SerializeField] private Vector2Int _woodNodeAmountRange = new Vector2Int(200, 300);
    [SerializeField] private Vector2Int _stoneNodeAmountRange = new Vector2Int(200, 300);
    [SerializeField] private Vector2Int _goldNodeAmountRange = new Vector2Int(200, 300);
    [SerializeField] private Vector2Int _foodNodeAmountRange = new Vector2Int(200, 300);
    
    [Header("Resource Noise (Forest/Ore)")]
    [UnityEngine.Serialization.FormerlySerializedAs("resourceNoiseScale")]
    [SerializeField] private float _resourceNoiseScale = 0.2f; 
    [UnityEngine.Serialization.FormerlySerializedAs("forestThreshold")]
    [Range(0f, 1f)] [SerializeField] private float _forestThreshold = 0.65f; 
    
    [Header("Scattered Resource Chances (0.01 = 1%)")]
    [UnityEngine.Serialization.FormerlySerializedAs("stoneSpawnChance")]
    [Range(0f, 1f)] [SerializeField] private float _stoneSpawnChance = 0.02f;    
    [UnityEngine.Serialization.FormerlySerializedAs("goldSpawnChance")]
    [Range(0f, 1f)] [SerializeField] private float _goldSpawnChance = 0.01f;   
    [SerializeField] [Range(0f, 1f)] private float _bushSpawnChance = 0.01f;

    [Header("Resource Placement Variation")]
    [SerializeField, Range(0f, 0.49f)] private float _resourcePositionJitter = 0.38f;

    [Header("Resource Clusters")]
    [SerializeField] private int _forestClusterCount = 18;
    [SerializeField] private Vector2Int _forestClusterRadiusRange = new Vector2Int(5, 10);
    [SerializeField, Range(0f, 1f)] private float _forestClusterDensity = 0.38f;
    [SerializeField] private int _maxTreeResources = 1200;
    [SerializeField] private int _stoneClusterCount = 14;
    [SerializeField] private Vector2Int _stoneClusterRadiusRange = new Vector2Int(2, 5);
    [SerializeField, Range(0f, 1f)] private float _stoneClusterDensity = 0.35f;
    [SerializeField] private int _maxStoneResources = 160;
    [SerializeField] private int _goldClusterCount = 5;
    [SerializeField] private Vector2Int _goldClusterRadiusRange = new Vector2Int(2, 4);
    [SerializeField, Range(0f, 1f)] private float _goldClusterDensity = 0.32f;
    [SerializeField] private int _maxGoldResources = 80;
    [SerializeField] private int _bushClusterCount = 14;
    [SerializeField] private Vector2Int _bushClusterRadiusRange = new Vector2Int(2, 5);
    [SerializeField, Range(0f, 1f)] private float _bushClusterDensity = 0.28f;
    [SerializeField] private int _maxBushResources = 160;
    [SerializeField] private int _resourceClusterMinDistance = 8;
    [SerializeField] private int _resourceClusterPlacementAttempts = 40;

    [Header("Scattered Resources")]
    [SerializeField] private int _scatteredTreeCount = 450;
    [SerializeField] private int _scatteredStoneCount = 90;
    [SerializeField] private int _scatteredGoldCount = 25;
    [SerializeField] private int _scatteredBushCount = 90;
    [SerializeField] private int _scatteredPlacementAttemptsMultiplier = 8;

    [Header("Starting Resources")]
    [SerializeField] private bool _guaranteeStartingResources = true;
    [SerializeField] private int _startingTreeCount = 28;
    [SerializeField] private int _startingStoneCount = 10;
    [SerializeField] private int _startingGoldCount = 0;
    [SerializeField] private int _startingBushCount = 0;
    [SerializeField] private int _startingResourceInnerRadiusCells = 10;
    [SerializeField] private int _startingResourceOuterRadiusCells = 22;
    [SerializeField] private int _startingResourcePlacementAttempts = 260;

    [Header("Resource Respawn")]
    [SerializeField] private bool _enableResourceRespawn = true;
    [SerializeField] private bool _respectResourceCapsOnRespawn = true;
    [SerializeField] private int _resourceRespawnSearchRadius = 8;
    [SerializeField] private int _resourceRespawnPlacementAttempts = 40;
    [SerializeField] private Vector2 _woodRespawnDelayRange = new Vector2(120f, 180f);
    [SerializeField] private Vector2 _stoneRespawnDelayRange = new Vector2(180f, 260f);
    [SerializeField] private Vector2 _goldRespawnDelayRange = new Vector2(240f, 360f);
    [SerializeField] private Vector2 _foodRespawnDelayRange = new Vector2(90f, 150f);

    private GridCell[,] _gridArray;
    private readonly List<PendingResourceRespawn> _pendingResourceRespawns = new List<PendingResourceRespawn>();

    private class PendingResourceRespawn
    {
        public ResourceType type;
        public int originX;
        public int originZ;
        public float readyTime;
    }

    void Awake()
    {
        if (_woodNodeAmountRange.x < 200) _woodNodeAmountRange = new Vector2Int(200, 300);
        if (_stoneNodeAmountRange.x < 200) _stoneNodeAmountRange = new Vector2Int(200, 300);
        if (_goldNodeAmountRange.x < 200) _goldNodeAmountRange = new Vector2Int(200, 300);
        if (_foodNodeAmountRange.x < 200) _foodNodeAmountRange = new Vector2Int(200, 300);

        InitGridFromTerrain();
    }

    private void Update()
    {
        ProcessResourceRespawns();
    }

    private void UpdateGridDimensions()
    {
        if (Terrain.activeTerrain != null)
        {
            _width = Mathf.RoundToInt(Terrain.activeTerrain.terrainData.size.x / _cellSize);
            _length = Mathf.RoundToInt(Terrain.activeTerrain.terrainData.size.z / _cellSize);
        }
        else
        {
            _width = 50;
            _length = 50;
        }
    }

    private void InitGridFromTerrain()
    {
        UpdateGridDimensions();
        _gridArray = new GridCell[_width, _length];
        
        for (int x = 0; x < _width; x++)
        {
            for (int z = 0; z < _length; z++)
            {
                _gridArray[x, z] = new GridCell(x, z, 0); 
            }
        }

        // Khôi phục lại trạng thái Tài nguyên
        foreach (Transform child in transform)
        {
            int x = Mathf.RoundToInt(child.position.x / _cellSize);
            int z = Mathf.RoundToInt(child.position.z / _cellSize);

            if (x >= 0 && x < _width && z >= 0 && z < _length)
            {
                GridCell cell = _gridArray[x, z];
                cell.hasResource = true;
                cell.resourceObject = child.gameObject;
                cell.isWalkable = false; 
                cell.isBuildable = false; 
                
                ResourceType type = ResourceType.Wood;
                if (child.name.StartsWith("Tree")) type = ResourceType.Wood;
                else if (child.name.StartsWith("Rock")) type = ResourceType.Stone;
                else if (child.name.StartsWith("Bush")) type = ResourceType.Food;
                else if (child.name.StartsWith("Gold")) type = ResourceType.Gold;

                cell.resourceType = type;

                // Lấy hoặc tự động thêm ResourceNode cho tài nguyên đặt sẵn trong Scene
                ResourceNode node = child.gameObject.GetComponent<ResourceNode>();
                if (node == null)
                {
                    node = child.gameObject.AddComponent<ResourceNode>();
                }

                node.Initialize(type, GetResourceNodeAmount(type, cell), cell);
                RegisterResourceNode(node);
            }
        }
    }

    public float GetCellSize() => _cellSize;
    public int GetWidth() => _width;
    public int GetLength() => _length;

    [ContextMenu("0. Generate Full Procedural Map")]
    private void GenerateFullProceduralMap()
    {
        ClearGrid();
        PrepareTerrainSize();
        GenerateTerrainShape();

        // [TEMPORARILY DISABLED] Grass generation from GridSystem
        // if (_useComputeShaderGrass)
        // {
        //     // Clear legacy details so they don't render
        //     Terrain terrain = Terrain.activeTerrain;
        //     if (terrain != null && terrain.terrainData != null)
        //     {
        //         TerrainData tData = terrain.terrainData;
        //         int[,] emptyLayer = new int[tData.detailHeight, tData.detailWidth];
        //         for (int i = 0; i < tData.detailPrototypes.Length; i++)
        //         {
        //             tData.SetDetailLayer(0, 0, i, emptyLayer);
        //         }
        //         terrain.Flush();
        //     }
        //
        //     // Trigger the ProceduralGrassRenderer
        //     ProceduralGrassRenderer grassRenderer = FindAnyObjectByType<ProceduralGrassRenderer>();
        //     if (grassRenderer != null)
        //     {
        //         grassRenderer.GenerateGrass(Terrain.activeTerrain);
        //     }
        // }
        // else
        // {
        //     GenerateTerrainDetails();
        // }

        BakeNavigationMesh();
        GenerateGrid();
        InitGridFromTerrain();
    }

    private void BakeNavigationMesh()
    {
        if (!_bakeNavMeshBeforeResources)
        {
            return;
        }

        if (_navMeshSurface == null)
        {
            _navMeshSurface = FindAnyObjectByType<NavMeshSurface>();
        }

        if (_navMeshSurface == null)
        {
            Debug.LogWarning("[GridSystem] Không tìm thấy NavMeshSurface để bake sau khi tạo terrain.");
            return;
        }

        _navMeshSurface.BuildNavMesh();
        Debug.Log("[GridSystem] Đã bake NavMesh sau khi tạo terrain và trước khi spawn tài nguyên.");
    }

    private void PrepareTerrainSize()
    {
        if (!_resizeTerrainOnGenerate)
        {
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("Không có Unity Terrain trong cảnh!");
            return;
        }

        TerrainData tData = terrain.terrainData;
        float height = Mathf.Max(1f, tData.size.y);
        tData.size = new Vector3(Mathf.Max(_cellSize, _mapWorldSize.x), height, Mathf.Max(_cellSize, _mapWorldSize.y));
        UpdateGridDimensions();
    }

    [ContextMenu("1. Generate Terrain Shape")]
    private void GenerateTerrainShape()
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("Không có Unity Terrain trong cảnh!");
            return;
        }

        TerrainData tData = terrain.terrainData;
        int res = tData.heightmapResolution;
        float[,] heights = new float[res, res];

        Vector2 seedOffset = GetSeedOffset(11);
        Vector2 riverSeedOffset = GetSeedOffset(37);

        // Quét từng điểm trên Heightmap
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float amplitude = 1f;
                float frequency = 1f;
                float noiseHeight = 0f;

                // FBM (Fractal Brownian Motion) - Xếp chồng nhiều lớp Noise
                for (int i = 0; i < _octaves; i++)
                {
                    float sampleX = (x / (float)res) * tData.size.x * _terrainNoiseScale * frequency + seedOffset.x;
                    float sampleY = (y / (float)res) * tData.size.z * _terrainNoiseScale * frequency + seedOffset.y;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY);
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= _persistance;
                    frequency *= _lacunarity;
                }

                // Dùng AnimationCurve để nặn hình dạng (ví dụ: bóp phẳng vùng thấp làm đồng bằng, kéo vuốt vùng cao làm núi)
                noiseHeight = _heightCurve.Evaluate(Mathf.Clamp01(noiseHeight / 2f)); // Chia 2 để chuẩn hóa tương đối

                // TẠO SÔNG (Ridge Noise)
                if (_generateRivers)
                {
                    float rX = (x / (float)res) * tData.size.x * _riverFrequency + riverSeedOffset.x;
                    float rY = (y / (float)res) * tData.size.z * _riverFrequency + riverSeedOffset.y;
                    
                    // Ridge Noise: Lấy giá trị tuyệt đối để tạo rãnh chữ V
                    float riverNoise = Mathf.Abs(Mathf.PerlinNoise(rX, rY) - 0.5f) * 2f; 
                    
                    if (riverNoise < _riverWidth) // Nếu lọt vào lòng sông
                    {
                        // Đào rãnh sông xuống
                        float carve = 1f - (riverNoise / _riverWidth); // 1 ở giữa tâm sông, 0 ở bờ
                        noiseHeight -= carve * _riverDepth;
                    }
                }

                // Áp dụng chiều cao chuẩn hóa (0.0 đến 1.0)
                heights[y, x] = Mathf.Clamp01(noiseHeight * (_terrainHeightMultiplier / tData.size.y));
            }
        }

        tData.SetHeights(0, 0, heights);
        Debug.Log("Đã nặn xong địa hình!");
    }

    [ContextMenu("2. Generate Terrain Details")]
    private void GenerateTerrainDetails()
    {
        if (!_generateTerrainDetails)
        {
            return;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            Debug.LogWarning("Khong co Unity Terrain trong canh!");
            return;
        }

        TerrainData tData = terrain.terrainData;
        DetailPrototype[] prototypes = EnsureTerrainDetailPrototypes(tData);
        if (prototypes.Length == 0)
        {
            Debug.LogWarning("[GridSystem] Khong the tao Terrain Detail vi chua co detail texture.");
            return;
        }

        if (_disableDetailBillboards)
        {
            for (int i = 0; i < prototypes.Length; i++)
            {
                if (prototypes[i].renderMode == DetailRenderMode.GrassBillboard)
                {
                    prototypes[i].renderMode = DetailRenderMode.Grass;
                }
            }

            tData.detailPrototypes = prototypes;
        }

        ApplyTerrainDetailPrototypeSettings(tData, prototypes);
        terrain.detailObjectDistance = Mathf.Max(10f, _detailDrawDistance);
        terrain.detailObjectDensity = Mathf.Clamp01(_terrainDetailObjectDensity);

        int detailWidth = tData.detailWidth;
        int detailHeight = tData.detailHeight;
        if (detailWidth <= 0 || detailHeight <= 0)
        {
            Debug.LogWarning("[GridSystem] Terrain Detail Resolution chua hop le.");
            return;
        }

        UpdateGridDimensions();

        float safeDetailNoiseScale = Mathf.Max(0.0001f, _detailNoiseScale);
        float maxHeight = Mathf.Clamp01(_maxDetailHeight01);
        Vector2 detailOffset = GetSeedOffset(137);

        for (int layer = 0; layer < prototypes.Length; layer++)
        {
            int[,] detailLayer = GenerateBrushDetailLayer(
                terrain,
                tData,
                detailWidth,
                detailHeight,
                layer,
                safeDetailNoiseScale,
                maxHeight,
                detailOffset);

            tData.SetDetailLayer(0, 0, layer, detailLayer);
        }

        terrain.Flush();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tData);
#endif
        Debug.Log("[GridSystem] Generated Terrain Details by code. Density = " + _detailDensity + ", Billboard = Off.");
    }

    private int[,] GenerateBrushDetailLayer(
        Terrain terrain,
        TerrainData tData,
        int detailWidth,
        int detailHeight,
        int layer,
        float safeDetailNoiseScale,
        float maxHeight,
        Vector2 detailOffset)
    {
        int[,] detailLayer = new int[detailHeight, detailWidth];
        int stampCount = Mathf.Max(0, _detailBrushStampCount);
        float terrainRadius = Mathf.Sqrt(tData.size.x * tData.size.x + tData.size.z * tData.size.z);
        float minRadius = _detailBrushUseTerrainSize
            ? terrainRadius
            : Mathf.Max(0.1f, Mathf.Min(_detailBrushRadiusRange.x, _detailBrushRadiusRange.y));
        float maxRadius = _detailBrushUseTerrainSize
            ? terrainRadius
            : Mathf.Max(minRadius, Mathf.Max(_detailBrushRadiusRange.x, _detailBrushRadiusRange.y));
        float falloffPower = Mathf.Lerp(2.5f, 0.45f, _detailBrushHardness);
        float coreRadius = Mathf.Clamp01(_detailBrushCoreRadius);

        for (int stamp = 0; stamp < stampCount; stamp++)
        {
            float centerX01 = GetDeterministic01(stamp, layer, 301);
            float centerZ01 = GetDeterministic01(stamp, layer, 307);
            float radiusWorld = Mathf.Lerp(minRadius, maxRadius, GetDeterministic01(stamp, layer, 313));

            int centerX = Mathf.RoundToInt(centerX01 * (detailWidth - 1));
            int centerY = Mathf.RoundToInt(centerZ01 * (detailHeight - 1));
            int radiusX = Mathf.CeilToInt(radiusWorld / Mathf.Max(0.01f, tData.size.x) * (detailWidth - 1));
            int radiusY = Mathf.CeilToInt(radiusWorld / Mathf.Max(0.01f, tData.size.z) * (detailHeight - 1));

            int minX = Mathf.Max(0, centerX - radiusX);
            int maxX = Mathf.Min(detailWidth - 1, centerX + radiusX);
            int minY = Mathf.Max(0, centerY - radiusY);
            int maxY = Mathf.Min(detailHeight - 1, centerY + radiusY);

            for (int y = minY; y <= maxY; y++)
            {
                float dy = radiusY <= 0 ? 0f : (y - centerY) / (float)radiusY;
                float normalizedZ = detailHeight <= 1 ? 0f : y / (float)(detailHeight - 1);

                for (int x = minX; x <= maxX; x++)
                {
                    float dx = radiusX <= 0 ? 0f : (x - centerX) / (float)radiusX;
                    float distance01 = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance01 > 1f)
                    {
                        continue;
                    }

                    float normalizedX = detailWidth <= 1 ? 0f : x / (float)(detailWidth - 1);
                    float worldX = terrain.transform.position.x + normalizedX * tData.size.x;
                    float worldZ = terrain.transform.position.z + normalizedZ * tData.size.z;
                    if (!CanPlaceDetailAt(tData, normalizedX, normalizedZ, worldX, worldZ, maxHeight))
                    {
                        continue;
                    }

                    float falloff;
                    if (distance01 <= coreRadius)
                    {
                        falloff = 1f;
                    }
                    else
                    {
                        float edgeDistance = Mathf.InverseLerp(coreRadius, 1f, distance01);
                        falloff = Mathf.Pow(1f - edgeDistance, falloffPower);
                    }

                    float noise = Mathf.PerlinNoise(
                        worldX * safeDetailNoiseScale + detailOffset.x + layer * 19.17f,
                        worldZ * safeDetailNoiseScale + detailOffset.y + layer * 31.43f);
                    float noiseMultiplier = Mathf.Lerp(1f, noise, _detailBrushNoiseStrength);
                    int density = Mathf.RoundToInt(_detailDensity * falloff * noiseMultiplier);

                    if (density > 0)
                    {
                        detailLayer[y, x] = Mathf.Clamp(detailLayer[y, x] + density, 0, _detailDensity);
                    }
                }
            }
        }

        return detailLayer;
    }

    private bool CanPlaceDetailAt(TerrainData tData, float normalizedX, float normalizedZ, float worldX, float worldZ, float maxHeight)
    {
        GetXY(new Vector3(worldX, 0f, worldZ), out int gridX, out int gridZ);
        if (IsInsideStartingSafeZone(gridX, gridZ))
        {
            return false;
        }

        float height01 = tData.GetInterpolatedHeight(normalizedX, normalizedZ) / Mathf.Max(1f, tData.size.y);
        if (height01 > maxHeight)
        {
            return false;
        }

        Vector3 normal = tData.GetInterpolatedNormal(normalizedX, normalizedZ);
        float slope = Vector3.Angle(normal, Vector3.up);
        return slope <= _maxDetailSlope;
    }

    private void ApplyTerrainDetailPrototypeSettings(TerrainData tData, DetailPrototype[] prototypes)
    {
        float minWidth = Mathf.Max(0.01f, Mathf.Min(_detailWidthRange.x, _detailWidthRange.y));
        float maxWidth = Mathf.Max(minWidth, Mathf.Max(_detailWidthRange.x, _detailWidthRange.y));
        float minHeight = Mathf.Max(0.01f, Mathf.Min(_detailHeightRange.x, _detailHeightRange.y));
        float maxHeight = Mathf.Max(minHeight, Mathf.Max(_detailHeightRange.x, _detailHeightRange.y));

        for (int i = 0; i < prototypes.Length; i++)
        {
            prototypes[i].minWidth = minWidth;
            prototypes[i].maxWidth = maxWidth;
            prototypes[i].minHeight = minHeight;
            prototypes[i].maxHeight = maxHeight;
            prototypes[i].noiseSpread = 0.1f;

            if (!prototypes[i].usePrototypeMesh)
            {
                prototypes[i].renderMode = DetailRenderMode.Grass;
            }
        }

        tData.detailPrototypes = prototypes;
    }

    private DetailPrototype[] EnsureTerrainDetailPrototypes(TerrainData tData)
    {
        DetailPrototype[] prototypes = tData.detailPrototypes;
        if (prototypes != null && prototypes.Length > 0)
        {
            return prototypes;
        }

        Texture2D texture = _detailTexture;
#if UNITY_EDITOR
        if (texture == null)
        {
            texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdAssets/Hand Painted Seamless Grass Texture/Textures/Albedo_GreenGrass.tga");
        }
#endif

        if (texture == null)
        {
            return System.Array.Empty<DetailPrototype>();
        }

        DetailPrototype grassPrototype = new DetailPrototype
        {
            prototypeTexture = texture,
            renderMode = DetailRenderMode.Grass,
            healthyColor = new Color(0.55f, 0.9f, 0.45f),
            dryColor = new Color(0.35f, 0.55f, 0.22f),
            minWidth = 0.4f,
            maxWidth = 0.8f,
            minHeight = 0.35f,
            maxHeight = 0.9f,
            noiseSpread = 0.35f,
            usePrototypeMesh = false
        };

        tData.detailPrototypes = new[] { grassPrototype };
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(tData);
#endif
        Debug.Log("[GridSystem] Added default non-billboard grass Detail Prototype to Terrain.");
        return tData.detailPrototypes;
    }

    [ContextMenu("3. Clear Grid Resources")]
    private void ClearGrid()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        _gridArray = null;
    }

    [ContextMenu("4. Generate Resources on Terrain")]
    private void GenerateGrid()
    {
        ClearGrid(); 
        _pendingResourceRespawns.Clear();
        UpdateGridDimensions();

        _gridArray = new GridCell[_width, _length];
        bool useLegacyScatter = false;
        Vector2 resourceNoiseOffset = Vector2.zero;
        for (int x = 0; x < _width; x++)
        {
            for (int z = 0; z < _length; z++)
            {
                _gridArray[x, z] = new GridCell(x, z, 0);

                if (IsInsideStartingSafeZone(x, z))
                {
                    continue;
                }

                float resXCoord = (float)x * _resourceNoiseScale + resourceNoiseOffset.x;
                float resZCoord = (float)z * _resourceNoiseScale + resourceNoiseOffset.y;
                float biomeNoise = Mathf.PerlinNoise(resXCoord, resZCoord);

                if (useLegacyScatter && biomeNoise > _forestThreshold && _treePrefab != null)
                {
                    InstantiateResource(_gridArray[x, z], _treePrefab, "Tree", ResourceType.Wood);
                }
                else if (useLegacyScatter)
                {
                    // Đá và Vàng sinh riêng lẻ rải rác (Dùng Random thay vì Perlin Noise)
                    float randomChance = GetDeterministic01(x, z, 101);
                    
                    if (randomChance < _stoneSpawnChance && _rockPrefab != null)
                    {
                        InstantiateResource(_gridArray[x, z], _rockPrefab, "Rock", ResourceType.Stone);
                    }
                    else if (randomChance >= _stoneSpawnChance && randomChance < (_stoneSpawnChance + _goldSpawnChance) && _goldPrefab != null)
                    {
                        InstantiateResource(_gridArray[x, z], _goldPrefab, "Gold", ResourceType.Gold);
                    }
                    else if (randomChance >= (_stoneSpawnChance + _goldSpawnChance) &&
                             randomChance < (_stoneSpawnChance + _goldSpawnChance + _bushSpawnChance) &&
                             _bushPrefab != null)
                    {
                        InstantiateResource(_gridArray[x, z], _bushPrefab, "Bush", ResourceType.Food);
                    }
                }
            }
        }

        GenerateResourceClusters();
    }

    private void GenerateResourceClusters()
    {
        List<Vector2Int> clusterCenters = new List<Vector2Int>();

        SpawnStartingResources();

        SpawnScatteredResources(_scatteredTreeCount, _maxTreeResources, _treePrefab, "Tree", ResourceType.Wood, 809);
        SpawnScatteredResources(_scatteredStoneCount, _maxStoneResources, _rockPrefab, "Rock", ResourceType.Stone, 907);
        SpawnScatteredResources(_scatteredGoldCount, _maxGoldResources, _goldPrefab, "Gold", ResourceType.Gold, 1009);
        SpawnScatteredResources(_scatteredBushCount, _maxBushResources, _bushPrefab, "Bush", ResourceType.Food, 1103);

        SpawnResourceClusters(_forestClusterCount, _forestClusterRadiusRange, _forestClusterDensity, _maxTreeResources, _treePrefab, "Tree", ResourceType.Wood, 401, clusterCenters);
        SpawnResourceClusters(_stoneClusterCount, _stoneClusterRadiusRange, _stoneClusterDensity, _maxStoneResources, _rockPrefab, "Rock", ResourceType.Stone, 503, clusterCenters);
        SpawnResourceClusters(_goldClusterCount, _goldClusterRadiusRange, _goldClusterDensity, _maxGoldResources, _goldPrefab, "Gold", ResourceType.Gold, 607, clusterCenters);
        SpawnResourceClusters(_bushClusterCount, _bushClusterRadiusRange, _bushClusterDensity, _maxBushResources, _bushPrefab, "Bush", ResourceType.Food, 709, clusterCenters);
    }

    private void SpawnStartingResources()
    {
        if (!_guaranteeStartingResources)
        {
            return;
        }

        SpawnStartingResourcesOfType(_startingTreeCount, _treePrefab, "Tree", ResourceType.Wood, 1201);
        SpawnStartingResourcesOfType(_startingStoneCount, _rockPrefab, "Rock", ResourceType.Stone, 1301);
        SpawnStartingResourcesOfType(_startingGoldCount, _goldPrefab, "Gold", ResourceType.Gold, 1401);
        SpawnStartingResourcesOfType(_startingBushCount, _bushPrefab, "Bush", ResourceType.Food, 1501);
    }

    private void SpawnStartingResourcesOfType(
        int desiredCount,
        GameObject prefab,
        string namePrefix,
        ResourceType type,
        int salt)
    {
        if (desiredCount <= 0 || prefab == null)
        {
            return;
        }

        int maxResources = GetMaxResourceCount(type);
        int budget = Mathf.Max(0, maxResources - CountResourcesOfType(type));
        int targetCount = Mathf.Min(desiredCount, budget);
        if (targetCount <= 0)
        {
            return;
        }

        int innerRadius = Mathf.Max(0, Mathf.Min(_startingResourceInnerRadiusCells, _startingResourceOuterRadiusCells));
        int outerRadius = Mathf.Max(innerRadius + 1, Mathf.Max(_startingResourceInnerRadiusCells, _startingResourceOuterRadiusCells));
        int attempts = Mathf.Max(targetCount, _startingResourcePlacementAttempts);
        Vector2Int center = new Vector2Int(_width / 2, _length / 2);
        System.Random random = CreateDeterministicRandom(salt);
        int spawnedCount = 0;

        for (int attempt = 0; attempt < attempts && spawnedCount < targetCount; attempt++)
        {
            float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
            float radius = Mathf.Sqrt(Mathf.Lerp(innerRadius * innerRadius, outerRadius * outerRadius, (float)random.NextDouble()));
            int x = center.x + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
            int z = center.y + Mathf.RoundToInt(Mathf.Sin(angle) * radius);

            if (!CanPlaceStartingResourceAt(x, z, center, innerRadius, outerRadius))
            {
                continue;
            }

            InstantiateResource(_gridArray[x, z], prefab, namePrefix, type);
            spawnedCount++;
        }
    }

    private bool CanPlaceStartingResourceAt(int x, int z, Vector2Int center, int innerRadius, int outerRadius)
    {
        if (x < 0 || x >= _width || z < 0 || z >= _length)
        {
            return false;
        }

        float distance = Vector2Int.Distance(new Vector2Int(x, z), center);
        if (distance < innerRadius || distance > outerRadius)
        {
            return false;
        }

        GridCell cell = _gridArray[x, z];
        return cell != null && !cell.hasResource && cell.isWalkable && cell.isBuildable;
    }

    private void SpawnScatteredResources(
        int desiredCount,
        int maxResources,
        GameObject prefab,
        string namePrefix,
        ResourceType type,
        int salt)
    {
        if (desiredCount <= 0 || maxResources <= 0 || prefab == null)
        {
            return;
        }

        int alreadySpawned = CountResourcesOfType(type);
        int budget = Mathf.Max(0, maxResources - alreadySpawned);
        int targetCount = Mathf.Min(desiredCount, budget);
        if (targetCount <= 0)
        {
            return;
        }

        System.Random random = CreateDeterministicRandom(salt);
        int maxAttempts = Mathf.Max(targetCount, targetCount * Mathf.Max(1, _scatteredPlacementAttemptsMultiplier));
        int spawnedCount = 0;

        for (int attempt = 0; attempt < maxAttempts && spawnedCount < targetCount; attempt++)
        {
            int x = random.Next(0, _width);
            int z = random.Next(0, _length);
            if (!CanPlaceResourceAt(x, z))
            {
                continue;
            }

            InstantiateResource(_gridArray[x, z], prefab, namePrefix, type);
            spawnedCount++;
        }
    }

    private int CountResourcesOfType(ResourceType type)
    {
        int count = 0;
        for (int x = 0; x < _width; x++)
        {
            for (int z = 0; z < _length; z++)
            {
                GridCell cell = _gridArray[x, z];
                if (cell != null && cell.hasResource && cell.resourceType == type)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void SpawnResourceClusters(
        int clusterCount,
        Vector2Int radiusRange,
        float density,
        int maxResources,
        GameObject prefab,
        string namePrefix,
        ResourceType type,
        int salt,
        List<Vector2Int> clusterCenters)
    {
        if (clusterCount <= 0 || prefab == null || maxResources <= 0)
        {
            return;
        }

        int minRadius = Mathf.Max(1, Mathf.Min(radiusRange.x, radiusRange.y));
        int maxRadius = Mathf.Max(minRadius, Mathf.Max(radiusRange.x, radiusRange.y));
        int attemptsPerCluster = Mathf.Max(1, _resourceClusterPlacementAttempts);

        System.Random clusterRandom = CreateDeterministicRandom(salt);

        int spawnedCount = 0;
        for (int clusterIndex = 0; clusterIndex < clusterCount && spawnedCount < maxResources; clusterIndex++)
        {
            if (!TryFindClusterCenter(clusterRandom, attemptsPerCluster, clusterCenters, out Vector2Int center))
            {
                continue;
            }

            int radius = clusterRandom.Next(minRadius, maxRadius + 1);
            spawnedCount += SpawnResourceCluster(center, radius, density, maxResources - spawnedCount, prefab, namePrefix, type, salt + clusterIndex * 17);
            clusterCenters.Add(center);
        }
    }

    private bool TryFindClusterCenter(
        System.Random clusterRandom,
        int attemptsPerCluster,
        List<Vector2Int> existingCenters,
        out Vector2Int center)
    {
        center = Vector2Int.zero;

        for (int attempt = 0; attempt < attemptsPerCluster; attempt++)
        {
            int x = clusterRandom.Next(0, _width);
            int z = clusterRandom.Next(0, _length);
            Vector2Int candidate = new Vector2Int(x, z);

            if (IsInsideStartingSafeZone(candidate.x, candidate.y) || !IsFarEnoughFromOtherClusters(candidate, existingCenters))
            {
                continue;
            }

            center = candidate;
            return true;
        }

        return false;
    }

    private System.Random CreateDeterministicRandom(int salt)
    {
        int seed = _useProceduralSeed ? Hash(_mapSeed, salt, 917) : UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        return new System.Random(seed);
    }

    private bool IsFarEnoughFromOtherClusters(Vector2Int candidate, List<Vector2Int> existingCenters)
    {
        if (_resourceClusterMinDistance <= 0)
        {
            return true;
        }

        for (int i = 0; i < existingCenters.Count; i++)
        {
            if (Vector2Int.Distance(candidate, existingCenters[i]) < _resourceClusterMinDistance)
            {
                return false;
            }
        }

        return true;
    }

    private int SpawnResourceCluster(
        Vector2Int center,
        int radius,
        float density,
        int remainingBudget,
        GameObject prefab,
        string namePrefix,
        ResourceType type,
        int salt)
    {
        int startX = Mathf.Max(0, center.x - radius);
        int endX = Mathf.Min(_width - 1, center.x + radius);
        int startZ = Mathf.Max(0, center.y - radius);
        int endZ = Mathf.Min(_length - 1, center.y + radius);
        float safeDensity = Mathf.Clamp01(density);
        int spawnedCount = 0;

        for (int x = startX; x <= endX; x++)
        {
            for (int z = startZ; z <= endZ; z++)
            {
                if (!CanPlaceResourceAt(x, z))
                {
                    continue;
                }

                float distance01 = Vector2.Distance(new Vector2(x, z), new Vector2(center.x, center.y)) / Mathf.Max(1f, radius);
                if (distance01 > 1f)
                {
                    continue;
                }

                float falloff = Mathf.Lerp(1f, 0.25f, distance01);
                float placementChance = safeDensity * falloff;
                if (GetDeterministic01(x, z, salt + 37) <= placementChance)
                {
                    InstantiateResource(_gridArray[x, z], prefab, namePrefix, type);
                    spawnedCount++;
                    if (spawnedCount >= remainingBudget)
                    {
                        return spawnedCount;
                    }
                }
            }
        }

        return spawnedCount;
    }

    private bool CanPlaceResourceAt(int x, int z)
    {
        if (x < 0 || x >= _width || z < 0 || z >= _length || IsInsideStartingSafeZone(x, z))
        {
            return false;
        }

        GridCell cell = _gridArray[x, z];
        return cell != null && !cell.hasResource && cell.isWalkable && cell.isBuildable;
    }

    private void InstantiateResource(GridCell cell, GameObject prefab, string namePrefix, ResourceType type)
    {
        Vector3 spawnPos = GetResourceSpawnPosition(cell); 
        Quaternion randomRotation = Quaternion.Euler(0, GetDeterministic01(cell.x, cell.z, 211) * 360f, 0);
        GameObject resObj = Instantiate(prefab, spawnPos, randomRotation, transform);
        resObj.name = namePrefix + "_" + cell.x + "_" + cell.z;
        ExcludeResourceFromNavMeshBuild(resObj);
        
        cell.hasResource = true;
        cell.resourceType = type;
        cell.resourceObject = resObj;
        cell.isWalkable = false; 
        cell.isBuildable = false;

        ResourceNode node = resObj.GetComponent<ResourceNode>();
        if (node == null)
        {
            node = resObj.AddComponent<ResourceNode>();
        }
        node.Initialize(type, GetResourceNodeAmount(type, cell), cell);
        RegisterResourceNode(node);
    }

    private int GetResourceNodeAmount(ResourceType type, GridCell cell)
    {
        Vector2Int range = GetResourceNodeAmountRange(type);
        int min = Mathf.Max(1, Mathf.Min(range.x, range.y));
        int max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        if (cell == null)
        {
            return UnityEngine.Random.Range(min, max + 1);
        }

        return Mathf.RoundToInt(Mathf.Lerp(min, max, GetDeterministic01(cell.x, cell.z, GetResourceAmountSalt(type))));
    }

    private Vector2Int GetResourceNodeAmountRange(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return _woodNodeAmountRange;
            case ResourceType.Stone:
                return _stoneNodeAmountRange;
            case ResourceType.Gold:
                return _goldNodeAmountRange;
            case ResourceType.Food:
                return _foodNodeAmountRange;
            default:
                return Vector2Int.one;
        }
    }

    private int GetResourceAmountSalt(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return 1301;
            case ResourceType.Stone:
                return 1303;
            case ResourceType.Gold:
                return 1307;
            case ResourceType.Food:
                return 1319;
            default:
                return 1321;
        }
    }

    private void RegisterResourceNode(ResourceNode node)
    {
        if (node == null)
        {
            return;
        }

        node.OnDepleted -= HandleResourceDepleted;
        node.OnDepleted += HandleResourceDepleted;
    }

    private void HandleResourceDepleted(ResourceNode node)
    {
        if (!_enableResourceRespawn || node == null || node.OccupiedCell == null)
        {
            return;
        }

        PendingResourceRespawn pending = new PendingResourceRespawn
        {
            type = node.ResourceType,
            originX = node.OccupiedCell.x,
            originZ = node.OccupiedCell.z,
            readyTime = Time.time + GetResourceRespawnDelay(node.ResourceType)
        };

        _pendingResourceRespawns.Add(pending);
    }

    private void ProcessResourceRespawns()
    {
        if (!_enableResourceRespawn || _pendingResourceRespawns.Count == 0 || _gridArray == null)
        {
            return;
        }

        for (int i = _pendingResourceRespawns.Count - 1; i >= 0; i--)
        {
            PendingResourceRespawn pending = _pendingResourceRespawns[i];
            if (Time.time < pending.readyTime)
            {
                continue;
            }

            if (TryRespawnResource(pending))
            {
                _pendingResourceRespawns.RemoveAt(i);
            }
            else
            {
                pending.readyTime = Time.time + 30f;
            }
        }
    }

    private bool TryRespawnResource(PendingResourceRespawn pending)
    {
        GameObject prefab = GetResourcePrefab(pending.type);
        if (prefab == null)
        {
            return true;
        }

        if (_respectResourceCapsOnRespawn && CountResourcesOfType(pending.type) >= GetMaxResourceCount(pending.type))
        {
            return false;
        }

        if (CanPlaceResourceAt(pending.originX, pending.originZ))
        {
            InstantiateResource(_gridArray[pending.originX, pending.originZ], prefab, GetResourceNamePrefix(pending.type), pending.type);
            return true;
        }

        int radius = Mathf.Max(1, _resourceRespawnSearchRadius);
        int attempts = Mathf.Max(1, _resourceRespawnPlacementAttempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            int x = pending.originX + UnityEngine.Random.Range(-radius, radius + 1);
            int z = pending.originZ + UnityEngine.Random.Range(-radius, radius + 1);
            if (!CanPlaceResourceAt(x, z))
            {
                continue;
            }

            InstantiateResource(_gridArray[x, z], prefab, GetResourceNamePrefix(pending.type), pending.type);
            return true;
        }

        return false;
    }

    private float GetResourceRespawnDelay(ResourceType type)
    {
        Vector2 range = GetResourceRespawnDelayRange(type);
        float min = Mathf.Max(0f, Mathf.Min(range.x, range.y));
        float max = Mathf.Max(min, Mathf.Max(range.x, range.y));
        return UnityEngine.Random.Range(min, max);
    }

    private Vector2 GetResourceRespawnDelayRange(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return _woodRespawnDelayRange;
            case ResourceType.Stone:
                return _stoneRespawnDelayRange;
            case ResourceType.Gold:
                return _goldRespawnDelayRange;
            case ResourceType.Food:
                return _foodRespawnDelayRange;
            default:
                return _woodRespawnDelayRange;
        }
    }

    private GameObject GetResourcePrefab(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return _treePrefab;
            case ResourceType.Stone:
                return _rockPrefab;
            case ResourceType.Gold:
                return _goldPrefab;
            case ResourceType.Food:
                return _bushPrefab;
            default:
                return null;
        }
    }

    private string GetResourceNamePrefix(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return "Tree";
            case ResourceType.Stone:
                return "Rock";
            case ResourceType.Gold:
                return "Gold";
            case ResourceType.Food:
                return "Bush";
            default:
                return "Resource";
        }
    }

    private int GetMaxResourceCount(ResourceType type)
    {
        switch (type)
        {
            case ResourceType.Wood:
                return _maxTreeResources;
            case ResourceType.Stone:
                return _maxStoneResources;
            case ResourceType.Gold:
                return _maxGoldResources;
            case ResourceType.Food:
                return _maxBushResources;
            default:
                return int.MaxValue;
        }
    }

    private void ExcludeResourceFromNavMeshBuild(GameObject resourceObject)
    {
        NavMeshModifier modifier = resourceObject.GetComponent<NavMeshModifier>();
        if (modifier == null)
        {
            modifier = resourceObject.AddComponent<NavMeshModifier>();
        }

        modifier.ignoreFromBuild = true;
    }

    private Vector3 GetResourceSpawnPosition(GridCell cell)
    {
        Vector3 spawnPos = GetWorldPosition(cell.x, cell.z);
        float maxOffset = _cellSize * Mathf.Clamp(_resourcePositionJitter, 0f, 0.49f);
        float offsetX = (GetDeterministic01(cell.x, cell.z, 223) * 2f - 1f) * maxOffset;
        float offsetZ = (GetDeterministic01(cell.x, cell.z, 227) * 2f - 1f) * maxOffset;

        spawnPos.x += offsetX;
        spawnPos.z += offsetZ;

        if (Terrain.activeTerrain != null)
        {
            spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos);
        }

        return spawnPos;
    }

    private Vector2 GetSeedOffset(int salt)
    {
        if (!_useProceduralSeed)
        {
            return new Vector2(UnityEngine.Random.Range(-10000f, 10000f), UnityEngine.Random.Range(-10000f, 10000f));
        }

        int hashA = Hash(_mapSeed, salt, 17);
        int hashB = Hash(_mapSeed, salt, 53);
        return new Vector2((hashA % 20000) - 10000f, (hashB % 20000) - 10000f);
    }

    private float GetDeterministic01(int x, int z, int salt)
    {
        if (!_useProceduralSeed)
        {
            return UnityEngine.Random.value;
        }

        int hash = Hash(x, z, _mapSeed + salt);
        return (hash & 0xFFFFFF) / 16777215f;
    }

    private int Hash(int a, int b, int c)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + a;
            hash = hash * 31 + b;
            hash = hash * 31 + c;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return Mathf.Abs(hash);
        }
    }

    public bool IsInsideStartingSafeZonePublic(int x, int z)
    {
        return IsInsideStartingSafeZone(x, z);
    }

    private bool IsInsideStartingSafeZone(int x, int z)
    {
        if (_startingSafeRadiusCells <= 0)
        {
            return false;
        }

        Vector2Int center = new Vector2Int(_width / 2, _length / 2);
        return Vector2Int.Distance(new Vector2Int(x, z), center) <= _startingSafeRadiusCells;
    }

    // Tương thích API cũ
    public Vector3 GetWorldPosition(int x, int z, int ignoredElevation)
    {
        return GetWorldPosition(x, z);
    }

    // Lấy tọa độ thế giới (bao gồm độ cao thực tế từ Terrain)
    public Vector3 GetWorldPosition(int x, int z)
    {
        float worldX = x * _cellSize;
        float worldZ = z * _cellSize;
        float worldY = 0f;

        if (Terrain.activeTerrain != null)
        {
            worldY = Terrain.activeTerrain.SampleHeight(new Vector3(worldX, 0, worldZ));
        }

        return new Vector3(worldX, worldY, worldZ);
    }

    public void GetXY(Vector3 worldPosition, out int x, out int z)
    {
        x = Mathf.RoundToInt(worldPosition.x / _cellSize);
        z = Mathf.RoundToInt(worldPosition.z / _cellSize);
    }

    public GridCell GetCell(Vector3 worldPosition)
    {
        GetXY(worldPosition, out int x, out int z);
        return GetCell(x, z);
    }

    public GridCell GetCell(int x, int z)
    {
        if (x >= 0 && z >= 0 && x < _width && z < _length)
        {
            if (_gridArray == null) return null; // Dành cho Editor mode
            return _gridArray[x, z];
        }
        return null; 
    }

    public void FlattenRectArea(int startX, int startZ, int sizeX, int sizeZ)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) return;

        TerrainData tData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;

        // 1. Tính toán độ cao trung bình của khu vực móng nhà
        float sumY = 0f;
        int count = 0;
        for (int x = 0; x < sizeX; x++)
        {
            for (int z = 0; z < sizeZ; z++)
            {
                sumY += terrain.SampleHeight(GetWorldPosition(startX + x, startZ + z));
                count++;
            }
        }
        float targetY = sumY / count; // Lấy trung bình cộng làm độ cao nền nhà
        float normalizedTarget = targetY / tData.size.y;

        // 2. Chuyển đổi vùng móng sang tọa độ Heightmap của Unity
        float startXWorld = startX * _cellSize - terrainPos.x;
        float startZWorld = startZ * _cellSize - terrainPos.z;
        float endXWorld = (startX + sizeX - 1) * _cellSize - terrainPos.x;
        float endZWorld = (startZ + sizeZ - 1) * _cellSize - terrainPos.z;

        // Đổ thêm một chút padding (mở rộng vùng ủi ra ngoài 1 ô) để làm mượt sườn dốc (Blend)
        startXWorld -= _cellSize;
        startZWorld -= _cellSize;
        endXWorld += _cellSize;
        endZWorld += _cellSize;

        int res = tData.heightmapResolution;
        int startHmX = Mathf.RoundToInt((startXWorld / tData.size.x) * (res - 1));
        int startHmY = Mathf.RoundToInt((startZWorld / tData.size.z) * (res - 1));
        int endHmX = Mathf.RoundToInt((endXWorld / tData.size.x) * (res - 1));
        int endHmY = Mathf.RoundToInt((endZWorld / tData.size.z) * (res - 1));

        startHmX = Mathf.Clamp(startHmX, 0, res - 1);
        startHmY = Mathf.Clamp(startHmY, 0, res - 1);
        endHmX = Mathf.Clamp(endHmX, 0, res - 1);
        endHmY = Mathf.Clamp(endHmY, 0, res - 1);

        int hmSizeX = endHmX - startHmX;
        int hmSizeY = endHmY - startHmY;

        if (hmSizeX > 0 && hmSizeY > 0)
        {
            float[,] heights = tData.GetHeights(startHmX, startHmY, hmSizeX, hmSizeY);

            // 3. Làm phẳng khu vực, đồng thời làm mượt (Lerp) ở phần viền để khỏi bị sắc cạnh
            for (int y = 0; y < hmSizeY; y++)
            {
                for (int x = 0; x < hmSizeX; x++)
                {
                    float blendX = Mathf.Min(x, hmSizeX - x - 1) / (float)(hmSizeX / 3f);
                    float blendY = Mathf.Min(y, hmSizeY - y - 1) / (float)(hmSizeY / 3f);
                    float blend = Mathf.Clamp01(Mathf.Min(blendX, blendY));
                    
                    heights[y, x] = Mathf.Lerp(heights[y, x], normalizedTarget, blend);
                }
            }
            
            tData.SetHeights(startHmX, startHmY, heights);
        }
    }

    public void FlattenArea(int centerX, int centerZ, int radius, int ignoredElevation)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null) 
        {
            Debug.LogWarning("Không tìm thấy Unity Terrain trong cảnh! Vui lòng tạo 1 Terrain.");
            return;
        }

        TerrainData tData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        
        float targetY = terrain.SampleHeight(GetWorldPosition(centerX, centerZ));
        float normalizedTarget = targetY / tData.size.y;

        // Mở rộng bán kính ra thêm 3 ô để làm khoảng đệm vuốt sườn dốc
        float radiusWorld = (radius + 3) * _cellSize;
        
        float startXWorld = centerX * _cellSize - terrainPos.x - radiusWorld;
        float startZWorld = centerZ * _cellSize - terrainPos.z - radiusWorld;
        float endXWorld = centerX * _cellSize - terrainPos.x + radiusWorld;
        float endZWorld = centerZ * _cellSize - terrainPos.z + radiusWorld;

        int res = tData.heightmapResolution;
        int startHmX = Mathf.RoundToInt((startXWorld / tData.size.x) * (res - 1));
        int startHmY = Mathf.RoundToInt((startZWorld / tData.size.z) * (res - 1));
        int endHmX = Mathf.RoundToInt((endXWorld / tData.size.x) * (res - 1));
        int endHmY = Mathf.RoundToInt((endZWorld / tData.size.z) * (res - 1));

        startHmX = Mathf.Clamp(startHmX, 0, res - 1);
        startHmY = Mathf.Clamp(startHmY, 0, res - 1);
        endHmX = Mathf.Clamp(endHmX, 0, res - 1);
        endHmY = Mathf.Clamp(endHmY, 0, res - 1);

        int sizeX = endHmX - startHmX;
        int sizeY = endHmY - startHmY;

        if (sizeX > 0 && sizeY > 0)
        {
            float[,] heights = tData.GetHeights(startHmX, startHmY, sizeX, sizeY);

            // Bán kính an toàn phẳng hoàn toàn
            float flatRadiusHm = (radius * _cellSize / tData.size.x) * (res - 1);
            float maxRadiusHm = sizeX / 2f;
            float hmCenterX = sizeX / 2f;
            float hmCenterY = sizeY / 2f;

            for (int y = 0; y < sizeY; y++)
            {
                for (int x = 0; x < sizeX; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(hmCenterX, hmCenterY));
                    
                    if (dist <= flatRadiusHm)
                    {
                        heights[y, x] = normalizedTarget; // Phẳng như cái dĩa
                    }
                    else if (dist < maxRadiusHm)
                    {
                        // Sườn dốc thoai thoải dần ra tự nhiên
                        float t = (dist - flatRadiusHm) / (maxRadiusHm - flatRadiusHm);
                        // Dùng SmoothStep để đường dốc uốn lượn hình chữ S đẹp mắt hơn
                        t = Mathf.SmoothStep(0f, 1f, t);
                        heights[y, x] = Mathf.Lerp(normalizedTarget, heights[y, x], t);
                    }
                }
            }
            
            tData.SetHeights(startHmX, startHmY, heights);
        }

        for (int x = centerX - radius; x <= centerX + radius; x++)
        {
            for (int z = centerZ - radius; z <= centerZ + radius; z++)
            {
                GridCell cell = GetCell(x, z);
                if (cell != null)
                {
                    if (cell.hasResource && cell.resourceObject != null)
                    {
                        if (Application.isPlaying) Destroy(cell.resourceObject); 
                        else DestroyImmediate(cell.resourceObject);
                        
                        cell.resourceObject = null;
                        cell.hasResource = false;
                        cell.isBuildable = true;
                        cell.isWalkable = true;
                    }
                }
            }
        }
    }
}
