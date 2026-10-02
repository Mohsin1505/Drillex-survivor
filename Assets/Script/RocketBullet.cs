using UnityEngine;

public class RocketBullet : MonoBehaviour
{

    // =====================
    // MOVEMENT
    // =====================

    [Header("Movement")]

    public float speed = 15f;

    public float lifeTime = 5f;




    // =====================
    // HOMING
    // =====================

    [Header("Homing")]

    public bool useHoming = true;

    public float turnSpeed = 8f;


    private Transform target;








    // =====================
    // DAMAGE
    // =====================

    [Header("Explosion")]

    public float damage;

    public float explosionRadius = 8f;


    public GameObject explosionEffect;









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

            Explode();

        }

    }












    void Explode()
    {



        if (explosionEffect != null)
        {

            Instantiate(
                explosionEffect,
                transform.position,
                Quaternion.identity
            );

        }









        Collider[] hits =
        Physics.OverlapSphere(
            transform.position,
            explosionRadius
        );










        foreach (Collider hit in hits)
        {

            if (
                hit.CompareTag("Enemy")
            )
            {


                // SMALL WORM


                SmallWorm worm =
                hit.GetComponentInParent
                <SmallWorm>();




                if (worm != null)
                {

                    worm.TakeDamage(
                        damage
                    );




                    Vector3 push =
                    worm.transform.position -
                    transform.position;




                    worm.ApplyKnockback(
                        push
                    );

                }









                // BIG WORM


                WormMovement boss =
                hit.GetComponentInParent
                <WormMovement>();




                if (boss != null)
                {

                    boss.TakeDamage(
                        damage
                    );

                }


            }

        }









        Destroy(
            gameObject
        );

    }









    private void OnDrawGizmosSelected()
    {

        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius
        );

    }


}