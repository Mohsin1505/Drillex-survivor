using UnityEngine;

public class BillboardUI : MonoBehaviour
{
    private Camera mainCamera;



    void Start()
    {
        mainCamera =
            Camera.main;
    }






    void LateUpdate()
    {
        if (mainCamera == null)
            return;




        transform.LookAt(
            transform.position +
            mainCamera.transform.forward
        );

    }

}