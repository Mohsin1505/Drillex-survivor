using UnityEngine;
using TMPro;


public class SafeZoneCompass : MonoBehaviour
{

    [Header("References")]

    public Transform safeZone;

    public Transform playerCamera;


    public RectTransform arrow;



    [Header("Distance UI")]

    public TMP_Text distanceText;






    void Update()
    {

        if (
            safeZone == null
            ||
            playerCamera == null
        )
            return;






        Vector3 direction =
            safeZone.position -
            playerCamera.position;



        direction.y = 0;







        Vector3 cameraForward =
            playerCamera.forward;


        cameraForward.y = 0;



        Vector3 cameraRight =
            playerCamera.right;


        cameraRight.y = 0;








        float angle =
        Mathf.Atan2(

            Vector3.Dot(
                direction,
                cameraRight
            ),

            Vector3.Dot(
                direction,
                cameraForward
            )

        )
        *
        Mathf.Rad2Deg;








        arrow.localRotation =
        Quaternion.Euler(
            0,
            0,
            -angle
        );








        float distance =
        Vector3.Distance(
            safeZone.position,
            playerCamera.position
        );




        distanceText.text =
            Mathf.RoundToInt(distance)
            +
            " m";

    }


}