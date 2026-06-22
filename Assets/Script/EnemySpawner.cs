using System.Collections.Generic;
using UnityEngine;


public class EnemySpawner : MonoBehaviour
{


    [System.Serializable]
    public class ZoneData
    {

        public string zoneName;



        [Header("Zone Area")]
        public Transform zonePlane;





        [Header("Enemies")]

        public GameObject[] enemies;

        public float spawnRate = 5f;

        public int maxEnemiesAlive = 20;



        [HideInInspector]
        public float spawnTimer;



        [HideInInspector]
        public List<GameObject> aliveEnemies =
            new List<GameObject>();









        [Header("Rocks")]

        public GameObject[] rocks;


        public int rockAmount = 70;


        public int minRockBeforeRespawn = 20;


        public int respawnRockAmount = 40;




        [HideInInspector]
        public List<GameObject> aliveRocks =
            new List<GameObject>();

    }









    [Header("Zones")]

    public ZoneData[] zones;







    [Header("Train")]

    public Transform train;








    [Header("Enemy Spawn Distance")]

    public float minDistanceFromTrain = 35f;

    public float maxDistanceFromTrain = 60f;


    public float enemyDespawnDistance = 120f;










    [Header("Spawn Height")]

    public float enemyHeightOffset = 1f;


    public float rockHeightOffset = 0f;










    [Header("Ground Detection")]

    public LayerMask groundLayer;


    public float rayHeight = 500f;













    void Start()
    {

        SpawnStartingRocks();

    }










    void Update()
    {


        ZoneData zone =
        GetCurrentZone();




        if (zone != null)
        {

            SpawnEnemyOverTime(
                zone
            );

        }




        CheckRockRespawn();

    }













    ZoneData GetCurrentZone()
    {


        foreach (ZoneData zone in zones)
        {

            Vector3 size =
            zone.zonePlane.localScale *
            10f;




            Vector3 center =
            zone.zonePlane.position;






            if (
                train.position.x >
                center.x - size.x / 2

                &&

                train.position.x <
                center.x + size.x / 2

                &&

                train.position.z >
                center.z - size.z / 2

                &&

                train.position.z <
                center.z + size.z / 2
            )
            {

                return zone;

            }

        }



        return null;

    }












    // =====================
    // ENEMY
    // =====================


    void SpawnEnemyOverTime(
        ZoneData zone
    )
    {


        zone.spawnTimer +=
        Time.deltaTime;





        if (
            zone.spawnTimer <
            zone.spawnRate
        )
            return;





        zone.spawnTimer = 0;






        CleanEnemyList(zone);





        if (
            zone.aliveEnemies.Count
            >=
            zone.maxEnemiesAlive
        )
            return;






        SpawnEnemy(zone);

    }












    void SpawnEnemy(
        ZoneData zone
    )
    {


        if (zone.enemies.Length == 0)
            return;






        Vector3 pos =
        Vector3.zero;


        bool valid =
        false;



        int attempts =
        30;







        while (
            attempts > 0 &&
            !valid
        )
        {


            pos =
            RandomPointOnPlane(
                zone.zonePlane,
                enemyHeightOffset
            );






            float distance =
            Vector3.Distance(
                pos,
                train.position
            );





            if (
                distance >= minDistanceFromTrain
                &&
                distance <= maxDistanceFromTrain
            )
            {

                valid = true;

            }




            attempts--;

        }








        if (!valid)
            return;








        GameObject enemy =
        Instantiate(

            zone.enemies[
            Random.Range(
                0,
                zone.enemies.Length
            )],


            pos,


            Quaternion.identity
        );







        zone.aliveEnemies.Add(
            enemy
        );

    }













    // =====================
    // ROCKS
    // =====================


    void SpawnStartingRocks()
    {


        foreach (
            ZoneData zone
            in zones
        )
        {

            SpawnRocks(
                zone,
                zone.rockAmount
            );

        }

    }










    void CheckRockRespawn()
    {


        foreach (
            ZoneData zone
            in zones
        )
        {


            CleanRockList(zone);





            if (
                zone.respawnRockAmount > 0
                &&
                zone.aliveRocks.Count
                <=
                zone.minRockBeforeRespawn
            )
            {

                SpawnRocks(
                    zone,
                    zone.respawnRockAmount
                );

            }

        }

    }













    void SpawnRocks(
        ZoneData zone,
        int amount
    )
    {


        if (zone.rocks.Length == 0)
            return;







        for (
            int i = 0;
            i < amount;
            i++
        )
        {


            GameObject rock =
            Instantiate(

                zone.rocks[
                Random.Range(
                    0,
                    zone.rocks.Length
                )],



                RandomPointOnPlane(
                    zone.zonePlane,
                    rockHeightOffset
                ),



                Quaternion.Euler(
                    0,
                    Random.Range(0, 360),
                    0
                )

            );






            zone.aliveRocks.Add(
                rock
            );

        }

    }













    void CleanRockList(
        ZoneData zone
    )
    {


        for (
            int i =
            zone.aliveRocks.Count - 1;

            i >= 0;

            i--
        )
        {


            if (
                zone.aliveRocks[i]
                ==
                null
            )
            {

                zone.aliveRocks
                .RemoveAt(i);

            }

        }

    }












    // =====================
    // GROUND POINT
    // =====================


    Vector3 RandomPointOnPlane(
        Transform plane,
        float heightOffset
    )
    {


        Vector3 size =
        plane.localScale *
        10f;





        float x =
        plane.position.x +
        Random.Range(
            -size.x / 2,
            size.x / 2
        );





        float z =
        plane.position.z +
        Random.Range(
            -size.z / 2,
            size.z / 2
        );








        RaycastHit hit;






        if (
            Physics.Raycast(

                new Vector3(
                    x,
                    rayHeight,
                    z
                ),


                Vector3.down,


                out hit,


                1000f,


                groundLayer
            )
        )
        {


            return
            hit.point
            +
            Vector3.up *
            heightOffset;

        }







        return new Vector3(
            x,
            plane.position.y,
            z
        );

    }













    void CleanEnemyList(
     ZoneData zone
 )
    {

        for (
            int i =
            zone.aliveEnemies.Count - 1;

            i >= 0;

            i--
        )
        {


            // already dead enemy

            if (
                zone.aliveEnemies[i]
                ==
                null
            )
            {

                zone.aliveEnemies
                .RemoveAt(i);


                continue;

            }









            // distance from train

            float distance =
            Vector3.Distance(

                zone.aliveEnemies[i]
                .transform.position,


                train.position

            );









            // remove far enemies

            if (
                distance
                >
                enemyDespawnDistance
            )
            {


                Destroy(

                    zone.aliveEnemies[i]

                );





                zone.aliveEnemies
                .RemoveAt(i);

            }


        }

    }


}