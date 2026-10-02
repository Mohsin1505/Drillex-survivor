using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UpgradeButtonUI : MonoBehaviour
{

    [Header("Unlock")]

    public int unlockLevel;



    [Header("UI")]

    public GameObject lockPanel;

    public TextMeshProUGUI lockText;

    public Button button;





    void Update()
    {

        CheckUnlock();

    }







    void CheckUnlock()
    {

        if (
        ExperienceManager.instance.currentLevel
        <
        unlockLevel
        )
        {


            lockPanel.SetActive(true);


            lockText.text =
            "Unlock Lv "
            +
            unlockLevel;



            button.interactable =
            false;

        }


        else
        {


            lockPanel.SetActive(false);



            button.interactable =
            true;

        }

    }

}