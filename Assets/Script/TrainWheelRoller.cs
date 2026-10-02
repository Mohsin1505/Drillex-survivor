using UnityEngine;

// Visual-only: rolls wheels from how far this object actually moved,
// so it works on cars that just follow the train (no speed value of their own).
public class TrainWheelRoller : MonoBehaviour
{
    [Header("Wheels")]
    public Transform[] wheels;

    public float wheelRadius = 0.17f;

    // Spoked wheels look frozen or spin backwards when they turn close to one
    // spoke gap per frame (wagon-wheel effect). Capping the per-frame step keeps
    // the roll readable at any speed / frame rate.
    public float maxDegreesPerFrame = 20f;


    private Vector3 lastPosition;


    void OnEnable()
    {
        lastPosition = transform.position;
    }


    void LateUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        if (wheels == null || wheelRadius <= 0f)
        {
            return;
        }

        float distance = Vector3.Dot(delta, transform.forward);
        float degrees = distance / wheelRadius * Mathf.Rad2Deg;
        degrees = Mathf.Clamp(degrees, -maxDegreesPerFrame, maxDegreesPerFrame);

        foreach (Transform wheel in wheels)
        {
            if (wheel != null)
            {
                wheel.Rotate(transform.right, degrees, Space.World);
            }
        }
    }
}
