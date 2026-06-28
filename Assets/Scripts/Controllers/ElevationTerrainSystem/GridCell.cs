using UnityEngine; 

public class GridCell
{
    public int x; 
    public int z; 
    public int elevation;

    public bool isWalkable; 
    public bool isBuildable;
    public bool hasBridge; // Đánh dấu ô này là một phần của cây cầu

    // --- TÀI NGUYÊN TRÊN Ô ĐẤT ---
    public bool hasResource;
    public ResourceType resourceType;
    public GameObject resourceObject;

    // --- BIẾN SỬ DỤNG CHO THUẬT TOÁN A* PATHFINDING ---
    public int gCost; // Khoảng cách từ điểm Bắt đầu
    public int hCost; // Ước lượng khoảng cách đến điểm Kết thúc
    public int fCost; // Tổng gCost + hCost
    public GridCell cameFromNode; // Trỏ về ô trước đó để dò lại đường đi

    public void CalculateFCost()
    {
        fCost = gCost + hCost;
    }
    // ---------------------------------------------------

    public GridCell(int x, int z, int elevation = 0)
    {
        this.x = x;
        this.z = z;
        this.elevation = elevation;
        this.isWalkable = true; 
        this.isBuildable = true;    
    }

}