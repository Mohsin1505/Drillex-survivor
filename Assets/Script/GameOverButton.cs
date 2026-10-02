using UnityEngine;


public class GameOverButtons : MonoBehaviour
{


    [Header("References")]

    public Transform train;


    public Transform safeZone;










    public void RepairTrain()
    {


        Time.timeScale = 1;





        // lose only current run resources

        if (Manager.instance != null)
        {

            Manager.instance.LoseRunOrbs();

        }










        // move train home

        train.position =
            safeZone.position;






        train.rotation =
            safeZone.rotation;










        // heal train

        TrainHealth hp =
        train.GetComponent<TrainHealth>();



        if (hp != null)
        {

            hp.FullRepair();

        }










        gameObject.SetActive(false);

    }












    public void AbandonRun()
    {


        Time.timeScale = 1;





        // wipe everything

        PlayerPrefs.DeleteAll();






        UnityEngine.SceneManagement
        .SceneManager
        .LoadScene(

            UnityEngine.SceneManagement
            .SceneManager
            .GetActiveScene()
            .buildIndex

        );

    }


}