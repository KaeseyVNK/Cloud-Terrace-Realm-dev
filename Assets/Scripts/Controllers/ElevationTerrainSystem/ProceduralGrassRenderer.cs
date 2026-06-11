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

    [Header("Assets & Shaders")]
    [Tooltip("Material using Custom/ProceduralGrass shader.")]
    [SerializeField] private Material _grassMaterial;
    [Tooltip("Compute shader for grass culling and deformation.")]
    [SerializeField] private ComputeShader _computeShader;

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

    // GPU Buffers
    private ComputeBuffer _sourceBuffer;
    private ComputeBuffer _culledBuffer;
    private ComputeBuffer _argsBuffer;

    private Mesh _bladeMesh;
    private MaterialPropertyBlock _propertyBlock;
    private Bounds _bounds;
    private int _totalSourceBlades;
    private bool _isInitialized;
    private int _diagnosticFrameCount;

    // Fog of War cached references
    private csFogWar _fogWar;
    private bool _fogAvailable;
    private FieldInfo _fogTextureLerpBufferField;
    private FieldInfo _fogLevelMidPointField;
    private FieldInfo _fogLevelDimXField;
    private FieldInfo _fogLevelDimYField;
    private FieldInfo _fogUnitScaleField;
    private FieldInfo _fogPlaneAlphaField;

    // Cached arrays to avoid allocations in LateUpdate
    private readonly Vector4[] _frustumPlanes = new Vector4[6];
    private readonly Vector4[] _interactorData = new Vector4[128];
    private readonly uint[] _argsDataBuffer = new uint[5];

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
                Debug.LogWarning("[ProceduralGrassRenderer] No Terrain found in scene to generate grass on!");
            }
        }

        InitializeFogOfWar();
    }

    /// <summary>
    /// Finds and caches csFogWar references for fog texture access via reflection.
    /// </summary>
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
            Debug.LogWarning("[ProceduralGrassRenderer] csFogWar not found. Fog culling disabled.");
            _fogAvailable = false;
            return;
        }

        // Cache reflection fields for private csFogWar members
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        _fogTextureLerpBufferField = typeof(csFogWar).GetField("fogPlaneTextureLerpBuffer", flags);
        _fogLevelMidPointField = typeof(csFogWar).GetField("levelMidPoint", flags);
        _fogLevelDimXField = typeof(csFogWar).GetField("levelDimensionX", flags);
        _fogLevelDimYField = typeof(csFogWar).GetField("levelDimensionY", flags);
        _fogUnitScaleField = typeof(csFogWar).GetField("unitScale", flags);
        _fogPlaneAlphaField = typeof(csFogWar).GetField("fogPlaneAlpha", flags);

        if (_fogTextureLerpBufferField == null || _fogLevelMidPointField == null)
        {
            Debug.LogWarning("[ProceduralGrassRenderer] Could not access csFogWar internals via reflection. Fog culling disabled.");
            _fogAvailable = false;
            return;
        }

        _fogAvailable = true;
        Debug.Log("[ProceduralGrassRenderer] Fog of War integration initialized successfully.");
    }

    /// <summary>
    /// Prepares the procedural mesh and material property block.
    /// </summary>
    private void InitializeRenderer()
    {
        if (_isInitialized) return;

        if (_grassMaterial == null)
        {
            Debug.LogError("[ProceduralGrassRenderer] Grass Material is not assigned in the Inspector!");
            return;
        }

        if (_computeShader == null)
        {
            Debug.LogError("[ProceduralGrassRenderer] Compute Shader is not assigned in the Inspector!");
            return;
        }

        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("[ProceduralGrassRenderer] Compute shaders are not supported on this graphics card or platform!");
            return;
        }

        _bladeMesh = CreateGrassBladeMesh();
        _propertyBlock = new MaterialPropertyBlock();
        _isInitialized = true;
        Debug.Log("[ProceduralGrassRenderer] Renderer successfully initialized.");
    }

    /// <summary>
    /// Generates grass blades procedurally on the provided terrain.
    /// </summary>
    public void GenerateGrass(Terrain terrain)
    {
        InitializeRenderer();

        if (terrain == null)
        {
            Debug.LogError("[ProceduralGrassRenderer] Cannot generate grass: Terrain argument is null!");
            return;
        }

        if (!_isInitialized)
        {
            Debug.LogError("[ProceduralGrassRenderer] Cannot generate grass: Initialization failed!");
            return;
        }

        // Release old buffers
        ReleaseBuffers();

        TerrainData tData = terrain.terrainData;
        Vector3 terrainSize = tData.size;
        Vector3 terrainPos = terrain.transform.position;

        // Set bounds enclosing the entire terrain region plus height padding
        _bounds = new Bounds(
            terrainPos + terrainSize * 0.5f,
            new Vector3(terrainSize.x, terrainSize.y + 100f, terrainSize.z)
        );

        List<GrassBladeData> blades = new List<GrassBladeData>();

        // Spatial spacing based on density
        float step = 1f / Mathf.Sqrt(_densityPerUnit);
        step = Mathf.Max(0.25f, step); // Clamp to prevent CPU overhead

        int stepsX = Mathf.FloorToInt(terrainSize.x / step);
        int stepsZ = Mathf.FloorToInt(terrainSize.z / step);

        System.Random rand = new System.Random(777);

        GridSystem grid = FindAnyObjectByType<GridSystem>();
        if (grid == null)
        {
            Debug.LogWarning("[ProceduralGrassRenderer] GridSystem not found, generating grass without safe-zone exclusions.");
        }

        int totalProcessed = 0;
        int outOfBounds = 0;
        int failedSafeZone = 0;
        int failedHeight = 0;
        int failedSlope = 0;

        // If the terrain is flat or has extremely low vertical scale, bypass height culling.
        float activeMaxHeightScale = (terrainSize.y <= 5f) ? 2.0f : _maxHeightScale;

        for (int x = 0; x < stepsX; x++)
        {
            for (int z = 0; z < stepsZ; z++)
            {
                totalProcessed++;
                // Jitter position to create organic layout
                float jitterX = ((float)rand.NextDouble() * 2f - 1f) * step * 0.45f;
                float jitterZ = ((float)rand.NextDouble() * 2f - 1f) * step * 0.45f;

                float localX = x * step + jitterX;
                float localZ = z * step + jitterZ;

                if (localX < 0 || localX >= terrainSize.x || localZ < 0 || localZ >= terrainSize.z)
                {
                    outOfBounds++;
                    continue;
                }

                float worldX = terrainPos.x + localX;
                float worldZ = terrainPos.z + localZ;

                // Query starting safe zone in GridSystem
                if (_excludeSafeZone && grid != null)
                {
                    float cellSize = grid.GetCellSize();
                    int gridX = Mathf.RoundToInt(worldX / cellSize);
                    int gridZ = Mathf.RoundToInt(worldZ / cellSize);

                    if (grid.IsInsideStartingSafeZonePublic(gridX, gridZ))
                    {
                        failedSafeZone++;
                        continue;
                    }
                }

                float normalizedX = localX / terrainSize.x;
                float normalizedZ = localZ / terrainSize.z;

                // Height check
                float height = tData.GetInterpolatedHeight(normalizedX, normalizedZ);
                float height01 = height / terrainSize.y;
                if (height01 > activeMaxHeightScale)
                {
                    failedHeight++;
                    continue;
                }

                // Slope check
                Vector3 normal = tData.GetInterpolatedNormal(normalizedX, normalizedZ);
                float slope = Vector3.Angle(normal, Vector3.up);
                if (slope > _maxSlope)
                {
                    failedSlope++;
                    continue;
                }

                // Populate blade data
                GrassBladeData blade = new GrassBladeData();
                blade.position = new Vector3(worldX, height + terrainPos.y, worldZ);
                blade.rotation = (float)(rand.NextDouble() * Mathf.PI * 2.0);
                
                float sizeLerp = (float)rand.NextDouble();
                blade.size = new Vector2(
                    Mathf.Lerp(_minSize.x, _minSize.y, sizeLerp),
                    Mathf.Lerp(_maxSize.x, _maxSize.y, (float)rand.NextDouble())
                );
                
                blade.bend = Mathf.Lerp(_minBend, _maxBend, (float)rand.NextDouble());
                blade.windEffect = Mathf.Lerp(0.6f, 1.2f, (float)rand.NextDouble());
                blade.bendDirection = Vector3.zero;

                blades.Add(blade);
            }
        }

        _totalSourceBlades = blades.Count;
        if (_totalSourceBlades == 0)
        {
            Debug.LogWarning($"[ProceduralGrassRenderer] No grass blades met placement criteria. Diagnostics: terrainSize={terrainSize}, totalProcessed={totalProcessed}, outOfBounds={outOfBounds}, failedSafeZone={failedSafeZone} (exclude={_excludeSafeZone}), failedHeight={failedHeight} (max={activeMaxHeightScale}), failedSlope={failedSlope} (max={_maxSlope}).");
            return;
        }

        // Instantiate GPU Buffers
        _sourceBuffer = new ComputeBuffer(_totalSourceBlades, 44, ComputeBufferType.Default);
        _sourceBuffer.SetData(blades);

        _culledBuffer = new ComputeBuffer(_totalSourceBlades, 44, ComputeBufferType.Append);

        _argsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
        uint[] args = new uint[5];
        args[0] = _bladeMesh.GetIndexCount(0); // 9 indices
        args[1] = 0; // Filled by GPU CopyCount
        args[2] = 0;
        args[3] = 0;
        args[4] = 0;
        _argsBuffer.SetData(args);

        Debug.Log($"[ProceduralGrassRenderer] Successfully spawned {_totalSourceBlades} grass blades. GPU buffers allocated.");
    }

    /// <summary>
    /// Creates a simple, curved 5-vertex blade mesh.
    /// </summary>
    private Mesh CreateGrassBladeMesh()
    {
        Mesh mesh = new Mesh();
        mesh.name = "GrassBlade";

        // 5 vertices for geometry bending
        Vector3[] vertices = new Vector3[5];
        Vector2[] uvs = new Vector2[5];
        int[] triangles = new int[9];

        // v0: base left
        vertices[0] = new Vector3(-0.5f, 0f, 0f);
        uvs[0] = new Vector2(0f, 0f);

        // v1: base right
        vertices[1] = new Vector3(0.5f, 0f, 0f);
        uvs[1] = new Vector2(1f, 0f);

        // v2: mid left
        vertices[2] = new Vector3(-0.35f, 0.5f, 0f);
        uvs[2] = new Vector2(0.15f, 0.5f);

        // v3: mid right
        vertices[3] = new Vector3(0.35f, 0.5f, 0f);
        uvs[3] = new Vector2(0.85f, 0.5f);

        // v4: tip
        vertices[4] = new Vector3(0f, 1.0f, 0f);
        uvs[4] = new Vector2(0.5f, 1.0f);

        // Triangles layout:
        // Tri 1 (base): 0 -> 2 -> 1
        triangles[0] = 0; triangles[1] = 2; triangles[2] = 1;
        // Tri 2 (base): 1 -> 2 -> 3
        triangles[3] = 1; triangles[4] = 2; triangles[5] = 3;
        // Tri 3 (tip):  2 -> 4 -> 3
        triangles[6] = 2; triangles[7] = 4; triangles[8] = 3;

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void LateUpdate()
    {
        if (!_isInitialized || _sourceBuffer == null || _culledBuffer == null || _argsBuffer == null || _totalSourceBlades == 0)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = Camera.current; // Fallback to current camera (e.g. editor camera)
        }

        if (cam == null)
        {
            return;
        }

        // Reset the counter of the append buffer
        _culledBuffer.SetCounterValue(0);

        // Extract camera frustum planes
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);
        for (int i = 0; i < 6; i++)
        {
            _frustumPlanes[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        }

        // Collect nearby grass interactors
        int interactorCount = 0;
        var activeInteractors = GrassInteractor.ActiveInteractors;
        Vector3 camPos = cam.transform.position;

        for (int i = 0; i < activeInteractors.Count && interactorCount < 128; i++)
        {
            var interactor = activeInteractors[i];
            if (interactor == null) continue;

            Vector3 pos = interactor.transform.position;
            // Only upload interactors within rendering distance to save CPU/GPU overhead
            if (Vector3.SqrMagnitude(pos - camPos) < _cullDistance * _cullDistance)
            {
                _interactorData[interactorCount] = new Vector4(pos.x, pos.y, pos.z, interactor.Radius);
                interactorCount++;
            }
        }

        // Clear unused interactor slots
        for (int i = interactorCount; i < 128; i++)
        {
            _interactorData[i] = Vector4.zero;
        }

        // Setup compute shader variables
        int kernel = _computeShader.FindKernel("CullAndProcess");
        _computeShader.SetBuffer(kernel, "_SourceBuffer", _sourceBuffer);
        _computeShader.SetBuffer(kernel, "_CulledBuffer", _culledBuffer);
        _computeShader.SetInt("_NumSourceBlades", _totalSourceBlades);
        _computeShader.SetVector("_CameraPosition", camPos);
        _computeShader.SetFloat("_CullDistance", _cullDistance);
        _computeShader.SetVectorArray("_FrustumPlanes", _frustumPlanes);
        _computeShader.SetInt("_EnableCulling", _enableCulling ? 1 : 0);
        _computeShader.SetInt("_NumInteractors", interactorCount);
        _computeShader.SetVectorArray("_Interactors", _interactorData);

        // Fog of War data
        SetupFogComputeData(kernel);

        // Dispatch compute culling
        int threadGroupsX = Mathf.CeilToInt((float)_totalSourceBlades / 64f);
        _computeShader.Dispatch(kernel, threadGroupsX, 1, 1);

        // Copy append buffer count to the indirect draw arguments (offset 4 bytes for instanceCount)
        ComputeBuffer.CopyCount(_culledBuffer, _argsBuffer, sizeof(uint));

        // Diagnostic log every 100 frames to monitor visible count in the Console
        _diagnosticFrameCount++;
        if (_diagnosticFrameCount >= 100)
        {
            _diagnosticFrameCount = 0;
            _argsBuffer.GetData(_argsDataBuffer);
            Debug.Log($"[ProceduralGrassRenderer Debug] GPU rendering: {_argsDataBuffer[1]} / {_totalSourceBlades} blades visible. bounds: {_bounds.center} size: {_bounds.size}");
        }

        // Draw instanced grass
        _propertyBlock.Clear();
        _propertyBlock.SetBuffer("_CulledBuffer", _culledBuffer);
        _propertyBlock.SetVector("_WindDirection", _windDirection);
        _propertyBlock.SetFloat("_WindSpeed", _windSpeed);
        _propertyBlock.SetFloat("_WindFrequency", _windFrequency);
        _propertyBlock.SetFloat("_WindStrength", _windStrength);

        Graphics.DrawMeshInstancedIndirect(
            _bladeMesh,
            0,
            _grassMaterial,
            _bounds,
            _argsBuffer,
            0,
            _propertyBlock,
            UnityEngine.Rendering.ShadowCastingMode.On,
            true,
            _layer
        );
    }

    /// <summary>
    /// Passes fog texture and world bounds to the compute shader for GPU-side fog culling.
    /// </summary>
    private void SetupFogComputeData(int kernel)
    {
        if (!_enableFogCulling || !_fogAvailable || _fogWar == null || !_fogWar.enabled)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        // Retrieve the fog lerp buffer texture (updated each frame by csFogWar)
        Texture2D fogTexture = _fogTextureLerpBufferField.GetValue(_fogWar) as Texture2D;
        if (fogTexture == null)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        // Retrieve fog world parameters
        Transform levelMidPoint = _fogLevelMidPointField.GetValue(_fogWar) as Transform;
        int dimX = (int)_fogLevelDimXField.GetValue(_fogWar);
        int dimY = (int)_fogLevelDimYField.GetValue(_fogWar);
        float fogUnitScale = (float)_fogUnitScaleField.GetValue(_fogWar);

        if (levelMidPoint == null)
        {
            _computeShader.SetInt("_EnableFogCulling", 0);
            return;
        }

        Vector2 fogCenter = new Vector2(levelMidPoint.position.x, levelMidPoint.position.z);
        Vector2 fogSize = new Vector2(dimX * fogUnitScale, dimY * fogUnitScale);

        // Read the current fogPlaneAlpha (changes dynamically between day/night)
        float currentFogPlaneAlpha = (float)_fogPlaneAlphaField.GetValue(_fogWar);
        if (currentFogPlaneAlpha < 0.01f)
        {
            // Fog alpha is essentially zero — no fog visible, skip fog culling
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
        if (_sourceBuffer != null)
        {
            _sourceBuffer.Release();
            _sourceBuffer = null;
        }
        if (_culledBuffer != null)
        {
            _culledBuffer.Release();
            _culledBuffer = null;
        }
        if (_argsBuffer != null)
        {
            _argsBuffer.Release();
            _argsBuffer = null;
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
