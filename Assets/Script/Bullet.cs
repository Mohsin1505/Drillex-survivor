using UnityEngine;

public class Bullet : MonoBehaviour
{

    // =====================
    // MOVEMENT
    // =====================

    public float speed = 50f;


    public float lifeTime = 3f;


    public float damage;





    // =====================
    // HOMING
    // =====================

    [Header("Homing")]

    public bool useHoming;

    public float turnSpeed = 5f;



    private Transform target;








    void Start()
    {

        Destroy(
            gameObject,
            lifeTime
        );

    }










    void Update()
    {


        if (
            useHoming &&
            target != null
        )
        {

            Vector3 direction =
                target.position -
                transform.position;



            Quaternion look =
            Quaternion.LookRotation(
                direction
            );




            transform.rotation =
            Quaternion.Lerp(
                transform.rotation,
                look,
                turnSpeed *
                Time.deltaTime
            );

        }








        transform.position +=
            transform.forward *
            speed *
            Time.deltaTime;

    }











    public void SetTarget(
        Transform newTarget
    )
    {

        target =
            newTarget;

    }













    private void OnTriggerEnter(
        Collider other
    )
    {

        if (
            other.CompareTag("Enemy")
        )
        {



            // SMALL WORM

            SmallWorm enemy =
            other.GetComponentInParent
            <SmallWorm>();





            if (enemy != null)
            {

                enemy.TakeDamage(
                    damage
                );





                Vector3 push =
                    enemy.transform.position -
                    transform.position;



                enemy.ApplyKnockback(
                    push
                );





                Destroy(
                    gameObject
                );


                return;

            }









            // BIG WORM

            WormMovement boss =
            other.GetComponentInParent
            <WormMovement>();




            if (boss != null)
            {

                boss.TakeDamage(
                    damage
                );



                Destroy(
                    gameObject
                );


                return;

            }

        }

    }


}