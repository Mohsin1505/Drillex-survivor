using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

// Lightweight desert air FX (all world-space, one shared soft-dot texture, 2 particle materials, no shadows/lights):
//  FX_DustMotes    - tiny warm motes floating in the air around the camera            (max 90)
//  FX_SandDrift    - thin streaks of sand blowing low across the ground near the train (max 70)
//  FX_DistantHaze  - a few huge faint dust puffs drifting in the mid/far distance     (max 10)
internal class CommandScript : IRunCommand
{
    const string Dir = "Assets/Art/Environment/DesertLevel";

    Material Mat(string name, Texture2D tex, Color col)
    {
        string path = Dir + "/Materials/" + name + ".mat";
        Directory.CreateDirectory(Dir + "/Materials");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", col);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SoftParticlesEnabled", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    ParticleSystem Make(string name, Transform parent, Vector3 localPos, Material mat)
    {
        var old = parent.Find(name);
        if (old) Object.DestroyImmediate(old.gameObject);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        r.sortingFudge = 0f;
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.loop = true;
        main.prewarm = true;
        main.startSpeed = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        return ps;
    }

    static Gradient Fade(Color c, float peak)
    {
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.25f), new GradientAlphaKey(peak, 0.7f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Lev1.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);

        // soft round dot
        string tpath = Dir + "/Textures/T_FX_SoftDot.png";
        const int N = 64;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f)) / (N / 2f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        File.WriteAllBytes(tpath, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(tpath);
        var ti = (TextureImporter)AssetImporter.GetAtPath(tpath);
        ti.alphaIsTransparency = true; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp; ti.maxTextureSize = 64;
        ti.SaveAndReimport();
        var dot = AssetDatabase.LoadAssetAtPath<Texture2D>(tpath);
        var mMote = Mat("M_FX_DustMote", dot, new Color(1f, 0.86f, 0.66f, 1f));
        var mHaze = Mat("M_FX_DustHaze", dot, new Color(0.95f, 0.70f, 0.50f, 1f));

        var cam = GameObject.Find("Main Camera").transform;
        var train = GameObject.Find("DRL_DrillVehicle_Train").transform;
        var wind = new Vector3(1f, 0f, 0.25f).normalized;

        // motes: around/in front of the camera
        var motes = Make("FX_DustMotes", cam, new Vector3(0f, -3f, 16f), mMote);
        {
            var main = motes.main;
            main.maxParticles = 90;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            var em = motes.emission; em.rateOverTime = 11f;
            var sh = motes.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(36f, 12f, 30f);
            var vel = motes.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(0.5f * wind.x, 1.4f * wind.x); vel.y = new ParticleSystem.MinMaxCurve(-0.08f, 0.18f); vel.z = new ParticleSystem.MinMaxCurve(0.5f * wind.z, 1.4f * wind.z);
            var no = motes.noise; no.enabled = true; no.strength = 0.25f; no.frequency = 0.25f; no.quality = ParticleSystemNoiseQuality.Low;
            var col = motes.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.55f);
        }

        // sand drift: low streaks blowing across the ground around the train
        var drift = Make("FX_SandDrift", train, new Vector3(0f, -0.35f, 14f), mMote);
        {
            var main = drift.main;
            main.maxParticles = 70;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
            var em = drift.emission; em.rateOverTime = 22f;
            var sh = drift.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(44f, 0.5f, 40f);
            var vel = drift.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(4.5f * wind.x, 7.5f * wind.x); vel.y = new ParticleSystem.MinMaxCurve(0f, 0.25f); vel.z = new ParticleSystem.MinMaxCurve(4.5f * wind.z, 7.5f * wind.z);
            var no = drift.noise; no.enabled = true; no.strength = 0.35f; no.frequency = 0.6f; no.quality = ParticleSystemNoiseQuality.Low;
            var col = drift.colorOverLifetime; col.enabled = true; col.color = Fade(new Color(1f, 0.82f, 0.62f), 0.32f);
            var r = drift.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.12f; r.lengthScale = 2.2f;
        }

        // distant haze puffs: dropped after review (fog already gives the depth; big quads = mobile overdraw)
        var oldHaze = cam.Find("FX_DistantHaze");
        if (oldHaze) Object.DestroyImmediate(oldHaze.gameObject);
        if (false)
        {
            var haze = Make("FX_DistantHaze", cam, new Vector3(0f, -6f, 110f), mHaze);
            var main = haze.main;
            main.maxParticles = 10;
            main.startLifetime = new ParticleSystem.MinMaxCurve(14f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(14f, 26f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var em = haze.emission; em.rateOverTime = 0.6f;
            var sh = haze.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(180f, 6f, 120f);
            var vel = haze.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(2f * wind.x, 4f * wind.x); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(2f * wind.z, 4f * wind.z);
            var col = haze.colorOverLifetime; col.enabled = true; col.color = Fade(Color.white, 0.10f);
            var r = haze.GetComponent<ParticleSystemRenderer>();
            r.maxParticleSize = 0.35f;
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("dust FX: motes 90, drift 70 max particles");
    }
}
