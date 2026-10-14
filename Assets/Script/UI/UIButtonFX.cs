using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Press / hover feedback for every Drillex button: squash on press, overshoot on release, click sound.
[DisallowMultipleComponent]
public class UIButtonFX : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
{
    public float pressedScale = 0.92f;
    public float hoverScale = 1.04f;
    public Graphic hoverGlow;
    public bool clickSound = true;

    Selectable selectable;
    bool hovered;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (hoverGlow != null) hoverGlow.canvasRenderer.SetAlpha(0f);
    }

    void OnDisable()
    {
        UITween.Kill(transform);
        transform.localScale = Vector3.one;
        hovered = false;
        if (hoverGlow != null) hoverGlow.canvasRenderer.SetAlpha(0f);
    }

    bool Interactable => selectable == null || selectable.IsInteractable();

    public void OnPointerDown(PointerEventData e)
    {
        if (!Interactable) return;
        UITween.Scale(transform, transform.localScale, Vector3.one * pressedScale, 0.08f, UITween.Ease.OutCubic);
    }

    public void OnPointerUp(PointerEventData e)
    {
        float target = hovered && Input.touchCount == 0 ? hoverScale : 1f;
        UITween.Scale(transform, transform.localScale, Vector3.one * target, 0.28f, UITween.Ease.OutBack);
        if (Interactable && clickSound) AudioManager.PlayUiClick();
    }

    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        if (!Interactable || Input.touchCount > 0) return;
        UITween.Scale(transform, transform.localScale, Vector3.one * hoverScale, 0.15f, UITween.Ease.OutCubic);
        if (hoverGlow != null) hoverGlow.CrossFadeAlpha(1f, 0.15f, true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovered = false;
        UITween.Scale(transform, transform.localScale, Vector3.one, 0.15f, UITween.Ease.OutCubic);
        if (hoverGlow != null) hoverGlow.CrossFadeAlpha(0f, 0.2f, true);
    }
}
