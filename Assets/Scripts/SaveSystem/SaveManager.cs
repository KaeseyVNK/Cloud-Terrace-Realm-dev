using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;

namespace CloudTerraceRealm.SaveSystem
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        public bool IsLoadingSave { get; set; } = false;

        [Header("Save Settings")]
        [SerializeField] private string _saveFileName = "autosave_v2.json";
        
        [Header("Prefab Registry")]
        [SerializeField] private List<SaveableEntity> _prefabRegistry = new List<SaveableEntity>();

        private string SavePath => Path.Combine(Application.persistentDataPath, _saveFileName);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public bool HasSave()
        {
            return File.Exists(SavePath);
        }

        public void DeleteSave()
        {
            if (HasSave())
            {
                File.Delete(SavePath);
                GameLog.Log("[SaveManager] Save file deleted.");
            }
        }

        public void SaveGame()
        {
            try
            {
                GameLog.Log("[SaveManager] Starting SaveGame...");
                
                SaveDataV2 data = new SaveDataV2();
                data.savedAtUtc = DateTime.UtcNow.ToString("o");
                data.version = 2;

                var gridSystem = FindAnyObjectByType<GridSystem>();
                if (gridSystem != null)
                {
                    data.mapSeed = GetPrivateField<int>(gridSystem, "_mapSeed");
                }

                // Thu thập tất cả các SaveableEntity hiện có trong cảnh
                var allEntities = FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include);
                
                foreach (var entity in allEntities)
                {
                    if (entity == null) continue;

                    EntitySaveData entry = new EntitySaveData
                    {
                        saveID = entity.SaveID,
                        prefabID = entity.PrefabID,
                        position = entity.transform.position,
                        rotation = entity.transform.eulerAngles,
                        stateJson = entity.CaptureState()
                    };
                    data.entities.Add(entry);
                }

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SavePath, json);
                
                GameLog.Log($"[SaveManager] Saved game successfully to: {SavePath} | Entities: {data.entities.Count}");
            }
            catch (Exception ex)
            {
                GameLog.LogError($"[SaveManager] Error during saving: {ex}");
            }
        }

        public void LoadGame()
        {
            if (!HasSave())
            {
                GameLog.LogWarning("[SaveManager] Load requested but no save file found.");
                return;
            }

            try
            {
                IsLoadingSave = true;
                GameLog.Log("[SaveManager] Starting LoadGame...");
                
                string json = File.ReadAllText(SavePath);
                SaveDataV2 data = JsonUtility.FromJson<SaveDataV2>(json);
                if (data == null)
                {
                    GameLog.LogError("[SaveManager] Failed to deserialize SaveDataV2");
                    return;
                }

                // Khôi phục seed bản đồ vào GridSystem trước khi tải các thực thể
                var gridSystem = FindAnyObjectByType<GridSystem>();
                if (gridSystem != null)
                {
                    SetPrivateField(gridSystem, "_mapSeed", data.mapSeed);
                    SetPrivateField(gridSystem, "_useProceduralSeed", false);
                    gridSystem.GenerateFullProceduralMap();
                }

                // Chuyển dữ liệu nạp vào dictionary để tra cứu nhanh bằng SaveID
                var loadDict = new Dictionary<string, EntitySaveData>();
                foreach (var entityData in data.entities)
                {
                    if (!string.IsNullOrEmpty(entityData.saveID))
                    {
                        loadDict[entityData.saveID] = entityData;
                    }
                }

                // Tìm các thực thể hiện hữu trong cảnh
                var sceneEntities = FindObjectsByType<SaveableEntity>(FindObjectsInactive.Include);
                var entitiesToKeep = new List<SaveableEntity>();

                foreach (var sceneEntity in sceneEntities)
                {
                    if (sceneEntity == null) continue;

                    // Nếu đối tượng trong cảnh nằm trong danh sách lưu
                    if (loadDict.TryGetValue(sceneEntity.SaveID, out var savedState))
                    {
                        var agent = sceneEntity.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null && agent.enabled)
                        {
                            agent.Warp(savedState.position);
                        }
                        else
                        {
                            sceneEntity.transform.position = savedState.position;
                        }
                        sceneEntity.transform.rotation = Quaternion.Euler(savedState.rotation);
                        sceneEntity.RestoreState(savedState.stateJson);
                        
                        entitiesToKeep.Add(sceneEntity);
                        loadDict.Remove(sceneEntity.SaveID); // Đã xử lý xong đối tượng trong cảnh
                    }
                    else
                    {
                        // Nếu đối tượng có SaveID nhưng KHÔNG có trong file save
                        // (nghĩa là nó là đối tượng động đã bị phá hủy hoặc thu hoạch trong game)
                        if (!string.IsNullOrEmpty(sceneEntity.PrefabID))
                        {
                            Destroy(sceneEntity.gameObject);
                        }
                    }
                }

                // Các đối tượng còn lại trong loadDict là các đối tượng được sinh ra động trong game
                foreach (var kvp in loadDict)
                {
                    EntitySaveData savedState = kvp.Value;
                    if (string.IsNullOrEmpty(savedState.prefabID)) continue;

                    SaveableEntity prefab = FindPrefabInRegistry(savedState.prefabID);
                    if (prefab != null)
                    {
                        // Khởi tạo đối tượng mà không set vị trí ngay để tránh lỗi NavMesh warp trên đối tượng chưa active
                        SaveableEntity spawned = Instantiate(prefab);
                        
                        // Gán lại SaveID đã lưu trước khi phục hồi trạng thái
                        SetSaveIDViaReflection(spawned, savedState.saveID);

                        var agent = spawned.GetComponent<UnityEngine.AI.NavMeshAgent>();
                        if (agent != null && agent.enabled)
                        {
                            agent.Warp(savedState.position);
                        }
                        else
                        {
                            spawned.transform.position = savedState.position;
                        }
                        spawned.transform.rotation = Quaternion.Euler(savedState.rotation);
                        
                        spawned.RestoreState(savedState.stateJson);
                    }
                    else
                    {
                        GameLog.LogError($"[SaveManager] PrefabID '{savedState.prefabID}' not found in registry!");
                    }
                }

                // Liên kết lại lính canh với phế tích tương ứng sau khi load
                LinkGuardsToRuins();
                LinkEventsToMapEventManager();

                GameLog.Log("[SaveManager] Loaded game successfully.");
            }
            catch (Exception ex)
            {
                GameLog.LogError($"[SaveManager] Error during loading: {ex}");
            }
            finally
            {
                IsLoadingSave = false;
            }
        }

        private void LinkEventsToMapEventManager()
        {
            if (MapEventManager.Instance == null) return;

            var manager = MapEventManager.Instance;
            
            var activeEvents = GetPrivateField<List<GameObject>>(manager, "_activeEvents") ?? new List<GameObject>();
            var activePortals = GetPrivateField<List<VoidPortal>>(manager, "_activePortals") ?? new List<VoidPortal>();
            var activeCaravans = GetPrivateField<List<MapEventManager.MerchantCaravanGroup>>(manager, "_activeCaravans") ?? new List<MapEventManager.MerchantCaravanGroup>();
            
            activeEvents.Clear();
            activePortals.Clear();
            activeCaravans.Clear();

            // 1. Phục hồi các Cổng Hư Không (Portals)
            var portals = FindObjectsByType<VoidPortal>(FindObjectsInactive.Include);
            foreach (var portal in portals)
            {
                if (portal != null && portal.currentState != CombatState.Dead)
                {
                    activePortals.Add(portal);
                    activeEvents.Add(portal.gameObject);
                }
            }

            // 2. Phục hồi các Đoàn Xe Thồ (Caravans)
            var caravans = FindObjectsByType<MerchantCaravanUnit>(FindObjectsInactive.Include);
            
            // Gom nhóm các caravan unit theo startPos của chúng
            var caravanGroups = new Dictionary<Vector3, List<MerchantCaravanUnit>>();
            foreach (var caravan in caravans)
            {
                if (caravan != null && caravan.currentState != CombatState.Dead)
                {
                    if (!caravanGroups.ContainsKey(caravan.startPos))
                    {
                        caravanGroups[caravan.startPos] = new List<MerchantCaravanUnit>();
                    }
                    caravanGroups[caravan.startPos].Add(caravan);
                }
            }

            foreach (var kvp in caravanGroups)
            {
                var firstUnit = kvp.Value[0];
                var group = new MapEventManager.MerchantCaravanGroup
                {
                    units = kvp.Value,
                    startPos = firstUnit.startPos,
                    destPos = firstUnit.destPos,
                    size = firstUnit.caravanSize,
                    isFinished = firstUnit.isFinished,
                    ambush30Triggered = firstUnit.ambush30Triggered,
                    ambush60Triggered = firstUnit.ambush60Triggered,
                    ambush90Triggered = firstUnit.ambush90Triggered
                };

                activeCaravans.Add(group);
                foreach (var unit in kvp.Value)
                {
                    activeEvents.Add(unit.gameObject);
                }

                GameLog.Log($"[SaveManager] Reconstructed caravan group at startPos {firstUnit.startPos} with {kvp.Value.Count} units");
            }

            SetPrivateField(manager, "_activeEvents", activeEvents);
            SetPrivateField(manager, "_activePortals", activePortals);
            SetPrivateField(manager, "_activeCaravans", activeCaravans);
        }

        private SaveableEntity FindPrefabInRegistry(string prefabID)
        {
            foreach (var prefab in _prefabRegistry)
            {
                if (prefab != null && prefab.PrefabID == prefabID)
                {
                    return prefab;
                }
            }
            return null;
        }

        private void SetSaveIDViaReflection(SaveableEntity entity, string saveID)
        {
            var field = typeof(SaveableEntity).GetField("_saveID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(entity, saveID);
            }
        }

        private void LinkGuardsToRuins()
        {
            var ruins = FindObjectsByType<AncientRuins>(FindObjectsInactive.Include);
            var guards = FindObjectsByType<EnemyUnitController>(FindObjectsInactive.Include);
            
            foreach (var ruin in ruins)
            {
                if (ruin == null || ruin.IsCleared) continue;
                
                var spawnedList = GetPrivateField<List<BaseCombatUnitController>>(ruin, "_spawnedGuards") ?? new List<BaseCombatUnitController>();
                spawnedList.Clear();
                
                foreach (var guard in guards)
                {
                    if (guard != null && guard.IsGuard && Vector3.Distance(guard.transform.position, ruin.transform.position) <= 15f)
                    {
                        spawnedList.Add(guard);
                    }
                }
                
                SetPrivateField(ruin, "_spawnedGuards", spawnedList);
                GameLog.Log($"[SaveManager] Linked {spawnedList.Count} guards to AncientRuins at {ruin.transform.position}");
            }
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            if (target == null) return default;
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field != null ? (T)field.GetValue(target) : default;
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null) return;
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }
    }

    [Serializable]
    public class EntitySaveData
    {
        public string saveID;
        public string prefabID;
        public Vector3 position;
        public Vector3 rotation;
        public string stateJson;
    }

    [Serializable]
    public class SaveDataV2
    {
        public int version;
        public string savedAtUtc;
        public int mapSeed;
        public List<EntitySaveData> entities = new List<EntitySaveData>();
    }
}
