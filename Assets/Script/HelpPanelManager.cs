using UnityEngine;


public class HelpPanelManager : MonoBehaviour
{


    [Header("Help UI")]

    public GameObject helpPanel;






    void Start()
    {

        helpPanel.SetActive(false);

    }









    public void OpenHelp()
    {

        helpPanel.SetActive(true);



        Time.timeScale = 0;

    }









    public void CloseHelp()
    {

        helpPanel.SetActive(false);



        Time.timeScale = 1;

    }


}