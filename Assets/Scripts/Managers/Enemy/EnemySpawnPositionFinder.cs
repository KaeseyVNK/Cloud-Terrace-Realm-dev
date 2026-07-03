using UnityEngine;
using UnityEngine.AI;
using FischlWorks_FogWar;

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

        // Tìm vị trí trung tâm (Nhà Chính, hoặc trung tâm bản đồ nếu chưa có Nhà Chính)
        Vector3 centerPos = Vector3.zero;
        BuildingManager buildingManager = BuildingManager.Instance != null ? BuildingManager.Instance : Object.FindAnyObjectByType<BuildingManager>();
        if (buildingManager != null && buildingManager.MainBuildingInstance != null)
        {
            centerPos = buildingManager.MainBuildingInstance.transform.position;
        }
        else
        {
            centerPos = _gridSystem.GetWorldPosition(width / 2, length / 2);
        }

        // Khoảng cách sinh quái lý tưởng từ Nhà Chính
        float minDist = 80f;
        float maxDist = 120f;

        int bestEdgeChoice = 0;
        int bestEdgeX = width / 2;
        int bestEdgeZ = length / 2;
        float bestScore = float.NegativeInfinity;
        int safeCandidateCount = Mathf.Max(4, candidateCount);

        for (int i = 0; i < safeCandidateCount; i++)
        {
            GetRandomPointAroundCenter(centerPos, minDist, maxDist, width, length, out int edgeX, out int edgeZ);

            Vector3 candidatePos = _gridSystem.GetWorldPosition(edgeX, edgeZ);
            int edgeChoice = GetEdgeChoiceFromDirection(centerPos, candidatePos);

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

    private void GetRandomPointAroundCenter(Vector3 center, float minDist, float maxDist, int width, int length, out int edgeX, out int edgeZ)
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist = Random.Range(minDist, maxDist);
        Vector3 candidatePos = center + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
        
        _gridSystem.GetXY(candidatePos, out int gX, out int gZ);
        
        // Giới hạn (Clamp) tọa độ lưới trong bản đồ để tránh sinh ngoài rìa trống
        edgeX = Mathf.Clamp(gX, 2, width - 3);
        edgeZ = Mathf.Clamp(gZ, 2, length - 3);
    }

    private int GetEdgeChoiceFromDirection(Vector3 fromCenter, Vector3 toPos)
    {
        Vector3 dir = (toPos - fromCenter).normalized;
        if (Mathf.Abs(dir.z) > Mathf.Abs(dir.x))
        {
            return dir.z > 0f ? 0 : 1; // 0 = North, 1 = South
        }
        else
        {
            return dir.x > 0f ? 3 : 2; // 3 = East, 2 = West
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

        // --- Đảm bảo sinh quái trong Sương Mù (Fog of War) ---
        var fogWar = Object.FindAnyObjectByType<csFogWar>();
        if (fogWar != null && fogWar.enabled)
        {
            // Kiểm tra nếu vị trí này người chơi nhìn thấy được (không bị sương mù che)
            bool isVisible = fogWar.CheckWorldGridRange(candidatePos) && 
                              fogWar.CheckVisibility(candidatePos, 0);
            if (isVisible)
            {
                // Trừ điểm cực kỳ nặng để ưu tiên các điểm tối trong sương mù
                score -= 100f;
            }
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
