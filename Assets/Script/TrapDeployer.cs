using UnityEngine;
using UnityEngine.UI;


public class TrapDeployer : MonoBehaviour
{

    [Header("Trap")]

    public GameObject trapPrefab;


    public Transform dropPoint;







    [Header("Cooldown")]

    public float cooldown = 10f;


    private float timer;





    [Header("UI")]

    public Slider cooldownSlider;









    void Start()
    {

        timer =
            cooldown;




        if (cooldownSlider != null)
        {

            cooldownSlider.maxValue =
                cooldown;



            cooldownSlider.value =
                cooldown;

        }

    }









    void Update()
    {

        if (timer < cooldown)
        {

            timer +=
                Time.deltaTime;




            if (timer > cooldown)
            {

                timer =
                    cooldown;

            }

        }








        UpdateCooldownUI();

    }










    void UpdateCooldownUI()
    {

        if (cooldownSlider == null)
            return;




        cooldownSlider.value =
            timer;

    }











    public void DeployTrap()
    {


        // still cooling

        if (timer < cooldown)
            return;





        // one trap allowed

        if (
            SoundTrap.activeTrap
            !=
            null
        )
            return;








        Instantiate(

            trapPrefab,

            dropPoint.position,

            Quaternion.identity

        );









        timer =
            0;






        UpdateCooldownUI();






        Debug.Log(
            "SOUND TRAP DEPLOYED"
        );

    }


}