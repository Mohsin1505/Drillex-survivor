using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using static DrillexUIKit;

// Main menu: a live 3D stage (the real drill train on desert-kit terrain, sunset light, fog,
// drifting dust) behind a restyled menu. Existing Play / Help buttons keep their events
// (SceneLoader.StartGame, HelpPanelManager.OpenHelp/CloseHelp); Settings and Exit are added.
public static class DrillexMenuBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string Kit = "Assets/Art/Environment/DesertKit/Prefabs/";

    [MenuItem("Drillex/UI/Rebuild Main Menu")]
    public static void Build()
    {
        // read the gameplay level's sun and post-processing so the menu matches it
        var lev = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude).FirstOrDefault(l => l.type == LightType.Directional);
        Color sunColor = sun ? sun.color : new Color(1f, 0.75f, 0.55f);
        float sunIntensity = sun ? sun.intensity : 1.6f;
        Quaternion sunRot = sun ? sun.transform.rotation : Quaternion.Euler(18, 230, 0);
        var vol = Object.FindAnyObjectByType<Volume>(FindObjectsInactive.Include);
        VolumeProfile profile = vol ? vol.sharedProfile : null;
        var skybox = RenderSettings.skybox;
        var fogColor = RenderSettings.fogColor;
        var amb = (RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor);

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // ---------------- environment ----------------
        RenderSettings.skybox = skybox;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogStartDistance = 25f;
        RenderSettings.fogEndDistance = 260f;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = amb.Item1; RenderSettings.ambientEquatorColor = amb.Item2; RenderSettings.ambientGroundColor = amb.Item3;

        var old = GameObject.Find("MenuStage"); if (old) Object.DestroyImmediate(old);
        var stage = new GameObject("MenuStage").transform;

        var lightGo = Object.FindObjectsByType<Light>(FindObjectsInactive.Include).FirstOrDefault(l => l.type == LightType.Directional)?.gameObject;
        if (lightGo == null) lightGo = new GameObject("Directional Light", typeof(Light));
        var light = lightGo.GetComponent<Light>();
        light.type = LightType.Directional; light.color = sunColor; light.intensity = sunIntensity; light.shadows = LightShadows.Soft;
        light.transform.rotation = sunRot;
        RenderSettings.sun = light;

        if (profile != null)
        {
            var v = Object.FindAnyObjectByType<Volume>(FindObjectsInactive.Include);
            if (v == null) v = new GameObject("Global Volume").AddComponent<Volume>();
            v.isGlobal = true; v.sharedProfile = profile;
        }

        // ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Sand"; ground.transform.SetParent(stage); ground.transform.localScale = new Vector3(40, 1, 40);
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        ground.GetComponent<MeshRenderer>().sharedMaterial = SandMaterial();

        // backdrop formations (far side of the train, as seen from the camera)
        Prop("ROCK_FORMATIONS/NATURAL_ARCH_01", stage, new Vector3(-26, 0, 48), 200, 1.6f);
        Prop("CLIFFS/CLIFF_TOWER_01", stage, new Vector3(18, 0, 60), 30, 1.8f);
        Prop("CLIFFS/CLIFF_TOWER_02", stage, new Vector3(42, 0, 44), 110, 1.5f);
        Prop("ROCK_FORMATIONS/ROCK_PILLAR_01", stage, new Vector3(-4, 0, 34), 10, 1.4f);
        Prop("ROCK_FORMATIONS/ROCK_FORMATION_01", stage, new Vector3(-48, 0, 26), 80, 1.6f);
        Prop("CLIFFS/CLIFF_STRAIGHT_01", stage, new Vector3(-70, 0, 70), 15, 2f);
        Prop("CLIFFS/CLIFF_STRAIGHT_02", stage, new Vector3(70, 0, 80), -20, 2f);
        // mid / foreground dressing
        Prop("BOULDERS/BOULDER_02", stage, new Vector3(8, 0, 9), 40, 1f);
        Prop("BOULDERS/BOULDER_04", stage, new Vector3(-9, 0, 8), 120, 1.1f);
        Prop("ROCK_VARIATIONS/ROCK_S_03", stage, new Vector3(5, 0, -6), 10, 1f);
        Prop("ROCK_VARIATIONS/ROCK_S_07", stage, new Vector3(-6, 0, -7), 70, 1f);
        Prop("VEGETATION/CACTUS_TALL_01", stage, new Vector3(11, 0, 14), 0, 1f);
        Prop("VEGETATION/CACTUS_ARM_01", stage, new Vector3(-14, 0, 16), 60, 1f);
        Prop("VEGETATION/DRY_BUSH_01", stage, new Vector3(3, 0, 6), 0, 1f);
        Prop("VEGETATION/DESERT_SHRUB_02", stage, new Vector3(-4, 0, 5), 0, 1f);
        Prop("PROPS/BONE_RIB_02", stage, new Vector3(7, 0, -3), 30, 1f);
        Prop("PROPS/SIGN_POST_01", stage, new Vector3(-7.5f, 0, 2.5f), 160, 1f);
        Prop("PROPS/BARREL_01", stage, new Vector3(-5.6f, 0, 2.8f), 0, 1f);
        Prop("PROPS/CRATE_01", stage, new Vector3(-6.4f, 0, 3.6f), 25, 1f);

        // ---------------- the train ----------------
        var train = new GameObject("Train").transform; train.SetParent(stage);
        float y = 0.5f;
        var head = Visual("Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Visual.prefab", train, new Vector3(4.4f, y, 0));
        Visual("Assets/Prefeb/Tesla Cannon.prefab", train, new Vector3(1.6f, y, 0));
        Visual("Assets/Prefeb/rocket launcher.prefab", train, new Vector3(-1.0f, y, 0));
        Visual("Assets/Prefeb/mini machine gun.prefab", train, new Vector3(-3.6f, y, 0));
        Visual("Assets/Prefeb/Storage.prefab", train, new Vector3(-6.2f, y, 0));
        train.rotation = Quaternion.Euler(0, 90, 0);
        // ground the train: lift so the lowest renderer sits on the sand
        float minY = float.MaxValue;
        foreach (var r in train.GetComponentsInChildren<Renderer>()) minY = Mathf.Min(minY, r.bounds.min.y);
        if (minY < float.MaxValue) train.position += Vector3.up * (0.02f - minY);
        var focus = new GameObject("CameraFocus").transform; focus.SetParent(stage); focus.position = new Vector3(0, 0.6f, -0.5f);

        // dust drifting through the shot
        var dustMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Environment/DesertLevel/Materials/M_FX_DustMote.mat");
        var dust = new GameObject("DustMotes").AddComponent<ParticleSystem>(); dust.transform.SetParent(stage); dust.transform.position = new Vector3(0, 1.5f, -2);
        var dm = dust.main; dm.loop = true; dm.startLifetime = 9f; dm.startSpeed = 0.2f; dm.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f); dm.maxParticles = 60; dm.startColor = new Color(1f, 0.85f, 0.65f, 0.6f); dm.prewarm = true; dm.simulationSpace = ParticleSystemSimulationSpace.World;
        var de = dust.emission; de.rateOverTime = 6;
        var ds = dust.shape; ds.shapeType = ParticleSystemShapeType.Box; ds.scale = new Vector3(24, 4, 12);
        var dv = dust.velocityOverLifetime; dv.enabled = true; dv.space = ParticleSystemSimulationSpace.World; dv.x = new ParticleSystem.MinMaxCurve(0.6f, 1.2f); dv.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.1f); dv.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        var dc = dust.colorOverLifetime; dc.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.3f), new GradientAlphaKey(1, 0.7f), new GradientAlphaKey(0, 1) }); dc.color = g;
        var dr = dust.GetComponent<ParticleSystemRenderer>(); dr.sharedMaterial = dustMat != null ? dustMat : AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/M_VFX_Dot_Add.mat"); dr.shadowCastingMode = ShadowCastingMode.Off;
        var haze = new GameObject("BlowingSand").AddComponent<ParticleSystem>(); haze.transform.SetParent(stage); haze.transform.position = new Vector3(-14, 0.4f, 2);
        var hm = haze.main; hm.loop = true; hm.startLifetime = 7f; hm.startSpeed = 0f; hm.startSize = new ParticleSystem.MinMaxCurve(2.5f, 5f); hm.maxParticles = 20; hm.startColor = new Color(0.86f, 0.6f, 0.42f, 0.16f); hm.prewarm = true; hm.simulationSpace = ParticleSystemSimulationSpace.World; hm.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
        var he = haze.emission; he.rateOverTime = 2.5f;
        var hs = haze.shape; hs.shapeType = ParticleSystemShapeType.Box; hs.scale = new Vector3(4, 0.5f, 18);
        var hv = haze.velocityOverLifetime; hv.enabled = true; hv.space = ParticleSystemSimulationSpace.World; hv.x = new ParticleSystem.MinMaxCurve(3f, 5f); hv.y = new ParticleSystem.MinMaxCurve(0f, 0f); hv.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var hc = haze.colorOverLifetime; hc.enabled = true; hc.color = g;
        var hr = haze.GetComponent<ParticleSystemRenderer>(); hr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/M_VFX_Smoke_Alpha.mat"); hr.shadowCastingMode = ShadowCastingMode.Off;

        // camera
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null) { cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>(); cam.tag = "MainCamera"; }
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.fieldOfView = 38f; cam.nearClipPlane = 0.2f; cam.farClipPlane = 600f;
        var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        if (camData != null) camData.renderPostProcessing = true;
        var rig = GetOrAdd<MenuCameraRig>(cam.gameObject);
        rig.focus = focus; rig.radius = 12.5f; rig.height = 2.6f; rig.startAngle = 196f; rig.swing = 14f; rig.period = 46f; rig.lookOffset = new Vector3(0, 0.4f, 0); rig.framingYaw = -13f;
        rig.Snap();

        BuildUI();

        // The crew/customise screen (owned by the character-creation work) re-installs itself
        // after every menu rebuild, if that editor code is present.
        var crew = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DrillexCrewMenu")).FirstOrDefault(t => t != null);
        crew?.GetMethod("Install", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Drillex main menu rebuilt");
    }

    // Conthrax with a soft dark underlay, used only by the logo so it reads against the bright sky.
    static Material LogoMaterial()
    {
        const string path = "Assets/UI/Fonts/Conthrax_Logo_Underlay.mat";
        System.IO.Directory.CreateDirectory("Assets/UI/Fonts");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(DrillexUIKit.Font.material); AssetDatabase.CreateAsset(m, path); }
        m.CopyPropertiesFromMaterial(DrillexUIKit.Font.material);
        m.EnableKeyword("UNDERLAY_ON");
        m.SetColor("_UnderlayColor", new Color(0.08f, 0.03f, 0.01f, 0.75f));
        m.SetFloat("_UnderlayOffsetX", 0.5f);
        m.SetFloat("_UnderlayOffsetY", -0.6f);
        m.SetFloat("_UnderlaySoftness", 0.45f);
        m.SetFloat("_UnderlayDilate", 0.2f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SandMaterial()
    {
        const string path = "Assets/Art/Environment/DesertLevel/Materials/M_Menu_Sand.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/DesertLevel/Textures/T_Desert_Sand.png"));
        m.SetTextureScale("_BaseMap", new Vector2(60, 60));
        m.SetColor("_BaseColor", new Color(1f, 0.93f, 0.88f));
        m.SetFloat("_Smoothness", 0.08f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Prop(string prefab, Transform parent, Vector3 pos, float yaw, float scale)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + prefab + ".prefab");
        if (p == null) { Debug.LogWarning("menu: missing " + prefab); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
        go.isStatic = true;
    }

    // Instantiates a car / head prefab as pure visuals: gameplay scripts, physics and colliders removed.
    static GameObject Visual(string path, Transform parent, Vector3 localPos)
    {
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (p == null) { Debug.LogWarning("menu: missing " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        foreach (var c in go.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(mb is LightningBurst)) Object.DestroyImmediate(mb);
        foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(rb);
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) if (ps.name.StartsWith("vfx_")) ps.gameObject.SetActive(false);
        go.transform.localPosition = localPos; go.transform.localRotation = Quaternion.Euler(0, 0, 0);
        return go;
    }

    // ---------------------------------------------------------------- UI

    static void BuildUI()
    {
        var loader = Object.FindAnyObjectByType<SceneLoader>(FindObjectsInactive.Include);
        var help = Object.FindAnyObjectByType<HelpPanelManager>(FindObjectsInactive.Include);
        var canvas = help.helpPanel.GetComponentInParent<Canvas>(true).rootCanvas;
        var croot = canvas.transform;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight = 1f;

        var buttons = croot.GetComponentsInChildren<Button>(true);
        var play = buttons.FirstOrDefault(b => Has(b, loader, "StartGame"));
        var helpBtn = buttons.FirstOrDefault(b => Has(b, help, "OpenHelp"));
        var back = buttons.FirstOrDefault(b => Has(b, help, "CloseHelp"));
        foreach (var b in new[] { play, helpBtn, back }) if (b) b.transform.SetParent(croot, false);
        help.helpPanel.transform.SetParent(croot, false);

        foreach (var n in new[] { "Panel", "MenuRoot", "SettingsPanel", "Settings", "Exit" })
        {
            var t = croot.Find(n);
            if (t != null && t != help.helpPanel.transform && (play == null || t != play.transform) && (helpBtn == null || t != helpBtn.transform) && (back == null || t != back.transform)) Object.DestroyImmediate(t.gameObject);
        }

        var root = Rect("MenuRoot", croot); Stretch(root); root.SetAsFirstSibling();
        var safe = Rect("Safe", root); Stretch(safe); safe.gameObject.AddComponent<SafeAreaFitter>();
        var ctl = root.gameObject.AddComponent<MainMenuController>();

        // left-side darkening so the menu reads over the bright desert
        var shade = Img("Shade", root, "ui_grad_h", new Color(0.03f, 0.025f, 0.02f, 0.92f));
        shade.rectTransform.anchorMin = new Vector2(0, 0); shade.rectTransform.anchorMax = new Vector2(0, 1); shade.rectTransform.pivot = new Vector2(0, 0.5f); shade.rectTransform.sizeDelta = new Vector2(1350, 0);
        shade.transform.SetAsFirstSibling();
        var bottom = Img("ShadeBottom", root, "ui_grad_v", new Color(0.03f, 0.025f, 0.02f, 0.7f));
        bottom.rectTransform.anchorMin = new Vector2(0, 0); bottom.rectTransform.anchorMax = new Vector2(1, 0); bottom.rectTransform.pivot = new Vector2(0.5f, 0); bottom.rectTransform.sizeDelta = new Vector2(0, 220);

        // title block
        var title = Rect("Title", safe);
        Place(title, new Vector2(0, 1), new Vector2(110, -120), new Vector2(900, 260), new Vector2(0, 1));
        var tg = title.gameObject.AddComponent<CanvasGroup>();
        var word = Text("Logo", title, "DRILLEX", 150, TextMain);
        Place(word.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(900, 160), new Vector2(0, 1));
        word.characterSpacing = 6; word.enableVertexGradient = true;
        word.fontSharedMaterial = LogoMaterial();
        word.colorGradient = new VertexGradient(Hex("#FFF1D0"), Hex("#FFF1D0"), Hex("#FFB13A"), Hex("#FF7A1E"));
        var stripe = HazardStrip("Hazard", title, 14);
        Place(stripe.rectTransform, new Vector2(0, 1), new Vector2(6, -168), new Vector2(560, 14), new Vector2(0, 1));
        var tag = Text("Tagline", title, "DRILL.  HAUL.  SURVIVE THE DESERT.", 24, Amber);
        Place(tag.rectTransform, new Vector2(0, 1), new Vector2(8, -198), new Vector2(800, 34), new Vector2(0, 1));

        // buttons
        float y = -470;
        var list = ctl.buttons;
        list.Clear();
        if (play) list.Add(Restyle(play, safe, "PLAY", "play", Hazard, true, ref y, 118, 46));
        var settingsBtn = Button("Settings", safe, "SETTINGS", "gear", Steel, false, 32);
        list.Add(PlaceMenu((RectTransform)settingsBtn.transform, ref y, 96));
        UnityEventTools.AddPersistentListener(settingsBtn.onClick, ctl.OpenSettings);
        if (helpBtn) list.Add(Restyle(helpBtn, safe, "HOW TO PLAY", "star", Steel, false, ref y, 96, 32));
        var exit = Button("Exit", safe, "EXIT", "exit", Danger, false, 32);
        list.Add(PlaceMenu((RectTransform)exit.transform, ref y, 96));
        UnityEventTools.AddPersistentListener(exit.onClick, loader.QuitGame);
        ctl.exitButton = exit.gameObject;
        ctl.title = title; ctl.titleGroup = tg;

        var footer = Rect("Footer", safe);
        Place(footer, new Vector2(0, 0), new Vector2(110, 36), new Vector2(900, 30), new Vector2(0, 0));
        var fg = footer.gameObject.AddComponent<CanvasGroup>();
        var ft = Text("Text", footer, "DRAG THE STEER PAD OR USE  A / D  TO STEER", 16, TextDim);
        Stretch(ft.rectTransform);
        ctl.footer = fg;

        // help panel
        RestyleHelp(help.helpPanel, back);

        // settings
        var settingsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSettingsBuilder.PrefabPath);
        var settings = (GameObject)PrefabUtility.InstantiatePrefab(settingsPrefab, croot);
        settings.name = "SettingsPanel"; settings.SetActive(false);
        ctl.settingsPanel = settings.GetComponent<SettingsPanel>();
        settings.transform.SetAsLastSibling();
    }

    static bool Has(Button b, Object target, string method)
    {
        if (target == null) return false;
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
            if (b.onClick.GetPersistentTarget(i) == target || (target is Component c && b.onClick.GetPersistentTarget(i) == c.gameObject))
                if (b.onClick.GetPersistentMethodName(i) == method) return true;
        return false;
    }

    static RectTransform PlaceMenu(RectTransform t, ref float y, float h)
    {
        Place(t, new Vector2(0, 1), new Vector2(110, y), new Vector2(h > 100 ? 470 : 420, h), new Vector2(0, 1));
        y -= h + 18;
        return t;
    }

    static RectTransform Restyle(Button b, RectTransform parent, string label, string icon, Color accent, bool filled, ref float y, float h, float font)
    {
        var t = (RectTransform)b.transform;
        t.SetParent(parent, false);
        t.name = label;
        Clear(t);
        foreach (var fx in t.GetComponents<UIButtonFX>()) Object.DestroyImmediate(fx);
        var img = t.GetComponent<Image>(); img.sprite = S(filled ? "ui_button_fill" : "ui_button"); img.type = Image.Type.Sliced; img.color = filled ? accent : Color.white;
        b.targetGraphic = img; b.transition = Selectable.Transition.ColorTint;
        var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = Color.white; cb.pressedColor = new Color(0.85f, 0.85f, 0.85f); cb.selectedColor = Color.white; b.colors = cb;
        var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
        var glow = Img("HoverGlow", t, "ui_frame_glow", accent, true); Stretch(glow.rectTransform, -16, -16, -16, -16);
        if (!filled)
        {
            var strip = Img("Accent", t, "ui_plate", accent, true);
            strip.rectTransform.anchorMin = new Vector2(0, 0); strip.rectTransform.anchorMax = new Vector2(0, 1); strip.rectTransform.pivot = new Vector2(0, 0.5f);
            strip.rectTransform.offsetMin = new Vector2(7, 14); strip.rectTransform.offsetMax = new Vector2(13, -14);
        }
        var ic = Img("Icon", t, icon, filled ? TextDark : accent);
        Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(30, 0), new Vector2(font * 1.2f, font * 1.2f), new Vector2(0, 0.5f));
        var tx = Text("Label", t, label, font, filled ? TextDark : TextMain); Stretch(tx.rectTransform, 30 + font * 1.2f + 18, 0, 12, 0);
        t.gameObject.AddComponent<UIButtonFX>().hoverGlow = glow;
        return PlaceMenu(t, ref y, h);
    }

    static void RestyleHelp(GameObject panel, Button back)
    {
        var t = (RectTransform)panel.transform;
        Stretch(t);
        var keepImage = t.Find("Image");
        Sprite helpSprite = keepImage ? keepImage.GetComponent<Image>().sprite : null;
        if (back) back.transform.SetParent(t.parent, false);
        Clear(t);
        var img = t.GetComponent<Image>(); img.sprite = null; img.color = Overlay; img.raycastTarget = true;
        GetOrAdd<CanvasGroup>(panel);
        var window = Img("Window", t, "ui_panel", Color.white, true, true).rectTransform;
        Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500, 920));
        GetOrAdd<UIPanel>(panel).window = window;
        var title = Text("Title", window, "HOW TO PLAY", 44, TextMain); Place(title.rectTransform, new Vector2(0, 1), new Vector2(44, -30), new Vector2(600, 56), new Vector2(0, 1));
        var stripe = HazardStrip("Hazard", window, 10);
        stripe.rectTransform.anchorMin = new Vector2(0, 1); stripe.rectTransform.anchorMax = new Vector2(1, 1); stripe.rectTransform.pivot = new Vector2(0.5f, 1);
        stripe.rectTransform.offsetMin = new Vector2(40, -104); stripe.rectTransform.offsetMax = new Vector2(-40, -94);
        var pic = Img("Image", window, null, Color.white); pic.sprite = helpSprite; pic.preserveAspect = true;
        Stretch(pic.rectTransform, 44, 150, 44, 124);
        if (back)
        {
            var bt = (RectTransform)back.transform; bt.SetParent(window, false);
            float y = 0;
            Restyle(back, window, "BACK", "close", Hazard, true, ref y, 92, 30);
            Place(bt, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(320, 92), new Vector2(0.5f, 0));
        }
        panel.SetActive(false);
    }
}
