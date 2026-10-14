using UnityEngine;
using UnityEngine.UI;


public class TrapDeployer : MonoBehaviour, IHudAbility
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











        // =====================
    // HUD ABILITY BUTTON
    // =====================

    public string AbilityId => "soundtrap";

    public string DisplayName => "Sound Trap";

    public float CooldownProgress =>
        cooldown <= 0 ? 1f : Mathf.Clamp01(timer / cooldown);

    public float CooldownRemaining =>
        Mathf.Max(0f, cooldown - timer);

    public bool IsActive =>
        SoundTrap.activeTrap != null;

    public bool CanActivate =>
        timer >= cooldown && SoundTrap.activeTrap == null;

    public void Activate()
    {
        DeployTrap();
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

        AudioManager.Sfx(l => l.trapDeploy, 0.9f);






        UpdateCooldownUI();






        Debug.Log(
            "SOUND TRAP DEPLOYED"
        );

    }


}