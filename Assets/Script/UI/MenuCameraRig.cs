using UnityEngine;

// Slow cinematic drift around the train on the main-menu stage.
public class MenuCameraRig : MonoBehaviour
{
    public Transform focus;
    public float radius = 9f;
    public float height = 3.2f;
    public float startAngle = 210f;
    public float swing = 18f;
    public float period = 40f;
    public Vector3 lookOffset = new Vector3(0f, 0.8f, 0f);
    [Tooltip("Turns the view after aiming at the focus so the train sits off-centre (negative = train on the right).")]
    public float framingYaw = -12f;

    float t;

    void LateUpdate()
    {
        t += Time.unscaledDeltaTime;
        Apply(t);
    }

    // Positions the camera for t = 0 (used by the editor builder so the scene view matches play mode).
    public void Snap() { Apply(0f); }

    void Apply(float time)
    {
        if (focus == null) return;
        float a = (startAngle + Mathf.Sin(time * Mathf.PI * 2f / period) * swing) * Mathf.Deg2Rad;
        Vector3 p = focus.position + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius + Vector3.up * (height + Mathf.Sin(time * 0.37f) * 0.15f);
        transform.position = p;
        transform.LookAt(focus.position + lookOffset);
        transform.Rotate(0f, framingYaw, 0f, Space.World);
    }
}
