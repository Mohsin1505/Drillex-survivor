using TMPro;
using UnityEngine;

// Number that rolls toward its target value and punches when it changes. Only rewrites the
// TMP text when the displayed integer actually changes (no per-frame string building otherwise).
public class AnimatedCounter : MonoBehaviour
{
    public TMP_Text label;
    public string prefix = "";
    public string suffix = "";
    public float rollTime = 0.45f;
    public Transform punchTarget;
    public bool thousandsSeparators = true;

    long target, shown;
    double from;
    float t = 1f;
    bool initialised;

    public void SetValue(long value, bool animate = true)
    {
        if (!initialised || !animate)
        {
            initialised = true;
            target = shown = value;
            t = 1f;
            Write(value);
            return;
        }
        if (value == target) return;
        if (punchTarget != null && value > target) UITween.Punch(punchTarget, 0.12f, 0.22f);
        from = shown;
        target = value;
        t = 0f;
    }

    void Update()
    {
        if (t >= 1f) return;
        t = Mathf.Min(1f, t + Time.unscaledDeltaTime / rollTime);
        float k = UITween.Evaluate(UITween.Ease.OutCubic, t);
        long v = (long)System.Math.Round(from + (target - from) * k);
        if (v != shown) { shown = v; Write(v); }
    }

    void Write(long v)
    {
        if (label == null) return;
        label.SetText(prefix + (thousandsSeparators ? v.ToString("N0") : v.ToString()) + suffix);
    }
}
