using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    [Header("Target")]

    public Transform target;



    [Header("Distance")]

    public float distance = 20f;



    [Header("Sensitivity")]

    public float mouseSensitivity = 3f;



    [Header("Vertical Limit")]

    public float minY = -20f;

    public float maxY = 70f;



    [Header("Smooth")]

    public float smoothSpeed = 10f;





    private float rotationX;

    private float rotationY;






    void LateUpdate()
    {
        if (target == null)
            return;



        HandleInput();


        MoveCamera();
    }








    void HandleInput()
    {
        if (Input.GetMouseButton(1))
        {
            rotationX +=
                Input.GetAxis("Mouse X")
                *
                mouseSensitivity;



            rotationY -=
                Input.GetAxis("Mouse Y")
                *
                mouseSensitivity;




            rotationY =
                Mathf.Clamp(
                    rotationY,
                    minY,
                    maxY
                );
        }
    }










    void MoveCamera()
    {
        Quaternion rotation =
            Quaternion.Euler(
                rotationY,
                rotationX,
                0
            );




        Vector3 direction =
            rotation *
            new Vector3(
                0,
                0,
                -distance
            );




        Vector3 wantedPosition =
            target.position
            +
            direction;





        transform.position =
            Vector3.Lerp(
                transform.position,
                wantedPosition,
                smoothSpeed *
                Time.deltaTime
            );





        transform.LookAt(
            target.position
        );
    }

}