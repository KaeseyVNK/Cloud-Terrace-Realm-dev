using System.Collections.Generic;
using UnityEngine;

public class Pathfinding
{
    private const int MOVE_STRAIGHT_COST = 10;
    private const int MOVE_DIAGONAL_COST = 14;

    private GridSystem gridSystem;
    private int width;
    private int length;
    
    public Pathfinding(GridSystem gridSystem, int width, int length)
    {
        this.gridSystem = gridSystem;
        this.width = width;
        this.length = length;
    }

    public List<GridCell> FindPath(int startX, int startZ, int endX, int endZ)
    {
        GridCell startNode = gridSystem.GetCell(startX, startZ);
        GridCell endNode = gridSystem.GetCell(endX, endZ);

        if (startNode == null || endNode == null || !endNode.isWalkable)
        {
            return null; // Không hợp lệ hoặc điểm cắp tới bị chặn
        }

        List<GridCell> openList = new List<GridCell> { startNode };
        List<GridCell> closedList = new List<GridCell>();

        // Khởi tạo các giá trị A*
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < length; z++)
            {
                GridCell pathNode = gridSystem.GetCell(x, z);
                if (pathNode != null)
                {
                    pathNode.gCost = int.MaxValue;
                    pathNode.CalculateFCost();
                    pathNode.cameFromNode = null;
                }
            }
        }

        startNode.gCost = 0;
        startNode.hCost = CalculateDistanceCost(startNode, endNode);
        startNode.CalculateFCost();

        while (openList.Count > 0)
        {
            GridCell currentNode = GetLowestFCostNode(openList);

            if (currentNode == endNode)
            {
                // Đã đến đích! Dò ngược lại quãng đường.
                return CalculatePath(endNode);
            }

            openList.Remove(currentNode);
            closedList.Add(currentNode);

            foreach (GridCell neighbourNode in GetNeighbourList(currentNode))
            {
                if (closedList.Contains(neighbourNode)) continue; // Đã duyệt qua
                if (!neighbourNode.isWalkable)
                {
                    closedList.Add(neighbourNode);
                    continue; 
                }

                // Kiểm tra độ cao (Chỉ được phép leo dốc chênh lệch tối đa 1 bậc)
                int elevationDiff = Mathf.Abs(currentNode.elevation - neighbourNode.elevation);
                if (elevationDiff > 1) 
                {
                    closedList.Add(neighbourNode);
                    continue; 
                }
                
                int elevationPenalty = elevationDiff * 5; 

                int tentativeGCost = currentNode.gCost + CalculateDistanceCost(currentNode, neighbourNode) + elevationPenalty;
                
                if (tentativeGCost < neighbourNode.gCost)
                {
                    neighbourNode.cameFromNode = currentNode;
                    neighbourNode.gCost = tentativeGCost;
                    neighbourNode.hCost = CalculateDistanceCost(neighbourNode, endNode);
                    neighbourNode.CalculateFCost();

                    if (!openList.Contains(neighbourNode))
                    {
                        openList.Add(neighbourNode);
                    }
                }
            }
        }

        // Không tìm thấy đường
        return null;
    }

    private List<GridCell> GetNeighbourList(GridCell currentNode)
    {
        List<GridCell> neighbourList = new List<GridCell>();

        if (currentNode.x - 1 >= 0)
        {
            // Trái
            neighbourList.Add(gridSystem.GetCell(currentNode.x - 1, currentNode.z));
            // Trái Xuống
            if (currentNode.z - 1 >= 0) neighbourList.Add(gridSystem.GetCell(currentNode.x - 1, currentNode.z - 1));
            // Trái Lên
            if (currentNode.z + 1 < length) neighbourList.Add(gridSystem.GetCell(currentNode.x - 1, currentNode.z + 1));
        }
        
        if (currentNode.x + 1 < width)
        {
            // Phải
            neighbourList.Add(gridSystem.GetCell(currentNode.x + 1, currentNode.z));
            // Phải Xuống
            if (currentNode.z - 1 >= 0) neighbourList.Add(gridSystem.GetCell(currentNode.x + 1, currentNode.z - 1));
            // Phải Lên
            if (currentNode.z + 1 < length) neighbourList.Add(gridSystem.GetCell(currentNode.x + 1, currentNode.z + 1));
        }
        
        // Xuống
        if (currentNode.z - 1 >= 0) neighbourList.Add(gridSystem.GetCell(currentNode.x, currentNode.z - 1));
        // Lên
        if (currentNode.z + 1 < length) neighbourList.Add(gridSystem.GetCell(currentNode.x, currentNode.z + 1));

        return neighbourList;
    }

    private List<GridCell> CalculatePath(GridCell endNode)
    {
        List<GridCell> path = new List<GridCell>();
        path.Add(endNode);

        GridCell currentNode = endNode;
        while (currentNode.cameFromNode != null)
        {
            path.Add(currentNode.cameFromNode);
            currentNode = currentNode.cameFromNode;
        }

        path.Reverse();
        return path;
    }

    private int CalculateDistanceCost(GridCell a, GridCell b)
    {
        int xDistance = Mathf.Abs(a.x - b.x);
        int zDistance = Mathf.Abs(a.z - b.z);
        // Tính toán khoảng cách trên Lưới vuông (Phương pháp Diagonal Distance)
        int remaining = Mathf.Abs(xDistance - zDistance);
        return MOVE_DIAGONAL_COST * Mathf.Min(xDistance, zDistance) + MOVE_STRAIGHT_COST * remaining;
    }

    private GridCell GetLowestFCostNode(List<GridCell> pathNodeList)
    {
        GridCell lowestFCostNode = pathNodeList[0];
        for (int i = 1; i < pathNodeList.Count; i++)
        {
            if (pathNodeList[i].fCost < lowestFCostNode.fCost)
            {
                lowestFCostNode = pathNodeList[i];
            }
        }
        return lowestFCostNode;
    }
}