using UnityEngine;
using UnityEngine.UI;


public class SmallWorm : MonoBehaviour
{

    [Header("Health")]
    public float maxHealth = 20f;

    private float currentHealth;

    public Slider healthSlider;



    [Header("Reward")]
    public int xpReward = 50;



    [Header("Knockback")]

    public float knockbackForce = 2f;

    public float knockbackTime = 0.12f;


    private Vector3 knockbackDirection;

    private float knockbackTimer;





    // =====================
    // TARGET
    // =====================

    private Transform target;

    private Transform trainTarget;


    private SoundTrap currentTrap;

    private bool wasChasingTrap;





    [Header("Sound Trap")]

    public float soundTrapDetectionRange = 60f;






    [Header("Chase")]

    public float moveSpeed = 5f;

    public float rotationSpeed = 5f;


    public float detectionRange = 20f;

    public float loseInterestRange = 80f;


    private bool isChasing;






    [Header("Idle Movement")]

    public float idleSpeed = 2f;

    public float changeDirectionTime = 3f;


    private float timer;

    private Vector3 idleDirection;







    [Header("Attack")]

    public float damage = 10f;

    public float attackRate = 1f;

    public float attackDistance = 3f;


    private float attackTimer;



    [Header("Train Attack Detection")]

    public float trainAttackRange = 3f;









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







        GameObject drill =
        GameObject.FindGameObjectWithTag(
            "Drill"
        );



        if (drill != null)
        {

            trainTarget =
                drill.transform;


            target =
                trainTarget;

        }





        PickRandomDirection();

    }









    void Update()
    {


        if (knockbackTimer > 0)
        {

            transform.position +=
                knockbackDirection *
                knockbackForce *
                Time.deltaTime;



            knockbackTimer -=
                Time.deltaTime;


            return;

        }






        CheckSoundTrap();


        CheckDistance();






        if (isChasing)
        {

            MoveToTarget();

        }
        else
        {

            IdleMove();

        }





        Attack();

    }










    void CheckSoundTrap()
    {


        if (
            wasChasingTrap
            &&
            SoundTrap.activeTrap == null
        )
        {

            wasChasingTrap =
                false;


            currentTrap =
                null;


            target =
                trainTarget;


            isChasing =
                false;


            attackTimer =
                0;


            timer =
                0;


            PickRandomDirection();


            return;

        }








        if (SoundTrap.activeTrap != null)
        {

            float distance =
            Vector3.Distance(
                transform.position,
                SoundTrap.activeTrap.transform.position
            );





            if (distance <= soundTrapDetectionRange)
            {

                currentTrap =
                    SoundTrap.activeTrap;


                target =
                    currentTrap.transform;


                wasChasingTrap =
                    true;


                isChasing =
                    true;


            }

        }

    }











    void CheckDistance()
    {

        if (target == null)
            return;



        if (wasChasingTrap)
            return;





        float distance =
        Vector3.Distance(
            transform.position,
            target.position
        );





        if (distance <= detectionRange)
        {

            isChasing =
                true;



            if (healthSlider != null)
                healthSlider.gameObject.SetActive(true);

        }








        if (
            isChasing
            &&
            distance >= loseInterestRange
        )
        {

            isChasing =
                false;



            PickRandomDirection();

        }

    }










    void IdleMove()
    {

        timer +=
            Time.deltaTime;




        if (timer >= changeDirectionTime)
        {

            PickRandomDirection();


            timer = 0;

        }





        transform.rotation =
        Quaternion.Lerp(
            transform.rotation,
            Quaternion.LookRotation(idleDirection),
            rotationSpeed *
            Time.deltaTime
        );





        transform.position +=
            transform.forward *
            idleSpeed *
            Time.deltaTime;

    }










    void PickRandomDirection()
    {

        idleDirection =
        new Vector3(

            Random.Range(-1f, 1f),

            0,

            Random.Range(-1f, 1f)

        ).normalized;

    }











    void MoveToTarget()
    {

        if (target == null)
            return;




        float distance =
        Vector3.Distance(
            transform.position,
            target.position
        );




        if (distance <= attackDistance)
            return;





        Vector3 dir =
        target.position -
        transform.position;



        dir.y = 0;




        if (dir.sqrMagnitude < .01f)
            return;





        transform.rotation =
        Quaternion.Lerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            rotationSpeed *
            Time.deltaTime
        );





        transform.position +=
            transform.forward *
            moveSpeed *
            Time.deltaTime;

    }











    void Attack()
    {


        // SOUND TRAP DAMAGE

        if (
            wasChasingTrap
            &&
            currentTrap != null
        )
        {


            if (
                Vector3.Distance(
                    transform.position,
                    currentTrap.transform.position
                )
                >
                attackDistance
            )
                return;





            DealDamageToTrap();


            return;

        }








        // TRAIN DAMAGE BY RANGE

        Collider[] hits =
        Physics.OverlapSphere(
            transform.position,
            trainAttackRange
        );






        foreach (Collider hit in hits)
        {

            TrainDamagePoint block =
            hit.GetComponent<TrainDamagePoint>();




            if (block != null)
            {


                attackTimer +=
                    Time.deltaTime;




                if (attackTimer >= attackRate)
                {

                    block.TakeDamage(
                        damage
                    );


                    attackTimer =
                        0;

                }



                return;

            }

        }

    }










    void DealDamageToTrap()
    {

        attackTimer +=
            Time.deltaTime;



        if (attackTimer >= attackRate)
        {

            currentTrap.TakeDamage(
                damage
            );


            attackTimer =
                0;

        }

    }











    public void TakeDamage(float damage)
    {

        if (healthSlider != null)
            healthSlider.gameObject.SetActive(true);



        currentHealth -= damage;



        if (healthSlider != null)
            healthSlider.value =
                currentHealth;



        if (currentHealth <= 0)
            Die();

    }









    public void ApplyKnockback(Vector3 direction)
    {

        direction.y = 0;


        knockbackDirection =
            direction.normalized;


        knockbackTimer =
            knockbackTime;

    }










    void Die()
    {

        if (ExperienceManager.instance != null)
        {

            ExperienceManager.instance.AddXP(
                xpReward
            );

        }



        Destroy(gameObject);

    }


}