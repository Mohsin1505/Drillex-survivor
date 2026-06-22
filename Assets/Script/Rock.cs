using UnityEngine;
using UnityEngine.UI;

public class Rock : MonoBehaviour
{

    // =====================
    // HEALTH
    // =====================

    [Header("Health")]

    public float maxHealth = 5f;

    private float currentHealth;






    // =====================
    // REWARD
    // =====================

    [Header("Reward")]

    public int orbAmount = 5;






    // =====================
    // UI
    // =====================

    [Header("UI")]

    public Slider healthSlider;









    void Start()
    {

        currentHealth =
            maxHealth;





        if (healthSlider != null)
        {

            healthSlider.maxValue =
                maxHealth;


            healthSlider.value =
                currentHealth;



            healthSlider.gameObject
            .SetActive(false);

        }

    }









    public void DamageRock(float damage)
    {

        currentHealth -=
            damage;





        if (healthSlider != null)
        {

            healthSlider.gameObject
            .SetActive(true);



            healthSlider.value =
                currentHealth;

        }







        if (currentHealth <= 0)
        {

            GiveOrb();



            Destroy(
                gameObject
            );

        }

    }











    void GiveOrb()
    {

        TrainStorageManager storage =
            FindObjectOfType
            <TrainStorageManager>();





        if (storage != null)
        {

            storage.AddOrb(
                orbAmount
            );
            ExperienceManager.instance.AddXP(30);

        }

    }

}