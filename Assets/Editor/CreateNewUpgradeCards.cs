using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor utility: tao 10 the nang cap moi da dang (Unlock, Instant manh, Stat kep Rare/Epic).
/// Menu: Cloud Terrace / Create New Upgrade Cards
/// </summary>
public static class CreateNewUpgradeCards
{
    private const string SavePath = "Assets/ScriptData/Upgrade Card Data/";

    [MenuItem("Cloud Terrace/Create New Upgrade Cards")]
    public static void CreateAll()
    {
        int created = 0;

        // COMMON
        created += TryCreate("HarvestSeason", card =>
        {
            card.cardName   = "Mua Thu Hoach";
            card.description = "Toc do thu hoach thuc an tang 30%. Dan lang no bung, lam viec hang hon!";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Common;
            card.foodGatherMultiplier = 1.30f;
        });

        created += TryCreate("SturdyTools", card =>
        {
            card.cardName   = "Cong Cu Ben";
            card.description = "Dan lang mang them +2 tai nguyen moi chuyen di.";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Common;
            card.villagerCarryCapacityBonus = 2;
        });

        created += TryCreate("QuickFoot", card =>
        {
            card.cardName   = "Chan Nhanh";
            card.description = "Toc do di chuyen dan lang +20%.";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Common;
            card.villagerSpeedMultiplier = 1.20f;
        });

        // RARE
        created += TryCreate("WarDrum", card =>
        {
            card.cardName   = "Trong Chien";
            card.description = "Toc do di chuyen linh +20% va sat thuong +15%.";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Rare;
            card.unitSpeedMultiplier  = 1.20f;
            card.unitDamageMultiplier = 1.15f;
        });

        created += TryCreate("FortifiedWalls", card =>
        {
            card.cardName   = "Tuong Luy Vung";
            card.description = "Mau toi da tat ca cong trinh tang 25%.";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Rare;
            card.buildingMaxHealthMultiplier = 1.25f;
        });

        created += TryCreate("EmergencySupply", card =>
        {
            card.cardName   = "Tiep Te Khan Cap";
            card.description = "Ngay lap tuc nhan duoc 150 Go, 100 Da va 80 Thuc An.";
            card.cardType   = UpgradeCardType.Instant;
            card.rarity     = CardRarity.Rare;
            card.instantWoodAmount  = 150;
            card.instantStoneAmount = 100;
            card.instantFoodAmount  = 80;
        });

        created += TryCreate("FieldMedic", card =>
        {
            card.cardName   = "Quan Y Chien Truong";
            card.description = "Hoi phuc 40% mau Nha Chinh.";
            card.cardType   = UpgradeCardType.Instant;
            card.rarity     = CardRarity.Rare;
            card.healMainBuildingPercent = 0.40f;
        });

        created += TryCreate("ClanReinforcement", card =>
        {
            card.cardName   = "Quan Tang Vien";
            card.description = "Trieu hoi ngay 4 dan binh tai Nha Chinh.";
            card.cardType   = UpgradeCardType.Instant;
            card.rarity     = CardRarity.Rare;
            card.instantMilitiaCount = 4;
        });

        // EPIC
        created += TryCreate("BattleHardened", card =>
        {
            card.cardName   = "Day Dan Chien Tran";
            card.description = "Mau linh +30%, sat thuong +20%, toc do +10%.";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Epic;
            card.unitHealthMultiplier = 1.30f;
            card.unitDamageMultiplier = 1.20f;
            card.unitSpeedMultiplier  = 1.10f;
        });

        created += TryCreate("AbundantLand", card =>
        {
            card.cardName   = "Dat Dai Mau Mo";
            card.description = "Toc do khai thac Go, Da, Vang va Thuc An deu tang 20%.";
            card.cardType   = UpgradeCardType.StatBuff;
            card.rarity     = CardRarity.Epic;
            card.woodGatherMultiplier  = 1.20f;
            card.stoneGatherMultiplier = 1.20f;
            card.goldGatherMultiplier  = 1.20f;
            card.foodGatherMultiplier  = 1.20f;
        });

        // LEGENDARY
        created += TryCreate("WrathOfHeaven", card =>
        {
            card.cardName   = "Thien Loi Giang Xuong";
            card.description = "Nha Chinh hoi phuc hoan toan. Nhan 200 Vang va 3 dan binh. An sung cua troi!";
            card.cardType   = UpgradeCardType.Instant;
            card.rarity     = CardRarity.Legendary;
            card.healMainBuilding      = true;
            card.instantResourceType   = ResourceType.Gold;
            card.instantResourceAmount = 200;
            card.instantMilitiaCount   = 3;
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[CreateNewUpgradeCards] Done! Created {created} new card assets in {SavePath}");
        EditorUtility.DisplayDialog("Tao The Thanh Cong", $"Da tao {created} the moi tai:\n{SavePath}", "OK");
    }

    private static int TryCreate(string fileName, System.Action<UpgradeCardData> configure)
    {
        string fullPath = SavePath + fileName + ".asset";
        if (AssetDatabase.LoadAssetAtPath<UpgradeCardData>(fullPath) != null)
        {
            Debug.Log($"[CreateNewUpgradeCards] Skipped (already exists): {fileName}");
            return 0;
        }

        var card = ScriptableObject.CreateInstance<UpgradeCardData>();
        card.cardId = fileName;
        configure(card);

        AssetDatabase.CreateAsset(card, fullPath);
        Debug.Log($"[CreateNewUpgradeCards] Created: {fileName} ({card.rarity})");
        return 1;
    }
}
