using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CloudTerraceRealm.SaveSystem
{
public static class SaveGameSystem
{
    private const int CurrentVersion = 2;
    private const string SaveFileName = "autosave.json";
    private const string GameSceneName = "GameScene1";

    private static bool _resumeRequested;
    private static bool _isQuitting = false;
    public static bool IsLoading { get; private set; }

    public static bool ResumeRequested => _resumeRequested;
    public static bool IsQuitting => _isQuitting;
    public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);

    public static bool IsGameScene(string sceneName)
    {
        return sceneName == "GameScene_Dev2" || sceneName == "GameScene1" || sceneName == "GameScene";
    }

    public static void SetQuitting(bool quitting)
    {
        _isQuitting = quitting;
        GameLog.Log($"[SaveGame] SetQuitting called. _isQuitting={_isQuitting}");
    }

    public static bool HasSaveGame()
    {
        return SaveManager.Instance != null ? SaveManager.Instance.HasSave() : File.Exists(SavePath);
    }

    public static void RequestResume()
    {
        _resumeRequested = true;
    }

    public static void ConsumeResumeRequest()
    {
        _resumeRequested = false;
    }

    public static void DeleteSave()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.DeleteSave();
        }
        else if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
        }
        _resumeRequested = false;
    }

    public static bool SaveCurrentGame()
    {
        GameLog.Log($"[SaveGame] SaveCurrentGame called. _isQuitting={_isQuitting}");
        if (_isQuitting)
        {
            GameLog.Log("[SaveGame] Save request ignored because application is quitting.");
            return false;
        }

        if (!IsGameScene(SceneManager.GetActiveScene().name))
        {
            GameLog.Log($"[SaveGame] SaveCurrentGame ignored because active scene is not game scene: {SceneManager.GetActiveScene().name}");
            return false;
        }

        // Tắt tự động lưu nếu các Manager cốt lõi đã bị hủy trong quá trình tắt game (Unity Teardown)
        if (GameManager.Instance == null || TimeManager.Instance == null || BuildingManager.Instance == null)
        {
            return false;
        }

        // 1. Lưu toàn bộ game state (resources, buildings, units, events...) ra autosave.json
        try
        {
            SaveData fullData = CaptureCurrentGame();
            string json = JsonUtility.ToJson(fullData, true);
            File.WriteAllText(SavePath, json);
            GameLog.Log($"[SaveGame] Full game state saved to {SavePath}");
        }
        catch (Exception ex)
        {
            GameLog.LogError($"[SaveGame] Failed to write full save data: {ex}");
        }

        // 2. Lưu entity-based state (SaveDataV2: mapSeed + SaveableEntity components) ra autosave_v2.json
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveGame();
        }
        else
        {
            // Warning (not Error): SaveManager chưa được khởi tạo trong scene hiện tại.
            GameLog.LogWarning("[SaveGame] SaveManager.Instance is null. Skipping SaveDataV2 (SaveManager not yet initialized in scene).");
        }

        return true;
    }

    public static bool TryLoad(out SaveData data)
    {
        data = null;
        if (!File.Exists(SavePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.version != CurrentVersion)
            {
                return false;
            }

            EnsureCollections(data);
            return true;
        }
        catch (Exception ex)
        {
            GameLog.LogError($"[SaveGame] Failed to load save file: {ex}");
            return false;
        }
    }

    public static void ApplyLoadedGame(SaveData data)
    {
        if (data == null)
        {
            return;
        }

        IsLoading = true;
        try
        {
            // 1. Tải thẻ bài, tài nguyên, thời gian, camera và công nghệ
            ApplyCards(data);
            ApplyResources(data.resources);
            ApplyTime(data);
            ApplyCamera(data);
            ApplyResourceNodes(data.resourceNodes);
            
            // 2. Dựng lại công trình và hàng đợi sản xuất
            ApplyBuildings(data.buildings);
            ApplyTechnologies(data);
            ApplyProductionQueues(data.productionQueues);
            
            // 3. Tải các sự kiện và quái vật trên bản đồ
            // Hủy sạch quái vật, sự kiện và phế tích cũ để tránh trùng lặp
            ClearSceneEnemiesAndEventsForLoad();
            ClearSceneRuinsForLoad();

            // Spawn phế tích cổ, cổng hư không, đoàn thương nhân, và quái vật
            ApplyRuins(data.activeRuins);
            ApplyPortals(data.voidPortals);
            ApplyCaravans(data.caravans);
            ApplyActiveEnemies(data.activeEnemies);

            // Thiết lập lại số đêm
            if (EnemyManager.Instance != null)
            {
                SetPrivateField(EnemyManager.Instance, "_currentNightNumber", data.enemyNightNumber);
            }

            // 4. Khôi phục các đơn vị thuộc phe Player (Dân làng, lính gác)
            ApplyVillagers(data.villagers);
            ApplyCombatUnits(data.combatUnits);
            
            RecalculateCardStats();
            
            GameLog.Log($"[SaveGame] Resume data applied | resources={data.resources?.Count ?? 0}, resourceNodes={data.resourceNodes?.Count ?? 0}, buildings={data.buildings?.Count ?? 0}, villagers={data.villagers?.Count ?? 0}, combatUnits={data.combatUnits?.Count ?? 0}, productionQueues={data.productionQueues?.Count ?? 0}, ruins={data.activeRuins?.Count ?? 0}, portals={data.voidPortals?.Count ?? 0}, caravans={data.caravans?.Count ?? 0}, enemies={data.activeEnemies?.Count ?? 0}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static void ClearSceneEnemiesAndEventsForLoad()
    {
        // 1. Tiêu diệt toàn bộ kẻ địch trong Registry
        var allEnemies = new List<BaseCombatUnitController>(BaseCombatUnitController.Registry);
        foreach (var unit in allEnemies)
        {
            if (unit != null && unit.faction == UnitFaction.Enemy)
            {
                UnityEngine.Object.Destroy(unit.gameObject);
            }
        }

        if (EnemyManager.Instance != null)
        {
            EnemyManager.Instance.ActiveEnemies.Clear();
        }

        // 2. Dọn dẹp MapEventManager các danh sách và sự kiện
        if (MapEventManager.Instance != null)
        {
            var activeEvents = GetPrivateField<List<GameObject>>(MapEventManager.Instance, "_activeEvents");
            if (activeEvents != null)
            {
                foreach (var evt in activeEvents)
                {
                    if (evt != null) UnityEngine.Object.Destroy(evt);
                }
                activeEvents.Clear();
            }

            var activePortals = GetPrivateField<List<VoidPortal>>(MapEventManager.Instance, "_activePortals");
            activePortals?.Clear();

            var activeCaravans = GetPrivateField<List<MapEventManager.MerchantCaravanGroup>>(MapEventManager.Instance, "_activeCaravans");
            activeCaravans?.Clear();
        }
    }

    private static void ClearSceneRuinsForLoad()
    {
        var existingRuins = UnityEngine.Object.FindObjectsByType<AncientRuins>(FindObjectsInactive.Include);
        foreach (var ruin in existingRuins)
        {
            if (ruin == null) continue;
            
            // Nhả các ô lưới
            var cells = GetPrivateField<List<GridCell>>(ruin, "_occupiedCells");
            if (cells != null)
            {
                foreach (var cell in cells)
                {
                    if (cell != null && cell.resourceObject == ruin.gameObject)
                    {
                        cell.isBuildable = true;
                        cell.isWalkable = true;
                        cell.hasResource = false;
                        cell.resourceObject = null;
                    }
                }
            }
            // Hủy lính canh cũ
            var guards = GetPrivateField<List<BaseCombatUnitController>>(ruin, "_spawnedGuards");
            if (guards != null)
            {
                foreach (var guard in guards)
                {
                    if (guard != null) UnityEngine.Object.Destroy(guard.gameObject);
                }
            }
            // Hủy phế tích
            UnityEngine.Object.Destroy(ruin.gameObject);
        }
        
        if (AncientRuinsSpawner.Instance != null)
        {
            var activeList = GetPrivateField<List<AncientRuins>>(AncientRuinsSpawner.Instance, "_activeRuins");
            activeList?.Clear();
        }
    }

    private static void ApplyRuins(List<RuinSaveEntry> savedRuins)
    {
        if (savedRuins == null || AncientRuinsSpawner.Instance == null) return;
        
        GridSystem grid = UnityEngine.Object.FindAnyObjectByType<GridSystem>();
        if (grid == null) return;
        
        var activeList = GetPrivateField<List<AncientRuins>>(AncientRuinsSpawner.Instance, "_activeRuins");
        var ruinsPrefabs = GetPrivateField<GameObject[]>(AncientRuinsSpawner.Instance, "_ruinsPrefabs");
        
        foreach (var entry in savedRuins)
        {
            // Tìm prefab phù hợp
            GameObject prefab = null;
            if (ruinsPrefabs != null)
            {
                foreach (var p in ruinsPrefabs)
                {
                    if (p != null && p.name == entry.prefabName)
                    {
                        prefab = p;
                        break;
                    }
                }
            }
            
            if (prefab == null && ruinsPrefabs != null && ruinsPrefabs.Length > 0)
            {
                prefab = ruinsPrefabs[0]; // Fallback
            }
            
            if (prefab == null) continue;
            
            // Spawn phế tích
            GameObject ruinObj = UnityEngine.Object.Instantiate(prefab, entry.position, Quaternion.Euler(entry.rotation));
            ruinObj.name = prefab.name + "_" + Mathf.RoundToInt(entry.position.x) + "_" + Mathf.RoundToInt(entry.position.z);
            
            AncientRuins ruinComp = ruinObj.GetComponent<AncientRuins>();
            if (ruinComp == null) ruinComp = ruinObj.AddComponent<AncientRuins>();
            
            // Báo cho ruin không tự spawn guards
            ruinComp.BypassSpawnGuards = true;
            
            // Thiết lập trạng thái
            SetPrivateField(ruinComp, "_isCleared", entry.isCleared);
            SetPrivateField(ruinComp, "_isExplored", entry.isExplored);
            SetPrivateField(ruinComp, "_explorationProgress", entry.explorationProgress);
            
            // Tính toán ô bị chiếm
            float cellSize = grid.GetCellSize();
            int startX = Mathf.RoundToInt((entry.position.x - cellSize / 2f) / cellSize);
            int startZ = Mathf.RoundToInt((entry.position.z - cellSize / 2f) / cellSize);
            
            List<GridCell> cellsToOccupy = new List<GridCell>();
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    GridCell cell = grid.GetCell(startX + x, startZ + z);
                    if (cell != null)
                    {
                        cellsToOccupy.Add(cell);
                        cell.isBuildable = false;
                        cell.isWalkable = false;
                        cell.hasResource = false;
                        cell.resourceObject = ruinObj;
                    }
                }
            }
            
            ruinComp.SetOccupiedCells(cellsToOccupy);
            
            // Spawn guards nếu chưa bị dọn
            var spawnedGuards = GetPrivateField<List<BaseCombatUnitController>>(ruinComp, "_spawnedGuards") ?? new List<BaseCombatUnitController>();
            if (!entry.isCleared && entry.guards != null)
            {
                // Tạo container Guards
                GameObject guardsContainer = new GameObject("Guards");
                guardsContainer.transform.SetParent(ruinObj.transform);
                guardsContainer.transform.localPosition = Vector3.zero;
                
                foreach (var gEntry in entry.guards)
                {
                    GameObject guardPrefab = EnemyManager.Instance.GetEnemyPrefabByName(gEntry.guardPrefabName);
                    if (guardPrefab == null) continue;
                    
                    GameObject guardObj = UnityEngine.Object.Instantiate(guardPrefab, gEntry.position, Quaternion.Euler(gEntry.rotation));
                    guardObj.transform.SetParent(guardsContainer.transform);
                    
                    BaseCombatUnitController guardUnit = guardObj.GetComponent<BaseCombatUnitController>();
                    if (guardUnit != null)
                    {
                        guardUnit.faction = UnitFaction.Enemy;
                        guardUnit.currentHealth = Mathf.Clamp(gEntry.currentHealth, 1, guardUnit.maxHealth);
                        
                        if (guardUnit is EnemyUnitController enemyUnit)
                        {
                            enemyUnit.CanRetreat = false;
                            enemyUnit.IsGuard = true;
                            enemyUnit.prefabName = gEntry.guardPrefabName;
                        }
                        
                        spawnedGuards.Add(guardUnit);
                    }
                }
                
                SetPrivateField(ruinComp, "_spawnedGuards", spawnedGuards);
            }
            
            if (activeList != null)
            {
                activeList.Add(ruinComp);
            }
        }
    }

    private static void ApplyPortals(List<VoidPortalSaveEntry> savedPortals)
    {
        if (savedPortals == null || MapEventManager.Instance == null) return;
        
        GridSystem grid = UnityEngine.Object.FindAnyObjectByType<GridSystem>();
        if (grid == null) return;
        
        GameObject portalPrefab = GetPrivateField<GameObject>(MapEventManager.Instance, "_voidPortalPrefab");
        var activePortals = GetPrivateField<List<VoidPortal>>(MapEventManager.Instance, "_activePortals");
        var activeEvents = GetPrivateField<List<GameObject>>(MapEventManager.Instance, "_activeEvents");
        
        if (portalPrefab == null) return;
        
        foreach (var entry in savedPortals)
        {
            GameObject portalObj = UnityEngine.Object.Instantiate(portalPrefab, entry.position, Quaternion.identity);
            portalObj.name = $"VoidPortalEvent_{Mathf.RoundToInt(entry.position.x)}_{Mathf.RoundToInt(entry.position.z)}";
            
            VoidPortal portalComp = portalObj.GetComponent<VoidPortal>();
            if (portalComp == null) portalComp = portalObj.AddComponent<VoidPortal>();
            
            portalComp.prefabName = portalPrefab.name;
            portalComp.currentHealth = Mathf.Clamp(entry.currentHealth, 1, portalComp.maxHealth);
            
            // Tính toán ô bị chiếm
            float cellSize = grid.GetCellSize();
            int startX = Mathf.RoundToInt((entry.position.x - cellSize / 2f) / cellSize);
            int startZ = Mathf.RoundToInt((entry.position.z - cellSize / 2f) / cellSize);
            
            List<GridCell> cellsToOccupy = new List<GridCell>();
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    GridCell cell = grid.GetCell(startX + x, startZ + z);
                    if (cell != null)
                    {
                        cellsToOccupy.Add(cell);
                        cell.isBuildable = false;
                        cell.isWalkable = false;
                        cell.hasResource = false;
                        cell.resourceObject = portalObj;
                    }
                }
            }
            
            portalComp.SetOccupiedCells(cellsToOccupy);
            
            activePortals?.Add(portalComp);
            activeEvents?.Add(portalObj);
        }
    }

    private static void ApplyCaravans(List<CaravanSaveEntry> savedCaravans)
    {
        if (savedCaravans == null || MapEventManager.Instance == null) return;
        
        GameObject caravanPrefab = GetPrivateField<GameObject>(MapEventManager.Instance, "_merchantCaravanPrefab");
        var activeCaravans = GetPrivateField<List<MapEventManager.MerchantCaravanGroup>>(MapEventManager.Instance, "_activeCaravans");
        var activeEvents = GetPrivateField<List<GameObject>>(MapEventManager.Instance, "_activeEvents");
        
        if (caravanPrefab == null) return;
        
        foreach (var entry in savedCaravans)
        {
            var group = new MapEventManager.MerchantCaravanGroup
            {
                startPos = entry.startPos,
                destPos = entry.destPos,
                size = entry.size,
                ambush30Triggered = entry.ambush30Triggered,
                ambush60Triggered = entry.ambush60Triggered,
                ambush90Triggered = entry.ambush90Triggered
            };
            
            for (int i = 0; i < entry.units.Count; i++)
            {
                var uEntry = entry.units[i];
                GameObject caravanObj = UnityEngine.Object.Instantiate(caravanPrefab, uEntry.position, Quaternion.Euler(uEntry.rotation));
                caravanObj.name = $"MerchantCaravan_{entry.size}_{i}";
                
                MerchantCaravanUnit caravanUnit = caravanObj.GetComponent<MerchantCaravanUnit>();
                if (caravanUnit == null) caravanUnit = caravanObj.AddComponent<MerchantCaravanUnit>();
                
                caravanUnit.currentHealth = Mathf.Clamp(uEntry.currentHealth, 1, caravanUnit.maxHealth);
                caravanUnit.prefabName = caravanPrefab.name;
                caravanUnit.SetDestination(entry.destPos);
                
                group.units.Add(caravanUnit);
                activeEvents?.Add(caravanObj);
            }
            
            activeCaravans?.Add(group);
        }
    }

    private static void ApplyActiveEnemies(List<EnemySaveEntry> savedEnemies)
    {
        if (savedEnemies == null || EnemyManager.Instance == null) return;
        
        foreach (var entry in savedEnemies)
        {
            GameObject prefab = EnemyManager.Instance.GetEnemyPrefabByName(entry.prefabName);
            if (prefab == null) continue;
            
            GameObject enemyObj = PoolManager.Instance.Spawn(prefab, entry.position, Quaternion.Euler(entry.rotation));
            enemyObj.name = entry.objectName;
            
            EnemyUnitController enemyCtrl = enemyObj.GetComponent<EnemyUnitController>();
            if (enemyCtrl == null) enemyCtrl = enemyObj.AddComponent<EnemyUnitController>();
            
            enemyCtrl.OnSpawnedFromPool();
            enemyCtrl.prefabName = entry.prefabName;
            enemyCtrl.currentHealth = Mathf.Clamp(entry.currentHealth, 1, enemyCtrl.maxHealth);
            
            // Warp NavMeshAgent
            var agent = enemyCtrl.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.Warp(entry.position);
            }
            
            // Nếu trời tối, tiếp tục lệnh đi công nhà chính
            if (TimeManager.Instance != null && TimeManager.Instance.IsNight)
            {
                var mainHouse = UnityEngine.Object.FindAnyObjectByType<MainBuildingCombatTarget>();
                if (mainHouse != null && agent != null && agent.enabled)
                {
                    agent.SetDestination(mainHouse.transform.position);
                }
            }
            
            var activeList = GetPrivateField<List<EnemyUnitController>>(EnemyManager.Instance, "_activeEnemies");
            if (activeList != null && !activeList.Contains(enemyCtrl))
            {
                activeList.Add(enemyCtrl);
            }
        }
    }

    private static SaveData CaptureCurrentGame()
    {
        SaveData data = new SaveData
        {
            version = CurrentVersion,
            savedAtUtc = DateTime.UtcNow.ToString("O")
        };

        if (TimeManager.Instance != null)
        {
            data.dayCount = TimeManager.Instance.dayCount;
            data.currentTime = TimeManager.Instance.currentTime;
        }

        if (Camera.main != null)
        {
            data.hasCamera = true;
            data.cameraPosition = Camera.main.transform.position;
            data.cameraRotation = Camera.main.transform.eulerAngles;
        }

        if (ResourceManager.Instance != null)
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                data.resources.Add(new ResourceEntry
                {
                    type = type,
                    amount = ResourceManager.Instance.GetResourceAmount(type)
                });
            }
        }

        CaptureCardState(data);
        CaptureTechnologyState(data);
        CaptureResourceNodes(data);
        if (BuildingManager.Instance != null)
        {
            foreach (KeyValuePair<GameObject, BuildingData> kvp in BuildingManager.Instance.BuildingDataMap)
            {
                GameObject building = kvp.Key;
                BuildingData buildingData = kvp.Value;
                if (building == null || buildingData == null)
                {
                    continue;
                }

                data.buildings.Add(new BuildingEntry
                {
                    buildingName = buildingData.buildingName,
                    position = building.transform.position,
                    rotation = building.transform.eulerAngles,
                    currentHealth = TryGetCurrentHealth(building),
                    constructionProgress = TryGetConstructionProgress(building),
                    isCompleted = TryGetConstructionCompleted(building)
                });
            }
        }

        CaptureProductionQueues(data);

        VillagerController[] villagers = UnityEngine.Object.FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        foreach (VillagerController villager in villagers)
        {
            if (villager == null || !villager.gameObject.activeInHierarchy)
            {
                continue;
            }

            var targetBuilding = GetPrivateField<ConstructibleBuilding>(villager, "_targetBuilding");
            var currentJob = GetPrivateField<Job>(villager, "_currentJob");
            var targetRuins = GetPrivateField<AncientRuins>(villager, "_targetRuins");
            var targetRiceField = GetPrivateField<RiceField>(villager, "_targetRiceField");
            var navAgent = villager.GetComponent<UnityEngine.AI.NavMeshAgent>();

            string tType = "None";
            Vector3 tPos = Vector3.zero;

            if (targetBuilding != null)
            {
                tType = "Building";
                tPos = targetBuilding.transform.position;
            }
            else if (targetRuins != null)
            {
                tType = "Ruins";
                tPos = targetRuins.transform.position;
            }
            else if (targetRiceField != null)
            {
                tType = "RiceField";
                tPos = targetRiceField.transform.position;
            }
            else if (currentJob != null)
            {
                tType = "Resource";
                tPos = currentJob.position;
            }
            else if (navAgent != null && navAgent.enabled && navAgent.hasPath)
            {
                tType = "Move";
                tPos = navAgent.destination;
            }

            data.villagers.Add(new UnitEntry
            {
                unitName = villager.name,
                position = villager.transform.position,
                rotation = villager.transform.eulerAngles,
                villagerState = GetPrivateField<VillagerState>(villager, "_currentState").ToString(),
                inventory = CaptureVillagerInventory(villager),
                targetType = tType,
                targetPosition = tPos
            });
        }

        foreach (BaseCombatUnitController unit in BaseCombatUnitController.Registry)
        {
            if (!IsSavableCombatUnit(unit))
            {
                continue;
            }

            data.combatUnits.Add(new CombatUnitEntry
            {
                unitName = unit.unitName,
                objectName = unit.name,
                position = unit.transform.position,
                rotation = unit.transform.eulerAngles,
                currentHealth = unit.currentHealth
            });
        }

        // Capture EnemyManager night number
        if (EnemyManager.Instance != null)
        {
            data.enemyNightNumber = GetPrivateField<int>(EnemyManager.Instance, "_currentNightNumber");
        }

        CaptureAncientRuins(data);
        CaptureVoidPortals(data);
        CaptureCaravans(data);
        CaptureActiveEnemies(data);

        return data;
    }

    private static void CaptureAncientRuins(SaveData data)
    {
        var ruins = UnityEngine.Object.FindObjectsByType<AncientRuins>(FindObjectsInactive.Include);
        foreach (var ruin in ruins)
        {
            if (ruin == null || !ruin.gameObject.activeInHierarchy) continue;
            
            // Lấy tên prefab từ name bằng cách tách phần số tọa độ
            string prefabName = ruin.gameObject.name.Split('_')[0];
            
            var entry = new RuinSaveEntry
            {
                prefabName = prefabName,
                position = ruin.transform.position,
                rotation = ruin.transform.eulerAngles,
                isCleared = ruin.IsCleared,
                isExplored = ruin.IsExplored,
                explorationProgress = ruin.ExplorationProgress
            };
            
            if (!ruin.IsCleared)
            {
                var guards = GetPrivateField<List<BaseCombatUnitController>>(ruin, "_spawnedGuards");
                if (guards != null)
                {
                    foreach (var guard in guards)
                    {
                        if (guard == null || guard.currentState == CombatState.Dead) continue;
                        
                        string guardPrefab = guard.prefabName;
                        if (string.IsNullOrEmpty(guardPrefab))
                        {
                            guardPrefab = guard.gameObject.name.Replace("(Clone)", "").Split('_')[0].Trim();
                        }
                        
                        entry.guards.Add(new GuardSaveEntry
                        {
                            guardPrefabName = guardPrefab,
                            position = guard.transform.position,
                            rotation = guard.transform.eulerAngles,
                            currentHealth = guard.currentHealth
                        });
                    }
                }
            }
            
            data.activeRuins.Add(entry);
        }
    }

    private static void CaptureVoidPortals(SaveData data)
    {
        var portals = UnityEngine.Object.FindObjectsByType<VoidPortal>(FindObjectsInactive.Include);
        foreach (var portal in portals)
        {
            if (portal == null || portal.currentState == CombatState.Dead) continue;
            
            data.voidPortals.Add(new VoidPortalSaveEntry
            {
                position = portal.transform.position,
                currentHealth = portal.currentHealth
            });
        }
    }

    private static void CaptureCaravans(SaveData data)
    {
        if (MapEventManager.Instance == null) return;
        
        var caravans = GetPrivateField<List<MapEventManager.MerchantCaravanGroup>>(MapEventManager.Instance, "_activeCaravans");
        if (caravans == null) return;
        
        foreach (var caravan in caravans)
        {
            if (caravan == null || caravan.isFinished || !caravan.IsAlive()) continue;
            
            var entry = new CaravanSaveEntry
            {
                startPos = caravan.startPos,
                destPos = caravan.destPos,
                size = caravan.size,
                ambush30Triggered = caravan.ambush30Triggered,
                ambush60Triggered = caravan.ambush60Triggered,
                ambush90Triggered = caravan.ambush90Triggered
            };
            
            foreach (var unit in caravan.units)
            {
                if (unit == null || unit.currentState == CombatState.Dead) continue;
                
                entry.units.Add(new CaravanUnitSaveEntry
                {
                    position = unit.transform.position,
                    rotation = unit.transform.eulerAngles,
                    currentHealth = unit.currentHealth
                });
            }
            
            data.caravans.Add(entry);
        }
    }

    private static void CaptureActiveEnemies(SaveData data)
    {
        foreach (BaseCombatUnitController unit in BaseCombatUnitController.Registry)
        {
            if (unit == null || unit.faction != UnitFaction.Enemy || unit.currentState == CombatState.Dead || !unit.gameObject.activeInHierarchy)
            {
                continue;
            }
            
            if (unit is VoidPortal) continue;
            if (IsRuinGuard(unit)) continue;
            
            string pName = unit.prefabName;
            if (string.IsNullOrEmpty(pName))
            {
                pName = unit.gameObject.name.Replace("(Clone)", "").Split('_')[0].Trim();
            }
            
            data.activeEnemies.Add(new EnemySaveEntry
            {
                prefabName = pName,
                objectName = unit.gameObject.name,
                position = unit.transform.position,
                rotation = unit.transform.eulerAngles,
                currentHealth = unit.currentHealth
            });
        }
    }

    private static bool IsRuinGuard(BaseCombatUnitController unit)
    {
        if (unit == null) return false;
        if (unit is EnemyUnitController enemyUnit && enemyUnit.IsGuard)
        {
            if (unit.transform.parent != null && unit.transform.parent.parent != null && unit.transform.parent.parent.GetComponent<AncientRuins>() != null)
            {
                return true;
            }
        }
        return false;
    }

    private static void EnsureCollections(SaveData data)
    {
        data.resources ??= new List<ResourceEntry>();
        data.resourceNodes ??= new List<ResourceNodeEntry>();
        data.buildings ??= new List<BuildingEntry>();
        data.villagers ??= new List<UnitEntry>();
        data.combatUnits ??= new List<CombatUnitEntry>();
        data.productionQueues ??= new List<ProductionQueueEntry>();
        data.unlockedTechnologyIds ??= new List<string>();
        data.unlockedCardIds ??= new List<string>();
        data.activeRuins ??= new List<RuinSaveEntry>();
        data.voidPortals ??= new List<VoidPortalSaveEntry>();
        data.caravans ??= new List<CaravanSaveEntry>();
        data.activeEnemies ??= new List<EnemySaveEntry>();
    }

    private static bool IsSavableCombatUnit(BaseCombatUnitController unit)
    {
        if (unit == null || unit.faction != UnitFaction.Player || !unit.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (unit is BuildingCombatTarget || unit is MainBuildingCombatTarget || unit is MerchantCaravanUnit)
        {
            return false;
        }

        return unit.GetComponent<ConstructibleBuilding>() == null;
    }

    private static int TryGetCurrentHealth(GameObject building)
    {
        BaseCombatUnitController combatTarget = building != null ? building.GetComponent<BaseCombatUnitController>() : null;
        return combatTarget != null ? combatTarget.currentHealth : 0;
    }

    private static float TryGetConstructionProgress(GameObject building)
    {
        ConstructibleBuilding constructible = building != null ? building.GetComponent<ConstructibleBuilding>() : null;
        return constructible != null ? constructible.CurrentProgress : 1f;
    }

    private static bool TryGetConstructionCompleted(GameObject building)
    {
        ConstructibleBuilding constructible = building != null ? building.GetComponent<ConstructibleBuilding>() : null;
        return constructible == null || constructible.IsCompleted;
    }

    private static void CaptureResourceNodes(SaveData data)
    {
        foreach (ResourceNode node in ResourceNode.Registry)
        {
            if (node == null || !node.gameObject.activeInHierarchy)
            {
                continue;
            }

            data.resourceNodes.Add(new ResourceNodeEntry
            {
                type = node.ResourceType,
                quantity = node.CurrentQuantity,
                position = node.transform.position
            });
        }
    }

    private static void CaptureProductionQueues(SaveData data)
    {
        foreach (BuildingProduction production in BuildingProduction.Registry)
        {
            if (production == null || !production.gameObject.activeInHierarchy)
            {
                continue;
            }

            ProductionQueueEntry entry = new ProductionQueueEntry
            {
                buildingPosition = production.transform.position,
                hasRallyPoint = production.HasRallyPoint,
                rallyPoint = production.RallyPoint,
                currentProductionTimer = production.CurrentProductionTimer,
                currentUnitName = GetUnitSaveName(production.CurrentProducingUnit)
            };

            foreach (UnitData unit in production.ProductionQueue)
            {
                string unitName = GetUnitSaveName(unit);
                if (!string.IsNullOrEmpty(unitName))
                {
                    entry.queuedUnitNames.Add(unitName);
                }
            }

            if (!string.IsNullOrEmpty(entry.currentUnitName) || entry.queuedUnitNames.Count > 0 || entry.hasRallyPoint)
            {
                data.productionQueues.Add(entry);
            }
        }
    }

    private static void CaptureTechnologyState(SaveData data)
    {
        if (!TechnologyManager.HasInstance)
        {
            return;
        }

        HashSet<string> unlockedIds = GetPrivateField<HashSet<string>>(TechnologyManager.Instance, "_unlockedTechnologyIds");
        if (unlockedIds == null)
        {
            return;
        }

        foreach (string id in unlockedIds)
        {
            if (!string.IsNullOrEmpty(id))
            {
                data.unlockedTechnologyIds.Add(id);
            }
        }
    }

    private static void CaptureCardState(SaveData data)
    {
        if (CardManager.Instance == null)
        {
            return;
        }

        foreach (UpgradeCardData card in CardManager.Instance.UnlockedCards)
        {
            string cardId = GetCardSaveId(card);
            if (!string.IsNullOrEmpty(cardId))
            {
                data.unlockedCardIds.Add(cardId);
            }
        }

        UpgradeCardData decree = CardManager.Instance.ActiveDecreeCard;
        data.activeDecreeCardId = GetCardSaveId(decree);
    }

    private static List<ResourceEntry> CaptureVillagerInventory(VillagerController villager)
    {
        List<ResourceEntry> inventory = new List<ResourceEntry>();
        Dictionary<ResourceType, int> rawInventory = GetPrivateField<Dictionary<ResourceType, int>>(villager, "_inventory");
        if (rawInventory == null)
        {
            return inventory;
        }

        foreach (KeyValuePair<ResourceType, int> kvp in rawInventory)
        {
            if (kvp.Value > 0)
            {
                inventory.Add(new ResourceEntry { type = kvp.Key, amount = kvp.Value });
            }
        }

        return inventory;
    }

    private static void ApplyResources(List<ResourceEntry> resources)
    {
        if (ResourceManager.Instance == null || resources == null)
        {
            return;
        }

        foreach (ResourceEntry entry in resources)
        {
            int current = ResourceManager.Instance.GetResourceAmount(entry.type);
            int delta = entry.amount - current;
            if (delta > 0)
            {
                ResourceManager.Instance.AddResource(entry.type, delta);
            }
            else if (delta < 0)
            {
                ResourceManager.Instance.TryConsumeResource(entry.type, -delta);
            }
        }
    }

    private static void ApplyTime(SaveData data)
    {
        if (TimeManager.Instance == null)
        {
            return;
        }

        TimeManager.Instance.dayCount = Mathf.Max(1, data.dayCount);
        TimeManager.Instance.currentTime = Mathf.Max(0f, data.currentTime);
    }

    private static void ApplyCamera(SaveData data)
    {
        if (!data.hasCamera || Camera.main == null)
        {
            return;
        }

        Camera.main.transform.position = data.cameraPosition;
        Camera.main.transform.eulerAngles = data.cameraRotation;
    }

    private static void ApplyResourceNodes(List<ResourceNodeEntry> savedNodes)
    {
        if (savedNodes == null)
        {
            return;
        }

        List<ResourceNode> currentNodes = new List<ResourceNode>(ResourceNode.Registry);
        foreach (ResourceNode node in currentNodes)
        {
            if (node == null)
            {
                continue;
            }

            ResourceNodeEntry match = FindResourceNodeEntry(savedNodes, node);
            if (match == null)
            {
                DestroyResourceNodeForLoad(node);
                continue;
            }

            node.CurrentQuantity = Mathf.Max(0, match.quantity);
            if (node.CurrentQuantity <= 0)
            {
                DestroyResourceNodeForLoad(node);
            }
        }
    }

    private static void DestroyResourceNodeForLoad(ResourceNode node)
    {
        if (node == null)
        {
            return;
        }

        GridCell cell = node.OccupiedCell;
        if (cell != null && cell.resourceObject == node.gameObject)
        {
            cell.hasResource = false;
            cell.resourceObject = null;
            cell.isBuildable = true;
            cell.isWalkable = true;
        }

        UnityEngine.Object.Destroy(node.gameObject);
    }

    private static ResourceNodeEntry FindResourceNodeEntry(List<ResourceNodeEntry> entries, ResourceNode node)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            ResourceNodeEntry entry = entries[i];
            if (entry.type == node.ResourceType && Vector3.SqrMagnitude(entry.position - node.transform.position) <= 1f)
            {
                return entry;
            }
        }

        return null;
    }

    private static void ApplyBuildings(List<BuildingEntry> buildings)
    {
        if (buildings == null || BuildingManager.Instance == null)
        {
            GameLog.LogWarning("[SaveGame] ApplyBuildings aborted: buildings is null or BuildingManager.Instance is null");
            return;
        }

        BuildingManager buildingManager = BuildingManager.Instance;
        GridSystem grid = UnityEngine.Object.FindAnyObjectByType<GridSystem>();
        if (grid == null)
        {
            GameLog.LogError("[SaveGame] ApplyBuildings aborted: GridSystem is null");
            return;
        }

        // Tiêu diệt sạch sẽ toàn bộ công trình cũ trong scene (cả pre-placed và đã đăng ký) để tránh trùng lặp
        var allBuildings = UnityEngine.Object.FindObjectsByType<ConstructibleBuilding>(FindObjectsInactive.Include);
        GameLog.Log($"[SaveGame] Destroying {allBuildings.Length} existing buildings in scene");
        foreach (ConstructibleBuilding cb in allBuildings)
        {
            if (cb != null && cb.gameObject != null)
            {
                if (buildingManager.BuildingDataMap.ContainsKey(cb.gameObject))
                {
                    buildingManager.DestroyBuilding(cb.gameObject);
                }
                else
                {
                    UnityEngine.Object.Destroy(cb.gameObject);
                }
            }
        }

        // Đảm bảo xóa sạch các dictionary trong BuildingManager
        var dataMap = GetPrivateField<Dictionary<GameObject, BuildingData>>(buildingManager, "_buildingDataMap");
        dataMap?.Clear();
        var structures = GetPrivateField<Dictionary<GridCell, GameObject>>(buildingManager, "_builtStructures");
        structures?.Clear();
        var counts = GetPrivateField<Dictionary<BuildingData, int>>(buildingManager, "_builtBuildingCounts");
        counts?.Clear();
        SetPrivateField(buildingManager, "_mainBuildingInstance", null);

        GameLog.Log($"[SaveGame] Re-instantiating {buildings.Count} buildings from save data");
        foreach (BuildingEntry entry in buildings)
        {
            if (string.IsNullOrEmpty(entry.buildingName))
            {
                GameLog.LogWarning("[SaveGame] Building entry has empty/null name, skipping");
                continue;
            }

            BuildingData data = FindBuildingData(entry.buildingName);
            if (data == null)
            {
                GameLog.LogError($"[SaveGame] FindBuildingData returned null for '{entry.buildingName}'! mainData='{(buildingManager.MainBuildingData != null ? buildingManager.MainBuildingData.buildingName : "null")}', availableCount={buildingManager.AvailableBuildings.Count}");
                continue;
            }
            if (data.buildingPrefab == null)
            {
                GameLog.LogError($"[SaveGame] Building prefab is null for '{entry.buildingName}'");
                continue;
            }

            int rotationIndex = Mathf.RoundToInt(entry.rotation.y / 90f) % 4;
            if (rotationIndex < 0)
            {
                rotationIndex += 4;
            }

            Vector2Int size = data.buildingSize;
            if (rotationIndex % 2 != 0)
            {
                size = new Vector2Int(size.y, size.x);
            }

            float cellSize = Mathf.Max(0.01f, grid.GetCellSize());
            int startX = Mathf.RoundToInt(entry.position.x / cellSize - (size.x - 1) * 0.5f);
            int startZ = Mathf.RoundToInt(entry.position.z / cellSize - (size.y - 1) * 0.5f);

            GameObject instance = UnityEngine.Object.Instantiate(data.buildingPrefab, entry.position, Quaternion.Euler(entry.rotation));
            instance.name = entry.buildingName;
            buildingManager.RegisterSpawnedBuilding(instance, data, startX, startZ, rotationIndex);
            
            if (data == buildingManager.MainBuildingData)
            {
                SetPrivateField(buildingManager, "_mainBuildingInstance", instance);
                GameLog.Log($"[SaveGame] Assigned new Main Building instance to BuildingManager: {instance.name}");
            }

            ApplyBuildingRuntimeState(instance, entry);
            GameLog.Log($"[SaveGame] Recreated building: '{entry.buildingName}' at position {entry.position}");
        }
    }

    private static void ApplyBuildingRuntimeState(GameObject building, BuildingEntry entry)
    {
        BaseCombatUnitController combatTarget = building != null ? building.GetComponent<BaseCombatUnitController>() : null;
        if (combatTarget != null && entry.currentHealth > 0)
        {
            combatTarget.currentHealth = Mathf.Clamp(entry.currentHealth, 1, combatTarget.maxHealth);
        }

        ApplyConstructionState(building, entry);
    }

    private static void ApplyConstructionState(GameObject building, BuildingEntry entry)
    {
        ConstructibleBuilding constructible = building != null ? building.GetComponent<ConstructibleBuilding>() : null;
        if (constructible == null)
        {
            return;
        }

        constructible.CurrentProgress = Mathf.Clamp01(entry.constructionProgress);
        constructible.IsCompleted = entry.isCompleted;
        if (entry.isCompleted)
        {
            constructible.CurrentProgress = 1f;
        }
        constructible.RefreshVisualPosition();
    }

    private static GameObject FindBuildingNear(BuildingEntry entry)
    {
        if (BuildingManager.Instance == null)
        {
            return null;
        }

        foreach (KeyValuePair<GameObject, BuildingData> kvp in BuildingManager.Instance.BuildingDataMap)
        {
            GameObject building = kvp.Key;
            BuildingData data = kvp.Value;
            if (building == null || data == null || data.buildingName != entry.buildingName)
            {
                continue;
            }

            if (Vector3.SqrMagnitude(building.transform.position - entry.position) <= 1f)
            {
                return building;
            }
        }

        return null;
    }

    private static BuildingData FindBuildingData(string buildingName)
    {
        if (BuildingManager.Instance == null)
        {
            return null;
        }

        BuildingData mainData = BuildingManager.Instance.MainBuildingData;
        if (mainData != null && mainData.buildingName == buildingName)
        {
            return mainData;
        }

        foreach (BuildingData data in BuildingManager.Instance.AvailableBuildings)
        {
            if (data != null && data.buildingName == buildingName)
            {
                return data;
            }
        }

        // Bổ sung: Tìm kiếm cấu hình Chợ trung lập (Neutral Market) từ GameManager
        if (GameManager.Instance != null)
        {
            BuildingData neutralData = GetPrivateField<BuildingData>(GameManager.Instance, "_neutralMarketData");
            if (neutralData != null && neutralData.buildingName == buildingName)
            {
                return neutralData;
            }
        }

        return null;
    }

    private static void ApplyProductionQueues(List<ProductionQueueEntry> queueEntries)
    {
        if (queueEntries == null)
        {
            return;
        }

        foreach (ProductionQueueEntry entry in queueEntries)
        {
            BuildingProduction production = FindProductionNear(entry.buildingPosition);
            if (production == null)
            {
                continue;
            }

            Queue<UnitData> queue = GetPrivateField<Queue<UnitData>>(production, "_productionQueue");
            if (queue == null)
            {
                continue;
            }

            queue.Clear();

            UnitData currentUnit = FindUnitData(entry.currentUnitName);
            SetPrivateField(production, "_currentProducingUnit", currentUnit);
            SetPrivateField(production, "_currentProductionTimer", Mathf.Max(0f, entry.currentProductionTimer));
            SetPrivateField(production, "_isProducing", currentUnit != null);

            foreach (string unitName in entry.queuedUnitNames)
            {
                UnitData unit = FindUnitData(unitName);
                if (unit != null)
                {
                    queue.Enqueue(unit);
                }
            }

            if (entry.hasRallyPoint)
            {
                production.SetRallyPoint(entry.rallyPoint);
            }
        }
    }

    private static BuildingProduction FindProductionNear(Vector3 position)
    {
        foreach (BuildingProduction production in BuildingProduction.Registry)
        {
            if (production != null && Vector3.SqrMagnitude(production.transform.position - position) <= 1f)
            {
                return production;
            }
        }

        return null;
    }

    private static void ApplyTechnologies(SaveData data)
    {
        if (data == null || data.unlockedTechnologyIds == null || data.unlockedTechnologyIds.Count == 0)
        {
            return;
        }

        TechnologyManager manager = TechnologyManager.Instance;
        foreach (string id in data.unlockedTechnologyIds)
        {
            TechnologyData tech = FindTechnologyData(id);
            if (tech != null)
            {
                manager.Unlock(tech);
            }
        }
    }

    private static void ApplyCards(SaveData data)
    {
        if (data == null || CardManager.Instance == null)
        {
            return;
        }

        MethodInfo applyEffects = typeof(CardManager).GetMethod("ApplyCardEffects", BindingFlags.Instance | BindingFlags.NonPublic);
        if (applyEffects != null && data.unlockedCardIds != null)
        {
            foreach (string cardId in data.unlockedCardIds)
            {
                UpgradeCardData card = FindCardData(cardId);
                if (card != null && !IsCardAlreadyUnlocked(card))
                {
                    applyEffects.Invoke(CardManager.Instance, new object[] { card, false });
                }
            }
        }

        UpgradeCardData decree = FindCardData(data.activeDecreeCardId);
        if (decree != null)
        {
            SetPrivateField(CardManager.Instance, "_activeDecreeCard", decree);
        }
    }

    private static void RecalculateCardStats()
    {
        if (CardManager.Instance == null)
        {
            return;
        }

        MethodInfo recalculate = typeof(CardManager).GetMethod("RecalculateAllUnitStats", BindingFlags.Instance | BindingFlags.NonPublic);
        recalculate?.Invoke(CardManager.Instance, null);
    }

    private static void ApplyVillagers(List<UnitEntry> villagers)
    {
        if (villagers == null || GameManager.Instance == null)
        {
            return;
        }

        // 1. Thu thập danh sách dân làng hiện tại đang hoạt động
        List<VillagerController> existing = GetActiveVillagers();

        // 2. Nếu thiếu dân làng so với dữ liệu load, bổ sung thêm
        for (int i = existing.Count; i < villagers.Count; i++)
        {
            GameManager.Instance.AddVillager(villagers[i].position);
        }

        // 3. Nếu thừa dân làng, tiêu diệt bớt và loại khỏi danh sách (tránh lỗi quét lại đối tượng đang bị hủy)
        existing = GetActiveVillagers();
        if (existing.Count > villagers.Count)
        {
            for (int i = villagers.Count; i < existing.Count; i++)
            {
                if (existing[i] != null)
                {
                    UnityEngine.Object.Destroy(existing[i].gameObject);
                }
            }
            existing.RemoveRange(villagers.Count, existing.Count - villagers.Count);
        }

        // 4. Định vị lại vị trí, hướng quay và hòm đồ cho dân làng
        int count = Mathf.Min(existing.Count, villagers.Count);
        for (int i = 0; i < count; i++)
        {
            if (existing[i] == null)
            {
                continue;
            }

            // Warp NavMeshAgent thay vì set transform.position trực tiếp để tránh lỗi đồng bộ vật lý NavMesh
            var agent = existing[i].GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                agent.Warp(villagers[i].position);
            }
            else
            {
                existing[i].transform.position = villagers[i].position;
            }

            existing[i].transform.eulerAngles = villagers[i].rotation;
            ApplyVillagerInventory(existing[i], villagers[i].inventory);

            if (System.Enum.TryParse<VillagerState>(villagers[i].villagerState, true, out var parsedState))
            {
                // Tạm đặt tất cả thành Idle trước, RestoreVillagerCommand sẽ đặt lại state đúng sau delay
                SetPrivateField(existing[i], "_currentState", VillagerState.Idle);
            }
            else
            {
                SetPrivateField(existing[i], "_currentState", VillagerState.Idle);
            }

            // Delay restore commands to next frame so NavMeshAgent is stable after Warp
            if (!string.IsNullOrEmpty(villagers[i].targetType) && villagers[i].targetType != "None")
            {
                var villagerRef = existing[i];
                var entryRef = villagers[i];
                villagerRef.StartCoroutine(DelayedRestoreVillagerCommand(villagerRef, entryRef));
            }
        }
    }

    private static System.Collections.IEnumerator DelayedRestoreVillagerCommand(VillagerController villager, UnitEntry entry)
    {
        // Wait 2 frames for NavMeshAgent to properly initialize after Warp
        yield return null;
        yield return null;

        if (villager == null || !villager.gameObject.activeInHierarchy)
        {
            yield break;
        }

        RestoreVillagerCommand(villager, entry);
    }

    private static void RestoreVillagerCommand(VillagerController villager, UnitEntry entry)
    {
        if (villager == null || entry == null || string.IsNullOrEmpty(entry.targetType) || entry.targetType == "None")
        {
            return;
        }

        Debug.Log($"[SaveGame] RestoreVillagerCommand: {villager.name} | type={entry.targetType} | pos={entry.targetPosition}");

        if (entry.targetType == "Building")
        {
            var targetObj = FindClosestObjectOfType<ConstructibleBuilding>(entry.targetPosition);
            Debug.Log($"[SaveGame]   -> Building found: {(targetObj != null ? targetObj.name : "NULL")}");
            if (targetObj != null)
            {
                villager.CommandBuild(targetObj);
            }
        }
        else if (entry.targetType == "Resource")
        {
            var targetObj = FindClosestObjectOfType<ResourceNode>(entry.targetPosition);
            Debug.Log($"[SaveGame]   -> ResourceNode found: {(targetObj != null ? targetObj.name + " (" + targetObj.ResourceType + ")" : "NULL")}");
            if (targetObj != null)
            {
                villager.CommandGather(targetObj, null);
            }
        }
        else if (entry.targetType == "Ruins")
        {
            var targetObj = FindClosestObjectOfType<AncientRuins>(entry.targetPosition);
            Debug.Log($"[SaveGame]   -> Ruins found: {(targetObj != null ? targetObj.name : "NULL")}");
            if (targetObj != null)
            {
                villager.CommandExplore(targetObj);
            }
        }
        else if (entry.targetType == "RiceField")
        {
            var targetObj = FindClosestObjectOfType<RiceField>(entry.targetPosition);
            Debug.Log($"[SaveGame]   -> RiceField found: {(targetObj != null ? targetObj.name : "NULL")}");
            if (targetObj != null)
            {
                villager.CommandFarm(targetObj);
            }
        }
        else if (entry.targetType == "Move")
        {
            Debug.Log($"[SaveGame]   -> Moving to {entry.targetPosition}");
            villager.CommandMoveTo(entry.targetPosition);
        }
    }

    private static T FindClosestObjectOfType<T>(Vector3 position, float maxDistance = 50f) where T : MonoBehaviour
    {
        T[] objects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
        T closest = null;
        float minDistance = maxDistance * maxDistance;
        foreach (var obj in objects)
        {
            float dist = (obj.transform.position - position).sqrMagnitude;
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = obj;
            }
        }
        return closest;
    }

    private static List<VillagerController> GetActiveVillagers()
    {
        VillagerController[] found = UnityEngine.Object.FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        return new List<VillagerController>(found);
    }

    private static void ApplyVillagerInventory(VillagerController villager, List<ResourceEntry> inventory)
    {
        Dictionary<ResourceType, int> rawInventory = GetPrivateField<Dictionary<ResourceType, int>>(villager, "_inventory");
        if (rawInventory == null)
        {
            return;
        }

        rawInventory.Clear();
        int total = 0;
        if (inventory != null)
        {
            foreach (ResourceEntry entry in inventory)
            {
                if (entry.amount <= 0)
                {
                    continue;
                }

                rawInventory[entry.type] = entry.amount;
                total += entry.amount;
            }
        }

        SetPrivateField(villager, "_totalCarryAmount", total);
        SetPrivateField(villager, "_currentState", VillagerState.Idle);
    }

    private static void ApplyCombatUnits(List<CombatUnitEntry> units)
    {
        if (units == null)
        {
            return;
        }

        // 1. Tiêu diệt toàn bộ đơn vị chiến đấu hiện tại của Player trong cảnh (duyệt qua bản sao để tránh InvalidOperationException)
        var existingUnits = new List<BaseCombatUnitController>(BaseCombatUnitController.Registry);
        foreach (BaseCombatUnitController unit in existingUnits)
        {
            if (IsSavableCombatUnit(unit))
            {
                UnityEngine.Object.Destroy(unit.gameObject);
            }
        }

        // 2. Khởi tạo mới toàn bộ các đơn vị chiến đấu từ dữ liệu đã lưu
        foreach (CombatUnitEntry entry in units)
        {
            UnitData data = FindUnitData(entry.unitName);
            if (data != null && data.unitPrefab != null)
            {
                GameObject spawned = UnityEngine.Object.Instantiate(data.unitPrefab, entry.position, Quaternion.Euler(entry.rotation));
                spawned.transform.SetParent(GameManager.CombatUnitsContainer);
                BaseCombatUnitController controller = spawned.GetComponent<BaseCombatUnitController>();
                if (controller != null)
                {
                    controller.unitName = entry.unitName;
                    controller.currentHealth = Mathf.Clamp(entry.currentHealth, 1, controller.maxHealth);

                    // Warp NavMeshAgent thay vì set transform.position trực tiếp để tránh lỗi đồng bộ vật lý NavMesh
                    var agent = controller.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    if (agent != null && agent.enabled)
                    {
                        agent.Warp(entry.position);
                    }
                }
            }
        }
    }

    private static UnitData FindUnitData(string unitName)
    {
        if (string.IsNullOrEmpty(unitName) || BuildingManager.Instance == null)
        {
            return null;
        }

        UnitData result = FindUnitDataInBuilding(BuildingManager.Instance.MainBuildingData, unitName);
        if (result != null)
        {
            return result;
        }

        foreach (BuildingData building in BuildingManager.Instance.AvailableBuildings)
        {
            result = FindUnitDataInBuilding(building, unitName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static UnitData FindUnitDataInBuilding(BuildingData building, string unitName)
    {
        if (building == null || building.producibleUnits == null)
        {
            return null;
        }

        foreach (UnitData unit in building.producibleUnits)
        {
            if (unit == null)
            {
                continue;
            }

            if (unit.unitName == unitName || unit.name == unitName)
            {
                return unit;
            }
        }

        return null;
    }

    private static string GetUnitSaveName(UnitData unit)
    {
        if (unit == null)
        {
            return "";
        }

        return !string.IsNullOrEmpty(unit.unitName) ? unit.unitName : unit.name;
    }

    private static UpgradeCardData FindCardData(string cardId)
    {
        if (string.IsNullOrEmpty(cardId) || CardManager.Instance == null)
        {
            return null;
        }

        foreach (UpgradeCardData card in CardManager.Instance.AllCards)
        {
            if (GetCardSaveId(card) == cardId)
            {
                return card;
            }
        }

        return null;
    }

    private static bool IsCardAlreadyUnlocked(UpgradeCardData card)
    {
        foreach (UpgradeCardData unlocked in CardManager.Instance.UnlockedCards)
        {
            if (unlocked == card || GetCardSaveId(unlocked) == GetCardSaveId(card))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetCardSaveId(UpgradeCardData card)
    {
        if (card == null)
        {
            return "";
        }

        return !string.IsNullOrEmpty(card.cardId) ? card.cardId : card.name;
    }

    private static TechnologyData FindTechnologyData(string technologyId)
    {
        if (string.IsNullOrEmpty(technologyId))
        {
            return null;
        }

        foreach (BlacksmithResearch research in BlacksmithResearch.ActiveBlacksmiths)
        {
            if (research == null)
            {
                continue;
            }

            foreach (TechnologyData tech in research.AvailableTechnologies)
            {
                if (tech != null && tech.technologyId == technologyId)
                {
                    return tech;
                }
            }
        }

        foreach (TechnologyData tech in BlacksmithResearch.GlobalCardTechnologies)
        {
            if (tech != null && tech.technologyId == technologyId)
            {
                return tech;
            }
        }

        return null;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        if (target == null)
        {
            return default;
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            return default;
        }

        object value = field.GetValue(target);
        return value is T typed ? typed : default;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        if (target == null)
        {
            return;
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, value);
        }
    }
}

public class SaveGameRuntime : MonoBehaviour
{
    private static SaveGameRuntime _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
        {
            return;
        }

        GameObject host = new GameObject("SaveGameRuntime");
        UnityEngine.Object.DontDestroyOnLoad(host);
        _instance = host.AddComponent<SaveGameRuntime>();
    }

    private void Awake()
    {
        Application.quitting += HandleQuitting;
    }

    private void OnDestroy()
    {
        Application.quitting -= HandleQuitting;
    }

    private void HandleQuitting()
    {
        GameLog.Log("[SaveGame] Application.quitting event triggered. Saving game before teardown...");
        SaveGameSystem.SaveCurrentGame();
        SaveGameSystem.SetQuitting(true);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameLog.Log($"[SaveGame] HandleSceneLoaded triggered for scene: {scene.name}. ResumeRequested: {SaveGameSystem.ResumeRequested}");
        if (SaveGameSystem.IsGameScene(scene.name))
        {
            StartCoroutine(ApplyResumeOnNextFrame());
        }
    }

    private IEnumerator ApplyResumeOnNextFrame()
    {
        GameLog.Log("[SaveGame] ApplyResumeOnNextFrame coroutine started.");
        yield return null;

        if (!SaveGameSystem.ResumeRequested)
        {
            yield break;
        }

        GameLog.Log("[SaveGame] Resume requested. Loading full game state...");

        // Retry loop: chờ SaveManager khởi tạo nếu chưa sẵn
        int retries = 0;
        while (SaveManager.Instance == null && retries < 3)
        {
            GameLog.LogWarning($"[SaveGame] SaveManager.Instance is null (attempt {retries + 1}). Waiting one frame...");
            yield return null;
            retries++;
        }

        // 1. Khôi phục entity-based state (mapSeed + SaveableEntity components) từ autosave_v2.json nếu hệ thống V2 có mặt
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.LoadGame();
        }
        else
        {
            GameLog.LogWarning("[SaveGame] SaveManager.Instance still null after retries. Skipping optional SaveDataV2 load.");
        }

        // 2. Khôi phục toàn bộ game state (resources, buildings, units...) từ autosave.json
        if (SaveGameSystem.TryLoad(out SaveData fullData))
        {
            GameLog.Log("[SaveGame] Full save data loaded. Applying game state...");
            SaveGameSystem.ApplyLoadedGame(fullData);
        }
        else
        {
            GameLog.LogWarning("[SaveGame] No full save data found (autosave.json missing or version mismatch). Game will use default state.");
        }

        SaveGameSystem.ConsumeResumeRequest();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            // Bỏ qua nếu ứng dụng đang thoát (tránh lưu đè dữ liệu rỗng khi tắt game)
            if (SaveGameSystem.IsQuitting)
            {
                GameLog.Log("[SaveGame] OnApplicationPause ignored because application is quitting.");
                return;
            }
            GameLog.Log("[SaveGame] OnApplicationPause (true). Auto-saving game for mobile background state...");
            SaveGameSystem.SaveCurrentGame();
        }
    }

    private void OnApplicationQuit()
    {
        GameLog.Log("[SaveGame] OnApplicationQuit triggered.");
        
        // Trong Unity Editor, Application.quitting không được kích hoạt khi tắt Play Mode.
        // Do đó ta cần gọi SaveCurrentGame ở đây để tự động lưu trong Editor.
#if UNITY_EDITOR
        if (!SaveGameSystem.IsQuitting)
        {
            SaveGameSystem.SaveCurrentGame();
            SaveGameSystem.SetQuitting(true);
        }
#else
        // Trong bản Build, việc lưu đã được xử lý an toàn trong HandleQuitting (Application.quitting).
        // Ta chỉ cần đảm bảo cờ SetQuitting được bật.
        SaveGameSystem.SetQuitting(true);
#endif
    }
}

