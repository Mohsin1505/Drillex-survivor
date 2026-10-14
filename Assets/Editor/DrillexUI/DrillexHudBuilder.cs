using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static DrillexUIKit;

// Rebuilds the Lev1 gameplay HUD, workshop, pause, game-over and worm-warning UI in the
// Drillex industrial style. Objects referenced by gameplay scripts (pause panel, upgrade
// panel/button, compass, game-over, worm warning) are kept and restyled so their existing
// references and button events stay intact. Safe to run repeatedly.
public static class DrillexHudBuilder
{
    const string ScenePath = "Assets/Scenes/Lev1.unity";

    [MenuItem("Drillex/UI/Rebuild Lev1 HUD")]
    public static void Build()
    {
        // always start from the saved scene so a failed run never leaves half-built UI behind
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        DrillexProgressionSetup.Configure();

        var manager = Object.FindAnyObjectByType<Manager>(FindObjectsInactive.Include);
        var xp = Object.FindAnyObjectByType<ExperienceManager>(FindObjectsInactive.Include);
        var storage = Object.FindAnyObjectByType<TrainStorageManager>(FindObjectsInactive.Include);
        var health = Object.FindAnyObjectByType<TrainHealth>(FindObjectsInactive.Include);
        var trap = Object.FindAnyObjectByType<TrapDeployer>(FindObjectsInactive.Include);
        var pause = Object.FindAnyObjectByType<PauseManager>(FindObjectsInactive.Include);
        var station = Object.FindAnyObjectByType<UpgradeStation>(FindObjectsInactive.Include);
        var compass = Object.FindAnyObjectByType<SafeZoneCompass>(FindObjectsInactive.Include);
        var gameOver = Object.FindAnyObjectByType<GameOverButtons>(FindObjectsInactive.Include);
        var canvas = manager.wormWarningPanel.GetComponentInParent<Canvas>(true).rootCanvas;
        var croot = canvas.transform;

        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f; // landscape phones: keep HUD height constant, extra width goes to the sides

        // ---- objects we keep (referenced by gameplay code / scene events) ----
        var pauseButton = croot.GetComponentsInChildren<Button>(true).FirstOrDefault(b => HasListener(b, pause, "PauseGame"));
        var keep = new List<Transform> { manager.wormWarningPanel.transform, station.upgradePanel.transform, station.upgradeButton.transform, compass.arrow.parent, gameOver.transform, pause.pausePanel.transform };
        if (pauseButton != null) keep.Add(pauseButton.transform);
        foreach (var k in keep) k.SetParent(croot, false);

        // legacy noise slider: Manager writes to it every frame, so it stays but hidden
        Transform legacy = croot.Find("_Legacy");
        if (legacy == null) legacy = Rect("_Legacy", croot);
        legacy.gameObject.SetActive(false);
        if (manager.noiseSlider != null) manager.noiseSlider.transform.SetParent(legacy, false);

        foreach (var n in new[] { "Score", "Storage", "ex", "Slider", "Lev ex", "trap", "HUD_Root", "Toast", "SettingsPanel" })
        {
            var t = croot.Find(n);
            if (t != null && !keep.Contains(t)) Object.DestroyImmediate(t.gameObject);
        }

        // ---- disconnect old text/slider widgets (all null-checked by their scripts) ----
        SetRef(manager, "permanentOrbText", null);
        SetRef(storage, "storageText", null);
        SetRef(xp, "xpSlider", null);
        SetRef(xp, "levelText", null);
        SetRef(xp, "xpText", null);
        SetRef(trap, "cooldownSlider", null);

        // train world-space health bar is replaced by the HUD hull gauge
        if (health.healthSlider != null)
        {
            var wc = health.healthSlider.GetComponentInParent<Canvas>(true);
            if (wc != null && wc.renderMode == RenderMode.WorldSpace) wc.gameObject.SetActive(false);
        }

        // ---- HUD root (safe area) ----
        var hud = Rect("HUD_Root", croot); Stretch(hud); hud.SetAsFirstSibling();
        hud.gameObject.AddComponent<SafeAreaFitter>();
        var hudGroup = hud.gameObject.AddComponent<CanvasGroup>();
        var ctl = hud.gameObject.AddComponent<HUDController>();

        BuildVitals(hud, ctl);
        BuildLevel(hud, ctl);
        RestylePause(hud, pauseButton);
        RestyleCompass(hud, compass);
        BuildJoystick(hud);
        BuildAbility(hud);
        RestyleUpgradeButton(hud, station.upgradeButton, ctl);

        ctl.levelIcon = S("star"); ctl.electricIcon = S("bolt"); ctl.rocketIcon = S("missile"); ctl.blockIcon = S("train"); ctl.warningIcon = S("warning");

        // ---- overlays (order = draw order) ----
        RestyleWarning(croot, manager.wormWarningPanel);
        DrillexWorkshopBuilder.Build(station.upgradePanel, station);
        RestyleGameOver(croot, gameOver);
        RestylePausePanel(croot, pause);

        var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSettingsBuilder.PrefabPath);
        if (settingsPrefab == null) { DrillexSettingsBuilder.Build(); settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSettingsBuilder.PrefabPath); }
        var settings = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, croot);
        settings.name = "SettingsPanel";
        settings.SetActive(false);

        BuildToast(croot);

        manager.wormWarningPanel.transform.SetSiblingIndex(1);
        station.upgradePanel.transform.SetAsLastSibling();
        gameOver.transform.SetAsLastSibling();
        pause.pausePanel.transform.SetAsLastSibling();
        settings.transform.SetAsLastSibling();
        croot.Find("Toast").SetAsLastSibling();
        legacy.SetAsLastSibling();

        SetRef(pause, "settingsPanel", settings.GetComponent<SettingsPanel>());
        SetRef(pause, "hudGroup", hudGroup);

        if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Drillex HUD rebuilt");
    }

    public static void OpenLev1() { EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single); }
    public static void OpenMenu() { EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single); }

    // ------------------------------------------------------------------ helpers

    static bool HasListener(Button b, Object target, string method)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            if (b.onClick.GetPersistentTarget(i) == target && b.onClick.GetPersistentMethodName(i) == method) return true;
        return false;
    }

    public static void SetRef(Object o, string field, Object value)
    {
        if (o == null) return;
        var so = new SerializedObject(o);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogWarning("no field " + field + " on " + o); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform Panel(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string sprite = "ui_panel")
    {
        var p = Img(name, parent, sprite, Color.white, true).rectTransform;
        Place(p, anchor, pos, size);
        return p;
    }

    // ------------------------------------------------------------------ top-left vitals

    static void BuildVitals(RectTransform hud, HUDController ctl)
    {
        // total orbs
        var orb = Panel("TotalOrbs", hud, new Vector2(0, 1), new Vector2(24, -20), new Vector2(350, 92));
        var glow = Img("OrbGlow", orb, "ui_glow", A(Ember, 0.55f));
        Place(glow.rectTransform, new Vector2(0, 0.5f), new Vector2(4, 0), new Vector2(100, 100), new Vector2(0, 0.5f));
        var icon = Img("OrbIcon", orb, "ui_orb", Color.white);
        Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(66, 66), new Vector2(0.5f, 0.5f));
        var lbl = Text("Label", orb, "TOTAL ORBS", 17, TextDim);
        Place(lbl.rectTransform, new Vector2(0, 1), new Vector2(102, -14), new Vector2(230, 24), new Vector2(0, 1));
        var num = Text("Value", orb, "0", 40, Amber);
        Place(num.rectTransform, new Vector2(0, 0), new Vector2(102, 10), new Vector2(236, 52), new Vector2(0, 0));
        num.enableAutoSizing = true; num.fontSizeMin = 22; num.fontSizeMax = 40;
        var counter = orb.gameObject.AddComponent<AnimatedCounter>();
        counter.label = num; counter.punchTarget = num.transform;
        ctl.totalOrbs = counter;
        ctl.orbIcon = glow.rectTransform; // slow rotation on the soft glow behind the orb

        // orbs in train
        var tr = Panel("TrainOrbs", hud, new Vector2(0, 1), new Vector2(24, -120), new Vector2(350, 92));
        var accent = Img("Accent", tr, "ui_plate", Hazard, true);
        accent.rectTransform.anchorMin = new Vector2(0, 0); accent.rectTransform.anchorMax = new Vector2(0, 1);
        accent.rectTransform.pivot = new Vector2(0, 0.5f); accent.rectTransform.offsetMin = new Vector2(8, 14); accent.rectTransform.offsetMax = new Vector2(14, -14);
        var sIcon = Img("Icon", tr, "storage", Steel);
        Place(sIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(54, 6), new Vector2(50, 50), new Vector2(0.5f, 0.5f));
        var tl = Text("Label", tr, "ORBS IN TRAIN", 17, TextDim);
        Place(tl.rectTransform, new Vector2(0, 1), new Vector2(102, -12), new Vector2(230, 24), new Vector2(0, 1));
        var tv = Text("Value", tr, "0 / 300", 30, TextMain);
        Place(tv.rectTransform, new Vector2(0, 1), new Vector2(102, -36), new Vector2(160, 36), new Vector2(0, 1));
        var full = Text("Full", tr, "FULL", 20, Danger, TextAlignmentOptions.Right);
        Place(full.rectTransform, new Vector2(1, 1), new Vector2(-20, -40), new Vector2(90, 30), new Vector2(1, 1));
        var fill = Bar("Bar", tr, Hazard, out var barRoot);
        barRoot.anchorMin = new Vector2(0, 0); barRoot.anchorMax = new Vector2(1, 0); barRoot.pivot = new Vector2(0.5f, 0);
        barRoot.offsetMin = new Vector2(102, 12); barRoot.offsetMax = new Vector2(-20, 28);
        ctl.trainOrbsText = tv; ctl.trainFill = fill; ctl.trainFullTag = full; ctl.trainPanelAccent = accent;

        // hull
        var hp = Panel("Hull", hud, new Vector2(0, 1), new Vector2(24, -220), new Vector2(350, 74));
        var hIcon = Img("Icon", hp, "shield", Hex("#8ee06a"));
        Place(hIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(54, 0), new Vector2(44, 44), new Vector2(0.5f, 0.5f));
        var hl = Text("Label", hp, "HULL", 17, TextDim);
        Place(hl.rectTransform, new Vector2(0, 1), new Vector2(102, -10), new Vector2(100, 24), new Vector2(0, 1));
        var hv = Text("Value", hp, "100 / 100", 20, TextMain, TextAlignmentOptions.Right);
        Place(hv.rectTransform, new Vector2(1, 1), new Vector2(-20, -9), new Vector2(160, 26), new Vector2(1, 1));
        var hfill = Bar("Bar", hp, Hex("#8ee06a"), out var hRoot);
        hRoot.anchorMin = new Vector2(0, 0); hRoot.anchorMax = new Vector2(1, 0); hRoot.pivot = new Vector2(0.5f, 0);
        hRoot.offsetMin = new Vector2(102, 12); hRoot.offsetMax = new Vector2(-20, 30);
        ctl.hullText = hv; ctl.hullFill = hfill;
    }

    // ------------------------------------------------------------------ top-centre level / xp / noise

    static void BuildLevel(RectTransform hud, HUDController ctl)
    {
        var lv = Panel("LevelXP", hud, new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(640, 92));
        var badge = Img("Badge", lv, "ui_button_fill", Hazard, true);
        Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(150, 66), new Vector2(0, 0.5f));
        var star = Img("Star", badge.transform, "star", TextDark);
        Place(star.rectTransform, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(34, 34), new Vector2(0, 0.5f));
        var lvText = Text("Level", badge.transform, "LV 1", 28, TextDark, TextAlignmentOptions.Center);
        Stretch(lvText.rectTransform, 46, 0, 8, 0);
        lvText.enableAutoSizing = true; lvText.fontSizeMin = 16; lvText.fontSizeMax = 28;

        var label = Text("Label", lv, "EXPERIENCE", 17, TextDim);
        Place(label.rectTransform, new Vector2(0, 1), new Vector2(184, -14), new Vector2(220, 24), new Vector2(0, 1));
        var xpv = Text("Value", lv, "0 / 100 XP", 20, Amber, TextAlignmentOptions.Right);
        Place(xpv.rectTransform, new Vector2(1, 1), new Vector2(-22, -12), new Vector2(260, 26), new Vector2(1, 1));

        var barRoot = Rect("Bar", lv);
        barRoot.anchorMin = new Vector2(0, 0); barRoot.anchorMax = new Vector2(1, 0); barRoot.pivot = new Vector2(0.5f, 0);
        barRoot.offsetMin = new Vector2(184, 16); barRoot.offsetMax = new Vector2(-22, 42);
        var track = Img("Track", barRoot, "ui_bar_bg", Color.white, true); Stretch(track.rectTransform);
        var gain = Filled("Gain", barRoot, "ui_bar_fill", A(Color.white, 0.85f), Image.FillMethod.Horizontal); Stretch(gain.rectTransform, 4, 4, 4, 4);
        var fill = Filled("Fill", barRoot, "ui_bar_fill", Amber, Image.FillMethod.Horizontal); Stretch(fill.rectTransform, 4, 4, 4, 4);
        ctl.levelText = lvText; ctl.xpText = xpv; ctl.xpFill = fill; ctl.xpGainFill = gain;

        // noise / worm threat
        var nz = Panel("Noise", hud, new Vector2(0.5f, 1), new Vector2(0, -118), new Vector2(460, 50), "ui_panel_dark");
        var nIcon = Img("Icon", nz, "soundtrap", TextDim);
        Place(nIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(18, 0), new Vector2(30, 30), new Vector2(0, 0.5f));
        var nl = Text("Label", nz, "NOISE", 17, TextDim);
        Place(nl.rectTransform, new Vector2(0, 0.5f), new Vector2(56, 0), new Vector2(90, 24), new Vector2(0, 0.5f));
        var nfill = Bar("Bar", nz, Hazard, out var nRoot);
        nRoot.anchorMin = new Vector2(0, 0.5f); nRoot.anchorMax = new Vector2(1, 0.5f); nRoot.pivot = new Vector2(0.5f, 0.5f);
        nRoot.offsetMin = new Vector2(146, -9); nRoot.offsetMax = new Vector2(-20, 9);
        ctl.noiseFill = nfill; ctl.noiseLabel = nl;
    }

    // ------------------------------------------------------------------ top-right

    static void RestylePause(RectTransform hud, Button pauseButton)
    {
        if (pauseButton == null) return;
        var t = (RectTransform)pauseButton.transform;
        t.name = "PauseButton";
        t.SetParent(hud, false);
        Clear(t);
        foreach (var fx in t.GetComponents<UIButtonFX>()) Object.DestroyImmediate(fx);
        Place(t, new Vector2(1, 1), new Vector2(-24, -20), new Vector2(92, 92));
        var img = t.GetComponent<Image>(); img.sprite = S("ui_button"); img.type = Image.Type.Sliced; img.color = Color.white;
        pauseButton.targetGraphic = img;
        var glow = Img("HoverGlow", t, "ui_frame_glow", Hazard, true); Stretch(glow.rectTransform, -16, -16, -16, -16);
        var icon = Img("Icon", t, "pause", TextMain); Stretch(icon.rectTransform, 26, 26, 26, 26);
        t.gameObject.AddComponent<UIButtonFX>().hoverGlow = glow;
        var nav = pauseButton.navigation; nav.mode = Navigation.Mode.None; pauseButton.navigation = nav;
    }

    static void RestyleCompass(RectTransform hud, SafeZoneCompass compass)
    {
        // the SafeZoneCompass script sits on a scene object; the UI widget is the arrow's parent
        var t = (RectTransform)compass.arrow.parent;
        t.name = "compass";
        t.SetParent(hud, false);
        Place(t, new Vector2(1, 1), new Vector2(-24, -132), new Vector2(150, 150));
        var bg = t.GetComponent<Image>(); bg.sprite = S("ui_circle"); bg.color = A(Hex("#111315"), 0.85f); bg.type = Image.Type.Simple; bg.raycastTarget = false;
        var old = t.Find("Ring"); if (old) Object.DestroyImmediate(old.gameObject);
        var oldLbl = t.Find("Label"); if (oldLbl) Object.DestroyImmediate(oldLbl.gameObject);
        var ring = Img("Ring", t, "ui_ring_metal", Color.white); Stretch(ring.rectTransform); ring.transform.SetAsFirstSibling();
        var arrow = compass.arrow;
        Place(arrow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
        var aimg = arrow.GetComponent<Image>(); if (aimg) { aimg.sprite = S("ui_ring_dashed"); aimg.color = A(Amber, 0.35f); }
        var tip = arrow.Find("Tip");
        if (tip == null) { tip = Img("Tip", arrow, "play", Amber).transform; }
        Place((RectTransform)tip, new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(46, 46));
        tip.localRotation = Quaternion.Euler(0, 0, 90);
        var dist = (RectTransform)compass.distanceText.transform;
        Place(dist, new Vector2(0.5f, 0), new Vector2(0, -34), new Vector2(160, 30), new Vector2(0.5f, 0));
        var dt = compass.distanceText as TextMeshProUGUI;
        if (dt) { dt.font = DrillexUIKit.Font; dt.fontSize = 22; dt.color = TextMain; dt.alignment = TextAlignmentOptions.Center; dt.text = "0 m"; }
        var label = Text("Label", t, "SAFE ZONE", 14, Amber, TextAlignmentOptions.Center);
        Place(label.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -56), new Vector2(160, 20), new Vector2(0.5f, 0));
    }

    // ------------------------------------------------------------------ bottom-left steering

    static void BuildJoystick(RectTransform hud)
    {
        var area = Img("SteerJoystick", hud, null, new Color(0, 0, 0, 0), false, true).rectTransform;
        Place(area, new Vector2(0, 0), new Vector2(0, 0), new Vector2(560, 300), new Vector2(0, 0));
        var group = area.gameObject.AddComponent<CanvasGroup>();

        var track = Img("Track", area, "ui_panel_dark", Color.white, true).rectTransform;
        Place(track, new Vector2(0, 0), new Vector2(60, 40), new Vector2(380, 96), new Vector2(0, 0));
        var stripe = HazardStrip("Hazard", track, 8);
        stripe.rectTransform.anchorMin = new Vector2(0, 0); stripe.rectTransform.anchorMax = new Vector2(1, 0);
        stripe.rectTransform.pivot = new Vector2(0.5f, 0); stripe.rectTransform.offsetMin = new Vector2(26, 10); stripe.rectTransform.offsetMax = new Vector2(-26, 18);
        stripe.color = A(Color.white, 0.6f);
        var left = Img("Left", track, "play", A(TextDim, 0.8f)); Place(left.rectTransform, new Vector2(0, 0.5f), new Vector2(26, 6), new Vector2(34, 34), new Vector2(0, 0.5f)); left.transform.localRotation = Quaternion.Euler(0, 0, 180);
        var right = Img("Right", track, "play", A(TextDim, 0.8f)); Place(right.rectTransform, new Vector2(1, 0.5f), new Vector2(-26, 6), new Vector2(34, 34), new Vector2(1, 0.5f));
        var label = Text("Label", area, "STEER", 16, TextDim, TextAlignmentOptions.Center);
        Place(label.rectTransform, new Vector2(0, 0), new Vector2(250, 164), new Vector2(200, 24), new Vector2(0.5f, 0));

        var knobRoot = Rect("KnobRoot", track); Place(knobRoot, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(0, 0));
        var knob = Rect("Knob", knobRoot); Place(knob, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(124, 124));
        var kg = Img("Glow", knob, "ui_ring_glow", A(Hazard, 0.5f)); Stretch(kg.rectTransform, -24, -24, -24, -24);
        var kb = Img("Base", knob, "ui_circle", Hex("#1a1c1f")); Stretch(kb.rectTransform, 6, 6, 6, 6);
        var kr = Img("Ring", knob, "ui_ring_metal", Color.white); Stretch(kr.rectTransform);
        var kc = Img("Grip", knob, "ui_circle", Hazard); Stretch(kc.rectTransform, 40, 40, 40, 40);

        var stick = area.gameObject.AddComponent<SteerJoystick>();
        stick.knob = knob; stick.track = knobRoot; stick.group = group; stick.radius = 130f; stick.touchOnly = true;
    }

    // ------------------------------------------------------------------ bottom-right ability + upgrade

    static void BuildAbility(RectTransform hud)
    {
        var root = Rect("AbilityButton", hud);
        Place(root, new Vector2(1, 0), new Vector2(-56, 64), new Vector2(200, 200), new Vector2(1, 0));
        var glow = Img("ReadyGlow", root, "ui_ring_glow", Color.white); Stretch(glow.rectTransform, -44, -44, -44, -44);
        var baseImg = Img("Base", root, "ui_circle", Hex("#141619"), false, true); Stretch(baseImg.rectTransform, 10, 10, 10, 10);
        var energy = Img("EnergyRing", root, "ui_ring", A(Color.white, 0.18f)); Stretch(energy.rectTransform, 18, 18, 18, 18);
        var cd = Filled("Cooldown", root, "ui_ring", Color.white, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
        cd.fillClockwise = true; Stretch(cd.rectTransform, 18, 18, 18, 18);
        var metal = Img("Bezel", root, "ui_ring_metal", Color.white); Stretch(metal.rectTransform);
        var active = Img("ActiveRing", root, "ui_ring_dashed", Color.white); Stretch(active.rectTransform, -16, -16, -16, -16);
        var icon = Img("Icon", root, "soundtrap", Color.white); Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(92, 92));
        var cdt = Text("CooldownText", root, "10", 40, TextMain, TextAlignmentOptions.Center);
        Place(cdt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(160, 60));
        var nameT = Text("Name", root, "SOUND TRAP", 17, TextMain, TextAlignmentOptions.Center);
        Place(nameT.rectTransform, new Vector2(0.5f, 0), new Vector2(0, -30), new Vector2(240, 24), new Vector2(0.5f, 0));

        var btn = baseImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = baseImg; btn.transition = Selectable.Transition.None;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;
        var fx = baseImg.gameObject.AddComponent<UIButtonFX>(); fx.clickSound = false; fx.hoverScale = 1f;

        var ab = root.gameObject.AddComponent<AbilityButton>();
        ab.button = btn; ab.icon = icon; ab.cooldownFill = cd; ab.energyRing = energy; ab.readyGlow = glow; ab.activeRing = active; ab.cooldownText = cdt; ab.nameText = nameT;
        ab.visuals = new List<AbilityButton.AbilityVisual>
        {
            new AbilityButton.AbilityVisual { abilityId = "soundtrap", icon = S("soundtrap"), color = Hex("#46E6B4") },
            new AbilityButton.AbilityVisual { abilityId = "fire", icon = S("flame"), color = Hex("#FF6A1E") },
            new AbilityButton.AbilityVisual { abilityId = "electric", icon = S("bolt"), color = Hex("#7FD8FF") },
            new AbilityButton.AbilityVisual { abilityId = "missile", icon = S("missile"), color = Hex("#FF8A2A") },
        };
    }

    static void RestyleUpgradeButton(RectTransform hud, GameObject buttonGo, HUDController ctl)
    {
        var t = (RectTransform)buttonGo.transform;
        t.name = "Upgrade Button";
        t.SetParent(hud, false);
        Clear(t);
        foreach (var fx in t.GetComponents<UIButtonFX>()) Object.DestroyImmediate(fx);
        foreach (var p in t.GetComponents<UIPulse>()) Object.DestroyImmediate(p);
        Place(t, new Vector2(1, 0), new Vector2(-300, 70), new Vector2(290, 112), new Vector2(1, 0));
        var img = t.GetComponent<Image>(); img.sprite = S("ui_button"); img.type = Image.Type.Sliced; img.color = Color.white;
        var btn = t.GetComponent<Button>(); btn.targetGraphic = img; btn.transition = Selectable.Transition.ColorTint;
        var cb = btn.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.selectedColor = Color.white; btn.colors = cb;
        var nav = btn.navigation; nav.mode = Navigation.Mode.None; btn.navigation = nav;

        var glow = Img("Glow", t, "ui_frame_glow", Amber, true); Stretch(glow.rectTransform, -18, -18, -18, -18);
        glow.transform.SetAsFirstSibling();
        var plate = Img("IconPlate", t, "ui_button_fill", Hazard, true);
        Place(plate.rectTransform, new Vector2(0, 0.5f), new Vector2(12, 0), new Vector2(92, 92), new Vector2(0, 0.5f));
        var icon = Img("Icon", plate.transform, "upgrade", TextDark); Stretch(icon.rectTransform, 14, 14, 14, 14);
        var l1 = Text("Label", t, "UPGRADE", 27, TextMain);
        Place(l1.rectTransform, new Vector2(0, 0.5f), new Vector2(116, 10), new Vector2(162, 40), new Vector2(0, 0.5f));
        l1.enableAutoSizing = true; l1.fontSizeMin = 18; l1.fontSizeMax = 27;
        var l2 = Text("Sub", t, "WORKSHOP", 15, Amber);
        Place(l2.rectTransform, new Vector2(0, 0.5f), new Vector2(120, -22), new Vector2(170, 22), new Vector2(0, 0.5f));
        var stripe = HazardStrip("Hazard", t, 7);
        stripe.rectTransform.anchorMin = new Vector2(0, 0); stripe.rectTransform.anchorMax = new Vector2(1, 0);
        stripe.rectTransform.pivot = new Vector2(0.5f, 0); stripe.rectTransform.offsetMin = new Vector2(116, 12); stripe.rectTransform.offsetMax = new Vector2(-20, 19);
        var badge = Img("Badge", t, "ui_circle", Danger);
        Place(badge.rectTransform, new Vector2(1, 1), new Vector2(10, 10), new Vector2(34, 34), new Vector2(0.5f, 0.5f));
        var bt = Text("Mark", badge.transform, "!", 22, Color.white, TextAlignmentOptions.Center); Stretch(bt.rectTransform);

        t.gameObject.AddComponent<UIButtonFX>();
        var pulse = t.gameObject.AddComponent<UIPulse>();
        pulse.glow = glow; pulse.minAlpha = 0.15f; pulse.maxAlpha = 0.75f; pulse.speed = 2.6f;
        ctl.upgradePulse = pulse; ctl.upgradeBadge = badge.gameObject;
    }

    // ------------------------------------------------------------------ overlays

    static void RestyleWarning(Transform croot, GameObject warning)
    {
        var t = (RectTransform)warning.transform;
        t.name = "Warning ";
        Stretch(t);
        Clear(t);
        var img = t.GetComponent<Image>(); img.sprite = null; img.color = new Color(0, 0, 0, 0); img.raycastTarget = false;
        var cg = t.GetComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.interactable = false;
        Color red = A(Danger, 0.55f);
        var top = Img("EdgeTop", t, "ui_grad_v", red); top.rectTransform.anchorMin = new Vector2(0, 1); top.rectTransform.anchorMax = new Vector2(1, 1); top.rectTransform.pivot = new Vector2(0.5f, 1); top.rectTransform.sizeDelta = new Vector2(0, 220); top.rectTransform.localScale = new Vector3(1, -1, 1); top.rectTransform.anchoredPosition = new Vector2(0, -220);
        var bot = Img("EdgeBottom", t, "ui_grad_v", red); bot.rectTransform.anchorMin = new Vector2(0, 0); bot.rectTransform.anchorMax = new Vector2(1, 0); bot.rectTransform.pivot = new Vector2(0.5f, 0); bot.rectTransform.sizeDelta = new Vector2(0, 220);
        var lft = Img("EdgeLeft", t, "ui_grad_h", red); lft.rectTransform.anchorMin = new Vector2(0, 0); lft.rectTransform.anchorMax = new Vector2(0, 1); lft.rectTransform.pivot = new Vector2(0, 0.5f); lft.rectTransform.sizeDelta = new Vector2(260, 0);
        var rgt = Img("EdgeRight", t, "ui_grad_h", red); rgt.rectTransform.anchorMin = new Vector2(1, 0); rgt.rectTransform.anchorMax = new Vector2(1, 1); rgt.rectTransform.pivot = new Vector2(1, 0.5f); rgt.rectTransform.sizeDelta = new Vector2(260, 0); rgt.rectTransform.localScale = new Vector3(-1, 1, 1); rgt.rectTransform.anchoredPosition = new Vector2(-260, 0);

        var banner = Img("Banner", t, "ui_panel", Color.white, true).rectTransform;
        Place(banner, new Vector2(0.5f, 0), new Vector2(0, 300), new Vector2(820, 112), new Vector2(0.5f, 0));
        var stripe = HazardStrip("Hazard", banner, 10); stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.pivot = new Vector2(0.5f, 1); stripe.rectTransform.offsetMin = new Vector2(24, -18); stripe.rectTransform.offsetMax = new Vector2(-24, -8);
        var icon = Img("Icon", banner, "warning", Danger); Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(30, -6), new Vector2(64, 64), new Vector2(0, 0.5f));
        var t1 = Text("Text (TMP)", banner, "SOMEONE HEARD YOU RUN!", 36, Danger); Place(t1.rectTransform, new Vector2(0, 0.5f), new Vector2(114, 8), new Vector2(680, 44), new Vector2(0, 0.5f));
        var t2 = Text("Sub", banner, "THE WORM IS HUNTING  -  STOP DRILLING", 17, TextMain); Place(t2.rectTransform, new Vector2(0, 0.5f), new Vector2(116, -28), new Vector2(680, 24), new Vector2(0, 0.5f));
    }

    static void RestyleGameOver(Transform croot, GameOverButtons go)
    {
        var t = (RectTransform)go.transform;
        Stretch(t);
        var img = t.GetComponent<Image>(); img.sprite = null; img.color = new Color(0.05f, 0.008f, 0.008f, 0.94f); img.raycastTarget = true;
        var repair = go.GetComponentsInChildren<Button>(true).FirstOrDefault(b => HasListener(b, go, "RepairTrain"));
        var abandon = go.GetComponentsInChildren<Button>(true).FirstOrDefault(b => HasListener(b, go, "AbandonRun"));
        if (repair) repair.transform.SetParent(croot, false);
        if (abandon) abandon.transform.SetParent(croot, false);
        Clear(t);
        GetOrAdd<CanvasGroup>(t.gameObject);

        var window = Panel("Window", t, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 560));
        var ui = GetOrAdd<UIPanel>(t.gameObject); ui.window = window;
        var stripe = HazardStrip("Hazard", window, 14); stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.pivot = new Vector2(0.5f, 1); stripe.rectTransform.offsetMin = new Vector2(30, -40); stripe.rectTransform.offsetMax = new Vector2(-30, -26);
        var icon = Img("Icon", window, "warning", Danger); Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -64), new Vector2(84, 84), new Vector2(0.5f, 1));
        var title = Text("Title", window, "TRAIN DESTROYED", 52, Danger, TextAlignmentOptions.Center); Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(760, 64), new Vector2(0.5f, 1));
        var sub = Text("Sub", window, "REPAIR AT THE SAFE ZONE AND LOSE THE ORBS IN THE TRAIN,\nOR ABANDON THE RUN AND START OVER.", 18, TextDim, TextAlignmentOptions.Center); Place(sub.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -236), new Vector2(760, 60), new Vector2(0.5f, 1));
        sub.lineSpacing = 18;

        if (repair) RebuildButton(repair, window, "REPAIR TRAIN", "repair", Hazard, true, new Vector2(-190, 56));
        if (abandon) RebuildButton(abandon, window, "ABANDON RUN", "exit", Danger, false, new Vector2(190, 56));
        t.gameObject.SetActive(false);
    }

    static void RebuildButton(Button b, RectTransform parent, string label, string icon, Color accent, bool filled, Vector2 pos)
    {
        var t = (RectTransform)b.transform;
        t.SetParent(parent, false);
        t.name = label;
        Clear(t);
        foreach (var fx in t.GetComponents<UIButtonFX>()) Object.DestroyImmediate(fx);
        Place(t, new Vector2(0.5f, 0), pos, new Vector2(350, 100), new Vector2(0.5f, 0));
        var img = t.GetComponent<Image>(); img.sprite = S(filled ? "ui_button_fill" : "ui_button"); img.type = Image.Type.Sliced; img.color = filled ? accent : Color.white;
        b.targetGraphic = img;
        var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.selectedColor = Color.white; b.colors = cb;
        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        var glow = Img("HoverGlow", t, "ui_frame_glow", accent, true); Stretch(glow.rectTransform, -16, -16, -16, -16);
        if (!filled)
        {
            var strip = Img("Accent", t, "ui_plate", accent, true);
            strip.rectTransform.anchorMin = new Vector2(0, 0); strip.rectTransform.anchorMax = new Vector2(0, 1); strip.rectTransform.pivot = new Vector2(0, 0.5f);
            strip.rectTransform.offsetMin = new Vector2(7, 14); strip.rectTransform.offsetMax = new Vector2(13, -14);
        }
        var ic = Img("Icon", t, icon, filled ? TextDark : accent); Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(42, 42), new Vector2(0, 0.5f));
        var tx = Text("Label", t, label, 24, filled ? TextDark : TextMain); Stretch(tx.rectTransform, 80, 0, 14, 0);
        tx.enableAutoSizing = true; tx.fontSizeMin = 16; tx.fontSizeMax = 24;
        t.gameObject.AddComponent<UIButtonFX>().hoverGlow = glow;
    }

    static void RestylePausePanel(Transform croot, PauseManager pause)
    {
        var t = (RectTransform)pause.pausePanel.transform;
        t.name = "pause";
        Stretch(t);
        Clear(t);
        var img = t.GetComponent<Image>(); img.sprite = null; img.color = Overlay; img.raycastTarget = true;
        GetOrAdd<CanvasGroup>(t.gameObject);
        var window = Panel("Window", t, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 720));
        var ui = GetOrAdd<UIPanel>(t.gameObject); ui.window = window;

        var icon = Img("Icon", window, "pause", Hazard); Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -42), new Vector2(56, 56), new Vector2(0.5f, 1));
        var title = Text("Title", window, "PAUSED", 54, TextMain, TextAlignmentOptions.Center); Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(560, 64), new Vector2(0.5f, 1));
        var stripe = HazardStrip("Hazard", window, 10); stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.pivot = new Vector2(0.5f, 1); stripe.rectTransform.offsetMin = new Vector2(60, -190); stripe.rectTransform.offsetMax = new Vector2(-60, -180);

        float y = -224;
        AddMenuButton(window, "RESUME", "play", Hazard, true, ref y, pause.ResumeGame);
        AddMenuButton(window, "RESTART", "restart", Steel, false, ref y, pause.RestartLevel);
        AddMenuButton(window, "SETTINGS", "gear", Steel, false, ref y, pause.OpenSettings);
        AddMenuButton(window, "MAIN MENU", "home", Steel, false, ref y, pause.MainMenu);
        t.gameObject.SetActive(false);
    }

    static void AddMenuButton(RectTransform window, string label, string icon, Color accent, bool filled, ref float y, UnityEngine.Events.UnityAction action)
    {
        var b = Button(label, window, label, icon, accent, filled, 30);
        Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(460, 100), new Vector2(0.5f, 1));
        UnityEventTools.AddPersistentListener(b.onClick, action);
        y -= 116;
    }

    static void BuildToast(Transform croot)
    {
        var root = Rect("Toast", croot); Stretch(root);
        var toast = root.gameObject.AddComponent<HUDToast>();
        var banner = Rect("Banner", root);
        Place(banner, new Vector2(0.5f, 1), new Vector2(0, -186), new Vector2(780, 104), new Vector2(0.5f, 1));
        var group = banner.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
        var burst = Img("ExplosiveFx", banner, "ui_burst", Ember); Place(burst.rectTransform, new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(260, 260), new Vector2(0.5f, 0.5f));
        var glow = Img("Glow", banner, "ui_frame_glow", Amber, true); Stretch(glow.rectTransform, -16, -16, -16, -16);
        var bg = Img("Bg", banner, "ui_panel", Color.white, true); Stretch(bg.rectTransform);
        var arcs = Img("ElectricFx", banner, "ui_arcs", Electric); Stretch(arcs.rectTransform, 10, 6, 10, 6);
        var strip = Img("Accent", banner, "ui_plate", Amber, true);
        strip.rectTransform.anchorMin = new Vector2(0, 0); strip.rectTransform.anchorMax = new Vector2(0, 1); strip.rectTransform.pivot = new Vector2(0, 0.5f);
        strip.rectTransform.offsetMin = new Vector2(8, 14); strip.rectTransform.offsetMax = new Vector2(16, -14);
        var icon = Img("Icon", banner, "star", Amber); Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(66, 0), new Vector2(60, 60), new Vector2(0.5f, 0.5f));
        var title = Text("Title", banner, "LEVEL 2", 32, TextMain); Place(title.rectTransform, new Vector2(0, 0.5f), new Vector2(118, 14), new Vector2(640, 40), new Vector2(0, 0.5f));
        title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 32;
        var sub = Text("Subtitle", banner, "NEW UPGRADES AVAILABLE", 18, TextDim); Place(sub.rectTransform, new Vector2(0, 0.5f), new Vector2(120, -22), new Vector2(640, 26), new Vector2(0, 0.5f));
        toast.banner = banner; toast.group = group; toast.accentStrip = strip; toast.accentGlow = glow; toast.icon = icon; toast.title = title; toast.subtitle = sub; toast.electricFx = arcs; toast.explosiveFx = burst;
    }
}
