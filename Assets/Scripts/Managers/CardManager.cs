using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour, CloudTerraceRealm.SaveSystem.ISaveable
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

    // Backing Fields
    private float _combatUnitMaxHealthMultiplier = 1.0f;
    private float _combatUnitAttackDamageMultiplier = 1.0f;
    private float _combatUnitMoveSpeedMultiplier = 1.0f;
    private float _villagerMoveSpeedMultiplier = 1.0f;
    private int _villagerCarryCapacityBonus = 0;
    private float _woodGatherSpeedMultiplier = 1.0f;
    private float _stoneGatherSpeedMultiplier = 1.0f;
    private float _goldGatherSpeedMultiplier = 1.0f;
    private float _foodGatherSpeedMultiplier = 1.0f;
    private float _buildingMaxHealthMultiplier = 1.0f;
    private int _populationCapBonus = 0;

    // Stat Multipliers
    public float CombatUnitMaxHealthMultiplier
    {
        get
        {
            float val = _combatUnitMaxHealthMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.unitHealthMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.unitHealthMultiplier;
            return val;
        }
    }
    public float CombatUnitAttackDamageMultiplier
    {
        get
        {
            float val = _combatUnitAttackDamageMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.unitDamageMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.unitDamageMultiplier;
            return val;
        }
    }
    public float CombatUnitMoveSpeedMultiplier
    {
        get
        {
            float val = _combatUnitMoveSpeedMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.unitSpeedMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.unitSpeedMultiplier;
            return val;
        }
    }

    public float VillagerMoveSpeedMultiplier
    {
        get
        {
            float val = _villagerMoveSpeedMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.villagerSpeedMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.villagerSpeedMultiplier;
            return val;
        }
    }
    public int VillagerCarryCapacityBonus
    {
        get
        {
            int val = _villagerCarryCapacityBonus;
            if (_activeDecreeCard != null) val += _activeDecreeCard.villagerCarryCapacityBonus;
            if (_activeNightDecreeCard != null) val += _activeNightDecreeCard.villagerCarryCapacityBonus;
            return val;
        }
    }
    
    public float WoodGatherSpeedMultiplier
    {
        get
        {
            float val = _woodGatherSpeedMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.woodGatherMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.woodGatherMultiplier;
            return val;
        }
    }
    public float StoneGatherSpeedMultiplier
    {
        get
        {
            float val = _stoneGatherSpeedMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.stoneGatherMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.stoneGatherMultiplier;
            return val;
        }
    }
    public float GoldGatherSpeedMultiplier
    {
        get
        {
            float val = _goldGatherSpeedMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.goldGatherMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.goldGatherMultiplier;
            return val;
        }
    }
    public float FoodGatherSpeedMultiplier
    {
        get
        {
            float val = _foodGatherSpeedMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.foodGatherMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.foodGatherMultiplier;
            return val;
        }
    }

    public float BuildingMaxHealthMultiplier
    {
        get
        {
            float val = _buildingMaxHealthMultiplier;
            if (_activeDecreeCard != null) val *= _activeDecreeCard.buildingMaxHealthMultiplier;
            if (_activeNightDecreeCard != null) val *= _activeNightDecreeCard.buildingMaxHealthMultiplier;
            return val;
        }
    }
    public int PopulationCapBonus
    {
        get
        {
            int val = _populationCapBonus;
            if (_activeDecreeCard != null) val += _activeDecreeCard.populationCapBonus;
            if (_activeNightDecreeCard != null) val += _activeNightDecreeCard.populationCapBonus;
            return val;
        }
    }

    [Header("Morning Decree System")]
    private UpgradeCardData _activeDecreeCard = null;
    public UpgradeCardData ActiveDecreeCard => _activeDecreeCard;

    [Header("Night Decree System")]
    private UpgradeCardData _activeNightDecreeCard = null;
    public UpgradeCardData ActiveNightDecreeCard => _activeNightDecreeCard;
    public float DecreeFogVisionMultiplier => IsDecreeActive("decree_night_scout") ? 2.0f : 1.0f;

    public bool IsDecreeActive(string decreeId)
    {
        return _activeDecreeCard != null && _activeDecreeCard.cardId == decreeId;
    }

    private readonly HashSet<string> _unlockedUnitIds = new HashSet<string>();
    private readonly HashSet<string> _discoveredUnitIds = new HashSet<string>();
    private readonly HashSet<string> _cardUnlockableUnits = new HashSet<string>();
    public event Action OnCardStateChanged;

    private bool _pendingSurvivalDraft = false;
    private bool _isPendingSurvivalBloodMoon = false;

    public bool PendingSurvivalDraft
    {
        get => _pendingSurvivalDraft;
        set => _pendingSurvivalDraft = value;
    }

    public bool IsPendingSurvivalBloodMoon
    {
        get => _isPendingSurvivalBloodMoon;
        set => _isPendingSurvivalBloodMoon = value;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSaveableEntity("Global_CardManager");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeCardUnlockableUnits();
        InitializeDefaultDecrees();
        InitializeDefaultNightDecrees();
    }

    private void EnsureSaveableEntity(string saveID)
    {
        var saveable = GetComponent<CloudTerraceRealm.SaveSystem.SaveableEntity>();
        if (saveable == null)
        {
            saveable = gameObject.AddComponent<CloudTerraceRealm.SaveSystem.SaveableEntity>();
            var field = typeof(CloudTerraceRealm.SaveSystem.SaveableEntity).GetField("_saveID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(saveable, saveID);
            }
        }
    }

    private void Start()
    {
        if (TechnologyManager.Instance != null)
        {
            TechnologyManager.Instance.OnTechnologyUnlocked += HandleTechnologyUnlocked;
        }

        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged += HandleDayChanged;
            TimeManager.Instance.OnDayNightChanged += HandleDayNightChanged;
        }

        // Add existing unlocked cards to available lists on start if pre-populated
        foreach (var card in _unlockedCards)
        {
            ApplyCardEffects(card, false);
        }
        OnCardStateChanged?.Invoke();

        if (TimeManager.Instance != null && TimeManager.Instance.dayCount == 1)
        {
            StartCoroutine(TriggerDecreeDraftDelayedRoutine());
        }
    }

    private void OnDestroy()
    {
        if (TechnologyManager.HasInstance)
        {
            TechnologyManager.Instance.OnTechnologyUnlocked -= HandleTechnologyUnlocked;
        }
        if (TimeManager.Instance != null)
        {
            TimeManager.Instance.OnDayChanged -= HandleDayChanged;
            TimeManager.Instance.OnDayNightChanged -= HandleDayNightChanged;
        }
    }

    private void HandleTechnologyUnlocked(TechnologyData tech)
    {
        if (tech == null || string.IsNullOrEmpty(tech.technologyId)) return;

        if (tech.technologyId.StartsWith("unlock_building_"))
        {
            string buildingName = tech.technologyId.Substring("unlock_building_".Length);
            foreach (var card in _allCards)
            {
                if (card != null && card.cardType == UpgradeCardType.Unlock && card.buildingToUnlock != null && card.buildingToUnlock.name == buildingName)
                {
                    ApplyCardEffects(card, false);
                    break;
                }
            }
        }
        else if (tech.technologyId.StartsWith("unlock_unit_"))
        {
            string unitName = tech.technologyId.Substring("unlock_unit_".Length);
            foreach (var card in _allCards)
            {
                if (card != null && card.cardType == UpgradeCardType.Unlock && card.unitToUnlock != null && card.unitToUnlock.name == unitName)
                {
                    ApplyCardEffects(card, false);
                    break;
                }
            }
        }
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

    public bool IsUnitDiscovered(UnitData unit)
    {
        if (unit == null) return true;
        if (IsCardUnlockableUnit(unit))
        {
            return _discoveredUnitIds.Contains(unit.name) || _discoveredUnitIds.Contains(unit.unitName);
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
    /// minimumRarity: đảm bảo ít nhất 1 trong các thẻ ≥ rarity yêu cầu.
    /// </summary>
    public void TriggerCardDraft(CardRarity minimumRarity = CardRarity.Common, bool isBloodMoon = false)
    {
        if (_draftUIController == null)
        {
            _draftUIController = FindAnyObjectByType<CardDraftUIController>(FindObjectsInactive.Include);
        }

        if (_draftUIController == null)
        {
            GameLog.LogError("[CardManager] Cannot trigger card draft: CardDraftUIController is missing!");
            return;
        }

        List<UpgradeCardData> choices = GetWeightedCardChoices(3, minimumRarity, isBloodMoon);
        if (choices.Count == 0)
        {
            GameLog.LogWarning("[CardManager] No cards available to draft.");
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
    private List<UpgradeCardData> GetWeightedCardChoices(int count, CardRarity minimumRarity, bool isBloodMoon = false)
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
            foreach (var c in currentPool) totalWeight += GetDynamicRarityWeight(c, isBloodMoon);

            // Quay số ngẫu nhiên
            int roll = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            UpgradeCardData picked = currentPool[currentPool.Count - 1]; // fallback

            foreach (var c in currentPool)
            {
                cumulative += GetDynamicRarityWeight(c, isBloodMoon);
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

    private int GetDynamicRarityWeight(UpgradeCardData card, bool isBloodMoon)
    {
        if (!isBloodMoon) return card.RarityWeight;

        return card.rarity switch
        {
            CardRarity.Common    => 20,  // Giảm cơ hội ra thẻ Common
            CardRarity.Rare      => 80,  // Tăng gấp đôi cơ hội Rare
            CardRarity.Epic      => 50,  // Tăng gấp ba cơ hội Epic
            CardRarity.Legendary => 25,  // Tăng gấp sáu cơ hội Legendary
            _                    => 20
        };
    }

    /// <summary>
    /// Apply the card chosen by the player and resume the game.
    /// </summary>
    public void ApplyCard(UpgradeCardData card)
    {
        if (card == null) return;

        GameLog.Log($"[CardManager] Applying Card: {card.cardName}");
        ApplyCardEffects(card, true);

        Time.timeScale = 1f; // Resume the game
        OnCardStateChanged?.Invoke();

        // Nếu có thẻ nâng cấp sống sót đêm đang chờ, kích hoạt nó ngay sau khi đóng sắc lệnh sáng
        if (card.cardType == UpgradeCardType.Decree && _pendingSurvivalDraft)
        {
            _pendingSurvivalDraft = false;
            StartCoroutine(TriggerCardDraftDelayedRoutine());
        }
    }

    private System.Collections.IEnumerator TriggerCardDraftDelayedRoutine()
    {
        yield return new WaitForSecondsRealtime(0.15f);
        TriggerCardDraft(CardRarity.Common, _isPendingSurvivalBloodMoon);
        _isPendingSurvivalBloodMoon = false;
    }

    private void ApplyCardEffects(UpgradeCardData card, bool triggerFloatingText)
    {
        if (card.cardType == UpgradeCardType.Unlock)
        {
            // Đăng ký đơn vị đã được phát hiện (discover) khi rút thẻ
            if (card.unitToUnlock != null)
            {
                if (!string.IsNullOrEmpty(card.unitToUnlock.name))
                    _discoveredUnitIds.Add(card.unitToUnlock.name);
                if (!string.IsNullOrEmpty(card.unitToUnlock.unitName))
                    _discoveredUnitIds.Add(card.unitToUnlock.unitName);
            }

            if (triggerFloatingText)
            {
                // Thay vì mở khóa ngay lập tức, chuyển thành công nghệ cần nghiên cứu ở lò rèn
                TechnologyData cardTech = ScriptableObject.CreateInstance<TechnologyData>();
                string typeKey = card.buildingToUnlock != null ? "building_" + card.buildingToUnlock.name : "unit_" + card.unitToUnlock.name;
                cardTech.technologyId = "unlock_" + typeKey;
                cardTech.technologyName = "Nghien cuu " + card.cardName;
                cardTech.description = "Mo khoa " + (card.buildingToUnlock != null ? card.buildingToUnlock.buildingName : card.unitToUnlock.unitName) + " de su dung.";
                cardTech.icon = card.icon;
                cardTech.researchTime = card.buildingToUnlock != null ? 15f : 12f;
                
                cardTech.researchCosts = new List<ResourceCost>
                {
                    new ResourceCost { resourceType = ResourceType.Gold, amount = 100 },
                    new ResourceCost { resourceType = ResourceType.Wood, amount = 50 }
                };

                if (!BlacksmithResearch.GlobalCardTechnologies.Contains(cardTech))
                {
                    BlacksmithResearch.GlobalCardTechnologies.Add(cardTech);
                }

                foreach (var blacksmith in BlacksmithResearch.ActiveBlacksmiths)
                {
                    blacksmith.AddCardTechnology(cardTech);
                }

                GameLog.Log($"[CardManager] Dang ky nghien cuu mo khoa cho: {card.cardName} tai Lo Ren.");
                return;
            }

            _unlockedCards.Add(card);
            
            // Unlock building
            if (card.buildingToUnlock != null && BuildingManager.Instance != null)
            {
                if (!BuildingManager.Instance.AvailableBuildings.Contains(card.buildingToUnlock))
                {
                    BuildingManager.Instance.AvailableBuildings.Add(card.buildingToUnlock);
                    GameLog.Log($"[CardManager] Unlocked Building: {card.buildingToUnlock.buildingName}");
                }
            }

            // Unlock unit
            if (card.unitToUnlock != null)
            {
                if (!string.IsNullOrEmpty(card.unitToUnlock.name))
                    _unlockedUnitIds.Add(card.unitToUnlock.name);
                if (!string.IsNullOrEmpty(card.unitToUnlock.unitName))
                    _unlockedUnitIds.Add(card.unitToUnlock.unitName);
                GameLog.Log($"[CardManager] Unlocked Unit: {card.unitToUnlock.unitName}");
            }
        }
        else if (card.cardType == UpgradeCardType.StatBuff)
        {
            _unlockedCards.Add(card);

            _combatUnitMaxHealthMultiplier *= card.unitHealthMultiplier;
            _combatUnitAttackDamageMultiplier *= card.unitDamageMultiplier;
            _combatUnitMoveSpeedMultiplier *= card.unitSpeedMultiplier;

            _villagerMoveSpeedMultiplier *= card.villagerSpeedMultiplier;
            _villagerCarryCapacityBonus += card.villagerCarryCapacityBonus;

            _woodGatherSpeedMultiplier *= card.woodGatherMultiplier;
            _stoneGatherSpeedMultiplier *= card.stoneGatherMultiplier;
            _goldGatherSpeedMultiplier *= card.goldGatherMultiplier;
            _foodGatherSpeedMultiplier *= card.foodGatherMultiplier;

            RecalculateAllUnitStats();
        }
        else if (card.cardType == UpgradeCardType.Decree)
        {
            if (card.cardId.StartsWith("night_decree"))
            {
                _activeNightDecreeCard = card;
                GameLog.Log($"[CardManager] Active Night Decree: {card.cardName}");
            }
            else
            {
                _activeDecreeCard = card;
                GameLog.Log($"[CardManager] Active Decree: {card.cardName}");
            }
            RecalculateAllUnitStats();
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
                _buildingMaxHealthMultiplier *= card.buildingMaxHealthMultiplier;
                GameLog.Log($"[CardManager] Building HP multiplier now: {BuildingMaxHealthMultiplier:F2}x");
            }

            if (card.populationCapBonus != 0)
            {
                _populationCapBonus += card.populationCapBonus;
                GameLog.Log($"[CardManager] Population cap bonus: +{card.populationCapBonus} (total: {PopulationCapBonus})");
            }
        }
    }

    private void SpawnInstantMilitia(int count)
    {
        MainBuildingCombatTarget mainBuilding = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainBuilding == null)
        {
            GameLog.LogWarning("[CardManager] Cannot spawn instant militia: Main Building is missing!");
            return;
        }

        Vector3 spawnPos = mainBuilding.transform.position + new Vector3(0, 0, -3f);
        if (UnityEngine.AI.NavMesh.SamplePosition(spawnPos, out UnityEngine.AI.NavMeshHit hit, 5f, ~2))
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
                GameLog.LogWarning("[CardManager] Militia Prefab is not assigned in CardManager!");
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
            GameLog.Log("[CardManager] Main Building fully healed!");
        }
    }

    private void HealMainBuildingByPercent(float percent)
    {
        MainBuildingCombatTarget mainBuilding = FindAnyObjectByType<MainBuildingCombatTarget>();
        if (mainBuilding != null)
        {
            float healAmount = mainBuilding.maxHealth * percent;
            mainBuilding.currentHealth = (int)Mathf.Min(mainBuilding.currentHealth + healAmount, mainBuilding.maxHealth);
            GameLog.Log($"[CardManager] Main Building healed by {percent * 100f:F0}% ({healAmount:F0} HP).");
        }
    }

    #region Morning Decree System Internals

    private void HandleDayChanged(int dayCount)
    {
        // 1. Clear previous decree
        _activeDecreeCard = null;
        RecalculateAllUnitStats();

        // 2. Trigger new decree draft
        TriggerDecreeDraft();
    }

    private System.Collections.IEnumerator TriggerDecreeDraftDelayedRoutine()
    {
        yield return new WaitForSeconds(0.2f);
        TriggerDecreeDraft();
    }

    public void TriggerDecreeDraft()
    {
        if (_draftUIController == null)
        {
            _draftUIController = FindAnyObjectByType<CardDraftUIController>(FindObjectsInactive.Include);
        }

        if (_draftUIController == null)
        {
            GameLog.LogError("[CardManager] Cannot trigger decree draft: CardDraftUIController is missing!");
            return;
        }

        // Lấy tất cả các thẻ Decree
        List<UpgradeCardData> decreeChoices = new List<UpgradeCardData>();
        foreach (var card in _allCards)
        {
            if (card != null && card.cardType == UpgradeCardType.Decree)
            {
                decreeChoices.Add(card);
            }
        }

        if (decreeChoices.Count == 0)
        {
            GameLog.LogWarning("[CardManager] No decree cards available in database. Initializing defaults...");
            InitializeDefaultDecrees();
            foreach (var card in _allCards)
            {
                if (card != null && card.cardType == UpgradeCardType.Decree)
                {
                    decreeChoices.Add(card);
                }
            }
        }

        // Xáo trộn và chọn tối đa 3 thẻ
        List<UpgradeCardData> choices = new List<UpgradeCardData>();
        List<UpgradeCardData> remaining = new List<UpgradeCardData>(decreeChoices);
        for (int i = 0; i < 3 && remaining.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, remaining.Count);
            choices.Add(remaining[idx]);
            remaining.RemoveAt(idx);
        }

        if (choices.Count > 0)
        {
            Time.timeScale = 0f; // Pause game
            _draftUIController.OpenMenu(choices);
        }
    }

    private void InitializeDefaultDecrees()
    {
        CreateDecreeIfNotExists("decree_good_harvest", "Bountiful Harvest", 
            "Farming speed increased by 50%, but soldier damage reduced by 15% for today.", 
            UpgradeCardType.Decree, 1.5f, 0.85f, 1f, 1f);
        CreateDecreeIfNotExists("decree_martial_law", "Martial Law", 
            "Food consumption reduced by 20%, but construction time increased by 20% for today.", 
            UpgradeCardType.Decree, 1f, 1f, 1f, 1f);
        CreateDecreeIfNotExists("decree_night_scout", "Night Scout", 
            "Fog of War vision doubled, but night monsters are 50% stronger for today.", 
            UpgradeCardType.Decree, 1f, 1f, 1f, 1f);
    }

    private void CreateDecreeIfNotExists(string id, string name, string desc, UpgradeCardType type, float foodMult, float dmgMult, float healthMult, float speedMult)
    {
        foreach (var c in _allCards)
        {
            if (c != null && c.cardId == id) return;
        }

        UpgradeCardData newCard = ScriptableObject.CreateInstance<UpgradeCardData>();
        newCard.cardId = id;
        newCard.cardName = name;
        newCard.description = desc;
        newCard.cardType = type;
        newCard.rarity = CardRarity.Rare;
        
        newCard.foodGatherMultiplier = foodMult;
        newCard.unitDamageMultiplier = dmgMult;
        newCard.unitHealthMultiplier = healthMult;
        newCard.unitSpeedMultiplier = speedMult;

        _allCards.Add(newCard);
    }

    private void RecalculateAllUnitStats()
    {
        // Cập nhật lại chỉ số cho tất cả lính chiến đấu
        foreach (var unit in BaseCombatUnitController.Registry)
        {
            if (unit != null)
            {
                unit.ApplyPlayerTechnologyStats();
            }
        }

        // Cập nhật lại chỉ số cho tất cả dân làng
        foreach (var villager in VillagerController.AllVillagers)
        {
            if (villager != null)
            {
                villager.ApplyVillagerTechnologyStats();
            }
        }
    }

    #endregion

    private string GetCardSaveId(UpgradeCardData card)
    {
        if (card == null) return "";
        return !string.IsNullOrEmpty(card.cardId) ? card.cardId : card.name;
    }

    private UpgradeCardData FindCardDataByID(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return null;
        foreach (UpgradeCardData card in _allCards)
        {
            if (GetCardSaveId(card) == cardId) return card;
        }
        return null;
    }

    [Serializable]
    private class CardSaveState
    {
        public List<string> unlockedCardIds = new List<string>();
        public string activeDecreeCardId = "";
    }

    public string CaptureState()
    {
        CardSaveState state = new CardSaveState();
        foreach (var card in _unlockedCards)
        {
            string id = GetCardSaveId(card);
            if (!string.IsNullOrEmpty(id))
            {
                state.unlockedCardIds.Add(id);
            }
        }
        state.activeDecreeCardId = GetCardSaveId(_activeDecreeCard);
        return JsonUtility.ToJson(state);
    }

    public void RestoreState(string stateJson)
    {
        if (string.IsNullOrEmpty(stateJson)) return;
        CardSaveState state = JsonUtility.FromJson<CardSaveState>(stateJson);
        if (state == null) return;

        _unlockedCards.Clear();

        if (state.unlockedCardIds != null)
        {
            foreach (var id in state.unlockedCardIds)
            {
                UpgradeCardData card = FindCardDataByID(id);
                if (card != null && !_unlockedCards.Contains(card))
                {
                    ApplyCardEffects(card, false);
                }
            }
        }

        UpgradeCardData decree = FindCardDataByID(state.activeDecreeCardId);
        _activeDecreeCard = decree;

        RecalculateAllUnitStats();
    }

    private void InitializeDefaultNightDecrees()
    {
        CreateNightDecreeIfNotExists("night_decree_defense", "Phòng Thủ Nửa Đêm",
            "Sức mạnh công trình +25%, sát thương của lính +25%, nhưng dân làng di chuyển chậm hơn 25% vào ban đêm.",
            1.25f, 1.25f, 1f, 0.75f, 1.25f);
            
        CreateNightDecreeIfNotExists("night_decree_scavenger", "Khai Thác Ban Đêm",
            "Tốc độ farm tài nguyên của dân làng +40% vào ban đêm, nhưng máu tối đa của lính giảm 15%.",
            1f, 1f, 0.85f, 1f, 1f, 1.4f);

        CreateNightDecreeIfNotExists("night_decree_frenzy", "Huyết Thệ Đêm",
            "Sát thương và tốc độ của lính +25%, nhưng máu tối đa của công trình giảm 15%.",
            1f, 1.25f, 1f, 1.25f, 0.85f);
    }

    private void CreateNightDecreeIfNotExists(string id, string name, string desc, float hpMult, float dmgMult, float unitSpeedMult, float villagerSpeedMult, float bldgHpMult, float gatherMult = 1f)
    {
        foreach (var c in _allCards)
        {
            if (c != null && c.cardId == id) return;
        }

        UpgradeCardData newCard = ScriptableObject.CreateInstance<UpgradeCardData>();
        newCard.cardId = id;
        newCard.cardName = name;
        newCard.description = desc;
        newCard.cardType = UpgradeCardType.Decree;
        newCard.rarity = CardRarity.Rare;
        
        newCard.unitHealthMultiplier = hpMult;
        newCard.unitDamageMultiplier = dmgMult;
        newCard.unitSpeedMultiplier = unitSpeedMult;
        newCard.villagerSpeedMultiplier = villagerSpeedMult;
        newCard.buildingMaxHealthMultiplier = bldgHpMult;
        newCard.woodGatherMultiplier = gatherMult;
        newCard.stoneGatherMultiplier = gatherMult;
        newCard.goldGatherMultiplier = gatherMult;
        newCard.foodGatherMultiplier = gatherMult;

        _allCards.Add(newCard);
    }

    private void HandleDayNightChanged(bool isNight)
    {
        if (isNight)
        {
            TriggerNightDecreeDraft();
        }
        else
        {
            _activeNightDecreeCard = null;
            RecalculateAllUnitStats();
        }
    }

    public void TriggerNightDecreeDraft()
    {
        if (_draftUIController == null)
        {
            _draftUIController = FindAnyObjectByType<CardDraftUIController>(FindObjectsInactive.Include);
        }

        if (_draftUIController == null)
        {
            GameLog.LogError("[CardManager] Cannot trigger night decree draft: CardDraftUIController is missing!");
            return;
        }

        List<UpgradeCardData> nightDecreeChoices = new List<UpgradeCardData>();
        foreach (var card in _allCards)
        {
            if (card != null && card.cardType == UpgradeCardType.Decree && card.cardId.StartsWith("night_decree"))
            {
                nightDecreeChoices.Add(card);
            }
        }

        if (nightDecreeChoices.Count == 0)
        {
            GameLog.LogWarning("[CardManager] No night decree cards available in database. Initializing defaults...");
            InitializeDefaultNightDecrees();
            foreach (var card in _allCards)
            {
                if (card != null && card.cardType == UpgradeCardType.Decree && card.cardId.StartsWith("night_decree"))
                {
                    nightDecreeChoices.Add(card);
                }
            }
        }

        List<UpgradeCardData> choices = new List<UpgradeCardData>();
        List<UpgradeCardData> remaining = new List<UpgradeCardData>(nightDecreeChoices);
        for (int i = 0; i < 3 && remaining.Count > 0; i++)
        {
            int idx = UnityEngine.Random.Range(0, remaining.Count);
            choices.Add(remaining[idx]);
            remaining.RemoveAt(idx);
        }

        if (choices.Count > 0)
        {
            Time.timeScale = 0f; // Pause game
            _draftUIController.OpenMenu(choices);
        }
    }
}
