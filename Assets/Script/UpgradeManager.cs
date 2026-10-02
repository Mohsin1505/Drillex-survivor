using UnityEngine;


public class UpgradeManager : MonoBehaviour
{

    public static UpgradeManager instance;



    // =====================
    // WEAPON DAMAGE BONUS
    // =====================

    [Header("Weapon Damage Bonus")]

    public float basicGunBonus;
    public float machineGunBonus;
    public float rocketGunBonus;






    // =====================
    // UPGRADE LEVEL LIMITS
    // =====================

    [Header("Upgrade Levels")]

    public int maxUpgradeLevel = 10;


    public int basicDamageLevel;
    public int machineDamageLevel;
    public int rocketDamageLevel;

    public int fireRateLevel;
    public int speedLevel;
    public int healthLevel;









    // =====================
    // BLOCK PREFABS
    // =====================

    [Header("Block Prefabs")]

    public GameObject storageBlockPrefab;

    public GameObject basicGunPrefab;

    public GameObject dualGunPrefab;

    public GameObject machineGunPrefab;

    public GameObject rocketGunPrefab;

    public GameObject flameThrowerPrefab;









    // =====================
    // COSTS
    // =====================

    [Header("Upgrade Costs")]

    public int basicGunUpgradeCost = 50;

    public int machineGunUpgradeCost = 100;

    public int rocketUpgradeCost = 200;


    public int fireRateCost = 50;

    public int speedCost = 50;

    public int healthCost = 100;




    [Header("Repair")]

    public int repairCost = 50;

    public float repairAmount = 50;











    [Header("Block Costs")]

    public int storageBlockCost = 500;

    public int basicGunCost = 1000;

    public int dualGunCost = 2000;

    public int machineGunCost = 3000;

    public int rocketGunCost = 5000;

    public int flameThrowerCost = 4000;









    // =====================
    // UNLOCK LEVELS
    // =====================

    [Header("Upgrade Unlock Levels")]

    public int basicDamageUnlockLevel = 1;

    public int machineDamageUnlockLevel = 5;

    public int rocketDamageUnlockLevel = 8;


    public int fireRateUnlockLevel = 3;

    public int speedUnlockLevel = 2;

    public int healthUnlockLevel = 2;











    [Header("Block Unlock Levels")]

    public int storageUnlockLevel = 2;

    public int basicGunUnlockLevel = 3;

    public int dualGunUnlockLevel = 5;

    public int flameThrowerUnlockLevel = 7;

    public int machineGunUnlockLevel = 8;

    public int rocketGunUnlockLevel = 10;









    // =====================
    // AMOUNTS
    // =====================

    [Header("Upgrade Amount")]

    public float basicDamageIncrease = 5;

    public float machineDamageIncrease = 2;

    public float rocketDamageIncrease = 25;


    public float fireRateDecrease = .1f;

    public float speedIncrease = 1;

    public float healthIncrease = 50;









    void Awake()
    {

        instance = this;

    }










    bool IsUnlocked(int level)
    {

        if (
            ExperienceManager.instance.currentLevel
            <
            level
        )
        {

            Debug.Log("LOCKED LEVEL " + level);

            return false;

        }


        return true;

    }










    bool SpendOrb(int amount)
    {


        if (
            Manager.instance.permanentOrb
            <
            amount
        )
        {

            return false;

        }





        Manager.instance.permanentOrb -=
            amount;



        Manager.instance.UpdateOrbUI();



        return true;

    }












    // =====================
    // REPAIR TRAIN
    // =====================


    public void RepairTrain()
    {


        if (!SpendOrb(repairCost))
        {

            Debug.Log(
                "NOT ENOUGH ORBS TO REPAIR"
            );


            return;

        }






        TrainHealth hp =
        FindObjectOfType<TrainHealth>();





        if (hp != null)
        {

            hp.RepairHealth(
                repairAmount
            );

        }



    }









    // =====================
    // DAMAGE UPGRADES
    // =====================


