using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Shared building blocks for the Drillex UI builders (palette, sprites, font, element factories).
// The builders generate the uGUI hierarchies so the look stays consistent and can be regenerated.
public static class DrillexUIKit
{
    // ---------------- palette ----------------
    public static readonly Color TextMain = Hex("#EDEFF2");
    public static readonly Color TextDim = Hex("#9AA1AA");
    public static readonly Color TextDark = Hex("#111214");
    public static readonly Color Amber = Hex("#FFC43D");
    public static readonly Color Hazard = Hex("#F2B10F");
    public static readonly Color Ember = Hex("#FF7A1E");
    public static readonly Color Danger = Hex("#FF4B33");
    public static readonly Color Electric = Hex("#5BCBFF");
    public static readonly Color RocketRed = Hex("#FF5229");
    public static readonly Color Steel = Hex("#B8BEC6");
    // project uses linear colour space, so UI alpha reads lighter than it looks in sRGB terms
    public static readonly Color Overlay = new Color(0.02f, 0.02f, 0.025f, 0.93f);

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    public static Color A(Color c, float a) { c.a = a; return c; }

    // ---------------- assets ----------------
    static TMP_FontAsset font;
    public static TMP_FontAsset Font => font != null ? font : (font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/FONT/conthrax/Conthrax-SemiBold SDF.asset"));

    public static Sprite S(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/" + name + ".png");
        if (s == null) s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/Icons/icon_" + name + ".png");
        if (s == null) Debug.LogWarning("DrillexUIKit: missing sprite " + name);
        return s;
    }

    // ---------------- layout ----------------
    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    // Anchor to a single point (corner/edge/centre) with a pivot matching the anchor.
    public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static Image Img(string name, Transform parent, string sprite, Color color, bool sliced = false, bool raycast = false)
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = string.IsNullOrEmpty(sprite) ? null : S(sprite);
        img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = raycast;
        if (!sliced && img.sprite != null) img.preserveAspect = false;
        return img;
    }

    public static Image Filled(string name, Transform parent, string sprite, Color color, Image.FillMethod method, int origin = 0)
    {
        var img = Img(name, parent, sprite, color);
        img.type = Image.Type.Filled;
        img.fillMethod = method;
        img.fillOrigin = origin;
        img.fillAmount = 1f;
        return img;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, bool bold = false)
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.characterSpacing = 2f;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    // Industrial button: gunmetal (or tinted fill) plate, optional icon, label, press feedback.
    public static Button Button(string name, Transform parent, string label, string icon, Color accent, bool filled, float fontSize = 34f)
    {
        var bg = Img(name, parent, filled ? "ui_button_fill" : "ui_button", filled ? accent : Color.white, true, true);
        var btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var cb = btn.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1.08f, 1.08f, 1.08f); cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
        cb.selectedColor = Color.white; cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f); cb.fadeDuration = 0.08f;
        btn.colors = cb;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

        var glow = Img("HoverGlow", bg.transform, "ui_frame_glow", A(accent, 1f), true);
        Stretch(glow.rectTransform, -16, -16, -16, -16);
        var fx = bg.gameObject.AddComponent<UIButtonFX>();
        fx.hoverGlow = glow;

        if (!filled)
        {
            var strip = Img("Accent", bg.transform, "ui_plate", accent, true);
            strip.rectTransform.anchorMin = new Vector2(0, 0); strip.rectTransform.anchorMax = new Vector2(0, 1);
            strip.rectTransform.pivot = new Vector2(0, 0.5f);
            strip.rectTransform.offsetMin = new Vector2(7, 12); strip.rectTransform.offsetMax = new Vector2(13, -12);
        }

        Color fg = filled ? TextDark : TextMain;
        float textLeft = 24;
        if (!string.IsNullOrEmpty(icon))
        {
            var ic = Img("Icon", bg.transform, icon, filled ? TextDark : accent);
            Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(26, 0), new Vector2(fontSize * 1.25f, fontSize * 1.25f), new Vector2(0, 0.5f));
            textLeft = 26 + fontSize * 1.25f + 14;
        }
        if (!string.IsNullOrEmpty(label))
        {
            var t = Text("Label", bg.transform, label, fontSize, fg, string.IsNullOrEmpty(icon) ? TextAlignmentOptions.Center : TextAlignmentOptions.Left);
            Stretch(t.rectTransform, string.IsNullOrEmpty(icon) ? 12 : textLeft, 0, 12, 0);
        }
        return btn;
    }

    // Square icon-only button (pause, close).
    public static Button IconButton(string name, Transform parent, string icon, Color accent, float size)
    {
        var bg = Img(name, parent, "ui_button", Color.white, true, true);
        bg.rectTransform.sizeDelta = new Vector2(size, size);
        var btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
        var glow = Img("HoverGlow", bg.transform, "ui_frame_glow", accent, true);
        Stretch(glow.rectTransform, -16, -16, -16, -16);
        bg.gameObject.AddComponent<UIButtonFX>().hoverGlow = glow;
        var ic = Img("Icon", bg.transform, icon, accent);
        Stretch(ic.rectTransform, size * 0.25f, size * 0.25f, size * 0.25f, size * 0.25f);
        return btn;
    }

    // Thin hazard stripe band.
    public static Image HazardStrip(string name, Transform parent, float height)
    {
        var img = Img(name, parent, "ui_hazard", Color.white);
        img.type = Image.Type.Tiled;
        img.pixelsPerUnitMultiplier = 2.5f;
        img.rectTransform.sizeDelta = new Vector2(100, height);
        return img;
    }

    // Gauge: dark track with a horizontally filled bar. Returns the fill image.
    public static Image Bar(string name, Transform parent, Color fill, out RectTransform root)
    {
        root = Rect(name, parent);
        var bg = Img("Track", root, "ui_bar_bg", Color.white, true);
        Stretch(bg.rectTransform);
        var f = Filled("Fill", root, "ui_bar_fill", fill, Image.FillMethod.Horizontal);
        Stretch(f.rectTransform, 4, 4, 4, 4);
        return f;
    }

    public static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
    }

    public static T Find<T>(Transform root, string name) where T : Component
    {
        foreach (var c in root.GetComponentsInChildren<T>(true)) if (c.name == name) return c;
        return null;
    }
}
