using UnityEngine;
using UnityEngine.SceneManagement;


public class GameOverManager : MonoBehaviour
{


    public static GameOverManager instance;



    [Header("UI")]

    public GameObject gameOverPanel;





    void Awake()
    {

        instance = this;

        gameOverPanel.SetActive(false);

    }







    public void GameOver()
    {

        Time.timeScale = 0;


        gameOverPanel.SetActive(true);

    }









    // =====================
    // BUTTON 1
    // =====================

    public void RepairTrain()
    {


        Time.timeScale = 1;



        // save current upgrades here later


        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );

    }










    // =====================
    // BUTTON 2
    // =====================

    public void AbandonRun()
    {


        Time.timeScale = 1;



        // The run state lives only in the scene, so reloading it resets the run.
        // PlayerPrefs is not cleared: it only holds player settings (audio), which must survive.



        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );

    }


}