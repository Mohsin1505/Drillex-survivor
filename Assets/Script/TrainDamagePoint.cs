using UnityEngine;

public class TrainDamagePoint : MonoBehaviour
{
    private TrainHealth trainHealth;



    void Start()
    {
        if (trainHealth == null)
        {
            trainHealth =
                GetComponentInParent<TrainHealth>();
        }
    }




    public void SetTrainHealth(
        TrainHealth health
    )
    {
        trainHealth = health;
    }




    public void TakeDamage(float damage)
    {
        if (trainHealth != null)
        {
            trainHealth.TakeDamage(
                damage
            );
        }
    }
}