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
        if (upgradePanel.activeSelf && Time.timeScale == 0f)
            return;

        upgradePanel.SetActive(true);
        Time.timeScale = 0f;
        AudioManager.Ui(l => l.uiOpen, 0.8f);
    }








        public void CloseUpgrade()
    {
        if (upgradePanel.activeSelf)
        {
            AudioManager.Ui(l => l.uiClose, 0.8f);

            UIPanel panel = upgradePanel.GetComponent<UIPanel>();
            if (panel != null) panel.Hide();
            else upgradePanel.SetActive(false);
        }

        if (PauseManager.IsPaused)
            PauseManager.SetResumeTimeScale(1f);
        else
            Time.timeScale = 1f;
    }

}