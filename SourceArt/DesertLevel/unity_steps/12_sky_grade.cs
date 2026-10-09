using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.IO;

// Sunset sky as a painted lat-long gradient (Skybox/Panoramic: one texture sample, cheap and fully art-directable),
// plus grading / fog / sun balance after review 1 (green procedural horizon, washed-out distance).
internal class CommandScript : IRunCommand
{
    const string Dir = "Assets/Art/Environment/DesertLevel";

    static Color Grad(float lat)
    {
        // lat in degrees
        var below = new Color(0.78f, 0.52f, 0.38f);
        var horizon = new Color(1.00f, 0.74f, 0.50f);
        var low = new Color(0.96f, 0.64f, 0.46f);
        var mid = new Color(0.72f, 0.56f, 0.58f);
        var high = new Color(0.40f, 0.44f, 0.62f);
        if (lat < 0f) return Color.Lerp(horizon, below, Mathf.Clamp01(-lat / 12f));
        if (lat < 8f) return Color.Lerp(horizon, low, lat / 8f);
        if (lat < 32f) return Color.Lerp(low, mid, (lat - 8f) / 24f);
        return Color.Lerp(mid, high, Mathf.Clamp01((lat - 32f) / 50f));
    }

    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Lev1.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        var sunT = GameObject.Find("Sun_DesertSunset").transform;
        var sun = sunT.GetComponent<Light>();
        sun.intensity = 1.75f;
        Vector3 sdir = -sunT.forward;                                      // toward the sun
        Vector3 hdir = new Vector3(sdir.x, 0.06f, sdir.z).normalized;      // horizon glow under it

        const int W = 1024, H = 512;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float lon = ((x + 0.5f) / W - 0.5f) * 2f * Mathf.PI;
                float lat = ((y + 0.5f) / H - 0.5f) * Mathf.PI;
                var d = new Vector3(Mathf.Cos(lat) * Mathf.Sin(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Cos(lon));
                Color c = Grad(lat * Mathf.Rad2Deg);
                float g = Mathf.Max(0f, Vector3.Dot(d, hdir));
                float s = Mathf.Max(0f, Vector3.Dot(d, sdir));
                c += new Color(0.30f, 0.16f, 0.05f) * Mathf.Pow(g, 5f) * (1f - Mathf.Clamp01(lat * Mathf.Rad2Deg / 40f));
                c += new Color(0.35f, 0.24f, 0.10f) * Mathf.Pow(s, 40f);
                c += new Color(1f, 0.9f, 0.7f) * Mathf.Pow(s, 1500f) * 0.9f;
                c.r = Mathf.Min(c.r, 1f); c.g = Mathf.Min(c.g, 1f); c.b = Mathf.Min(c.b, 1f); c.a = 1f;
                px[y * W + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        string tpath = Dir + "/Textures/T_Sky_DesertSunset.png";
        File.WriteAllBytes(tpath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(tpath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tpath);
        ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        foreach (var plat in new[] { "Android", "iPhone" })
        {
            var st = ti.GetPlatformTextureSettings(plat);
            st.overridden = true; st.maxTextureSize = 1024; st.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(st);
        }
        ti.SaveAndReimport();

        string skyPath = Dir + "/SKY_DesertSunset.mat";
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        sky.shader = Shader.Find("Skybox/Panoramic");
        sky.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tpath));
        sky.SetFloat("_Mapping", 1f);
        sky.SetFloat("_ImageType", 0f);
        sky.SetFloat("_Exposure", 1.0f);
        sky.SetFloat("_Rotation", 0f);
        sky.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
        EditorUtility.SetDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.fogColor = new Color(0.90f, 0.62f, 0.44f);
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 600f;
        GameObject.Find("Main Camera").GetComponent<Camera>().farClipPlane = 640f;
        RenderSettings.ambientSkyColor = new Color(0.46f, 0.48f, 0.64f);
        RenderSettings.ambientEquatorColor = new Color(0.72f, 0.50f, 0.37f);

        var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Dir + "/VP_DesertSunset.asset");
        vp.TryGet<ColorAdjustments>(out var ca);
        ca.postExposure.Override(0.0f); ca.contrast.Override(12f); ca.saturation.Override(4f);
        vp.TryGet<WhiteBalance>(out var wb);
        wb.temperature.Override(4f); wb.tint.Override(0f);
        vp.TryGet<Tonemapping>(out var tm);
        tm.mode.Override(TonemappingMode.Neutral);
        EditorUtility.SetDirty(vp);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("sky + grade updated; sun dir " + sdir);
    }
}
