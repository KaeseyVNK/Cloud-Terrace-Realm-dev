using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Quản lý cơ chế đói lương thực của dân làng, kiểm tra và tiêu thụ lương thực hàng ngày.
/// Tự động khởi chạy khi load game.
/// </summary>
public class HungerSystem : MonoBehaviour
{
    public static HungerSystem Instance { get; private set; }

    public bool IsFoodShortage { get; private set; } = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // Tránh tạo trùng lặp nếu đã tồn tại trong scene
        if (FindAnyObjectByType<HungerSystem>() != null)
        {
            return;
        }

        GameObject go = new GameObject("HungerSystem");
        go.AddComponent<HungerSystem>();
        DontDestroyOnLoad(go);
        Debug.Log("[HungerSystem] Đã tự động khởi chạy hệ thống đói lương thực.");
    }

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

    private void Start()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
        }
        else
        {
            Debug.LogError("[HungerSystem] Không tìm thấy TimeManager.Instance để đăng ký sự kiện!");
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
        }
    }

    private void HandleDayChanged(int dayCount)
    {
        if (ResourceManager.Instance == null)
        {
            return;
        }

        // Lấy danh sách cư dân từ SpawnedVillagers tĩnh để bao gồm cả cư dân đang trú ẩn
        List<VillagerController> villagers = VillagerController.SpawnedVillagers;
        int foodNeeded = villagers.Count;

        if (foodNeeded <= 0)
        {
            IsFoodShortage = false;
            return;
        }

        int foodAvailable = ResourceManager.Instance.GetResourceAmount(ResourceType.Food);

        if (foodAvailable >= foodNeeded)
        {
            ResourceManager.Instance.TryConsumeResource(ResourceType.Food, foodNeeded);

            // Tất cả cư dân được ăn no
            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null)
                {
                    villagers[i].SetHungry(false);
                }
            }

            IsFoodShortage = false;
            Debug.Log($"[HungerSystem] Ngày {dayCount}: Đã tiêu thụ {foodNeeded} lương thực cho {villagers.Count} cư dân. Mọi người đều no bụng.");
        }
        else
        {
            // Thiếu hụt lương thực
            ResourceManager.Instance.TryConsumeResource(ResourceType.Food, foodAvailable);

            // Nuôi sống X dân làng đầu tiên, số còn lại bị đói
            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null)
                {
                    if (i < foodAvailable)
                    {
                        villagers[i].SetHungry(false);
                    }
                    else
                    {
                        villagers[i].SetHungry(true);
                    }
                }
            }

            IsFoodShortage = true;
            Debug.LogWarning($"[HungerSystem] Ngày {dayCount}: THIẾU LƯƠNG THỰC! Chỉ cung cấp được {foodAvailable}/{foodNeeded} phần ăn. {foodNeeded - foodAvailable} cư dân bị đói!");
        }
    }
}
