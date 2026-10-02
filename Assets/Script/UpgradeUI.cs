using UnityEngine;
using TMPro;


public class UpgradeUI : MonoBehaviour
{

    [Header("Weapon Upgrade Text")]

    public TMP_Text basicDamageText;

    public TMP_Text machineDamageText;

    public TMP_Text rocketDamageText;



    [Header("Train Upgrade Text")]

    public TMP_Text fireRateText;

    public TMP_Text speedText;

    public TMP_Text healthText;







    void Update()
    {

        UpdateTexts();

    }









    void UpdateTexts()
    {

        if (UpgradeManager.instance == null)
            return;



        UpgradeManager u =
        UpgradeManager.instance;








        if (basicDamageText != null)
        {

            basicDamageText.text =
            "Basic Damage\n" +
            "Level: " +
            u.basicDamageLevel +
            "/" +
            u.maxUpgradeLevel +
            "\nCost: " +
            u.basicGunUpgradeCost;

        }








        if (machineDamageText != null)
        {

            machineDamageText.text =
            "Machine Damage\n" +
            "Level: " +
            u.machineDamageLevel +
            "/" +
            u.maxUpgradeLevel +
            "\nCost: " +
            u.machineGunUpgradeCost;

        }








        if (rocketDamageText != null)
        {

            rocketDamageText.text =
            "Rocket Damage\n" +
            "Level: " +
            u.rocketDamageLevel +
            "/" +
            u.maxUpgradeLevel +
            "\nCost: " +
            u.rocketUpgradeCost;

        }








        if (fireRateText != null)
        {

            fireRateText.text =
            "Fire Rate\n" +
            "Level: " +
            u.fireRateLevel +
            "/" +
            u.maxUpgradeLevel +
            "\nCost: " +
            u.fireRateCost;

        }









        if (speedText != null)
        {

            speedText.text =
            "Train Speed\n" +
            "Level: " +
            u.speedLevel +
            "/" +
            u.maxUpgradeLevel +
            "\nCost: " +
            u.speedCost;

        }










        if (healthText != null)
        {

            healthText.text =
            "Train Health\n" +
            "Level: " +
            u.healthLevel +
            "/" +
            u.maxUpgradeLevel +
            "\nCost: " +
            u.healthCost;

        }

    }


}