using UnityEngine;
using UnityEngine.EventSystems;

// Horizontal virtual joystick for steering on touch screens (also works with a mouse).
// Writes MobileInput.Steer; keyboard steering keeps working because TrainMovement sums both.
public class SteerJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public RectTransform knob;
    public RectTransform track;
    public CanvasGroup group;
    public float radius = 110f;
    public float deadZone = 0.08f;
    [Tooltip("Hide on platforms without touch (the editor always shows it).")]
    public bool touchOnly = true;

    int pointer = int.MinValue;

    void Start()
    {
        if (touchOnly && !Application.isEditor && !Application.isMobilePlatform && !Input.touchSupported) gameObject.SetActive(false);
        SetIdle(true);
    }

    void OnDisable()
    {
        MobileInput.Steer = 0f;
        pointer = int.MinValue;
        if (knob) knob.anchoredPosition = Vector2.zero;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (pointer != int.MinValue) return;
        pointer = e.pointerId;
        SetIdle(false);
        OnDrag(e);
    }

    public void OnDrag(PointerEventData e)
    {
        if (e.pointerId != pointer) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out Vector2 local);
        float x = Mathf.Clamp(local.x, -radius, radius);
        knob.anchoredPosition = new Vector2(x, 0f);
        float v = x / radius;
        MobileInput.Steer = Mathf.Abs(v) < deadZone ? 0f : v;
    }

    public void OnPointerUp(PointerEventData e)
    {
        if (e.pointerId != pointer) return;
        pointer = int.MinValue;
        MobileInput.Steer = 0f;
        Vector2 from = knob.anchoredPosition;
        UITween.Run(knob, 0.18f, k => { if (knob) knob.anchoredPosition = Vector2.Lerp(from, Vector2.zero, k); }, UITween.Ease.OutCubic);
        SetIdle(true);
    }

    void SetIdle(bool idle)
    {
        if (group != null) UITween.Fade(group, idle ? 0.6f : 1f, 0.15f);
    }
}
