using UnityEngine;

public class Orb : MonoBehaviour
{
    public int orbAmount = 1;


    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Manager.instance.AddOrb(orbAmount);

            Destroy(gameObject);
        }
    }
}