using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ExperienceManager : MonoBehaviour
{

    public static ExperienceManager instance;



    // =====================
    // LEVEL DATA
    // =====================

    [Header("Level")]

    public int currentLevel = 1;

    public int currentXP = 0;

    public int xpNeeded = 100;







    // =====================
    // UI
    // =====================

    [Header("UI")]

    public Slider xpSlider;

    public TextMeshProUGUI levelText;

    public TextMeshProUGUI xpText;








    private void Awake()
    {

        instance = this;

    }








    void Start()
    {

        UpdateUI();

    }









    // =====================
    // ADD EXPERIENCE
    // =====================


    public void AddXP(int amount)
    {

        currentXP += amount;





        while (currentXP >= xpNeeded)
        {

            LevelUp();

        }






        UpdateUI();

    }












    void LevelUp()
    {

        currentXP -= xpNeeded;



        currentLevel++;




        xpNeeded += 100;





        Debug.Log(
            "LEVEL UP : "
            +
            currentLevel
        );

    }













    void UpdateUI()
    {


        if (xpSlider != null)
        {

            xpSlider.maxValue =
                xpNeeded;



            xpSlider.value =
                currentXP;

        }







        if (levelText != null)
        {

            levelText.text =
            "LEVEL "
            +
            currentLevel;

        }








        if (xpText != null)
        {

            xpText.text =
            currentXP
            +
            " / "
            +
            xpNeeded
            +
            " XP";

        }


    }


}