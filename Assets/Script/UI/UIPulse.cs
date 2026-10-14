using UnityEngine;
using UnityEngine.UI;

// Restrained breathing glow (alpha + slight scale) used for "upgrade available" and ready states.
public class UIPulse : MonoBehaviour
{
    public Graphic glow;
    [Tooltip("Decorative child only (glow ring) - never the button itself, which UIButtonFX animates.")]
    public Transform scaleTarget;
    public bool pulsing;
    public float speed = 2.2f;
    public float minAlpha = 0.15f, maxAlpha = 0.85f;
    public float scaleAmount = 0.035f;

    float t;
    float current;

    void OnEnable() { current = 0f; Apply(1f); }

    void Update()
    {
        t += Time.unscaledDeltaTime * speed;
        float wave = 0.5f + 0.5f * Mathf.Sin(t);
        float target = pulsing ? Mathf.Lerp(minAlpha, maxAlpha, wave) : 0f;
        current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * 3f);
        Apply(pulsing ? 1f + scaleAmount * wave : 1f);
    }

    void Apply(float s)
    {
        if (glow != null) glow.canvasRenderer.SetAlpha(current);
        if (scaleTarget != null) scaleTarget.localScale = Vector3.one * s;
    }
}
