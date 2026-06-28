using UnityEngine;
using UnityEngine.AI;

public class EnemySpawnPositionFinder
{
    private GridSystem _gridSystem;
    private int _lastSpawnEdgeChoice = -1;

    public EnemySpawnPositionFinder(GridSystem gridSystem)
    {
        _gridSystem = gridSystem;
    }

    public void SetGridSystem(GridSystem gridSystem)
    {
        _gridSystem = gridSystem;
    }

    public Vector3 GetSpawnPositionNear(Vector3 centerSpawnPos, float spawnRadius)
    {
        float safeRadius = Mathf.Max(0.1f, spawnRadius);
        Vector2 randomCircle = Random.insideUnitCircle * safeRadius;
        Vector3 spawnPos = centerSpawnPos + new Vector3(randomCircle.x, 0f, randomCircle.y);

        if (Terrain.activeTerrain != null)
        {
            spawnPos.y = Terrain.activeTerrain.SampleHeight(spawnPos);
        }

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, safeRadius * 2f, ~2))
        {
            spawnPos = hit.position;
        }

        return spawnPos;
    }

    public bool TryGetSpawnEdge(
        int candidateCount,
        float minSpawnDistanceFromCamera,
        bool avoidRepeatingSpawnEdge,
        out Vector3 centerSpawnPos,
        out string edgeName)
    {
        centerSpawnPos = Vector3.zero;
        edgeName = string.Empty;

        if (_gridSystem == null)
        {
            _gridSystem = Object.FindAnyObjectByType<GridSystem>();
            if (_gridSystem == null)
            {
                Debug.LogError("[EnemySpawnPositionFinder] Không tìm thấy GridSystem trong cảnh!");
                return false;
            }
        }

        int width = _gridSystem.GetWidth();
        int length = _gridSystem.GetLength();
        if (width <= 0 || length <= 0)
        {
            Debug.LogError($"[EnemySpawnPositionFinder] Kích thước GridSystem không hợp lệ: {width}x{length}");
            return false;
        }

        int bestEdgeChoice = 0;
        int bestEdgeX = 0;
        int bestEdgeZ = length - 1;
        float bestScore = float.NegativeInfinity;
        int safeCandidateCount = Mathf.Max(4, candidateCount);

        for (int i = 0; i < safeCandidateCount; i++)
        {
            int edgeChoice = Random.Range(0, 4);
            GetRandomPointOnEdge(edgeChoice, width, length, out int edgeX, out int edgeZ);

            Vector3 candidatePos = _gridSystem.GetWorldPosition(edgeX, edgeZ);
            float score = GetSpawnCandidateScore(candidatePos, edgeChoice, minSpawnDistanceFromCamera, avoidRepeatingSpawnEdge);
            if (score > bestScore)
            {
                bestScore = score;
                bestEdgeChoice = edgeChoice;
                bestEdgeX = edgeX;
                bestEdgeZ = edgeZ;
            }
        }

        centerSpawnPos = _gridSystem.GetWorldPosition(bestEdgeX, bestEdgeZ);
        edgeName = GetSpawnEdgeName(bestEdgeChoice);
        _lastSpawnEdgeChoice = bestEdgeChoice;
        return true;
    }

    private void GetRandomPointOnEdge(int edgeChoice, int width, int length, out int edgeX, out int edgeZ)
    {
        edgeX = 0;
        edgeZ = 0;

        switch (edgeChoice)
        {
            case 0:
                edgeX = Random.Range(0, width);
                edgeZ = length - 1;
                break;
            case 1:
                edgeX = Random.Range(0, width);
                edgeZ = 0;
                break;
            case 2:
                edgeX = 0;
                edgeZ = Random.Range(0, length);
                break;
            default:
                edgeX = width - 1;
                edgeZ = Random.Range(0, length);
                break;
        }
    }

    private float GetSpawnCandidateScore(Vector3 candidatePos, int edgeChoice, float minSpawnDistanceFromCamera, bool avoidRepeatingSpawnEdge)
    {
        float score = Random.Range(0f, 5f);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float safeMinDistance = Mathf.Max(0f, minSpawnDistanceFromCamera);
            float cameraDistance = Vector3.Distance(candidatePos, mainCamera.transform.position);
            score += Mathf.Min(cameraDistance, safeMinDistance) * 0.5f;
            if (cameraDistance < safeMinDistance)
            {
                score -= (safeMinDistance - cameraDistance) * 2f;
            }
        }

        BuildingManager buildingManager = BuildingManager.Instance != null ? BuildingManager.Instance : Object.FindAnyObjectByType<BuildingManager>();
        if (buildingManager != null && buildingManager.MainBuildingInstance != null)
        {
            float mainBuildingDistance = Vector3.Distance(candidatePos, buildingManager.MainBuildingInstance.transform.position);
            score += mainBuildingDistance * 0.15f;
        }

        if (avoidRepeatingSpawnEdge && edgeChoice == _lastSpawnEdgeChoice)
        {
            score -= 25f;
        }

        return score;
    }

    private string GetSpawnEdgeName(int edgeChoice)
    {
        switch (edgeChoice)
        {
            case 0:
                return "North";
            case 1:
                return "South";
            case 2:
                return "West";
            default:
                return "East";
        }
    }
}
