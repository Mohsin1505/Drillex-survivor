using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Manager : MonoBehaviour
{
    public static Manager instance;



    // =========================
    // ORB SYSTEM
    // =========================

    [Header("Orb System")]

    public int permanentOrb;

    public TextMeshProUGUI permanentOrbText;







    // =========================
    // NOISE SYSTEM
    // =========================

    [Header("Noise Meter")]

    public Slider noiseSlider;


    public float maxNoise = 100f;

    public float currentNoise;


    public float drainSpeed = 20f;

    public float refillSpeed = 10f;


    private bool isDrilling;









    // =========================
    // WORM SYSTEM
    // =========================

    [Header("Big Worm")]

    public GameObject wormPrefab;


    public Transform player;


    public Transform wormHome;


    public WormMovement currentWorm;





    [Header("Worm Spawn")]

    public float minSpawnDistance = 100f;

    public float maxSpawnDistance = 250f;


    public bool spawnPositiveZ = true;


    private bool wormSpawned;









    // =========================
    // WARNING UI
    // =========================

    [Header("Warning UI")]

    public GameObject wormWarningPanel;









    private void Awake()
    {
        instance = this;
    }









    void Start()
    {

        UpdateOrbUI();




        currentNoise =
            maxNoise;



        noiseSlider.maxValue =
            maxNoise;



        noiseSlider.value =
            currentNoise;





        if (wormWarningPanel != null)
        {
            wormWarningPanel.SetActive(false);
        }

    }









    void Update()
    {
        HandleNoise();
    }










    // =========================
    // ORBS
    // =========================


    public void AddOrb(int amount)
    {

        permanentOrb += amount;


        UpdateOrbUI();

    }










    public void DepositOrbs()
    {

        TrainStorageManager storage =
            FindObjectOfType<TrainStorageManager>();



        if (storage != null)
        {
            storage.EmptyStorage();
        }



        Debug.Log("ORB SAVED");

    }










    public void LoseRunOrbs()
    {

        TrainStorageManager storage =
            FindObjectOfType<TrainStorageManager>();



        if (storage != null)
        {
            storage.currentOrb = 0;
        }




        Debug.Log("RUN LOST");

    }










    // NOW PUBLIC FOR UPGRADE MANAGER

    public void UpdateOrbUI()
    {

        if (permanentOrbText != null)
        {

            permanentOrbText.text =
                "TOTAL ORBS : "
                +
                permanentOrb;

        }

    }













    // =========================
    // NOISE
    // =========================


    void HandleNoise()
    {

        if (isDrilling)
        {
            currentNoise -=
                drainSpeed *
                Time.deltaTime;
        }


        else
        {
            currentNoise +=
                refillSpeed *
                Time.deltaTime;
        }






        currentNoise =
            Mathf.Clamp(
                currentNoise,
                0,
                maxNoise
            );




        noiseSlider.value =
            currentNoise;




        isDrilling = false;






        if (currentNoise <= 0 &&
           wormSpawned == false)
        {

            SpawnWorm();

        }

    }









    public void PlayerIsDrilling()
    {
        isDrilling = true;
    }













    // =========================
    // SPAWN WORM
    // =========================


    void SpawnWorm()
    {


        if (currentWorm != null)
        {

            currentWorm.ChaseAgain();


            wormSpawned = true;



            if (wormWarningPanel != null)
            {
                wormWarningPanel.SetActive(true);
            }


            return;

        }







        wormSpawned = true;





        if (wormWarningPanel != null)
        {
            wormWarningPanel.SetActive(true);
        }







        float distance =
            Random.Range(
                minSpawnDistance,
                maxSpawnDistance
            );





        float spawnZ =
            spawnPositiveZ
            ?
            player.position.z + distance
            :
            player.position.z - distance;







        Vector3 spawnPosition =
            new Vector3(
                player.position.x,
                player.position.y,
                spawnZ
            );








        GameObject worm =
            Instantiate(
                wormPrefab,
                spawnPosition,
                Quaternion.identity
            );








        currentWorm =
            worm.GetComponent<WormMovement>();



        currentWorm.target =
            player;


        currentWorm.wormHome =
            wormHome;

    }













    // =========================
    // SAFE ZONE
    // =========================


    public void RemoveWorm()
    {

        if (currentWorm != null)
        {
            currentWorm.RunAway();
        }





        if (wormWarningPanel != null)
        {
            wormWarningPanel.SetActive(false);
        }




        wormSpawned = false;


        currentNoise =
            maxNoise;



        noiseSlider.value =
            currentNoise;

    }

}