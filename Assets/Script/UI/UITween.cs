using System;
using System.Collections.Generic;
using UnityEngine;

// Tiny unscaled tween runner for UI. Runs on unscaled time so menus animate while the game is paused.
public class UITween : MonoBehaviour
{
    public enum Ease { Linear, OutCubic, InCubic, OutBack, InOutSine }

    class Tween
    {
        public object owner;
        public float duration, time, delay;
        public Ease ease;
        public Action<float> step;
        public Action done;
    }

    static UITween runner;
    readonly List<Tween> tweens = new List<Tween>();
    readonly List<Tween> adding = new List<Tween>();

    static UITween Runner
    {
        get
        {
            if (runner == null)
            {
                var go = new GameObject("[UITween]");
                go.hideFlags = HideFlags.HideInHierarchy;
                DontDestroyOnLoad(go);
                runner = go.AddComponent<UITween>();
            }
            return runner;
        }
    }

    public static float Evaluate(Ease e, float t)
    {
        switch (e)
        {
            case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
            case Ease.InCubic: return t * t * t;
            case Ease.OutBack: { const float c1 = 1.70158f, c3 = c1 + 1f; return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f); }
            case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
            default: return t;
        }
    }

    // Generic 0..1 tween. Starting a tween with the same owner replaces the previous one.
    public static void Run(object owner, float duration, Action<float> step, Ease ease = Ease.OutCubic, Action done = null, float delay = 0f)
    {
        if (!Application.isPlaying) { step(1f); done?.Invoke(); return; }
        Kill(owner);
        Runner.adding.Add(new Tween { owner = owner, duration = Mathf.Max(0.0001f, duration), step = step, ease = ease, done = done, delay = delay });
    }

    public static void Kill(object owner)
    {
        if (runner == null || owner == null) return;
        runner.tweens.RemoveAll(t => t.owner == owner);
        runner.adding.RemoveAll(t => t.owner == owner);
    }

    public static void Scale(Transform t, Vector3 from, Vector3 to, float duration, Ease ease = Ease.OutBack, Action done = null, float delay = 0f)
    {
        t.localScale = from;
        Run(t, duration, k => { if (t) t.localScale = Vector3.LerpUnclamped(from, to, k); }, ease, done, delay);
    }

    public static void Fade(CanvasGroup g, float to, float duration, Ease ease = Ease.OutCubic, Action done = null, float delay = 0f)
    {
        float from = g.alpha;
        Run(g, duration, k => { if (g) g.alpha = Mathf.LerpUnclamped(from, to, k); }, ease, done, delay);
    }

    public static void Move(RectTransform r, Vector2 from, Vector2 to, float duration, Ease ease = Ease.OutCubic, Action done = null, float delay = 0f)
    {
        r.anchoredPosition = from;
        Run(r, duration, k => { if (r) r.anchoredPosition = Vector2.LerpUnclamped(from, to, k); }, ease, done, delay);
    }

    // Quick scale "punch" that settles back to 1.
    public static void Punch(Transform t, float amount = 0.15f, float duration = 0.25f)
    {
        Run(t, duration, k => { if (t) t.localScale = Vector3.one * (1f + amount * Mathf.Sin(k * Mathf.PI)); }, Ease.Linear, () => { if (t) t.localScale = Vector3.one; });
    }

    // Horizontal shake used for "not enough orbs".
    public static void Shake(RectTransform r, float amount = 12f, float duration = 0.35f)
    {
        Vector2 home = r.anchoredPosition;
        Run(r, duration, k => { if (r) r.anchoredPosition = home + Vector2.right * Mathf.Sin(k * Mathf.PI * 6f) * amount * (1f - k); }, Ease.Linear, () => { if (r) r.anchoredPosition = home; });
    }

    void Update()
    {
        if (adding.Count > 0) { tweens.AddRange(adding); adding.Clear(); }
        float dt = Time.unscaledDeltaTime;
        for (int i = tweens.Count - 1; i >= 0; i--)
        {
            if (i >= tweens.Count) continue;
            var tw = tweens[i];
            if (tw.delay > 0f) { tw.delay -= dt; continue; }
            tw.time += dt;
            float k = Mathf.Clamp01(tw.time / tw.duration);
            try { tw.step(Evaluate(tw.ease, k)); }
            catch (Exception e) { Debug.LogException(e); tweens.Remove(tw); continue; }
            if (k >= 1f)
            {
                tweens.Remove(tw);
                tw.done?.Invoke();
            }
        }
    }
}
