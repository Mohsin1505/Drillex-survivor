using UnityEngine;

public class FlameThrower : MonoBehaviour
{


    [Header("Detection")]

    public float range = 15f;

    public string enemyTag = "Enemy";




    [Header("Rotation")]

    public float rotationSpeed = 8f;





    [Header("Damage")]

    public float damagePerSecond = 20f;

    public float flameAngle = 35f;





    [Header("Effect")]

    public ParticleSystem flameEffect;





        private Transform target;

    private AudioSource flameAudio;







    void Start()
    {

                if (flameEffect != null)
        {
            flameEffect.Stop();
        }

        if (AudioManager.Instance != null && AudioManager.Instance.Library != null && AudioManager.Instance.Library.flameLoop != null)
        {
            flameAudio = gameObject.AddComponent<AudioSource>();
            flameAudio.clip = AudioManager.Instance.Library.flameLoop;
            flameAudio.loop = true;
            flameAudio.playOnAwake = false;
            flameAudio.spatialBlend = 0.6f;
            flameAudio.minDistance = 8f;
            flameAudio.maxDistance = 60f;
            flameAudio.rolloffMode = AudioRolloffMode.Linear;
        }
    }

    // looping roar that fades with the flame; volume follows the SFX setting
    void UpdateFlameAudio(bool burning)
    {
        if (flameAudio == null)
            return;

        float target = burning ? 0.55f * AudioManager.SfxScale : 0f;
        flameAudio.volume = Mathf.MoveTowards(flameAudio.volume, target, Time.deltaTime * 3f);

        if (burning && !flameAudio.isPlaying)
            flameAudio.Play();
        else if (!burning && flameAudio.isPlaying && flameAudio.volume <= 0f)
            flameAudio.Stop();
    }









    void Update()
    {

                FindClosestEnemy();

        UpdateFlameAudio(target != null);

        if (target != null)
        {

            RotateToEnemy();


            Burn();


            if (flameEffect != null &&
               !flameEffect.isPlaying)
            {

                flameEffect.Play();

            }

        }


        else
        {

            if (flameEffect != null &&
               flameEffect.isPlaying)
            {

                flameEffect.Stop();

            }

        }

    }









    void FindClosestEnemy()
    {

        GameObject[] enemies =
        GameObject.FindGameObjectsWithTag(
            enemyTag
        );



        float closestDistance =
            Mathf.Infinity;


        Transform closest =
            null;





        foreach (GameObject enemy in enemies)
        {

            float distance =
            Vector3.Distance(
                transform.position,
                enemy.transform.position
            );



            if (distance < closestDistance &&
               distance <= range)
            {

                closestDistance =
                    distance;


                closest =
                    enemy.transform;

            }

        }




        target =
            closest;

    }











    void RotateToEnemy()
    {

        Vector3 direction =
            target.position -
            transform.position;



        direction.y = 0;




        Quaternion look =
        Quaternion.LookRotation(
            direction
        );




        transform.rotation =
        Quaternion.Lerp(
            transform.rotation,
            look,
            rotationSpeed *
            Time.deltaTime
        );

    }









    void Burn()
    {

        Collider[] hits =
        Physics.OverlapSphere(
            transform.position,
            range
        );




        foreach (Collider hit in hits)
        {

            if (hit.CompareTag(enemyTag))
            {


                Vector3 direction =
                    hit.transform.position -
                    transform.position;



                direction.y = 0;




                float angle =
                Vector3.Angle(
                    transform.forward,
                    direction
                );





                if (angle <= flameAngle)
                {

                    SmallWorm worm =
                    hit.GetComponent<SmallWorm>();



                    if (worm != null)
                    {

                        worm.TakeDamage(
                            damagePerSecond *
                            Time.deltaTime
                        );

                    }

                }

            }

        }

    }









    private void OnDrawGizmosSelected()
    {

        Gizmos.DrawWireSphere(
            transform.position,
            range
        );

    }

}