using UnityEngine;

// Visual-only animation for the hero drill vehicle:
// spins the drill rotor (faster while drilling) and rolls the wheels
// from TrainMovement's current speed. No gameplay logic lives here.
public class DrillVehicleAnimator : MonoBehaviour
{
    // =====================
    // REFERENCES
    // =====================

    [Header("References")]
    public TrainMovement trainMovement;

    public Transform drillSocket;

    public Transform[] wheels;


    // =====================
    // DRILL SPIN
    // =====================

    [Header("Drill Spin (degrees / second)")]
    public float idleDrillSpeed = 180f;

    public float drillingDrillSpeed = 900f;

    public float spinBlend = 6f;


    // =====================
    // WHEELS
    // =====================

    [Header("Wheels")]
    public float wheelRadius = 0.17f;


    private Transform drillRotor;

    private float currentDrillSpeed;


    void Start()
    {
        if (trainMovement == null)
        {
            trainMovement = GetComponentInParent<TrainMovement>();
        }

        FindDrillRotor();
    }


    // Call after swapping the module under DrillSocket.
    public void FindDrillRotor()
    {
        drillRotor = null;

        if (drillSocket == null)
        {
            return;
        }

        foreach (Transform t in drillSocket.GetComponentsInChildren<Transform>())
        {
            if (t.name == "DrillRotor")
            {
                drillRotor = t;
                break;
            }
        }
    }


    void Update()
    {
        bool drilling = false;
        float speed = 0f;

        if (trainMovement != null)
        {
            speed = trainMovement.currentSpeed;
            drilling = trainMovement.currentSpeed <= trainMovement.drillingSpeed;
        }

        if (drillRotor != null)
        {
            float target = drilling ? drillingDrillSpeed : idleDrillSpeed;

            currentDrillSpeed = Mathf.Lerp(currentDrillSpeed, target, spinBlend * Time.deltaTime);

            drillRotor.Rotate(Vector3.forward, currentDrillSpeed * Time.deltaTime, Space.Self);
        }

        if (wheels != null && wheelRadius > 0f)
        {
            float degrees = speed / wheelRadius * Mathf.Rad2Deg * Time.deltaTime;

            foreach (Transform wheel in wheels)
            {
                if (wheel != null)
                {
                    // vehicle's right axis, so mirrored wheels on both sides roll the same way
                    wheel.Rotate(transform.right, degrees, Space.World);
                }
            }
        }
    }
}
