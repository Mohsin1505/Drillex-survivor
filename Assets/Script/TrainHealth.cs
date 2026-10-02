using UnityEngine;
using UnityEngine.UI;


public class TrainHealth : MonoBehaviour
{

    [Header("Health")]

    public float maxHealth = 100f;

    private float currentHealth;


    public Slider healthSlider;





    [Header("Game Over")]

    public GameObject gameOverPanel;










    void Start()
    {

        currentHealth =
            maxHealth;



        UpdateHealthBar();





        if (gameOverPanel != null)
        {

            gameOverPanel.SetActive(false);

        }

    }












    public void TakeDamage(
        float damage
    )
    {

        currentHealth -=
            damage;





        UpdateHealthBar();






        if (currentHealth <= 0)
        {

            GameOver();

        }

    }












    public void IncreaseMaxHealth(
        float amount
    )
    {


        maxHealth +=
            amount;



        currentHealth +=
            amount;




        UpdateHealthBar();

    }












    // CALLED WHEN REPAIR BUTTON PRESSED

    public void FullRepair()
    {

        currentHealth =
            maxHealth;



        UpdateHealthBar();



        Debug.Log(
            "TRAIN REPAIRED"
        );

    }












    void UpdateHealthBar()
    {


        if (healthSlider != null)
        {

            healthSlider.maxValue =
                maxHealth;


            healthSlider.value =
                currentHealth;

        }

    }






    public void RepairHealth(float amount)
    {

        currentHealth += amount;



        if (currentHealth > maxHealth)
        {

            currentHealth = maxHealth;

        }



        UpdateHealthBar();



        Debug.Log(
            "TRAIN REPAIRED"
        );

    }





    void GameOver()
    {


        Debug.Log(
            "TRAIN DESTROYED"
        );





        if (gameOverPanel != null)
        {

            gameOverPanel.SetActive(true);

        }





        Time.timeScale = 0;

    }


}