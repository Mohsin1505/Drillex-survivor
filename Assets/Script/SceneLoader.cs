using UnityEngine;
using UnityEngine.SceneManagement;


public class SceneLoader : MonoBehaviour
{


    public void StartGame()
    {

        Time.timeScale = 1;


        SceneManager.LoadScene(
            "Lev1"
        );

    }




    public void QuitGame()
    {

        Application.Quit();

    }


}