[Serializable]
public class SaveData
{
    public int version;
    public string savedAtUtc;
    public int dayCount;
    public float currentTime;
    public bool hasCamera;
    public Vector3 cameraPosition;
    public Vector3 cameraRotation;
    public List<ResourceEntry> resources = new List<ResourceEntry>();
    public List<ResourceNodeEntry> resourceNodes = new List<ResourceNodeEntry>();
    public List<BuildingEntry> buildings = new List<BuildingEntry>();
    public List<ProductionQueueEntry> productionQueues = new List<ProductionQueueEntry>();
    public List<string> unlockedTechnologyIds = new List<string>();
    public List<string> unlockedCardIds = new List<string>();
    public string activeDecreeCardId;
    public List<UnitEntry> villagers = new List<UnitEntry>();
    public List<CombatUnitEntry> combatUnits = new List<CombatUnitEntry>();

    // New Overhaul Fields
    public int enemyNightNumber;
    public List<RuinSaveEntry> activeRuins = new List<RuinSaveEntry>();
    public List<VoidPortalSaveEntry> voidPortals = new List<VoidPortalSaveEntry>();
    public List<CaravanSaveEntry> caravans = new List<CaravanSaveEntry>();
    public List<EnemySaveEntry> activeEnemies = new List<EnemySaveEntry>();
}

