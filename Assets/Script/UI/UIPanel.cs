using UnityEngine;

// Animated show/hide for any full-screen or popup panel (unscaled, works while paused).
// The panel's GameObject is still what existing scripts toggle; this just animates it.
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    public RectTransform window;
    public float duration = 0.28f;
    public Vector2 slideFrom = new Vector2(0f, -40f);

    CanvasGroup group;
    Vector2 windowHome;
    bool homeKnown;

    public bool IsOpen => gameObject.activeSelf && !closing;
    bool closing;

    void Awake()
    {
        group = GetComponent<CanvasGroup>();
        if (window != null && !homeKnown) { windowHome = window.anchoredPosition; homeKnown = true; }
    }

    // Plays the entrance every time the panel is activated, also when legacy code calls SetActive(true).
    void OnEnable()
    {
        if (group == null) Awake();
        closing = false;
        group.alpha = 0f;
        group.interactable = true;
        group.blocksRaycasts = true;
        UITween.Fade(group, 1f, duration * 0.8f);
        if (window != null)
        {
            window.localScale = Vector3.one * 0.94f;
            UITween.Scale(window, Vector3.one * 0.94f, Vector3.one, duration, UITween.Ease.OutBack);
            var w = window;
            Vector2 from = windowHome + slideFrom, to = windowHome;
            UITween.Run(this, duration, k => { if (w) w.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, UITween.Ease.OutCubic);
        }
    }

    public void Show()
    {
        if (gameObject.activeSelf && !closing) return;
        gameObject.SetActive(true);
        if (closing) OnEnable();
    }

    public void Hide(System.Action done = null)
    {
        if (!gameObject.activeSelf) { done?.Invoke(); return; }
        closing = true;
        group.interactable = false;
        UITween.Fade(group, 0f, duration * 0.7f, UITween.Ease.OutCubic, () =>
        {
            closing = false;
            if (this) gameObject.SetActive(false);
            done?.Invoke();
        });
        if (window != null) UITween.Scale(window, window.localScale, Vector3.one * 0.96f, duration * 0.7f, UITween.Ease.OutCubic);
    }
}
