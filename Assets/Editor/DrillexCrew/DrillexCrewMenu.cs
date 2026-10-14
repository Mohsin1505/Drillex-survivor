using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static DrillexUIKit;

// Adds the crew character and the CREW customise screen to the main menu. Idempotent: called at the
// end of DrillexMenuBuilder.Build() (which recreates MenuRoot / MenuStage), or on its own from the menu.
public static class DrillexCrewMenu
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string IconDir = "Assets/UI/Sprites/Icons";
    // stage placement: the point on the sand under this viewport position of the menu camera's
    // opening shot (open ground left of the drill head), turned towards the camera
    static readonly Vector2 CharacterViewport = new Vector2(0.64f, 0.2f);
    const float TurnTowardTrain = 25f;

    [MenuItem("Drillex/Crew/Install Crew Screen In Main Menu")]
    public static void InstallInMainMenu()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Install();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // Works on the currently open MainMenu scene; the caller saves it.
    public static void Install()
    {
        EnsureIcons();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexCrewSetup.PrefabPath);
        var stage = GameObject.Find("MenuStage");
        var menuRoot = GameObject.Find("MenuRoot");
        if (prefab == null || stage == null || menuRoot == null) { Debug.LogWarning("DrillexCrewMenu: missing prefab, MenuStage or MenuRoot"); return; }

        // ---------------- character on the stage ----------------
        var oldChar = stage.transform.Find("CrewMember"); if (oldChar) Object.DestroyImmediate(oldChar.gameObject);
        var oldFocus = stage.transform.Find("CrewFocus"); if (oldFocus) Object.DestroyImmediate(oldFocus.gameObject);
        var rig = Object.FindAnyObjectByType<MenuCameraRig>(FindObjectsInactive.Include);
        Vector3 pos = new Vector3(2.5f, 0f, -6.2f);
        float yaw = -150f;
        if (rig != null)
        {
            rig.Snap();
            var cam = rig.GetComponent<Camera>();
            var ray = cam.ViewportPointToRay(new Vector3(CharacterViewport.x, CharacterViewport.y, 0));
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist)) pos = ray.GetPoint(dist);
            Vector3 toCam = cam.transform.position - pos; toCam.y = 0;
            yaw = Quaternion.LookRotation(toCam).eulerAngles.y + TurnTowardTrain;
        }
        var ch = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage.transform);
        ch.name = "CrewMember";
        ch.transform.position = pos;
        ch.transform.rotation = Quaternion.Euler(0, yaw, 0);
        var focus = new GameObject("CrewFocus").transform; focus.SetParent(stage.transform);
        focus.position = pos + Vector3.up * 1.05f;

        // ---------------- UI ----------------
        var croot = menuRoot.GetComponentInParent<Canvas>(true).rootCanvas.transform;
        var safe = menuRoot.transform.Find("Safe") as RectTransform;
        var ctl = menuRoot.GetComponent<MainMenuController>();
        var oldPanel = croot.Find("CrewPanel"); if (oldPanel) Object.DestroyImmediate(oldPanel.gameObject);
        var oldBtn = safe.Find("CREW"); if (oldBtn) Object.DestroyImmediate(oldBtn.gameObject);

        var menuGroup = GetOrAdd<CanvasGroup>(menuRoot);
        var panel = BuildPanel(croot);
        panel.character = ch.GetComponent<CrewAppearance>();
        panel.cameraRig = rig;
        panel.crewFocus = focus;
        panel.menuGroup = menuGroup;
        panel.crewAngle = Mathf.Repeat(yaw, 360f);

        // CREW button: slots in under PLAY, the menu buttons below shift down to make room
        var crewBtn = Button("CREW", safe, "CREW", "crew", Amber, false, 32);
        var rt = (RectTransform)crewBtn.transform;
        Place(rt, new Vector2(0, 1), new Vector2(110, -470), new Vector2(420, 96), new Vector2(0, 1));
        UnityEventTools.AddPersistentListener(crewBtn.onClick, panel.Open);
        var tag = Text("Callsign", rt, "RUSTY", 18, Amber, TextAlignmentOptions.Right);
        Stretch(tag.rectTransform, 0, 0, 26, 0);
        panel.menuNameLabel = tag;

        // re-stack the menu column top-down from PLAY so repeated installs never drift
        const float gap = 14;
        var list = ctl.buttons;
        list.RemoveAll(b => b == null || b.name == "CREW");
        int playIdx = list.FindIndex(b => b.name == "PLAY");
        list.Insert(playIdx >= 0 ? playIdx + 1 : 0, rt);
        float total = list.Sum(b => b.sizeDelta.y) + gap * (list.Count - 1);
        // keep the column clear of the footer (1080 reference height, ~90 px bottom margin)
        float y = Mathf.Max(playIdx >= 0 ? list[playIdx].anchoredPosition.y : -470f, -990f + total);
        foreach (var b in list)
        {
            if (b == rt) b.sizeDelta = new Vector2(420, 96);
            b.anchoredPosition = new Vector2(b.anchoredPosition.x, y);
            y -= b.sizeDelta.y + gap;
        }
        rt.SetSiblingIndex(list[0].GetSiblingIndex() + 1);
        EditorUtility.SetDirty(ctl);
        Debug.Log("Drillex crew screen installed");
    }

    // ---------------------------------------------------------------- panel
    static CrewSelectPanel BuildPanel(Transform croot)
    {
        var t = Rect("CrewPanel", croot); Stretch(t);
        GetOrAdd<CanvasGroup>(t.gameObject);
        // right-side dark gradient so the window reads over the desert, character stays visible on the left
        var shade = Img("Shade", t, "ui_grad_h", new Color(0.03f, 0.025f, 0.02f, 0.9f));
        shade.rectTransform.anchorMin = new Vector2(1, 0); shade.rectTransform.anchorMax = new Vector2(1, 1); shade.rectTransform.pivot = new Vector2(1, 0.5f);
        shade.rectTransform.sizeDelta = new Vector2(1250, 0); shade.rectTransform.localScale = new Vector3(-1, 1, 1);
        var safe = Rect("Safe", t); Stretch(safe); safe.gameObject.AddComponent<SafeAreaFitter>();

        var window = Img("Window", safe, "ui_panel", Color.white, true, true).rectTransform;
        Place(window, new Vector2(1, 0.5f), new Vector2(-90, 0), new Vector2(760, 940), new Vector2(1, 0.5f));
        var ui = t.gameObject.AddComponent<UIPanel>(); ui.window = window; ui.slideFrom = new Vector2(60, 0);
        var p = t.gameObject.AddComponent<CrewSelectPanel>();

        // header
        var hIcon = Img("HeaderIcon", window, "ui_button_fill", Amber, true);
        Place(hIcon.rectTransform, new Vector2(0, 1), new Vector2(36, -30), new Vector2(84, 84), new Vector2(0, 1));
        var glyph = Img("Glyph", hIcon.transform, "crew", TextDark); Stretch(glyph.rectTransform, 12, 12, 12, 12);
        var title = Text("Title", window, "CREW", 50, TextMain);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(140, -26), new Vector2(420, 60), new Vector2(0, 1));
        var sub = Text("Subtitle", window, "CUSTOMISE YOUR DRILLER", 18, Amber);
        Place(sub.rectTransform, new Vector2(0, 1), new Vector2(142, -86), new Vector2(480, 26), new Vector2(0, 1));
        var close = IconButton("Close", window, "close", TextMain, 84);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(84, 84), new Vector2(1, 1));
        UnityEventTools.AddPersistentListener(close.onClick, p.Cancel);
        var stripe = HazardStrip("Hazard", window, 10);
        stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.pivot = new Vector2(0.5f, 1);
        stripe.rectTransform.offsetMin = new Vector2(36, -136); stripe.rectTransform.offsetMax = new Vector2(-36, -126);

        // callsign
        float y = -166;
        var nameRow = Row(window, "Callsign", "CALLSIGN", ref y, 128);
        p.nameField = NameField(nameRow);
        var dice = IconButton("RandomName", nameRow, "dice", Amber, 72);
        Place((RectTransform)dice.transform, new Vector2(1, 0), new Vector2(-18, 16), new Vector2(72, 72), new Vector2(1, 0));
        UnityEventTools.AddPersistentListener(dice.onClick, p.Randomize);

        // selectors
        p.suitValue = Selector(window, "Suit", "SUIT", ref y, p.PrevSuit, p.NextSuit, out p.suitIndex, out var suitSw);
        p.suitSwatch = suitSw;
        var acc = Img("Accent", suitSw.transform, "ui_plate", Amber, true);
        acc.rectTransform.anchorMin = new Vector2(0, 0); acc.rectTransform.anchorMax = new Vector2(1, 0); acc.rectTransform.pivot = new Vector2(0.5f, 0);
        acc.rectTransform.offsetMin = new Vector2(6, 6); acc.rectTransform.offsetMax = new Vector2(-6, 18);
        p.suitSwatchAccent = acc;
        p.skinValue = Selector(window, "Skin", "SKIN TONE", ref y, p.PrevSkin, p.NextSkin, out p.skinIndex, out p.skinSwatch);
        p.headgearValue = Selector(window, "Headgear", "HEADGEAR", ref y, p.PrevHeadgear, p.NextHeadgear, out p.headgearIndex, out var hgSw);
        var hgIcon = Img("Icon", hgSw.transform, "crew", Amber); Stretch(hgIcon.rectTransform, 10, 10, 10, 10);
        hgSw.color = new Color(0.12f, 0.13f, 0.14f);

        // actions
        var random = Button("Randomize", window, "RANDOM", "dice", Steel, false, 28);
        Place((RectTransform)random.transform, new Vector2(0, 0), new Vector2(36, 36), new Vector2(300, 100), new Vector2(0, 0));
        UnityEventTools.AddPersistentListener(random.onClick, p.Randomize);
        var confirm = Button("Confirm", window, "CONFIRM", "check", Hazard, true, 34);
        Place((RectTransform)confirm.transform, new Vector2(1, 0), new Vector2(-36, 36), new Vector2(370, 100), new Vector2(1, 0));
        UnityEventTools.AddPersistentListener(confirm.onClick, p.Confirm);

        t.gameObject.SetActive(false);
        return p;
    }

    static RectTransform Row(RectTransform window, string name, string label, ref float y, float h)
    {
        var row = Img(name, window, "ui_panel_dark", Color.white, true).rectTransform;
        row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
        row.offsetMin = new Vector2(36, y - h); row.offsetMax = new Vector2(-36, y);
        var l = Text("Label", row, label, 16, TextDim);
        Place(l.rectTransform, new Vector2(0, 1), new Vector2(24, -14), new Vector2(300, 22), new Vector2(0, 1));
        y -= h + 16;
        return row;
    }

    static TextMeshProUGUI Selector(RectTransform window, string name, string label, ref float y, UnityEngine.Events.UnityAction prev, UnityEngine.Events.UnityAction next, out TextMeshProUGUI index, out Image swatch)
    {
        var row = Row(window, name, label, ref y, 128);
        var sw = Img("Swatch", row, "ui_plate", Color.white, true);
        Place(sw.rectTransform, new Vector2(0, 0), new Vector2(24, 18), new Vector2(64, 64), new Vector2(0, 0));
        swatch = sw;
        var value = Text("Value", row, "-", 30, TextMain);
        Place(value.rectTransform, new Vector2(0, 0), new Vector2(104, 22), new Vector2(330, 56), new Vector2(0, 0));
        value.alignment = TextAlignmentOptions.Left;
        index = Text("Index", row, "1/1", 16, TextDim, TextAlignmentOptions.Right);
        Place(index.rectTransform, new Vector2(1, 1), new Vector2(-24, -14), new Vector2(120, 22), new Vector2(1, 1));
        var r = IconButton("Next", row, "chevron", Amber, 76);
        Place((RectTransform)r.transform, new Vector2(1, 0), new Vector2(-16, 14), new Vector2(76, 76), new Vector2(1, 0));
        UnityEventTools.AddPersistentListener(r.onClick, next);
        var l = IconButton("Prev", row, "chevron", Amber, 76);
        Place((RectTransform)l.transform, new Vector2(1, 0), new Vector2(-104, 14), new Vector2(76, 76), new Vector2(1, 0));
        l.transform.Find("Icon").localScale = new Vector3(-1, 1, 1);
        UnityEventTools.AddPersistentListener(l.onClick, prev);
        return value;
    }

    static TMP_InputField NameField(RectTransform row)
    {
        var bg = Img("NameField", row, "ui_bar_bg", Color.white, true, true);
        bg.rectTransform.anchorMin = new Vector2(0, 0); bg.rectTransform.anchorMax = new Vector2(1, 0); bg.rectTransform.pivot = new Vector2(0, 0);
        bg.rectTransform.offsetMin = new Vector2(20, 16); bg.rectTransform.offsetMax = new Vector2(-108, 88);
        var area = Rect("Text Area", bg.transform); Stretch(area, 20, 4, 20, 4);
        area.gameObject.AddComponent<RectMask2D>();
        var ph = Text("Placeholder", area, "ENTER A NAME", 30, A(TextDim, 0.6f)); Stretch(ph.rectTransform);
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        var tx = Text("Text", area, "", 34, Amber); Stretch(tx.rectTransform);
        tx.alignment = TextAlignmentOptions.MidlineLeft;
        var f = bg.gameObject.AddComponent<TMP_InputField>();
        f.textViewport = area; f.textComponent = tx; f.placeholder = ph; f.fontAsset = DrillexUIKit.Font;
        f.characterLimit = CrewProfile.MaxNameLength;
        f.contentType = TMP_InputField.ContentType.Standard;
        f.lineType = TMP_InputField.LineType.SingleLine;
        f.caretColor = Amber; f.customCaretColor = true; f.caretWidth = 3;
        f.selectionColor = A(Hazard, 0.35f);
        f.onFocusSelectAll = true;
        f.targetGraphic = bg;
        var nav = f.navigation; nav.mode = Navigation.Mode.None; f.navigation = nav;
        return f;
    }

    // ---------------------------------------------------------------- icons (same SDF style as the UI kit)
    static void EnsureIcons()
    {
        Make("crew", p => U(
            // hard hat with a head lamp, over a bearded face
            Sub(Sub(Circle(p, V(0, 8), 36), Box(p, V(0, -32), V(70, 40))), Circle(p, V(0, 26), 10)),
            Circle(p, V(0, 26), 5),
            RBox(p, V(0, 6), V(50, 7), 3),
            Sub(Circle(p, V(0, -24), 28), Box(p, V(0, 26), V(60, 30)))));
        Make("chevron", p => U(Seg(p, V(-12, 30), V(16, 0), 9), Seg(p, V(16, 0), V(-12, -30), 9)));
        Make("dice", p => Sub(RBox(p, V(0, 0), V(40, 40), 10),
            U(Circle(p, V(-18, 18), 7), Circle(p, V(18, 18), 7), Circle(p, V(0, 0), 7), Circle(p, V(-18, -18), 7), Circle(p, V(18, -18), 7))));
    }

    delegate float Sdf(Vector2 p);
    static Vector2 V(float x, float y) => new Vector2(x, y);
    static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
    static float Box(Vector2 p, Vector2 c, Vector2 h)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y);
        return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
    }
    static float RBox(Vector2 p, Vector2 c, Vector2 h, float r) => Box(p, c, h - Vector2.one * r) - r;
    static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * k).magnitude - r;
    }
    static float U(params float[] d) => d.Min();
    static float Sub(float a, float b) => Mathf.Max(a, -b);

    static void Make(string name, Sdf f)
    {
        string path = IconDir + "/icon_" + name + ".png";
        const int size = 128;
        var t = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - 64f, y + 0.5f - 64f);
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - f(p)));
            }
        t.SetPixels(px); t.Apply();
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        // copy import settings from an existing kit icon so it packs into the same atlas the same way
        var src = (TextureImporter)AssetImporter.GetAtPath(IconDir + "/icon_play.png");
        var dst = (TextureImporter)AssetImporter.GetAtPath(path);
        if (src != null && dst != null)
        {
            var s = new TextureImporterSettings(); src.ReadTextureSettings(s); dst.SetTextureSettings(s);
            foreach (var plat in new[] { "Standalone", "Android", "iPhone", "WebGL" }) dst.SetPlatformTextureSettings(src.GetPlatformTextureSettings(plat));
            dst.textureCompression = src.textureCompression;
            dst.maxTextureSize = src.maxTextureSize;
            dst.SaveAndReimport();
        }
    }
}
