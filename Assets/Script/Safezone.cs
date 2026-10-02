using UnityEngine;

public class SafeZone : MonoBehaviour
{

    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Drill"))
        {

            Manager.instance.DepositOrbs();



            Manager.instance.RemoveWorm();


            Debug.Log("PLAYER SAFE");

        }

    }

}