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
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name != "MainMenuScene" && scene.name != "LoadingScene")
            {
                EnsureInstance();
            }
        };
        EnsureInstance();
    }

    private static void EnsureInstance()
    {
        if (Instance != null) return;
        if (FindAnyObjectByType<HungerSystem>() != null) return;

        GameObject go = new GameObject("HungerSystem");
        go.AddComponent<HungerSystem>();
        DontDestroyOnLoad(go);
        GameLog.Log("[HungerSystem] Đã tự động khởi chạy hệ thống đói lương thực.");
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
            GameLog.LogError("[HungerSystem] Không tìm thấy TimeManager.Instance để đăng ký sự kiện!");
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged += HandleResourceChanged;
        }
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
        }

        if (ResourceManager.Instance != null)
        {
            ResourceManager.Instance.OnResourceChanged -= HandleResourceChanged;
        }
    }

    private void HandleResourceChanged(ResourceType type, int amount)
    {
        if (type == ResourceType.Food && amount > 0)
        {
            List<VillagerController> villagers = VillagerController.SpawnedVillagers;
            List<VillagerController> hungryVillagers = new List<VillagerController>();
            
            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null && villagers[i].IsHungry)
                {
                    hungryVillagers.Add(villagers[i]);
                }
            }

            List<BaseCombatUnitController> hungryCombatUnits = new List<BaseCombatUnitController>();
            foreach (var unit in BaseCombatUnitController.Registry)
            {
                if (unit != null && unit.faction == UnitFaction.Player && !(unit is BuildingCombatTarget) && unit.IsHungry)
                {
                    hungryCombatUnits.Add(unit);
                }
            }

            int totalHungry = hungryVillagers.Count + hungryCombatUnits.Count;
            if (totalHungry > 0)
            {
                int foodToConsume = Mathf.Min(totalHungry, amount);
                if (foodToConsume > 0)
                {
                    if (ResourceManager.Instance.TryConsumeResource(ResourceType.Food, foodToConsume))
                    {
                        int consumed = 0;
                        Vector3 spawnPos = Vector3.zero;
                        bool hasPos = false;

                        for (int i = 0; i < hungryVillagers.Count && consumed < foodToConsume; i++)
                        {
                            hungryVillagers[i].SetHungry(false);
                            if (!hasPos) { spawnPos = hungryVillagers[i].transform.position; hasPos = true; }
                            consumed++;
                        }

                        for (int i = 0; i < hungryCombatUnits.Count && consumed < foodToConsume; i++)
                        {
                            hungryCombatUnits[i].SetHungry(false);
                            if (!hasPos) { spawnPos = hungryCombatUnits[i].transform.position; hasPos = true; }
                            consumed++;
                        }
                        
                        if (hasPos)
                        {
                            MyGame.UI.FloatingText.Spawn(spawnPos, $"-{foodToConsume} Lương thực", new Color(0.95f, 0.26f, 0.21f));
                        }

                        bool anyHungry = false;
                        for (int i = 0; i < villagers.Count; i++)
                        {
                            if (villagers[i] != null && villagers[i].IsHungry)
                            {
                                anyHungry = true;
                                break;
                            }
                        }
                        if (!anyHungry)
                        {
                            foreach (var unit in BaseCombatUnitController.Registry)
                            {
                                if (unit != null && unit.faction == UnitFaction.Player && !(unit is BuildingCombatTarget) && unit.IsHungry)
                                {
                                    anyHungry = true;
                                    break;
                                }
                            }
                        }
                        IsFoodShortage = anyHungry;
                        GameLog.Log($"[HungerSystem] Đã có thêm lương thực! {foodToConsume} cư dân đói đã được ăn no.");
                    }
                }
            }
        }
    }


    private void HandleDayChanged(int dayCount)
    {
        if (ResourceManager.Instance == null)
        {
            return;
        }

        List<VillagerController> villagers = VillagerController.SpawnedVillagers;
        
        List<BaseCombatUnitController> combatUnits = new List<BaseCombatUnitController>();
        foreach (var unit in BaseCombatUnitController.Registry)
        {
            if (unit != null && unit.faction == UnitFaction.Player && !(unit is BuildingCombatTarget) && unit.currentState != CombatState.Dead)
            {
                combatUnits.Add(unit);
            }
        }

        int totalUnits = villagers.Count + combatUnits.Count;
        int foodNeeded = totalUnits;
        if (CardManager.Instance != null && CardManager.Instance.IsDecreeActive("decree_martial_law"))
        {
            foodNeeded = Mathf.CeilToInt(totalUnits * 0.8f);
        }

        if (foodNeeded <= 0)
        {
            IsFoodShortage = false;
            return;
        }

        int foodAvailable = ResourceManager.Instance.GetResourceAmount(ResourceType.Food);

        if (foodAvailable >= foodNeeded)
        {
            ResourceManager.Instance.TryConsumeResource(ResourceType.Food, foodNeeded);

            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null) villagers[i].SetHungry(false);
            }
            for (int i = 0; i < combatUnits.Count; i++)
            {
                if (combatUnits[i] != null) combatUnits[i].SetHungry(false);
            }

            IsFoodShortage = false;
            GameLog.Log($"[HungerSystem] Ngày {dayCount}: Đã tiêu thụ {foodNeeded} lương thực cho {villagers.Count} dân làng và {combatUnits.Count} lính. Mọi người đều no bụng.");
            
            SpawnFoodDeductionFloatingText(foodNeeded);
        }
        else
        {
            ResourceManager.Instance.TryConsumeResource(ResourceType.Food, foodAvailable);

            int fedCount = 0;
            for (int i = 0; i < villagers.Count; i++)
            {
                if (villagers[i] != null)
                {
                    if (fedCount < foodAvailable)
                    {
                        villagers[i].SetHungry(false);
                        fedCount++;
                    }
                    else
                    {
                        villagers[i].SetHungry(true);
                    }
                }
            }
            for (int i = 0; i < combatUnits.Count; i++)
            {
                if (combatUnits[i] != null)
                {
                    if (fedCount < foodAvailable)
                    {
                        combatUnits[i].SetHungry(false);
                        fedCount++;
                    }
                    else
                    {
                        combatUnits[i].SetHungry(true);
                    }
                }
            }

            IsFoodShortage = true;
            GameLog.LogWarning($"[HungerSystem] Ngày {dayCount}: THIẾU LƯƠNG THỰC! Chỉ cung cấp được {foodAvailable}/{foodNeeded} phần ăn. {foodNeeded - foodAvailable} cư dân bị đói!");
            
            if (foodAvailable > 0)
            {
                SpawnFoodDeductionFloatingText(foodAvailable);
            }
        }
    }

    private void SpawnFoodDeductionFloatingText(int amount)
    {
        Vector3 spawnPos = Vector3.zero;
        var mainHouse = UnityEngine.Object.FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainHouse != null)
        {
            spawnPos = mainHouse.transform.position;
        }
        else
        {
            List<VillagerController> villagers = VillagerController.SpawnedVillagers;
            if (villagers.Count > 0 && villagers[0] != null)
            {
                spawnPos = villagers[0].transform.position;
            }
        }
        MyGame.UI.FloatingText.Spawn(spawnPos, $"-{amount} Lương thực", new Color(0.95f, 0.26f, 0.21f));
    }
}
