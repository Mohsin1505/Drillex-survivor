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



        [Header("Impact (visual / audio only)")]
    public GameObject impactEffect;

    public bool electricImpactSound;

    private Transform target;

    private float life;

    private TrailRenderer[] trails;








        void Awake()
    {
        trails = GetComponentsInChildren<TrailRenderer>(true);
    }

    // Bullets come from PrefabPool, so state is reset on every spawn instead of in Start.
    void OnEnable()
    {
        life = lifeTime;
        target = null;

        if (trails != null)
        {
            foreach (TrailRenderer t in trails)
                t.Clear();
        }
    }

    void Despawn()
    {
        PrefabPool.Release(gameObject);
    }

    void Impact()
    {
        if (impactEffect != null)
        {
            PooledVfx.Play(
                impactEffect,
                transform.position,
                Quaternion.LookRotation(-transform.forward)
            );
        }

        if (electricImpactSound)
            AudioManager.Sfx(l => l.electricImpact, 0.6f, transform.position, 0.06f);
        else
            AudioManager.Sfx(l => l.bulletImpact, 0.35f, transform.position, 0.06f, 0.12f);
    }










        void Update()
    {
        life -=
            Time.deltaTime;

        if (life <= 0)
        {
            Despawn();
            return;
        }



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
                Impact();
                Despawn();


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
                Impact();
                Despawn();


                return;

            }

        }

    }


}