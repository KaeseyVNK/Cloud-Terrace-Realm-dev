using UnityEngine;

public class BuildGridOverlay : MonoBehaviour
{
    [SerializeField] private Color _gridColor = new Color(0.15f, 0.9f, 1f, 0.7f);
    [SerializeField] private float _heightOffset = 0.12f;
    [SerializeField] private float _lineWidth = 0.045f;
    [SerializeField] private int _maxVisibleCellsPerAxis = 90;
    [SerializeField] private int _terrainLineSegments = 6;

    private GridSystem _gridSystem;
    private GameObject _gridObject;
    private Mesh _gridMesh;
    private MeshRenderer _meshRenderer;
    private Material _lineMaterial;
    private BuildingManager _buildingManager;
    private int _lastStartX = int.MinValue;
    private int _lastEndX = int.MinValue;
    private int _lastStartZ = int.MinValue;
    private int _lastEndZ = int.MinValue;
    private float _lastCellSize = -1f;
    private bool _lastVisible;

    private void Awake()
    {
        _gridSystem = FindAnyObjectByType<GridSystem>();
        EnsureGridObject();
    }

    private void OnDisable()
    {
        if (_gridObject != null)
        {
            Destroy(_gridObject);
            _gridObject = null;
        }

        if (_gridMesh != null)
        {
            Destroy(_gridMesh);
            _gridMesh = null;
        }

        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
            _lineMaterial = null;
        }
    }

    private void LateUpdate()
    {
        bool shouldShow = TryGetBuildModeActive();
        if (!shouldShow)
        {
            SetVisible(false);
            return;
        }

        if (_gridSystem == null)
        {
            _gridSystem = FindAnyObjectByType<GridSystem>();
        }

        if (_gridSystem == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        EnsureGridObject();
        SetVisible(true);
        if (_gridObject == null || _gridMesh == null || _meshRenderer == null)
        {
            return;
        }

        float cellSize = _gridSystem.GetCellSize();
        int width = _gridSystem.GetWidth();
        int length = _gridSystem.GetLength();
        if ((width <= 0 || length <= 0) && Terrain.activeTerrain != null)
        {
            TerrainData terrainData = Terrain.activeTerrain.terrainData;
            width = Mathf.RoundToInt(terrainData.size.x / cellSize);
            length = Mathf.RoundToInt(terrainData.size.z / cellSize);
        }

        if (cellSize <= 0f || width <= 0 || length <= 0)
        {
            return;
        }

        Vector3 camPos = cam.transform.position;
        int centerX = Mathf.RoundToInt(camPos.x / cellSize);
        int centerZ = Mathf.RoundToInt(camPos.z / cellSize);
        int halfRange = Mathf.Max(6, _maxVisibleCellsPerAxis / 2);

        int startX = Mathf.Clamp(centerX - halfRange, 0, width);
        int endX = Mathf.Clamp(centerX + halfRange, 0, width);
        int startZ = Mathf.Clamp(centerZ - halfRange, 0, length);
        int endZ = Mathf.Clamp(centerZ + halfRange, 0, length);

        if (startX == _lastStartX
            && endX == _lastEndX
            && startZ == _lastStartZ
            && endZ == _lastEndZ
            && Mathf.Approximately(cellSize, _lastCellSize)
            && _gridMesh.vertexCount > 0)
        {
            return;
        }

        RebuildGridMesh(startX, endX, startZ, endZ, cellSize);
        _lastStartX = startX;
        _lastEndX = endX;
        _lastStartZ = startZ;
        _lastEndZ = endZ;
        _lastCellSize = cellSize;
    }


    private void SetVisible(bool visible)
    {
        _lastVisible = visible;
        if (_gridObject != null)
        {
            if (_gridObject.activeSelf != visible)
            {
                _gridObject.SetActive(visible);
            }
        }

        if (!visible)
        {
            _lastStartX = int.MinValue;
            _lastEndX = int.MinValue;
            _lastStartZ = int.MinValue;
            _lastEndZ = int.MinValue;
            _lastCellSize = -1f;
        }
    }

    private void RebuildGridMesh(int startX, int endX, int startZ, int endZ, float cellSize)
    {
        int segments = Mathf.Max(1, _terrainLineSegments);
        int verticalLines = Mathf.Max(0, endX - startX + 1);
        int horizontalLines = Mathf.Max(0, endZ - startZ + 1);
        int stripSegmentCount = (verticalLines + horizontalLines) * segments;
        Vector3[] vertices = new Vector3[stripSegmentCount * 4];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[stripSegmentCount * 6];
        int vertexIndex = 0;
        int triangleIndex = 0;

        for (int x = startX; x <= endX; x++)
        {
            AddTerrainStrip(vertices, colors, triangles, ref vertexIndex, ref triangleIndex,
                new Vector3(x * cellSize, 0f, startZ * cellSize),
                new Vector3(x * cellSize, 0f, endZ * cellSize),
                segments);
        }

        for (int z = startZ; z <= endZ; z++)
        {
            AddTerrainStrip(vertices, colors, triangles, ref vertexIndex, ref triangleIndex,
                new Vector3(startX * cellSize, 0f, z * cellSize),
                new Vector3(endX * cellSize, 0f, z * cellSize),
                segments);
        }

        _gridMesh.Clear();
        _gridMesh.vertices = vertices;
        _gridMesh.colors = colors;
        _gridMesh.triangles = triangles;
        _gridMesh.RecalculateBounds();
    }

    private void AddTerrainStrip(Vector3[] vertices, Color[] colors, int[] triangles, ref int vertexIndex, ref int triangleIndex, Vector3 start, Vector3 end, int segments)
    {
        float halfWidth = Mathf.Max(0.005f, _lineWidth * 0.5f);
        Vector3 previous = ProjectToTerrain(start);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            Vector3 next = ProjectToTerrain(Vector3.Lerp(start, end, t));
            Vector3 direction = next - previous;
            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z);
            if (flatDirection.sqrMagnitude < 0.0001f)
            {
                flatDirection = Vector3.forward;
            }

            Vector3 side = Vector3.Cross(Vector3.up, flatDirection.normalized) * halfWidth;

            vertices[vertexIndex] = previous - side;
            vertices[vertexIndex + 1] = previous + side;
            vertices[vertexIndex + 2] = next - side;
            vertices[vertexIndex + 3] = next + side;

            colors[vertexIndex] = _gridColor;
            colors[vertexIndex + 1] = _gridColor;
            colors[vertexIndex + 2] = _gridColor;
            colors[vertexIndex + 3] = _gridColor;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex + 2;
            triangles[triangleIndex + 4] = vertexIndex + 1;
            triangles[triangleIndex + 5] = vertexIndex + 3;

            vertexIndex += 4;
            triangleIndex += 6;
            previous = next;
        }
    }

    private Vector3 ProjectToTerrain(Vector3 position)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            position.y = terrain.SampleHeight(position) + terrain.transform.position.y + _heightOffset;
        }
        else
        {
            position.y += _heightOffset;
        }

        return position;
    }

    private void EnsureGridObject()
    {
        EnsureMaterial();

        if (_gridObject != null)
        {
            ResetGridObjectTransform();
            return;
        }

        _gridObject = new GameObject("Runtime Build Grid Overlay");
        ResetGridObjectTransform();
        _gridObject.hideFlags = HideFlags.DontSave;

        MeshFilter meshFilter = _gridObject.AddComponent<MeshFilter>();
        _meshRenderer = _gridObject.AddComponent<MeshRenderer>();
        _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _meshRenderer.receiveShadows = false;
        _meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        _meshRenderer.sharedMaterial = _lineMaterial;

        _gridMesh = new Mesh
        {
            name = "Runtime Build Grid Mesh",
            hideFlags = HideFlags.DontSave
        };
        _gridMesh.MarkDynamic();
        meshFilter.sharedMesh = _gridMesh;
        _gridObject.SetActive(false);
    }

    private void ResetGridObjectTransform()
    {
        if (_gridObject == null)
        {
            return;
        }

        _gridObject.transform.SetParent(null, true);
        _gridObject.transform.position = Vector3.zero;
        _gridObject.transform.rotation = Quaternion.identity;
        _gridObject.transform.localScale = Vector3.one;
    }

    private void EnsureMaterial()
    {
        if (_lineMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }

        if (shader == null)
        {
            return;
        }

        _lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        if (_lineMaterial.HasProperty("_BaseColor"))
        {
            _lineMaterial.SetColor("_BaseColor", _gridColor);
        }
        if (_lineMaterial.HasProperty("_Color"))
        {
            _lineMaterial.SetColor("_Color", _gridColor);
        }

        _lineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _lineMaterial.SetInt("_ZWrite", 0);
    }

    private bool TryGetBuildModeActive()
    {
        if (BuildingManager.Instance != null)
        {
            _buildingManager = BuildingManager.Instance;
        }
        else if (_buildingManager == null)
        {
            _buildingManager = FindAnyObjectByType<BuildingManager>();
        }

        return _buildingManager != null && _buildingManager.IsBuildMode;
    }
}
