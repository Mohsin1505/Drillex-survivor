using System;
using System.Collections.Generic;
using UnityEngine;

// UI-side description of every purchasable item, read straight from UpgradeManager so the
// shop always shows the same costs, unlock levels and limits the purchase code enforces.
// No gameplay values live here; purchases call the existing UpgradeManager methods.
public enum UpgradeId
{
    Repair,
    BasicDamage,
    MachineDamage,
    RocketDamage,
    TrainSpeed,
    Health,
    StorageBlock,
    BasicGun,
    DualGun,
    MachineGun,
    FlameThrower,
    ElectricGun,
    RocketGun,
    RemoveBlock
}

public enum UpgradeKind { Block, Upgrade, Service }

public enum UpgradeState { Available, NeedOrbs, Locked, Maxed, Unavailable }

public class UpgradeEntry
{
    public UpgradeId id;
    public UpgradeKind kind;
    public string title;
    public string icon;
    public Color accent;
    public Func<int> cost;
    public Func<int> unlockLevel;
    public Func<int> level;      // upgrades only
    public Func<int> maxLevel;   // upgrades only
    public Func<GameObject> prefab; // blocks only
    public Func<string> stats;
    public Action buy;
    public Func<string> unavailableReason; // services (e.g. hull already full)
}

public static class UpgradeCatalog
{
    // Shared palette so the HUD, shop and toasts agree.
    public static readonly Color Hazard = new Color(1f, 0.74f, 0.12f);
    public static readonly Color Ember = new Color(1f, 0.45f, 0.12f);
    public static readonly Color Rocket = new Color(1f, 0.32f, 0.16f);
    public static readonly Color Electric = new Color(0.35f, 0.8f, 1f);
    public static readonly Color Steel = new Color(0.72f, 0.76f, 0.8f);
    public static readonly Color Heal = new Color(0.45f, 0.9f, 0.45f);

    static List<UpgradeEntry> entries;

    public static IReadOnlyList<UpgradeEntry> Entries
    {
        get { if (entries == null) Build(); return entries; }
    }

    public static UpgradeEntry Get(UpgradeId id)
    {
        foreach (var e in Entries) if (e.id == id) return e;
        return null;
    }

    static UpgradeManager U => UpgradeManager.instance;

    static int PlayerLevel => ExperienceManager.instance != null ? ExperienceManager.instance.currentLevel : 1;
    public static long Orbs => Manager.instance != null ? Manager.instance.permanentOrb : 0;

    public static UpgradeState StateOf(UpgradeEntry e)
    {
        if (U == null) return UpgradeState.Unavailable;
        if (PlayerLevel < e.unlockLevel()) return UpgradeState.Locked;
        if (e.kind == UpgradeKind.Upgrade && e.level() >= e.maxLevel()) return UpgradeState.Maxed;
        if (e.unavailableReason != null && !string.IsNullOrEmpty(e.unavailableReason())) return UpgradeState.Unavailable;
        if (Orbs < e.cost()) return UpgradeState.NeedOrbs;
        return UpgradeState.Available;
    }

    public static int OwnedCount(UpgradeEntry e)
    {
        if (e.prefab == null) return 0;
        var train = UnityEngine.Object.FindAnyObjectByType<TrainMovement>();
        return train != null ? train.CountBlocks(e.prefab()) : 0;
    }

    // True when at least one block or upgrade can be bought right now (drives the HUD pulse).
    public static bool AnythingAffordable()
    {
        if (U == null) return false;
        foreach (var e in Entries)
            if (e.kind != UpgradeKind.Service && StateOf(e) == UpgradeState.Available) return true;
        return false;
    }

    static string GunStats(GameObject prefab)
    {
        if (prefab == null) return "";
        var gun = prefab.GetComponentInChildren<AutoGun>(true);
        var guns = prefab.GetComponentsInChildren<AutoGun>(false);
        var flame = prefab.GetComponentInChildren<FlameThrower>(true);
        var storage = prefab.GetComponentInChildren<StorageBlock>(true);
        if (storage != null) return "+" + storage.storageAmount + " ORB CAPACITY";
        if (flame != null) return Mathf.RoundToInt(flame.damagePerSecond) + " DMG/S   RANGE " + Mathf.RoundToInt(flame.range);
        if (gun != null)
        {
            int barrels = Mathf.Max(1, guns.Length);
            string dmg = Mathf.RoundToInt(gun.baseDamage) + (barrels > 1 ? " x" + barrels : "");
            float rate = gun.fireRate > 0 ? 1f / gun.fireRate : 0f;
            return "DMG " + dmg + "   " + rate.ToString(rate < 2f ? "0.##" : "0") + "/S   RANGE " + Mathf.RoundToInt(gun.range);
        }
        return "";
    }

