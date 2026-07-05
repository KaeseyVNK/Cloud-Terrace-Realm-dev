using UnityEngine;
using System.Collections.Generic;

public class JobBroker : MonoBehaviour
{
    public static JobBroker Instance;

    private List<Job> availableJobs = new List<Job>();
    private List<VillagerController> idleVillagers = new List<VillagerController>();

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // Hệ thống tự động giao việc đã được tắt để nhường chỗ cho điều khiển RTS
    }

    // Quét tìm cư dân đang rảnh
    void ScanForIdleVillagers()
    {
        idleVillagers.Clear();
        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        foreach (var v in allVillagers)
        {
            if (v.currentState == VillagerState.Idle)
                idleVillagers.Add(v);
        }
    }

    // Gán job cho cư dân rảnh
    void AssignJobs()
    {
        if (idleVillagers.Count == 0 || availableJobs.Count == 0) return;

        // 1. Thống kê khối lượng công việc và số lượng nhân sự hiện tại DỰA TRÊN TÀI NGUYÊN
        Dictionary<ResourceType, int> totalJobsCount = new Dictionary<ResourceType, int>();
        Dictionary<ResourceType, int> activeWorkersCount = new Dictionary<ResourceType, int>();

        foreach (var job in availableJobs)
        {
            if (!totalJobsCount.ContainsKey(job.targetResource))
            {
                totalJobsCount[job.targetResource] = 0;
                activeWorkersCount[job.targetResource] = 0;
            }
            totalJobsCount[job.targetResource]++;
        }

        VillagerController[] allVillagers = FindObjectsByType<VillagerController>(FindObjectsInactive.Exclude);
        foreach (var v in allVillagers)
        {
            if (v.currentState != VillagerState.Idle && v.currentJob != null)
            {
                if (activeWorkersCount.ContainsKey(v.currentJob.targetResource))
                {
                    activeWorkersCount[v.currentJob.targetResource]++;
                }
            }
        }

        // 2. Phân công từng người rảnh rỗi dựa trên sự cân bằng tài nguyên
        foreach (var villager in idleVillagers)
        {
            ResourceType? targetRes = null;
            float lowestRatio = float.MaxValue;
            
            // Tìm loại tài nguyên đang thiếu người thu thập nhất
            foreach (var kvp in totalJobsCount)
            {
                ResourceType type = kvp.Key;
                int total = kvp.Value;
                if (total == 0) continue;

                int workers = activeWorkersCount[type];
                float ratio = (float)workers / total;

                if (ratio < lowestRatio)
                {
                    lowestRatio = ratio;
                    targetRes = type;
                }
            }

            Job job = null;
            if (targetRes.HasValue)
            {
                job = GetClosestJobOfType(villager, targetRes.Value);
            }

            // Fallback: Nếu không tìm được (có thể do tất cả job của loại đó đều đang có người làm)
            if (job == null) 
            {
                job = GetNextAvailableJob(villager); 
            }

            if (job != null)
            {
                job.isAssigned = true; // Đánh dấu đã có người làm
                villager.AssignJob(job);
                GameLog.Log($"Phân bổ cân bằng: Dân làng được giao việc thu thập {job.targetResource} (Tỷ lệ nhân sự: {lowestRatio:F2})");
                
                // Cập nhật lại số lượng thợ để tính toán chính xác cho người rảnh tiếp theo
                if (activeWorkersCount.ContainsKey(job.targetResource))
                {
                    activeWorkersCount[job.targetResource]++;
                }
            }
        }
    }

    // Lấy job gần nhất CỦA MỘT LOẠI TÀI NGUYÊN CỤ THỂ (ưu tiên chưa ai nhận)
    Job GetClosestJobOfType(VillagerController villager, ResourceType targetType)
    {
        Job bestJob = null;
        float shortestDist = float.MaxValue;

        foreach (var job in availableJobs)
        {
            if (job.targetResource == targetType && !job.isAssigned) 
            {
                float dist = Vector3.Distance(villager.transform.position, job.position);
                if (dist < shortestDist)
                {
                    shortestDist = dist;
                    bestJob = job;
                }
            }
        }
        return bestJob;
    }

    // Lấy job gần nhất (ưu tiên job chưa ai nhận)
    Job GetNextAvailableJob(VillagerController villager)
    {
        Job bestJob = null;
        float shortestDist = float.MaxValue;

        // 1. Tìm job chưa có ai làm và gần nhất
        foreach (var job in availableJobs)
        {
            if (!job.isAssigned) 
            {
                float dist = Vector3.Distance(villager.transform.position, job.position);
                if (dist < shortestDist)
                {
                    shortestDist = dist;
                    bestJob = job;
                }
            }
        }

        if (bestJob != null) return bestJob;

        // 2. Nếu mọi job đều đã có người làm -> Cho phép làm chung!
        // Tìm job gần nhất dù đã có người làm
        shortestDist = float.MaxValue;
        foreach (var job in availableJobs)
        {
            float dist = Vector3.Distance(villager.transform.position, job.position);
            if (dist < shortestDist)
            {
                shortestDist = dist;
                bestJob = job;
            }
        }

        return bestJob;
    }

    // Thêm job mới (ZoningTool sẽ gọi hàm này)
    public void AddJob(ZoneType type, ResourceType resource, Vector3 position)
    {
        availableJobs.Add(new Job(type, resource, position));
        GameLog.Log("Them viec moi: " + type + " - " + resource + " tai " + position);
    }

    // Xóa job khi xong việc
    public void RemoveJob(Job job)
    {
        availableJobs.Remove(job);
    }
}