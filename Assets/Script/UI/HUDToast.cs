using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Banner notifications (level up, unlocks, purchases, warnings). Queued, unscaled, one at a time.
public class HUDToast : MonoBehaviour
{
    public enum Style { Normal, Electric, Explosive }

    public RectTransform banner;
    public CanvasGroup group;
    public Image accentStrip;
    public Image accentGlow;
    public Image icon;
    public TMP_Text title;
    public TMP_Text subtitle;
    [Tooltip("Flickering arc overlay used for the electric unlock.")]
    public Image electricFx;
    [Tooltip("Burst overlay used for the rocket unlock.")]
    public Image explosiveFx;
    public float hold = 2.0f;

    struct Item { public string title, sub; public Color color; public Sprite icon; public Style style; }

    static HUDToast instance;
    readonly Queue<Item> queue = new Queue<Item>();
    float timer;
    bool showing;
    Style currentStyle;
    Vector2 home;
    float gap;

    void Awake()
    {
        instance = this;
        home = banner.anchoredPosition;
        group.alpha = 0f;
        banner.gameObject.SetActive(false);
    }

    void OnDestroy() { if (instance == this) instance = null; }

    public static void Show(string title, string subtitle, Color color, Sprite icon = null, Style style = Style.Normal)
    {
        if (instance == null) return;
        // collapse repeated messages (e.g. spamming a locked button)
        foreach (var q in instance.queue) if (q.title == title && q.sub == subtitle) return;
        instance.queue.Enqueue(new Item { title = title, sub = subtitle, color = color, icon = icon, style = style });
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (showing)
        {
            timer -= dt;
            Animate(dt);
            if (timer <= 0f) HideCurrent();
            return;
        }
        if (gap > 0f) { gap -= dt; return; }
        if (queue.Count > 0) Present(queue.Dequeue());
    }

    void Present(Item it)
    {
        showing = true;
        timer = hold + (it.style != Style.Normal ? 0.8f : 0f);
        currentStyle = it.style;
        banner.gameObject.SetActive(true);
        title.text = it.title;
        subtitle.text = it.sub;
        accentStrip.color = it.color;
        var g = it.color; g.a = 1f;
        if (accentGlow) accentGlow.color = g;
        if (icon)
        {
            icon.gameObject.SetActive(it.icon != null);
            icon.sprite = it.icon;
            icon.color = it.color;
        }
        if (electricFx) { electricFx.gameObject.SetActive(it.style == Style.Electric); electricFx.color = it.color; }
        if (explosiveFx) { explosiveFx.gameObject.SetActive(it.style == Style.Explosive); explosiveFx.color = it.color; }
        group.alpha = 0f;
        UITween.Fade(group, 1f, 0.2f);
        UITween.Move(banner, home + new Vector2(0f, 60f), home, 0.35f, UITween.Ease.OutBack);
    }

    void Animate(float dt)
    {
        if (currentStyle == Style.Electric && electricFx)
        {
            // irregular crackle, settles down over time
            float a = Random.value < 0.35f ? Random.Range(0.4f, 1f) : 0.1f;
            electricFx.canvasRenderer.SetAlpha(a * Mathf.Clamp01(timer / 1.5f));
            electricFx.rectTransform.localScale = new Vector3(Random.value < 0.5f ? 1f : -1f, 1f, 1f);
        }
        if (currentStyle == Style.Explosive && explosiveFx)
        {
            float life = Mathf.Clamp01(1f - (timer / (hold + 0.8f)));
            float k = Mathf.Clamp01(life * 3f);
            explosiveFx.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1.35f, UITween.Evaluate(UITween.Ease.OutCubic, k));
            explosiveFx.canvasRenderer.SetAlpha(1f - k);
        }
    }

    void HideCurrent()
    {
        showing = false;
        var b = banner;
        UITween.Fade(group, 0f, 0.25f, UITween.Ease.OutCubic, () => { if (b && !showing) b.gameObject.SetActive(false); });
        // small gap before the next toast so they read as separate messages
        gap = 0.35f;
    }
}
