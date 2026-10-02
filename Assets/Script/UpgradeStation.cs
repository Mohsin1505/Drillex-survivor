using UnityEngine;

public class UpgradeStation : MonoBehaviour
{

    [Header("UI")]

    public GameObject upgradeButton;

    public GameObject upgradePanel;






    void Start()
    {
        upgradeButton.SetActive(false);

        upgradePanel.SetActive(false);
    }








    private void OnTriggerEnter(Collider other)
    {

        if (other.CompareTag("Drill"))
        {

            upgradeButton.SetActive(true);

            Debug.Log("TRAIN ENTER SAFE ZONE");

        }

    }








    private void OnTriggerExit(Collider other)
    {

        if (other.CompareTag("Drill"))
        {

            upgradeButton.SetActive(false);


            CloseUpgrade();

        }

    }









    public void OpenUpgrade()
    {

        upgradePanel.SetActive(true);


        Time.timeScale = 0f;

    }








    public void CloseUpgrade()
    {

        upgradePanel.SetActive(false);


        Time.timeScale = 1f;

    }

}