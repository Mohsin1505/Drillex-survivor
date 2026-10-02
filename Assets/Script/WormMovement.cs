using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


public class WormMovement : MonoBehaviour
{


    // =========================
    // HEALTH
    // =========================

    [Header("Health")]

    public float maxHealth = 5000f;

    private float currentHealth;


    public Slider bossHealthSlider;

    public GameObject bossHealthPanel;









    // =========================
    // ATTACK
    // =========================

    [Header("Attack")]

    public float damage = 200f;

    public float attackDistance = 5f;

    public float attackRate = 1f;

    public float stunDuration = 3f;


    private float attackTimer;

    private bool stunned;

    private float stunTimer;










    // =========================
    // MOVEMENT
    // =========================

    [Header("Movement")]

    public float MoveSpeed = 5f;

    public float TurnSpeed = 5f;










    // =========================
    // BODY
    // =========================

    [Header("Body")]

    public GameObject BodyPrefab;

    public int BodyCount = 5;

    public float BodySpeed = 5f;

    public int Gap = 10;











    // =========================
    // TARGET
    // =========================

    [Header("Target")]

    public Transform target;










    // =========================
    // RETREAT
    // =========================

    [Header("Retreat Home")]

    public Transform wormHome;

    public float homeReachDistance = 5f;


    private bool retreating;









    private List<GameObject> BodyParts =
        new List<GameObject>();


    private List<Vector3> PositionHistory =
        new List<Vector3>();









    void Start()
    {

        currentHealth =
            maxHealth;





        // FIND DRILL

        GameObject drill =
        GameObject.FindGameObjectWithTag(
            "Drill"
        );



        if (drill != null)
        {

            target =
                drill.transform;

        }








        // HEALTH BAR

        if (bossHealthPanel != null)
        {

            bossHealthPanel
            .SetActive(true);

        }





        if (bossHealthSlider != null)
        {

            bossHealthSlider.maxValue =
                maxHealth;



            bossHealthSlider.value =
                currentHealth;

        }









        for (int i = 0; i < BodyCount; i++)
        {

            GrowWorm();

        }

    }












    void Update()
    {


        // STUN

        if (stunned)
        {

            stunTimer -=
                Time.deltaTime;



            if (stunTimer <= 0)
            {

                stunned = false;

            }



            MoveBody();


            return;

        }








        if (retreating)
        {

            ReturnHome();

        }

        else
        {

            FollowTarget();

        }





        MoveBody();

    }













    void FollowTarget()
    {

        Transform currentTarget =
            target;





        // TRAP PRIORITY

        if (
            SoundTrap.activeTrap
            !=
            null
        )
        {

            currentTarget =
            SoundTrap
            .activeTrap
            .transform;

        }






        if (currentTarget == null)
            return;








        float distance =
        Vector3.Distance(
            transform.position,
            currentTarget.position
        );








        if (distance <= attackDistance)
        {

            AttackTarget(
                currentTarget
            );


            return;

        }









        Vector3 direction =
            currentTarget.position -
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
            TurnSpeed *
            Time.deltaTime
        );








        transform.position +=
            transform.forward *
            MoveSpeed *
            Time.deltaTime;







        SavePosition();

    }














    void AttackTarget(
        Transform currentTarget
    )
    {


        attackTimer +=
            Time.deltaTime;




        if (attackTimer < attackRate)
            return;







        // ATTACK TRAP


        SoundTrap trap =
        currentTarget.GetComponent
        <SoundTrap>();



        if (trap != null)
        {

            trap.TakeDamage(
                damage
            );



            Stun();



            attackTimer = 0;


            return;

        }









        // ATTACK TRAIN


        TrainDamagePoint train =
        currentTarget.GetComponent
        <TrainDamagePoint>();




        if (train != null)
        {

            train.TakeDamage(
                damage
            );



            Stun();

        }







        attackTimer = 0;

    }













    void Stun()
    {

        stunned = true;


        stunTimer =
            stunDuration;

    }













    public void TakeDamage(
        float amount
    )
    {

        currentHealth -=
            amount;





        if (bossHealthSlider != null)
        {

            bossHealthSlider.value =
                currentHealth;

        }







        if (currentHealth <= 0)
        {

            Die();

        }

    }













    void Die()
    {

        if (bossHealthPanel != null)
        {

            bossHealthPanel
            .SetActive(false);

        }



        Debug.Log(
            "BIG WORM DEAD"
        );



        Destroy(
            gameObject
        );

    }












    public void RunAway()
    {

        retreating = true;

    }











    void ReturnHome()
    {

        if (wormHome == null)
            return;






        Vector3 direction =
            wormHome.position -
            transform.position;



        direction.y = 0;






        transform.rotation =
        Quaternion.Lerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            TurnSpeed *
            Time.deltaTime
        );







        transform.position +=
            transform.forward *
            MoveSpeed *
            Time.deltaTime;






        SavePosition();








        if (
        Vector3.Distance(
            transform.position,
            wormHome.position
        )
        <=
        homeReachDistance
        )
        {

            Destroy(
                gameObject
            );

        }

    }













    void SavePosition()
    {

        PositionHistory.Insert(
            0,
            transform.position
        );



        if (PositionHistory.Count > 500)
        {

            PositionHistory.RemoveAt(
                PositionHistory.Count - 1
            );

        }

    }












    void MoveBody()
    {

        if (PositionHistory.Count == 0)
            return;






        int index = 0;



        foreach (GameObject body in BodyParts)
        {

            Vector3 point =
            PositionHistory[
            Mathf.Clamp(
                index * Gap,
                0,
                PositionHistory.Count - 1
            )];







            body.transform.position +=
            (
                point -
                body.transform.position
            )
            *
            BodySpeed
            *
            Time.deltaTime;






            body.transform.LookAt(
                point
            );



            index++;

        }

    }












    public void ChaseAgain()
    {

        retreating = false;

    }












    void GrowWorm()
    {

        GameObject body =
        Instantiate(
            BodyPrefab,
            transform.position,
            transform.rotation
        );




        body.transform.parent =
            transform;



        BodyParts.Add(
            body
        );

    }


}