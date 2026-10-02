using UnityEngine;
using TMPro;


public class TrainStorageManager : MonoBehaviour
{

    [Header("Capacity")]

    public int baseStorage = 100;

    public int totalStorage;






    [Header("Current Run Storage")]

    public int currentOrb;






    [Header("UI")]

    public TMP_Text storageText;











    void Start()
    {

        CalculateStorage();


        UpdateUI();

    }












    public void CalculateStorage()
    {

        totalStorage =
            baseStorage;






        StorageBlock[] storages =
            FindObjectsOfType<StorageBlock>();







        foreach (
            StorageBlock block
            in storages
        )
        {

            totalStorage +=
                block.storageAmount;

        }






        UpdateUI();

    }












    public void AddOrb(
        int amount
    )
    {


        // STORAGE FULL

        if (
            currentOrb
            >=
            totalStorage
        )
        {


            Debug.Log(
                "STORAGE FULL"
            );



            return;

        }










        currentOrb +=
            amount;










        if (
            currentOrb
            >
            totalStorage
        )
        {

            currentOrb =
                totalStorage;

        }








        UpdateUI();

    }












    public void EmptyStorage()
    {


        if (
            currentOrb
            <=
            0
        )
            return;








        // SEND TRAIN ORBS TO TOTAL ORBS

        Manager.instance.AddOrb(
            currentOrb
        );









        // EMPTY TRAIN STORAGE

        currentOrb =
            0;








        UpdateUI();


    }












    void UpdateUI()
    {


        if (
            storageText
            !=
            null
        )
        {


            storageText.text =
                "ORBS IN TRAIN\n"
                +
                currentOrb
                +
                " / "
                +
                totalStorage;

        }


    }


}