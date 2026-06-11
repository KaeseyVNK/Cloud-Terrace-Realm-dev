using FischlWorks_FogWar;
using System.Reflection;
using UnityEngine;

public class TerrainDetailFogMask : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Terrain targetTerrain;
    [SerializeField] private csFogWar fogWar;

    [Header("Masking")]
    [SerializeField] private bool hideDetailsOutsideVision = true;
    [SerializeField] private float updateInterval = 1.25f;
    [SerializeField, Range(1, 32)] private int detailSampleStep = 8;
    [SerializeField] private int visibilityPadding = 0;
    [SerializeField] private bool restoreDetailsWhenDisabled = true;
    [SerializeField] private bool hideDetailsWhenFogNotReady = false;

    private int[][,] originalDetailLayers;
    private int capturedDetailWidth;
    private int capturedDetailHeight;
    private int capturedLayerCount;
    private float updateTimer;
    private bool detailsAreMasked;

    private void Awake()
    {
        ResolveReferences();
        CaptureCurrentDetails();
    }

    private void OnEnable()
    {
        ResolveReferences();
        if (originalDetailLayers == null)
        {
            CaptureCurrentDetails();
        }

        ApplyFogMaskNow();
    }

    private void OnDisable()
    {
        if (restoreDetailsWhenDisabled)
        {
            RestoreOriginalDetails();
        }
    }

    private void Update()
    {
        if (!hideDetailsOutsideVision)
        {
            if (detailsAreMasked)
            {
                RestoreOriginalDetails();
            }

            return;
        }

        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval)
        {
            return;
        }

        updateTimer = 0f;
        ApplyFogMaskNow();
    }

    [ContextMenu("Capture Current Terrain Details")]
    public void CaptureCurrentDetails()
    {
        ResolveReferences();
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (!CurrentTerrainHasDetails(terrainData))
        {
            TryRegenerateTerrainDetails();
        }

        capturedDetailWidth = terrainData.detailWidth;
        capturedDetailHeight = terrainData.detailHeight;
        capturedLayerCount = terrainData.detailPrototypes.Length;
        originalDetailLayers = new int[capturedLayerCount][,];

        for (int layer = 0; layer < capturedLayerCount; layer++)
        {
            originalDetailLayers[layer] = terrainData.GetDetailLayer(0, 0, capturedDetailWidth, capturedDetailHeight, layer);
        }

        if (!CapturedDetailsHaveAnyDensity())
        {
            TryRegenerateTerrainDetails();

            for (int layer = 0; layer < capturedLayerCount; layer++)
            {
                originalDetailLayers[layer] = terrainData.GetDetailLayer(0, 0, capturedDetailWidth, capturedDetailHeight, layer);
            }
        }

        detailsAreMasked = false;
    }

    [ContextMenu("Apply Fog Mask Now")]
    public void ApplyFogMaskNow()
    {
        ResolveReferences();
        if (!CanApplyMask())
        {
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (!IsFogReady(terrainData))
        {
            if (hideDetailsWhenFogNotReady)
            {
                ApplyHiddenDetails();
            }

            return;
        }

        int step = Mathf.Max(1, detailSampleStep);

        for (int layer = 0; layer < capturedLayerCount; layer++)
        {
            int[,] sourceLayer = originalDetailLayers[layer];
            int[,] maskedLayer = new int[capturedDetailHeight, capturedDetailWidth];

            for (int y = 0; y < capturedDetailHeight; y += step)
            {
                int yEnd = Mathf.Min(capturedDetailHeight, y + step);
                float sampleY = y + (yEnd - y) * 0.5f;
                float normalizedZ = capturedDetailHeight <= 1 ? 0f : sampleY / (capturedDetailHeight - 1);

                for (int x = 0; x < capturedDetailWidth; x += step)
                {
                    int xEnd = Mathf.Min(capturedDetailWidth, x + step);
                    float sampleX = x + (xEnd - x) * 0.5f;
                    float normalizedX = capturedDetailWidth <= 1 ? 0f : sampleX / (capturedDetailWidth - 1);
                    Vector3 worldPosition = DetailToWorldPosition(targetTerrain, terrainData, normalizedX, normalizedZ);
                    bool visible = CheckVisibilitySafe(worldPosition);

                    if (!visible)
                    {
                        continue;
                    }

                    for (int blockY = y; blockY < yEnd; blockY++)
                    {
                        for (int blockX = x; blockX < xEnd; blockX++)
                        {
                            maskedLayer[blockY, blockX] = sourceLayer[blockY, blockX];
                        }
                    }
                }
            }

            terrainData.SetDetailLayer(0, 0, layer, maskedLayer);
        }

        targetTerrain.Flush();
        detailsAreMasked = true;
    }

    [ContextMenu("Restore Original Terrain Details")]
    public void RestoreOriginalDetails()
    {
        if (targetTerrain == null || targetTerrain.terrainData == null || originalDetailLayers == null)
        {
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (terrainData.detailWidth != capturedDetailWidth ||
            terrainData.detailHeight != capturedDetailHeight ||
            terrainData.detailPrototypes.Length != capturedLayerCount)
        {
            detailsAreMasked = false;
            return;
        }

        for (int layer = 0; layer < capturedLayerCount; layer++)
        {
            terrainData.SetDetailLayer(0, 0, layer, originalDetailLayers[layer]);
        }

        targetTerrain.Flush();
        detailsAreMasked = false;
    }

    private void ApplyHiddenDetails()
    {
        if (targetTerrain == null || targetTerrain.terrainData == null || originalDetailLayers == null)
        {
            return;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        for (int layer = 0; layer < capturedLayerCount; layer++)
        {
            terrainData.SetDetailLayer(0, 0, layer, new int[capturedDetailHeight, capturedDetailWidth]);
        }

        targetTerrain.Flush();
        detailsAreMasked = true;
    }

    private bool CanApplyMask()
    {
        if (targetTerrain == null || targetTerrain.terrainData == null || fogWar == null || !fogWar.enabled)
        {
            return false;
        }

        TerrainData terrainData = targetTerrain.terrainData;
        if (originalDetailLayers == null ||
            terrainData.detailWidth != capturedDetailWidth ||
            terrainData.detailHeight != capturedDetailHeight ||
            terrainData.detailPrototypes.Length != capturedLayerCount)
        {
            CaptureCurrentDetails();
        }

        return originalDetailLayers != null && capturedLayerCount > 0;
    }

    private bool CurrentTerrainHasDetails(TerrainData terrainData)
    {
        if (terrainData == null || terrainData.detailPrototypes.Length == 0)
        {
            return false;
        }

        int width = terrainData.detailWidth;
        int height = terrainData.detailHeight;
        for (int layer = 0; layer < terrainData.detailPrototypes.Length; layer++)
        {
            int[,] detailLayer = terrainData.GetDetailLayer(0, 0, width, height, layer);
            if (LayerHasAnyDensity(detailLayer, width, height))
            {
                return true;
            }
        }

        return false;
    }

    private bool CapturedDetailsHaveAnyDensity()
    {
        if (originalDetailLayers == null)
        {
            return false;
        }

        for (int layer = 0; layer < originalDetailLayers.Length; layer++)
        {
            if (LayerHasAnyDensity(originalDetailLayers[layer], capturedDetailWidth, capturedDetailHeight))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LayerHasAnyDensity(int[,] layer, int width, int height)
    {
        if (layer == null)
        {
            return false;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (layer[y, x] > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void TryRegenerateTerrainDetails()
    {
        GridSystem gridSystem = FindAnyObjectByType<GridSystem>(FindObjectsInactive.Include);
        if (gridSystem == null)
        {
            return;
        }

        MethodInfo generateDetails = typeof(GridSystem).GetMethod("GenerateTerrainDetails", BindingFlags.Instance | BindingFlags.NonPublic);
        generateDetails?.Invoke(gridSystem, null);
    }

    private bool CheckVisibilitySafe(Vector3 worldPosition)
    {
        try
        {
            if (fogWar == null || !fogWar.CheckWorldGridRange(worldPosition))
            {
                return false;
            }

            return fogWar.CheckVisibility(worldPosition, visibilityPadding);
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
        catch (System.ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (System.IndexOutOfRangeException)
        {
            return false;
        }
    }

    private bool IsFogReady(TerrainData terrainData)
    {
        if (fogWar == null)
        {
            return false;
        }

        try
        {
            Vector3 center = targetTerrain.transform.position + new Vector3(terrainData.size.x * 0.5f, 0f, terrainData.size.z * 0.5f);
            center.y = targetTerrain.SampleHeight(center);
            if (!fogWar.CheckWorldGridRange(center))
            {
                return false;
            }

            fogWar.CheckVisibility(center, visibilityPadding);
            return true;
        }
        catch (System.NullReferenceException)
        {
            return false;
        }
        catch (System.ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (System.IndexOutOfRangeException)
        {
            return false;
        }
    }

    private void ResolveReferences()
    {
        if (targetTerrain == null)
        {
            targetTerrain = Terrain.activeTerrain;
        }

        if (fogWar == null)
        {
            fogWar = FindAnyObjectByType<csFogWar>(FindObjectsInactive.Include);
        }
    }

    private static Vector3 DetailToWorldPosition(Terrain terrain, TerrainData terrainData, float normalizedX, float normalizedZ)
    {
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 worldPosition = new Vector3(
            terrainPosition.x + normalizedX * terrainData.size.x,
            terrainPosition.y,
            terrainPosition.z + normalizedZ * terrainData.size.z);

        worldPosition.y = terrain.SampleHeight(worldPosition);
        return worldPosition;
    }
}
