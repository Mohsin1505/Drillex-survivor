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
        Rocket
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




    private float fireTimer;


    private Transform target;









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









        GameObject bullet =
        Instantiate(
            bulletPrefab,
            shootPoint.position,
            shootPoint.rotation
        );








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