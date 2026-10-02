using System.Collections.Generic;
using UnityEngine;

public class TrainMovement : MonoBehaviour
{

    // =====================
    // SPEED
    // =====================

    [Header("Speed")]

    public float normalSpeed = 5f;

    public float drillingSpeed = 2f;

    public float currentSpeed;






    // =====================
    // STEERING
    // =====================

    [Header("Steering")]

    public float steerSpeed = 180f;








    // =====================
    // BLOCK SYSTEM
    // =====================

    [Header("Starting Blocks")]

    public GameObject[] startingBlocks;




    [Header("Follow Settings")]

    public float blockDistance = 3f;









    private bool isDrilling;





    private List<Transform> trainBlocks =
        new List<Transform>();









    void Start()
    {

        SpawnStartingBlocks();

    }









    void Update()
    {

        HandleSpeed();


        MoveHead();


        MoveBlocks();

    }










    // =====================
    // SPEED CONTROL
    // =====================


    void HandleSpeed()
    {

        if (isDrilling)
        {
            currentSpeed =
                drillingSpeed;
        }

        else
        {
            currentSpeed =
                normalSpeed;
        }

    }









    public void SetDrilling(bool value)
    {

        isDrilling =
            value;

    }











    // =====================
    // HEAD MOVEMENT
    // =====================


    void MoveHead()
    {

        transform.position +=
            transform.forward *
            currentSpeed *
            Time.deltaTime;






        float steer =
            Input.GetAxis(
                "Horizontal"
            );





        transform.Rotate(
            Vector3.up *
            steer *
            Time.deltaTime *
            steerSpeed
        );

    }











    // =====================
    // SPAWN BLOCKS
    // =====================


    void SpawnStartingBlocks()
    {


        Vector3 spawnPosition =
            transform.position;






        foreach (GameObject prefab in startingBlocks)
        {


            spawnPosition -=
                transform.forward *
                blockDistance;






            GameObject block =
            Instantiate(
                prefab,
                spawnPosition,
                transform.rotation
            );








            // connect damage system

            TrainDamagePoint damage =
                block.GetComponent
                <TrainDamagePoint>();



            if (damage != null)
            {

                damage.SetTrainHealth(
                    GetComponent<TrainHealth>()
                );

            }








            trainBlocks.Add(
                block.transform
            );

        }










        // update storage

        TrainStorageManager storage =
            GetComponent
            <TrainStorageManager>();



        if (storage != null)
        {

            storage.CalculateStorage();

        }

    }











    // =====================
    // MOVE BLOCKS
    // =====================


    void MoveBlocks()
    {

        for (int i = 0;
            i < trainBlocks.Count;
            i++)
        {




            Transform target;


            if (i == 0)
            {
                target =
                    transform;
            }

            else
            {
                target =
                    trainBlocks[i - 1];
            }








            float distance =
            Vector3.Distance(
                trainBlocks[i].position,
                target.position
            );








            if (distance > blockDistance)
            {


                Vector3 direction =
                (
                    target.position -
                    trainBlocks[i].position
                ).normalized;






                trainBlocks[i].position +=
                    direction *
                    (distance - blockDistance);

            }








            // rotate to front block

            Vector3 lookDirection =
                target.position -
                trainBlocks[i].position;




            lookDirection.y = 0;






            if (lookDirection != Vector3.zero)
            {

                trainBlocks[i].rotation =
                    Quaternion.LookRotation(
                        lookDirection
                    );

            }

        }

    }
    // =====================
    // ADD NEW TRAIN BLOCK
    // =====================


    public void AddTrainBlock(GameObject blockPrefab)
    {

        Vector3 spawnPosition;


        // if train already has blocks
        if (trainBlocks.Count > 0)
        {

            Transform lastBlock =
                trainBlocks[
                    trainBlocks.Count - 1
                ];


            spawnPosition =
                lastBlock.position -
                lastBlock.forward *
                blockDistance;

        }


        // if only head exists
        else
        {

            spawnPosition =
                transform.position -
                transform.forward *
                blockDistance;

        }






        GameObject block =
        Instantiate(
            blockPrefab,
            spawnPosition,
            transform.rotation
        );






        // connect health system

        TrainDamagePoint damage =
            block.GetComponent
            <TrainDamagePoint>();


        if (damage != null)
        {

            damage.SetTrainHealth(
                GetComponent<TrainHealth>()
            );

        }








        trainBlocks.Add(
            block.transform
        );








        // recalculate storage

        TrainStorageManager storage =
            GetComponent
            <TrainStorageManager>();


        if (storage != null)
        {

            storage.CalculateStorage();

        }


    }
    // =====================
    // REMOVE LAST BLOCK
    // =====================


    public void RemoveLastBlock()
    {

        if (trainBlocks.Count <= 0)
        {
            return;
        }



        Transform lastBlock =
            trainBlocks[
                trainBlocks.Count - 1
            ];



        trainBlocks.Remove(
            lastBlock
        );



        Destroy(
            lastBlock.gameObject
        );




        TrainStorageManager storage =
            GetComponent<TrainStorageManager>();


        if (storage != null)
        {
            storage.CalculateStorage();
        }

    }

}