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









        private float life;

    private bool finished;

    void Start()
    {
        life =
            lifeTime;
    }

    // Exhaust particles and trails are unparented when the rocket ends so the smoke
    // finishes fading where it was instead of vanishing or hanging off a deleted object.
    void ReleaseTrails()
    {
        foreach (ParticleSystem ps in GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.transform.parent != transform)
                continue;

            ps.transform.SetParent(null, true);
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Destroy(ps.gameObject, ps.main.startLifetime.constantMax + 0.5f);
        }

        foreach (TrailRenderer tr in GetComponentsInChildren<TrailRenderer>())
        {
            tr.transform.SetParent(null, true);
            tr.emitting = false;
            Destroy(tr.gameObject, tr.time + 0.1f);
        }
    }

    void Expire()
    {
        if (finished)
            return;

        finished = true;
        ReleaseTrails();
        Destroy(gameObject);
    }









        void Update()
    {
        life -=
            Time.deltaTime;

        if (life <= 0)
        {
            Expire();
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

            Explode();

        }

    }












        void Explode()
    {
        // Damage below runs on every call exactly as before (a rocket touching two enemy
        // colliders in one physics step still explodes twice); only the visuals/audio play once.
        bool firstExplosion = !finished;

        finished = true;

        if (firstExplosion && explosionEffect != null)
        {
            if (explosionEffect.GetComponent<PooledVfx>() != null)
            {
                PooledVfx.Play(
                    explosionEffect,
                    transform.position,
                    Quaternion.identity
                );
            }
            else
            {
                Instantiate(
                    explosionEffect,
                    transform.position,
                    Quaternion.identity
                );
            }
        }

        if (firstExplosion)
            AudioManager.Sfx(l => l.explosion, 1f, transform.position, 0.05f);









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









                ReleaseTrails();

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