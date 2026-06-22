using UnityEngine;


public class SoundTrap : MonoBehaviour
{

    public static SoundTrap activeTrap;



    [Header("Health")]

    public float maxHealth = 300f;

    private float currentHealth;




    [Header("Lifetime")]

    public float lifeTime = 10f;







    void Start()
    {

        activeTrap = this;


        currentHealth =
            maxHealth;



        Invoke(
            nameof(DestroyTrap),
            lifeTime
        );

    }










    public void TakeDamage(
        float damage
    )
    {

        currentHealth -=
            damage;



        if (currentHealth <= 0)
        {

            DestroyTrap();

        }

    }









    void DestroyTrap()
    {

        if (activeTrap == this)
        {

            activeTrap = null;

        }



        Destroy(
            gameObject
        );

    }










    private void OnDestroy()
    {

        if (activeTrap == this)
        {

            activeTrap = null;

        }

    }


}