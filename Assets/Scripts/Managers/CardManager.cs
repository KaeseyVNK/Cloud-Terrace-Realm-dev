using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance { get; private set; }

    [Header("Card Database")]
    [SerializeField] private List<UpgradeCardData> _allCards = new List<UpgradeCardData>();
    [SerializeField] private List<UpgradeCardData> _unlockedCards = new List<UpgradeCardData>();

    public List<UpgradeCardData> AllCards => _allCards;
    public IReadOnlyList<UpgradeCardData> UnlockedCards => _unlockedCards;

    [Header("Instant Spawning")]
    [SerializeField] private GameObject _militiaPrefab;

    [Header("UI Controller")]
    [SerializeField] private CardDraftUIController _draftUIController;

    // Stat Multipliers
    public float CombatUnitMaxHealthMultiplier { get; private set; } = 1.0f;
    public float CombatUnitAttackDamageMultiplier { get; private set; } = 1.0f;
    public float CombatUnitMoveSpeedMultiplier { get; private set; } = 1.0f;

    public float VillagerMoveSpeedMultiplier { get; private set; } = 1.0f;
    public int VillagerCarryCapacityBonus { get; private set; } = 0;
    
    public float WoodGatherSpeedMultiplier { get; private set; } = 1.0f;
    public float StoneGatherSpeedMultiplier { get; private set; } = 1.0f;
    public float GoldGatherSpeedMultiplier { get; private set; } = 1.0f;
    public float FoodGatherSpeedMultiplier { get; private set; } = 1.0f;

    public float BuildingMaxHealthMultiplier { get; private set; } = 1.0f;
    public int PopulationCapBonus { get; private set; } = 0;

    private readonly HashSet<string> _unlockedUnitIds = new HashSet<string>();
    private readonly HashSet<string> _cardUnlockableUnits = new HashSet<string>();

    public event Action OnCardStateChanged;

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
            return;
        }

        InitializeCardUnlockableUnits();
    }

    private void Start()
    {
        // Add existing unlocked cards to available lists on start if pre-populated
        foreach (var card in _unlockedCards)
        {
            ApplyCardEffects(card, false);
        }
        OnCardStateChanged?.Invoke();
    }

    private void Update()
    {
        // Debug Key to test drafting
        if (Input.GetKeyDown(KeyCode.K))
        {
            TriggerCardDraft();
        }
    }

    private void InitializeCardUnlockableUnits()
    {
        _cardUnlockableUnits.Clear();
        foreach (var card in _allCards)
        {
            if (card != null && card.cardType == UpgradeCardType.Unlock && card.unitToUnlock != null)
            {
                if (!string.IsNullOrEmpty(card.unitToUnlock.name))
                    _cardUnlockableUnits.Add(card.unitToUnlock.name);
                if (!string.IsNullOrEmpty(card.unitToUnlock.unitName))
                    _cardUnlockableUnits.Add(card.unitToUnlock.unitName);
            }
        }
    }

    private bool IsCardUnlockableUnit(UnitData unit)
    {
        if (unit == null) return false;
        return _cardUnlockableUnits.Contains(unit.name) || _cardUnlockableUnits.Contains(unit.unitName);
    }

    public bool IsUnitUnlocked(UnitData unit)
    {
        if (unit == null) return true;
        if (IsCardUnlockableUnit(unit))
        {
            return _unlockedUnitIds.Contains(unit.name) || _unlockedUnitIds.Contains(unit.unitName);
        }
        return true;
    }

    public float GetVillagerGatherSpeedMultiplier(ResourceType resourceType)
    {
        return resourceType switch
        {
            ResourceType.Wood => WoodGatherSpeedMultiplier,
            ResourceType.Stone => StoneGatherSpeedMultiplier,
            ResourceType.Gold => GoldGatherSpeedMultiplier,
            ResourceType.Food => FoodGatherSpeedMultiplier,
            _ => 1.0f
        };
    }

    /// <summary>
    /// Kích hoạt màn hình chọn thẻ nâng cấp (dừng game).
    /// minimumRarity: đảm bảo ít nhất 1 trong 3 thẻ ≥ rarity yêu cầu.
    /// </summary>
    public void TriggerCardDraft(CardRarity minimumRarity = CardRarity.Common)
    {
        if (_draftUIController == null)
        {
            _draftUIController = FindAnyObjectByType<CardDraftUIController>(FindObjectsInactive.Include);
        }

        if (_draftUIController == null)
        {
            Debug.LogError("[CardManager] Cannot trigger card draft: CardDraftUIController is missing!");
            return;
        }

        List<UpgradeCardData> choices = GetWeightedCardChoices(3, minimumRarity);
        if (choices.Count == 0)
        {
            Debug.LogWarning("[CardManager] No cards available to draft.");
            return;
        }

        Time.timeScale = 0f; // Dừng game
        _draftUIController.OpenMenu(choices);
    }

    /// <summary>
    /// Chọn thẻ ngẫu nhiên có trọng số theo rarity.
    /// - count: số thẻ cần chọn (thường là 3).
    /// - minimumRarity: nếu khác Common, slot đầu tiên sẽ được bảo đảm ≥ rarity này.
    /// </summary>
    private List<UpgradeCardData> GetWeightedCardChoices(int count, CardRarity minimumRarity)
    {
        // Xây pool loại bỏ Unlock đã có
        List<UpgradeCardData> pool = new List<UpgradeCardData>();
        foreach (var card in _allCards)
        {
            if (card == null) continue;
            if (card.cardType == UpgradeCardType.Unlock && _unlockedCards.Contains(card)) continue;
            pool.Add(card);
        }

        if (pool.Count == 0) return new List<UpgradeCardData>();

        List<UpgradeCardData> choices = new List<UpgradeCardData>();
        List<UpgradeCardData> remaining = new List<UpgradeCardData>(pool);

        for (int i = 0; i < count && remaining.Count > 0; i++)
        {
            // Slot đầu tiên: lọc theo minimumRarity nếu có yêu cầu
            List<UpgradeCardData> currentPool = remaining;
            if (i == 0 && minimumRarity > CardRarity.Common)
            {
                var filtered = remaining.FindAll(c => c.rarity >= minimumRarity);
                if (filtered.Count > 0) currentPool = filtered;
            }

            // Tính tổng trọng số
            int totalWeight = 0;
            foreach (var c in currentPool) totalWeight += c.RarityWeight;

            // Quay số ngẫu nhiên
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            UpgradeCardData picked = currentPool[currentPool.Count - 1]; // fallback

            foreach (var c in currentPool)
            {
                cumulative += c.RarityWeight;
                if (roll < cumulative)
                {
                    picked = c;
                    break;
                }
            }

            choices.Add(picked);
            remaining.Remove(picked);
        }

        return choices;
    }

    /// <summary>
    /// Apply the card chosen by the player and resume the game.
    /// </summary>
    public void ApplyCard(UpgradeCardData card)
    {
        if (card == null) return;

        Debug.Log($"[CardManager] Applying Card: {card.cardName}");
        ApplyCardEffects(card, true);

        Time.timeScale = 1f; // Resume the game
        OnCardStateChanged?.Invoke();
    }

    private void ApplyCardEffects(UpgradeCardData card, bool triggerFloatingText)
    {
        if (card.cardType == UpgradeCardType.Unlock)
        {
            _unlockedCards.Add(card);
            
            // Unlock building
            if (card.buildingToUnlock != null && BuildingManager.Instance != null)
            {
                if (!BuildingManager.Instance.AvailableBuildings.Contains(card.buildingToUnlock))
                {
                    BuildingManager.Instance.AvailableBuildings.Add(card.buildingToUnlock);
                    Debug.Log($"[CardManager] Unlocked Building: {card.buildingToUnlock.buildingName}");
                }
            }

            // Unlock unit
            if (card.unitToUnlock != null)
            {
                if (!string.IsNullOrEmpty(card.unitToUnlock.name))
                    _unlockedUnitIds.Add(card.unitToUnlock.name);
                if (!string.IsNullOrEmpty(card.unitToUnlock.unitName))
                    _unlockedUnitIds.Add(card.unitToUnlock.unitName);
                Debug.Log($"[CardManager] Unlocked Unit: {card.unitToUnlock.unitName}");
            }
        }
        else if (card.cardType == UpgradeCardType.StatBuff)
        {
            _unlockedCards.Add(card);

            CombatUnitMaxHealthMultiplier *= card.unitHealthMultiplier;
            CombatUnitAttackDamageMultiplier *= card.unitDamageMultiplier;
            CombatUnitMoveSpeedMultiplier *= card.unitSpeedMultiplier;

            VillagerMoveSpeedMultiplier *= card.villagerSpeedMultiplier;
            VillagerCarryCapacityBonus += card.villagerCarryCapacityBonus;

            WoodGatherSpeedMultiplier *= card.woodGatherMultiplier;
            StoneGatherSpeedMultiplier *= card.stoneGatherMultiplier;
            GoldGatherSpeedMultiplier *= card.goldGatherMultiplier;
            FoodGatherSpeedMultiplier *= card.foodGatherMultiplier;

            // Recalculate stats for all existing combat units
            foreach (var unit in BaseCombatUnitController.Registry)
            {
                if (unit != null)
                {
                    unit.ApplyPlayerTechnologyStats();
                }
            }

            // Recalculate stats for all existing villagers
            foreach (var villager in VillagerController.AllVillagers)
            {
                if (villager != null)
                {
                    villager.ApplyVillagerTechnologyStats();
                }
            }
        }
        else if (card.cardType == UpgradeCardType.Instant)
        {
            // Instants are not saved in _unlockedCards to allow repeated pick
            if (ResourceManager.Instance != null)
            {
                // Tài nguyên đa dụng (trường cũ)
                if (card.instantResourceAmount > 0)
                    ResourceManager.Instance.AddResource(card.instantResourceType, card.instantResourceAmount);

                // Tài nguyên đa loại riêng lẻ
                if (card.instantWoodAmount > 0)
                    ResourceManager.Instance.AddResource(ResourceType.Wood, card.instantWoodAmount);
                if (card.instantStoneAmount > 0)
                    ResourceManager.Instance.AddResource(ResourceType.Stone, card.instantStoneAmount);
                if (card.instantFoodAmount > 0)
                    ResourceManager.Instance.AddResource(ResourceType.Food, card.instantFoodAmount);
            }

            if (card.instantMilitiaCount > 0)
            {
                SpawnInstantMilitia(card.instantMilitiaCount);
            }

            if (card.healMainBuilding)
            {
                HealMainBuilding();
            }
            else if (card.healMainBuildingPercent > 0f)
            {
                HealMainBuildingByPercent(card.healMainBuildingPercent);
            }
        }

        // StatBuff cũng có thể có building / population buff
        if (card.cardType == UpgradeCardType.StatBuff)
        {
            if (card.buildingMaxHealthMultiplier != 1f)
            {
                BuildingMaxHealthMultiplier *= card.buildingMaxHealthMultiplier;
                Debug.Log($"[CardManager] Building HP multiplier now: {BuildingMaxHealthMultiplier:F2}x");
            }

            if (card.populationCapBonus != 0)
            {
                PopulationCapBonus += card.populationCapBonus;
                Debug.Log($"[CardManager] Population cap bonus: +{card.populationCapBonus} (total: {PopulationCapBonus})");
            }
        }
    }

    private void SpawnInstantMilitia(int count)
    {
        MainBuildingCombatTarget mainBuilding = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainBuilding == null)
        {
            Debug.LogWarning("[CardManager] Cannot spawn instant militia: Main Building is missing!");
            return;
        }

        Vector3 spawnPos = mainBuilding.transform.position + new Vector3(0, 0, -3f);
        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 5f, UnityEngine.AI.NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        for (int i = 0; i < count; i++)
        {
            if (_militiaPrefab != null)
            {
                GameObject spawned = Instantiate(_militiaPrefab, spawnPos, Quaternion.identity);
                if (spawned != null)
                {
                    spawned.transform.SetParent(GameManager.CombatUnitsContainer);
                    
                    // Trigger standard combat unit initialization
                    BaseCombatUnitController unit = spawned.GetComponent<BaseCombatUnitController>();
                    if (unit != null)
                    {
                        unit.ApplyPlayerTechnologyStats();
                    }
                }
            }
            else
            {
                Debug.LogWarning("[CardManager] Militia Prefab is not assigned in CardManager!");
                break;
            }
        }
    }

    private void HealMainBuilding()
    {
        MainBuildingCombatTarget mainBuilding = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainBuilding != null)
        {
            mainBuilding.currentHealth = mainBuilding.maxHealth;
            Debug.Log("[CardManager] Main Building fully healed!");
        }
    }

    private void HealMainBuildingByPercent(float percent)
    {
        MainBuildingCombatTarget mainBuilding = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainBuilding != null)
        {
            float healAmount = mainBuilding.maxHealth * percent;
            mainBuilding.currentHealth = (int)Mathf.Min(mainBuilding.currentHealth + healAmount, mainBuilding.maxHealth);
            Debug.Log($"[CardManager] Main Building healed by {percent * 100f:F0}% ({healAmount:F0} HP).");
        }
    }
}
