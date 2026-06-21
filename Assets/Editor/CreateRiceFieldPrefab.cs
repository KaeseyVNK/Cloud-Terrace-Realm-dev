using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class CreateRiceFieldPrefab : EditorWindow
{
    [MenuItem("Tools/Cloud Terrace/Setup Rice Field Building")]
    public static void SetupRiceField()
    {
        // 1. Tạo RiceHarvestNode Prefab phụ (chứa ResourceNode giới hạn 1 slot)
        string harvestPrefabPath = "Assets/Prefabs/rice_harvest_node.prefab";
        GameObject harvestGO = new GameObject("RiceHarvestNode");
        
        // Thêm BoxCollider làm vùng click chuột gặt lúa
        BoxCollider col = harvestGO.AddComponent<BoxCollider>();
        col.size = new Vector3(2f, 1f, 2f);
        col.center = new Vector3(0f, 0.5f, 0f);

        // Thêm ResourceNode và cấu hình
        ResourceNode resourceNode = harvestGO.AddComponent<ResourceNode>();
        resourceNode.ResourceType = ResourceType.Food;
        resourceNode.CurrentQuantity = 100;
        resourceNode.MaxHarvestSlots = 1; // 1 ruộng do đúng 1 dân đảm nhiệm!

        // Tạo thư mục nếu chưa có
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        // Lưu thành Prefab
        GameObject harvestPrefab = PrefabUtility.SaveAsPrefabAsset(harvestGO, harvestPrefabPath);
        DestroyImmediate(harvestGO);
        Debug.Log($"[RiceField Setup] Đã tạo thành công Harvest Node tại: {harvestPrefabPath}");

        // 2. Tạo RiceField Prefab chính
        string fieldPrefabPath = "Assets/Prefabs/rice_field.prefab";
        GameObject fieldGO = new GameObject("rice_field");

        // Thêm các component xây dựng cơ bản
        RiceField riceField = fieldGO.AddComponent<RiceField>();
        ConstructibleBuilding cb = fieldGO.AddComponent<ConstructibleBuilding>();
        cb.TotalBuildTime = 15f;
        cb.buildingGridSize = new Vector2Int(2, 2);

        // Thêm BoxCollider của ruộng lúa
        BoxCollider fieldCol = fieldGO.AddComponent<BoxCollider>();
        fieldCol.size = new Vector3(4f, 0.2f, 4f);
        fieldCol.center = new Vector3(0f, 0.1f, 0f);

        // Tạo cụm EmptyState
        GameObject emptyState = GameObject.CreatePrimitive(PrimitiveType.Cube);
        emptyState.name = "EmptyState";
        emptyState.transform.SetParent(fieldGO.transform);
        emptyState.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        emptyState.transform.localScale = new Vector3(4f, 0.1f, 4f);
        DestroyImmediate(emptyState.GetComponent<BoxCollider>()); // Xóa collider của cube con để tránh xung đột

        // Thêm Material bùn đất cho EmptyState (nếu có sẵn)
        Renderer emptyRen = emptyState.GetComponent<Renderer>();
        if (emptyRen != null)
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                urpShader = Shader.Find("Standard"); // Fallback
            }
            Material mudMat = new Material(urpShader);
            mudMat.name = "RiceField_Mud_Material";
            
            // Trong URP, màu cơ bản là _BaseColor, trong Standard là _Color. Ta gán cả 2 để đảm bảo
            if (mudMat.HasProperty("_BaseColor"))
            {
                mudMat.SetColor("_BaseColor", new Color(0.25f, 0.15f, 0.08f));
            }
            else
            {
                mudMat.color = new Color(0.25f, 0.15f, 0.08f);
            }

            // Gán độ nhám cao (roughness) và giảm bóng (metallic) cho giống bùn đất
            if (mudMat.HasProperty("_Roughness"))
            {
                mudMat.SetFloat("_Roughness", 0.9f);
            }
            else if (mudMat.HasProperty("_Glossiness")) // Standard Shader sử dụng Glossiness (ngược với Roughness)
            {
                mudMat.SetFloat("_Glossiness", 0.1f);
            }

            mudMat.enableInstancing = true;

            // Lưu Material thành asset để tránh bị mất lúc chạy
            string mudMatPath = "Assets/Prefabs/rice_field_mud.mat";
            AssetDatabase.CreateAsset(mudMat, mudMatPath);
            emptyRen.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(mudMatPath);
        }

        // Tìm prefab cây lúa gốc của người dùng
        GameObject ricePlantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/rice_plant.prefab");
        if (ricePlantPrefab == null)
        {
            Debug.LogWarning("[RiceField Setup] Không tìm thấy prefab cây lúa tại 'Assets/Prefabs/rice_plant.prefab'. Vui lòng đảm bảo file tồn tại.");
        }

        // Tạo cụm GrowingState (Lúa xanh)
        GameObject growingState = new GameObject("GrowingState");
        growingState.transform.SetParent(fieldGO.transform);
        growingState.transform.localPosition = Vector3.zero;

        // Tạo cụm RipeState (Lúa chín vàng)
        GameObject ripeState = new GameObject("RipeState");
        ripeState.transform.SetParent(fieldGO.transform);
        ripeState.transform.localPosition = Vector3.zero;

        // Xếp lưới lúa 3x3 (9 cây) trong ruộng
        if (ricePlantPrefab != null)
        {
            float spacing = 1.1f;
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3 localPos = new Vector3(x * spacing, 0f, z * spacing);

                    // 1. Tạo cây cho GrowingState
                    GameObject greenPlant = PrefabUtility.InstantiatePrefab(ricePlantPrefab, growingState.transform) as GameObject;
                    greenPlant.transform.localPosition = localPos;
                    greenPlant.transform.localRotation = ricePlantPrefab.transform.localRotation;

                    // Đảm bảo bật GPU Instancing trên các material của cây lúa để mượt game
                    Renderer[] greenRens = greenPlant.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in greenRens)
                    {
                        if (r.sharedMaterial != null)
                        {
                            r.sharedMaterial.enableInstancing = true;
                        }
                    }

                    // 2. Tạo cây cho RipeState
                    GameObject goldPlant = PrefabUtility.InstantiatePrefab(ricePlantPrefab, ripeState.transform) as GameObject;
                    goldPlant.transform.localPosition = localPos;
                    goldPlant.transform.localRotation = ricePlantPrefab.transform.localRotation;

                    // Đổi màu chín vàng cho RipeState
                    Renderer[] goldRens = goldPlant.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in goldRens)
                    {
                        if (r.sharedMaterial != null)
                        {
                            // Tạo phiên bản material chín vàng độc lập
                            Material goldMat = new Material(r.sharedMaterial);
                            goldMat.name = r.sharedMaterial.name + "_Gold";
                            goldMat.color = new Color(0.95f, 0.75f, 0.15f); // Màu chín vàng tươi
                            goldMat.enableInstancing = true;
                            r.sharedMaterial = goldMat;
                        }
                    }
                }
            }
        }

        // Liên kết các cụm Visual vào RiceField script
        // Gán các trường serialized thông qua SerializedObject để an toàn
        SerializedObject so = new SerializedObject(riceField);
        so.FindProperty("_emptyVisual").objectReferenceValue = emptyState;
        so.FindProperty("_growingVisual").objectReferenceValue = growingState;
        so.FindProperty("_ripeVisual").objectReferenceValue = ripeState;
        so.FindProperty("_harvestNodePrefab").objectReferenceValue = harvestPrefab;
        so.FindProperty("_riceYield").intValue = 120; // 120 Food mỗi đợt
        so.FindProperty("_growingDuration").floatValue = 15f;
        so.FindProperty("_ripeDuration").floatValue = 15f;
        so.ApplyModifiedProperties();

        // Lưu thành Prefab
        GameObject fieldPrefab = PrefabUtility.SaveAsPrefabAsset(fieldGO, fieldPrefabPath);
        DestroyImmediate(fieldGO);
        Debug.Log($"[RiceField Setup] Đã tạo thành công Rice Field Prefab tại: {fieldPrefabPath}");

        // 3. Tạo BuildingData ScriptableObject cho Ruộng lúa
        string assetPath = "Assets/ScriptData/Building Data/Rice Field.asset";
        
        // Tạo thư mục ScriptData nếu chưa có
        if (!AssetDatabase.IsValidFolder("Assets/ScriptData"))
        {
            AssetDatabase.CreateFolder("Assets", "ScriptData");
        }
        if (!AssetDatabase.IsValidFolder("Assets/ScriptData/Building Data"))
        {
            AssetDatabase.CreateFolder("Assets/ScriptData", "Building Data");
        }

        BuildingData buildingData = AssetDatabase.LoadAssetAtPath<BuildingData>(assetPath);
        bool isNewAsset = false;
        if (buildingData == null)
        {
            buildingData = ScriptableObject.CreateInstance<BuildingData>();
            isNewAsset = true;
        }

        buildingData.buildingName = "Rice Field";
        buildingData.description = "A wet paddy field for producing food. Auto-regrows. Worked by 1 villager.";
        buildingData.category = BuildingCategory.Production;
        buildingData.buildingPrefab = fieldPrefab;
        buildingData.buildingSize = new Vector2Int(2, 2);
        buildingData.maxHealth = 400;
        buildingData.buildTime = 15f;
        buildingData.isInstantBuild = true;
        buildingData.isStorage = false;

        // Cấu hình chi phí xây dựng: 60 Gỗ
        ResourceCost cost = new ResourceCost
        {
            resourceType = ResourceType.Wood,
            amount = 60
        };
        buildingData.buildCosts = new List<ResourceCost> { cost };

        // Cập nhật Sprite Icon nếu có sẵn biểu tượng sản xuất lúa/lương thực
        Sprite foodIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ThirdAssets/fantasy_ui_asset_sheet_transparent_clean_3");
        if (foodIcon != null)
        {
            buildingData.icon = foodIcon;
        }

        if (isNewAsset)
        {
            AssetDatabase.CreateAsset(buildingData, assetPath);
        }
        else
        {
            EditorUtility.SetDirty(buildingData);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[RiceField Setup] Đã tạo thành công Building Data asset tại: {assetPath}");

        // 4. Liên kết Ruộng lúa với BuildingManager có sẵn trong dự án
        RegisterInBuildingManager(buildingData);

        AssetDatabase.Refresh();
        Debug.Log("[RiceField Setup] Done!");
    }

    private static void RegisterInBuildingManager(BuildingData riceFieldData)
    {
        // Quét tìm tất cả asset BuildingManager trong Project
        string[] guids = AssetDatabase.FindAssets("t:GameObject");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null)
            {
                BuildingManager bm = go.GetComponent<BuildingManager>();
                if (bm != null)
                {
                    // Đăng ký vào AvailableBuildings nếu chưa có
                    SerializedObject soManager = new SerializedObject(bm);
                    SerializedProperty listProp = soManager.FindProperty("_availableBuildings");
                    
                    bool alreadyExists = false;
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        if (listProp.GetArrayElementAtIndex(i).objectReferenceValue == riceFieldData)
                        {
                            alreadyExists = true;
                            break;
                        }
                    }

                    if (!alreadyExists)
                    {
                        listProp.InsertArrayElementAtIndex(listProp.arraySize);
                        listProp.GetArrayElementAtIndex(listProp.arraySize - 1).objectReferenceValue = riceFieldData;
                        soManager.ApplyModifiedProperties();
                        EditorUtility.SetDirty(go);
                        Debug.Log($"[RiceField Setup] Đã tự động thêm Ruộng Lúa vào danh sách AvailableBuildings của BuildingManager Prefab tại: {path}");
                    }
                }
            }
        }

        // Kiểm tra xem có BuildingManager nào đang hoạt động trong Scene hiện tại không
        BuildingManager activeManager = FindFirstObjectByType<BuildingManager>();
        if (activeManager != null)
        {
            if (!activeManager.AvailableBuildings.Contains(riceFieldData))
            {
                activeManager.AvailableBuildings.Add(riceFieldData);
                EditorUtility.SetDirty(activeManager);
                Debug.Log("[RiceField Setup] Đã thêm Ruộng Lúa vào BuildingManager hiện tại trong Scene.");
            }
        }
    }
}