[Serializable]
public class ResourceEntry
{
    public ResourceType type;
    public int amount;
}

[Serializable]
public class ResourceNodeEntry
{
    public ResourceType type;
    public int quantity;
    public Vector3 position;
}

[Serializable]
public class UnitEntry
{
    public string unitName;
    public Vector3 position;
    public Vector3 rotation;
    public string villagerState;
    public List<ResourceEntry> inventory = new List<ResourceEntry>();
    public string targetType;
    public Vector3 targetPosition;
}

[Serializable]
public class BuildingEntry
{
    public string buildingName;
    public Vector3 position;
    public Vector3 rotation;
    public int currentHealth;
    public float constructionProgress;
    public bool isCompleted;
}

[Serializable]
public class ProductionQueueEntry
{
    public Vector3 buildingPosition;
    public string currentUnitName;
    public float currentProductionTimer;
    public List<string> queuedUnitNames = new List<string>();
    public bool hasRallyPoint;
    public Vector3 rallyPoint;
}

[Serializable]
public class CombatUnitEntry : UnitEntry
{
    public string objectName;
    public int currentHealth;
}

[Serializable]
public class RuinSaveEntry
{
    public string prefabName;
    public Vector3 position;
    public Vector3 rotation;
    public bool isCleared;
    public bool isExplored;
    public float explorationProgress;
    public List<GuardSaveEntry> guards = new List<GuardSaveEntry>();
}

[Serializable]
public class GuardSaveEntry
{
    public string guardPrefabName;
    public Vector3 position;
    public Vector3 rotation;
    public int currentHealth;
}

[Serializable]
public class VoidPortalSaveEntry
{
    public Vector3 position;
    public int currentHealth;
}

[Serializable]
public class CaravanSaveEntry
{
    public Vector3 startPos;
    public Vector3 destPos;
    public int size;
    public bool ambush30Triggered;
    public bool ambush60Triggered;
    public bool ambush90Triggered;
    public List<CaravanUnitSaveEntry> units = new List<CaravanUnitSaveEntry>();
}

[Serializable]
public class CaravanUnitSaveEntry
{
    public Vector3 position;
    public Vector3 rotation;
    public int currentHealth;
}

[Serializable]
public class EnemySaveEntry
{
    public string prefabName;
    public string objectName;
    public Vector3 position;
    public Vector3 rotation;
    public int currentHealth;
}
}
