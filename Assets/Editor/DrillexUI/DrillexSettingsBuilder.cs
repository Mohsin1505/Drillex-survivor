using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static DrillexUIKit;

// Builds the shared Settings panel prefab (used by the main menu and the pause menu).
public static class DrillexSettingsBuilder
{
    public const string PrefabPath = "Assets/UI/Prefabs/UI_SettingsPanel.prefab";

    [MenuItem("Drillex/UI/Rebuild Settings Panel Prefab")]
    public static void Build()
    {
        System.IO.Directory.CreateDirectory("Assets/UI/Prefabs");
        var root = new GameObject("SettingsPanel", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)root.transform;
        Stretch(rt);

        var overlay = root.AddComponent<Image>();
        overlay.color = Overlay;
        overlay.raycastTarget = true;
        root.AddComponent<CanvasGroup>();

        var window = Img("Window", rt, "ui_panel", Color.white, true, true).rectTransform;
        Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860, 660));

        // header
        var gear = Img("HeaderIcon", window, "gear", Hazard);
        Place(gear.rectTransform, new Vector2(0, 1), new Vector2(44, -40), new Vector2(54, 54), new Vector2(0, 1));
        var title = Text("Title", window, "SETTINGS", 46, TextMain);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(116, -38), new Vector2(500, 60), new Vector2(0, 1));
        var stripe = HazardStrip("Hazard", window, 10);
        stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1);
        stripe.rectTransform.pivot = new Vector2(0.5f, 1);
        stripe.rectTransform.offsetMin = new Vector2(40, -122); stripe.rectTransform.offsetMax = new Vector2(-40, -112);
        var close = IconButton("Close", window, "close", TextMain, 76);
        Place((RectTransform)close.transform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(76, 76), new Vector2(1, 1));

        var panel = root.AddComponent<SettingsPanel>();
        var ui = root.AddComponent<UIPanel>();
        ui.window = window;

        float y = -170;
        panel.masterSlider = SliderRow(window, "MASTER VOLUME", "sfx", ref y, null, out panel.masterValue, out _);
        panel.musicSlider = SliderRow(window, "MUSIC", "music", ref y, "music", out panel.musicValue, out panel.musicToggle);
        panel.sfxSlider = SliderRow(window, "SOUND EFFECTS", "speaker", ref y, "sfx", out panel.sfxValue, out panel.sfxToggle);

        var done = Button("Done", window, "DONE", "check", Hazard, true, 32);
        Place((RectTransform)done.transform, new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(300, 92), new Vector2(0.5f, 0));

        var hint = Text("Hint", window, "SETTINGS ARE SAVED AUTOMATICALLY", 16, TextDim, TextAlignmentOptions.Center);
        Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 146), new Vector2(700, 30), new Vector2(0.5f, 0));

        UnityEventTools.AddPersistentListener(close.onClick, panel.Close);
        UnityEventTools.AddPersistentListener(done.onClick, panel.Close);

        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
    }

    static Slider SliderRow(RectTransform window, string label, string icon, ref float y, string toggleKey, out TMP_Text value, out Toggle toggle)
    {
        var row = Rect(label, window);
        row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1); row.pivot = new Vector2(0.5f, 1);
        row.offsetMin = new Vector2(48, y - 120); row.offsetMax = new Vector2(-48, y);
        y -= 132;

        var plate = Img("Plate", row, "ui_panel_dark", Color.white, true);
        Stretch(plate.rectTransform);

        var ic = Img("Icon", row, icon, Steel);
        Place(ic.rectTransform, new Vector2(0, 1), new Vector2(26, -20), new Vector2(40, 40), new Vector2(0, 1));
        var t = Text("Label", row, label, 26, TextMain);
        Place(t.rectTransform, new Vector2(0, 1), new Vector2(80, -20), new Vector2(420, 40), new Vector2(0, 1));

        value = Text("Value", row, "100%", 26, Amber, TextAlignmentOptions.Right);
        Place(value.rectTransform, new Vector2(1, 0), new Vector2(-28, 20), new Vector2(110, 40), new Vector2(1, 0));

        // slider
        var sRoot = Rect("Slider", row);
        sRoot.anchorMin = new Vector2(0, 0); sRoot.anchorMax = new Vector2(1, 0); sRoot.pivot = new Vector2(0.5f, 0);
        sRoot.offsetMin = new Vector2(30, 22); sRoot.offsetMax = new Vector2(-160, 58);
        var track = Img("Track", sRoot, "ui_bar_bg", Color.white, true, true);
        Stretch(track.rectTransform);
        var fillArea = Rect("Fill Area", sRoot); Stretch(fillArea, 6, 6, 6, 6);
        var fill = Img("Fill", fillArea, "ui_bar_fill", Amber);
        fill.type = Image.Type.Simple;
        Stretch(fill.rectTransform);
        var handleArea = Rect("Handle Area", sRoot); Stretch(handleArea, 14, 0, 14, 0);
        var handle = Img("Handle", handleArea, "ui_button", Color.white, true, true);
        handle.rectTransform.sizeDelta = new Vector2(30, 0);
        handle.rectTransform.anchorMin = new Vector2(0, -0.3f); handle.rectTransform.anchorMax = new Vector2(0, 1.3f);
        var grip = Img("Grip", handle.transform, "ui_plate", Hazard);
        Stretch(grip.rectTransform, 11, 12, 11, 12);
        var slider = sRoot.gameObject.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0; slider.maxValue = 1; slider.value = 0.8f;
        var nav = slider.navigation; nav.mode = Navigation.Mode.None; slider.navigation = nav;

        toggle = null;
        if (toggleKey != null)
        {
            var tg = Img("Toggle", row, "ui_button_fill", Hazard, true, true);
            Place(tg.rectTransform, new Vector2(1, 1), new Vector2(-28, -16), new Vector2(128, 50), new Vector2(1, 1));
            var knob = Img("Knob", tg.transform, "ui_button", Color.white, true);
            Place(knob.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(56, 42));
            var st = Text("State", tg.transform, "ON", 20, TextDark, TextAlignmentOptions.Center);
            Place(st.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-27, 0), new Vector2(64, 40));
            toggle = tg.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = tg;
            toggle.isOn = true;
            var tnav = toggle.navigation; tnav.mode = Navigation.Mode.None; toggle.navigation = tnav;
            var vis = tg.gameObject.AddComponent<ToggleSwitchVisual>();
            vis.knob = knob.rectTransform; vis.track = tg; vis.stateLabel = st; vis.travel = 34; vis.onColor = Hazard; vis.offColor = Hex("#3a3e44");
            tg.gameObject.AddComponent<UIButtonFX>().clickSound = false;
        }
        return slider;
    }
}
