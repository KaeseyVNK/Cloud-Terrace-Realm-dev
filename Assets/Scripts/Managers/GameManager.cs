using System;
using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    private static GameManager s_instance;
    public static GameManager Instance => s_instance;

    private GridSystem _gridSystem;
    private BuildingManager _buildingManager;

    [Header("Starting Entities")]
    [UnityEngine.Serialization.FormerlySerializedAs("villagerPrefab")]
    [SerializeField] private GameObject _villagerPrefab;
    
    [UnityEngine.Serialization.FormerlySerializedAs("startingVillagers")]
    [SerializeField] private int _startingVillagers = 3;
    
    [UnityEngine.Serialization.FormerlySerializedAs("flatAreaRadius")]
    [SerializeField] private int _flatAreaRadius = 5;

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
    }

    void Start()
    {
        InitGame();
    }

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
                    
                    Instantiate(_villagerPrefab, spawnPos, Quaternion.identity);
                    spawnedCount++;
                }
            }
        }
    }
}
