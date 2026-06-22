using UnityEngine;

public class Drill : MonoBehaviour
{

    [Header("Drill Settings")]

    public float drillDamage = 10f;

    public float damageRate = 0.2f;


    private float timer;





    [Header("Train")]

    public TrainMovement trainMovement;



    private bool isTouchingRock;







    void FixedUpdate()
    {

        if (trainMovement != null)
        {
            trainMovement.SetDrilling(
                isTouchingRock
            );
        }




        if (isTouchingRock)
        {
            Manager.instance.PlayerIsDrilling();
        }





        // reset after physics check
        isTouchingRock = false;

    }









    private void OnTriggerStay(Collider other)
    {

        if (other.CompareTag("Rock"))
        {

            isTouchingRock = true;






            timer += Time.deltaTime;






            if (timer >= damageRate)
            {

                Rock rock =
                    other.GetComponent<Rock>();





                if (rock != null)
                {
                    rock.DamageRock(
                        drillDamage
                    );
                }




                timer = 0;

            }

        }

    }










    private void OnTriggerExit(Collider other)
    {

        if (other.CompareTag("Rock"))
        {
            timer = 0;
        }

    }

}