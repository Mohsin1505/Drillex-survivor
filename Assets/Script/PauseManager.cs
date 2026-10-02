using UnityEngine;
using UnityEngine.SceneManagement;


public class PauseManager : MonoBehaviour
{

    public GameObject pausePanel;





    void Start()
    {

        pausePanel.SetActive(false);

    }






    public void PauseGame()
    {

        pausePanel.SetActive(true);


        Time.timeScale = 0;

    }






    public void ResumeGame()
    {

        pausePanel.SetActive(false);


        Time.timeScale = 1;

    }






    public void MainMenu()
    {

        Time.timeScale = 1;


        SceneManager.LoadScene(
            "MainMenu"
        );

    }


}