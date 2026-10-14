using UnityEngine;

public class AutoGun : MonoBehaviour
{


    // =========================
    // WEAPON TYPE
    // =========================

    public enum WeaponType
    {
                Basic,
        MachineGun,
        Rocket,
        Electric
    }



    [Header("Weapon Type")]

    public WeaponType weaponType;





    [Header("Weapon Damage")]

    public float baseDamage = 10f;









    // =========================
    // DETECTION
    // =========================

    [Header("Detection")]

    public float range = 30f;

    public string enemyTag = "Enemy";










    // =========================
    // ROTATION
    // =========================

    [Header("Rotation")]

    public float rotationSpeed = 8f;










    // =========================
    // SHOOTING
    // =========================

    [Header("Shooting")]

    public GameObject bulletPrefab;

    public Transform shootPoint;

    public float fireRate = 1f;




        // =========================
    // EFFECTS (visual / audio only)
    // =========================

    public enum ShotSound
    {
        Auto,
        Basic,
        Double,
        MachineGun,
        Rocket,
        Electric,
        None
    }

    [Header("Effects")]
    [Tooltip("Spawned once under the shoot point and replayed on every shot.")]
    public GameObject muzzleFlashPrefab;

    public ShotSound shotSound = ShotSound.Auto;

    [Range(0f, 1f)]
    public float shotVolume = 0.7f;

    private ParticleSystem muzzleFlash;

    private LightningBurst muzzleArcs;

    private float fireTimer;


    private Transform target;









        void Start()
    {
        if (muzzleFlashPrefab != null && shootPoint != null)
        {
            GameObject fx =
            Instantiate(
                muzzleFlashPrefab,
                shootPoint
            );

            fx.transform.localPosition = Vector3.zero;
            fx.transform.localRotation = Quaternion.identity;

            muzzleFlash = fx.GetComponent<ParticleSystem>();
            muzzleArcs = fx.GetComponentInChildren<LightningBurst>(true);

            if (muzzleArcs != null)
                muzzleArcs.enabled = false;
        }
    }

    void PlayShotEffects()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            muzzleFlash.Play(true);
        }

        if (muzzleArcs != null)
        {
            muzzleArcs.enabled = false;
            muzzleArcs.enabled = true;
        }

        ShotSound s = shotSound;

        if (s == ShotSound.Auto)
        {
            if (weaponType == WeaponType.MachineGun) s = ShotSound.MachineGun;
            else if (weaponType == WeaponType.Rocket) s = ShotSound.Rocket;
            else if (weaponType == WeaponType.Electric) s = ShotSound.Electric;
            else s = ShotSound.Basic;
        }

        Vector3 pos = shootPoint.position;

        switch (s)
        {
            case ShotSound.Basic: AudioManager.Sfx(l => l.basicShot, shotVolume, pos, 0.05f); break;
            case ShotSound.Double: AudioManager.Sfx(l => l.doubleShot, shotVolume, pos, 0.05f); break;
            case ShotSound.MachineGun: AudioManager.Sfx(l => l.machineGunShot, shotVolume, pos, 0.07f, 0.08f); break;
            case ShotSound.Rocket: AudioManager.Sfx(l => l.rocketLaunch, shotVolume, pos, 0.1f); break;
            case ShotSound.Electric: AudioManager.Sfx(l => l.electricShot, shotVolume, pos, 0.05f); break;
        }
    }

    void Update()
    {

        FindClosestEnemy();




        if (target != null)
        {

            RotateGun();


            Shoot();

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






            if (
                distance < closestDistance
                &&
                distance <= range
            )
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












    void RotateGun()
    {

        Vector3 direction =
            target.position -
            transform.position;




        direction.y = 0;





        Quaternion lookRotation =
        Quaternion.LookRotation(
            direction
        );





        transform.rotation =
        Quaternion.Lerp(
            transform.rotation,
            lookRotation,
            rotationSpeed *
            Time.deltaTime
        );

    }












    void Shoot()
    {

        fireTimer +=
            Time.deltaTime;





        if (fireTimer < fireRate)
            return;









                // plain bullets are pooled (machine gun fires 10 per second); rockets are rare and instantiated
        GameObject bullet =
        bulletPrefab.GetComponent<Bullet>() != null
        ?
        PrefabPool.Spawn(
            bulletPrefab,
            shootPoint.position,
            shootPoint.rotation
        )
        :
        Instantiate(
            bulletPrefab,
            shootPoint.position,
            shootPoint.rotation
        );

        PlayShotEffects();








        float finalDamage =
            baseDamage;








        if (
            UpgradeManager.instance
            !=
            null
        )
        {


            if (
                weaponType ==
                WeaponType.Basic
            )
            {

                finalDamage +=
                UpgradeManager
                .instance
                .basicGunBonus;

            }






            if (
                weaponType ==
                WeaponType.MachineGun
            )
            {

                finalDamage +=
                UpgradeManager
                .instance
                .machineGunBonus;

            }






                        if (
                weaponType ==
                WeaponType.Rocket
            )
            {
                finalDamage +=
                UpgradeManager
                .instance
                .rocketGunBonus;
            }

            if (
                weaponType ==
                WeaponType.Electric
            )
            {
                finalDamage +=
                UpgradeManager
                .instance
                .electricGunBonus;
            }


        }









        // =========================
        // NORMAL BULLET
        // =========================


        Bullet normalBullet =
        bullet.GetComponent
        <Bullet>();




        if (normalBullet != null)
        {

            normalBullet.damage =
                finalDamage;




            normalBullet.SetTarget(
                target
            );

        }









        // =========================
        // ROCKET BULLET
        // =========================


        RocketBullet rocket =
        bullet.GetComponent
        <RocketBullet>();




        if (rocket != null)
        {

            rocket.damage =
                finalDamage;




            rocket.SetTarget(
                target
            );

        }










        bullet.transform.parent =
            null;





        fireTimer = 0;

    }


}