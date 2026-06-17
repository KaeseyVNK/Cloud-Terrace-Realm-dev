using UnityEngine;
using System.Collections.Generic;

public enum BuildingCategory
{
    Residential,
    Production,
    Goods,
    Cultural,
    Hospital,
    Military,
    Roads,
    Expansion
}

[CreateAssetMenu(fileName = "New Building Data", menuName = "Cloud Terrace/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Basic Info")]
    public string buildingName;
    [TextArea] public string description;

    [Header("UI")]
    public BuildingCategory category = BuildingCategory.Residential;
    public Sprite icon;

    [Header("Visuals")]
    public GameObject buildingPrefab;
    [Tooltip("Kích thước công trình (X và Z) tính bằng ô lưới")]
    public Vector2Int buildingSize = new Vector2Int(1, 1);

    [Header("Costs")]
    public List<ResourceCost> buildCosts;

    [Header("Stats")]
    public int maxHealth = 100;
    public int maxWorkers = 0; // Số dân tối đa có thể làm việc ở đây
    public float buildTime = 15f; // Thời gian cần xây dựng (giây) để hoàn thành công trình
    
    [Header("Special Settings")]
    [Tooltip("Nếu true, công trình sẽ được xây dựng ngay lập tức mà không cần dân và móng")]
    public bool isInstantBuild = false;
    
    [Header("Storage Settings")]
    public bool isStorage = false; // Đánh dấu nếu công trình này có thể chứa tài nguyên
    public List<ResourceType> acceptedResources; // Nếu trống và isStorage = true thì nhận MỌI LOẠI. Nếu có phần tử thì chỉ nhận những loại đó.

    [Header("Requirements")]
    [Tooltip("Các công trình bắt buộc phải có trước khi xây công trình này")]
    public List<BuildingData> requiredBuildings; 

    [Header("Production")]
    [Tooltip("Danh sách các Unit (lính/dân) có thể sản xuất tại đây")]
    public List<UnitData> producibleUnits;
}

[System.Serializable]
public class ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}
