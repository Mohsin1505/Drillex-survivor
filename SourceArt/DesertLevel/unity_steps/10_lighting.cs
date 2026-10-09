using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Lev1 desert sunset look: low warm sun, gradient ambient, sunset procedural sky, warm linear fog,
// colour grading volume, longer camera far clip for the distant canyon. One realtime light (the sun).
internal class CommandScript : IRunCommand
{
    const string Dir = "Assets/Art/Environment/DesertLevel";

    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Art/Environment", "DesertLevel");

        // --- sun: low, from the far-left of the canyon, so camera-facing sides catch golden light + long shadows
        var sunGo = GameObject.Find("Directional Light");
        sunGo.name = "Sun_DesertSunset";
        var sun = sunGo.GetComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.74f, 0.48f);
        sun.intensity = 2.1f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.82f;
        sun.shadowBias = 0.05f;
        sun.shadowNormalBias = 0.4f;
        sunGo.transform.rotation = Quaternion.Euler(24f, 150f, 0f);
        RenderSettings.sun = sun;

        // --- ambient: warm gradient (bluish sky, sand bounce) so shadows are coloured, never black
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.50f, 0.52f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.78f, 0.55f, 0.40f);
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.28f, 0.19f);
        RenderSettings.ambientIntensity = 1f;

        // --- sky
        string skyPath = Dir + "/SKY_DesertSunset.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
        sky.SetFloat("_SunDisk", 2f);
        sky.SetFloat("_SunSize", 0.06f);
        sky.SetFloat("_SunSizeConvergence", 4f);
        sky.SetFloat("_AtmosphereThickness", 1.9f);
        sky.SetColor("_SkyTint", new Color(0.92f, 0.62f, 0.50f));
        sky.SetColor("_GroundColor", new Color(0.62f, 0.42f, 0.30f));
        sky.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        // --- fog: warm dust haze, linear (cheapest on mobile); near stays crisp, canyon walls soften, far fades
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.93f, 0.67f, 0.47f);
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 820f;

        // --- camera: see the distant canyon (fog hides the clip)
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        cam.farClipPlane = 900f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var cd = cam.GetComponent<UniversalAdditionalCameraData>();
        cd.renderPostProcessing = true;

        // --- grading volume (own profile, the shared sample profile is left untouched)
        string vpPath = Dir + "/VP_DesertSunset.asset";
        var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(vpPath);
        if (vp == null) { vp = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(vp, vpPath); }
        T Get<T>() where T : VolumeComponent { if (!vp.TryGet<T>(out var c)) { c = vp.Add<T>(true); AssetDatabase.AddObjectToAsset(c, vp); } c.active = true; return c; }
        var tm = Get<Tonemapping>(); tm.mode.Override(TonemappingMode.ACES);
        var ca = Get<ColorAdjustments>();
        ca.postExposure.Override(0.25f); ca.contrast.Override(14f); ca.saturation.Override(12f); ca.colorFilter.Override(new Color(1f, 0.97f, 0.93f));
        var wb = Get<WhiteBalance>(); wb.temperature.Override(10f); wb.tint.Override(2f);
        var smh = Get<ShadowsMidtonesHighlights>();
        smh.shadows.Override(new Vector4(0.92f, 0.94f, 1.08f, 0f));      // cool violet shadows
        smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.92f, 0f));    // golden highlights
        var bloom = Get<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.6f); bloom.tint.Override(new Color(1f, 0.8f, 0.6f));
        var vig = Get<Vignette>(); vig.intensity.Override(0.22f); vig.smoothness.Override(0.45f); vig.color.Override(new Color(0.25f, 0.12f, 0.06f));
        EditorUtility.SetDirty(vp);
        var vol = GameObject.Find("Global Volume").GetComponent<Volume>();
        vol.sharedProfile = vp;
        vol.isGlobal = true;

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("lighting set; volume comps: " + string.Join(",", vp.components.ConvertAll(c => c.GetType().Name)));
    }
}