    public void UpgradeBasicGunDamage()
    {

        if (basicDamageLevel >= maxUpgradeLevel)
            return;


        if (!IsUnlocked(basicDamageUnlockLevel))
            return;


        if (!SpendOrb(basicGunUpgradeCost))
            return;




        basicGunBonus += basicDamageIncrease;


        basicDamageLevel++;


        basicGunUpgradeCost += 50;

    }







    public void UpgradeMachineGunDamage()
    {

        if (machineDamageLevel >= maxUpgradeLevel)
            return;


        if (!IsUnlocked(machineDamageUnlockLevel))
            return;


        if (!SpendOrb(machineGunUpgradeCost))
            return;



        machineGunBonus += machineDamageIncrease;


        machineDamageLevel++;


        machineGunUpgradeCost += 100;

    }







    public void UpgradeRocketDamage()
    {

        if (rocketDamageLevel >= maxUpgradeLevel)
            return;


        if (!IsUnlocked(rocketDamageUnlockLevel))
            return;


        if (!SpendOrb(rocketUpgradeCost))
            return;



        rocketGunBonus += rocketDamageIncrease;


        rocketDamageLevel++;


        rocketUpgradeCost += 200;

    }









    public void UpgradeFireRate()
    {

        if (fireRateLevel >= maxUpgradeLevel)
            return;


        if (!SpendOrb(fireRateCost))
            return;



        foreach (AutoGun gun in FindObjectsOfType<AutoGun>())
        {

            gun.fireRate -= fireRateDecrease;


            if (gun.fireRate < .1f)
                gun.fireRate = .1f;

        }



        fireRateLevel++;


        fireRateCost += 50;

    }









    public void UpgradeTrainSpeed()
    {

        if (speedLevel >= maxUpgradeLevel)
            return;



        if (!SpendOrb(speedCost))
            return;





        TrainMovement train =
        FindObjectOfType<TrainMovement>();



        if (train != null)
        {

            train.normalSpeed += speedIncrease;

        }



        speedLevel++;


        speedCost += 50;

    }










    public void UpgradeHealth()
    {

        if (healthLevel >= maxUpgradeLevel)
            return;


        if (!SpendOrb(healthCost))
            return;





        TrainHealth hp =
        FindObjectOfType<TrainHealth>();


        if (hp != null)
        {

            hp.IncreaseMaxHealth(
                healthIncrease
            );

        }



        healthLevel++;


        healthCost += 100;

    }










    // =====================
    // BLOCKS
    // =====================


    void BuyBlock(GameObject prefab, int cost)
    {


        if (!SpendOrb(cost))
            return;



        FindObjectOfType<TrainMovement>()
        .AddTrainBlock(
            prefab
        );

    }








    public void BuyStorageBlock()
    {
        if (IsUnlocked(storageUnlockLevel))
            BuyBlock(storageBlockPrefab, storageBlockCost);
    }


    public void BuyBasicGun()
    {
        if (IsUnlocked(basicGunUnlockLevel))
            BuyBlock(basicGunPrefab, basicGunCost);
    }


    public void BuyDualGun()
    {
        if (IsUnlocked(dualGunUnlockLevel))
            BuyBlock(dualGunPrefab, dualGunCost);
    }


    public void BuyFlameThrower()
    {
        if (IsUnlocked(flameThrowerUnlockLevel))
            BuyBlock(flameThrowerPrefab, flameThrowerCost);
    }


    public void BuyMachineGun()
    {
        if (IsUnlocked(machineGunUnlockLevel))
            BuyBlock(machineGunPrefab, machineGunCost);
    }


    public void BuyRocketGun()
    {
        if (IsUnlocked(rocketGunUnlockLevel))
            BuyBlock(rocketGunPrefab, rocketGunCost);
    }









    public void RemoveLastBlock()
    {


        TrainMovement train =
        FindObjectOfType<TrainMovement>();


        if (train != null)
        {

            train.RemoveLastBlock();

        }

    }


}