    static void Build()
    {
        entries = new List<UpgradeEntry>
        {
            // ---------------- train blocks (sorted by unlock level in the UI) ----------------
            Block(UpgradeId.StorageBlock, "STORAGE CAR", "storage", Steel, () => U.storageBlockCost, () => U.storageUnlockLevel, () => U.storageBlockPrefab, () => U.BuyStorageBlock()),
            Block(UpgradeId.BasicGun, "CANNON CAR", "cannon", Hazard, () => U.basicGunCost, () => U.basicGunUnlockLevel, () => U.basicGunPrefab, () => U.BuyBasicGun()),
            Block(UpgradeId.DualGun, "TWIN CANNON CAR", "twin", Hazard, () => U.dualGunCost, () => U.dualGunUnlockLevel, () => U.dualGunPrefab, () => U.BuyDualGun()),
            Block(UpgradeId.MachineGun, "GATLING CAR", "gatling", Hazard, () => U.machineGunCost, () => U.machineGunUnlockLevel, () => U.machineGunPrefab, () => U.BuyMachineGun()),
            Block(UpgradeId.FlameThrower, "FLAMETHROWER CAR", "flame", Ember, () => U.flameThrowerCost, () => U.flameThrowerUnlockLevel, () => U.flameThrowerPrefab, () => U.BuyFlameThrower()),
            Block(UpgradeId.ElectricGun, "TESLA ARC CAR", "bolt", Electric, () => U.electricGunCost, () => U.electricGunUnlockLevel, () => U.electricGunPrefab, () => U.BuyElectricGun()),
            Block(UpgradeId.RocketGun, "ROCKET CAR", "missile", Rocket, () => U.rocketGunCost, () => U.rocketGunUnlockLevel, () => U.rocketGunPrefab, () => U.BuyRocketGun()),

            // ---------------- upgrades ----------------
            Upgrade(UpgradeId.BasicDamage, "CANNON DAMAGE", "cannon", Hazard, () => U.basicGunUpgradeCost, () => U.basicDamageUnlockLevel, () => U.basicDamageLevel, () => "+" + U.basicDamageIncrease + " DMG PER LEVEL", () => U.UpgradeBasicGunDamage()),
            Upgrade(UpgradeId.MachineDamage, "GATLING DAMAGE", "gatling", Hazard, () => U.machineGunUpgradeCost, () => U.machineDamageUnlockLevel, () => U.machineDamageLevel, () => "+" + U.machineDamageIncrease + " DMG PER LEVEL", () => U.UpgradeMachineGunDamage()),
            Upgrade(UpgradeId.RocketDamage, "ROCKET DAMAGE", "missile", Rocket, () => U.rocketUpgradeCost, () => U.rocketDamageUnlockLevel, () => U.rocketDamageLevel, () => "+" + U.rocketDamageIncrease + " DMG PER LEVEL", () => U.UpgradeRocketDamage()),
            Upgrade(UpgradeId.TrainSpeed, "ENGINE SPEED", "speed", Steel, () => U.speedCost, () => U.speedUnlockLevel, () => U.speedLevel, () => "+" + U.speedIncrease + " SPEED PER LEVEL", () => U.UpgradeTrainSpeed()),
            Upgrade(UpgradeId.Health, "ARMOR PLATING", "shield", Heal, () => U.healthCost, () => U.healthUnlockLevel, () => U.healthLevel, () => "+" + U.healthIncrease + " MAX HULL PER LEVEL", () => U.UpgradeHealth()),

            // ---------------- services ----------------
            new UpgradeEntry
            {
                id = UpgradeId.Repair, kind = UpgradeKind.Service, title = "REPAIR", icon = "repair", accent = Heal,
                cost = () => U.repairCost, unlockLevel = () => 0,
                stats = () => "+" + U.repairAmount + " HULL",
                buy = () => U.RepairTrain(),
                unavailableReason = () =>
                {
                    var hp = UnityEngine.Object.FindAnyObjectByType<TrainHealth>();
                    return hp != null && hp.CurrentHealth >= hp.maxHealth ? "HULL FULL" : null;
                }
            },
            new UpgradeEntry
            {
                id = UpgradeId.RemoveBlock, kind = UpgradeKind.Service, title = "DETACH LAST CAR", icon = "detach", accent = Steel,
                cost = () => 0, unlockLevel = () => 0,
                stats = () => "REMOVES THE REAR CAR",
                buy = () => U.RemoveLastBlock(),
                unavailableReason = () =>
                {
                    var t = UnityEngine.Object.FindAnyObjectByType<TrainMovement>();
                    return t != null && t.BlockCount == 0 ? "NO CARS" : null;
                }
            },
        };
    }

    static UpgradeEntry Block(UpgradeId id, string title, string icon, Color accent, Func<int> cost, Func<int> unlock, Func<GameObject> prefab, Action buy)
    {
        return new UpgradeEntry { id = id, kind = UpgradeKind.Block, title = title, icon = icon, accent = accent, cost = cost, unlockLevel = unlock, prefab = prefab, buy = buy, stats = () => GunStats(prefab()) };
    }

    static UpgradeEntry Upgrade(UpgradeId id, string title, string icon, Color accent, Func<int> cost, Func<int> unlock, Func<int> level, Func<string> stats, Action buy)
    {
        return new UpgradeEntry { id = id, kind = UpgradeKind.Upgrade, title = title, icon = icon, accent = accent, cost = cost, unlockLevel = unlock, level = level, maxLevel = () => U.maxUpgradeLevel, stats = stats, buy = buy };
    }
}
