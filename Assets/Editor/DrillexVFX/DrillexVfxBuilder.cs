using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Builds the per-weapon VFX (muzzles, projectiles, impacts, explosion) from the generated VFX
// textures, then hooks them onto the existing weapon prefabs and finishes the Electric (Tesla)
// block. Visual/audio wiring only: damage, fire rate, range and radius values are not changed,
// except on the Tesla car, which never had electric stats before.
public static class DrillexVfxBuilder
{
    const string TexDir = "Assets/Art/VFX/Textures";
    const string MatDir = "Assets/Art/VFX/Materials";
    const string FxDir = "Assets/Art/VFX/Weapons";

    static Material mGlow, mFlash, mSpark, mFire, mSmoke, mBeam, mBeamAlpha, mRing, mDot, mDebris, mDotAlpha;

    // ================================================================ materials

    static Material Mat(string name, string tex, bool additive, Color tint)
    {
        string path = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + tex + ".png"));
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", additive ? 2f : 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", 1f);
        m.SetFloat("_DstBlendAlpha", additive ? 1f : (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_ColorMode", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
        m.SetOverrideTag("RenderType", "Transparent");
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Materials()
    {
        Directory.CreateDirectory(MatDir);
        mGlow = Mat("M_VFX_Glow_Add", "T_VFX_Glow", true, Color.white);
        mFlash = Mat("M_VFX_Flash_Add", "T_VFX_Flash", true, Color.white);
        mSpark = Mat("M_VFX_Spark_Add", "T_VFX_Streak", true, Color.white);
        mFire = Mat("M_VFX_Fire_Add", "T_VFX_Fire", true, Color.white);
        mSmoke = Mat("M_VFX_Smoke_Alpha", "T_VFX_Smoke", false, Color.white);
        mBeam = Mat("M_VFX_Beam_Add", "T_VFX_Beam", true, Color.white);
        mRing = Mat("M_VFX_Ring_Add", "T_VFX_Ring", true, Color.white);
        mDot = Mat("M_VFX_Dot_Add", "T_VFX_SoftDot", true, Color.white);
        mDebris = Mat("M_VFX_Debris_Alpha", "T_VFX_SoftDot", false, Color.white);
        // alpha-blended variants keep electric blue readable over bright sand (additive washes to white)
        mBeamAlpha = Mat("M_VFX_Beam_Alpha", "T_VFX_Beam", false, Color.white);
        mDotAlpha = Mat("M_VFX_Glow_Alpha", "T_VFX_Glow", false, Color.white);
    }

    // ================================================================ particle helper

    class PS
    {
        public ParticleSystem ps;
        public PS(Transform parent, string name, Material mat, bool world)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.sortMode = ParticleSystemSortMode.None;
            var m = ps.main;
            m.playOnAwake = false;
            m.loop = false;
            m.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
            m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.duration = 0.3f;
            var e = ps.emission; e.rateOverTime = 0;
            var s = ps.shape; s.enabled = false;
        }
        public PS Main(float life, float size, float speed, Color c, int max = 30)
        {
            var m = ps.main;
            m.startLifetime = life; m.startSize = size; m.startSpeed = speed; m.startColor = c; m.maxParticles = max;
            return this;
        }
        public PS Max(int n) { var m = ps.main; m.maxParticles = n; return this; }
        public PS Life(float a, float b) { var m = ps.main; m.startLifetime = new ParticleSystem.MinMaxCurve(a, b); return this; }
        public PS Size(float a, float b) { var m = ps.main; m.startSize = new ParticleSystem.MinMaxCurve(a, b); return this; }
        public PS Speed(float a, float b) { var m = ps.main; m.startSpeed = new ParticleSystem.MinMaxCurve(a, b); return this; }
        public PS Rot() { var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); return this; }
        public PS Gravity(float g) { var m = ps.main; m.gravityModifier = g; return this; }
        public PS Loop(float rate, float duration = 1f) { var m = ps.main; m.loop = true; m.duration = duration; var e = ps.emission; e.rateOverTime = rate; return this; }
        public PS Distance(float rate) { var e = ps.emission; e.rateOverDistance = rate; var m = ps.main; m.loop = true; return this; }
        public PS Burst(int count, float time = 0f, float probability = 1f)
        {
            var e = ps.emission;
            var b = new ParticleSystem.Burst(time, (short)count) { probability = probability };
            var list = new ParticleSystem.Burst[e.burstCount + 1];
            e.GetBursts(list); list[e.burstCount] = b; e.SetBursts(list);
            return this;
        }
        public PS Cone(float angle, float radius = 0.02f)
        {
            var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone; s.angle = angle; s.radius = radius; s.radiusThickness = 1f;
            return this;
        }
        public PS Sphere(float radius)
        {
            var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Sphere; s.radius = radius;
            return this;
        }
        public PS Hemi(float radius)
        {
            var s = ps.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Hemisphere; s.radius = radius;
            s.rotation = new Vector3(-90, 0, 0);
            return this;
        }
        public PS Fade(Color start, Color end, float alphaHoldUntil = 0.2f)
        {
            var c = ps.colorOverLifetime; c.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(start, 0), new GradientColorKey(end, 1) },
                      new[] { new GradientAlphaKey(start.a, 0), new GradientAlphaKey(start.a, alphaHoldUntil), new GradientAlphaKey(0, 1) });
            c.color = g;
            return this;
        }
        public PS Grow(float from, float to)
        {
            var s = ps.sizeOverLifetime; s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, from, 1, to));
            return this;
        }
        public PS Drag(float d) { var l = ps.limitVelocityOverLifetime; l.enabled = true; l.drag = d; l.multiplyDragByParticleSize = false; return this; }
        public PS Stretch(float length, float speedScale = 0.02f)
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = length; r.velocityScale = speedScale;
            return this;
        }
        public PS Flat()
        {
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            return this;
        }
        public PS Up(float min, float max)
        {
            var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
            v.x = new ParticleSystem.MinMaxCurve(0, 0); v.z = new ParticleSystem.MinMaxCurve(0, 0); v.y = new ParticleSystem.MinMaxCurve(min, max);
            return this;
        }
        public PS Order(int o) { ps.GetComponent<ParticleSystemRenderer>().sortingFudge = -o; return this; }
    }

    static GameObject Root(string name, bool rootParticle = true)
    {
        var go = new GameObject(name);
        if (rootParticle)
        {
            // empty root system so ParticleSystem.Play(true) drives all children
            var ps = go.AddComponent<ParticleSystem>();
            var m = ps.main; m.playOnAwake = false; m.loop = false; m.duration = 0.2f; m.maxParticles = 1;
            var e = ps.emission; e.enabled = false;
            var s = ps.shape; s.enabled = false;
            go.GetComponent<ParticleSystemRenderer>().enabled = false;
        }
        return go;
    }

    static GameObject Save(GameObject go, string name)
    {
        Directory.CreateDirectory(FxDir);
        var p = PrefabUtility.SaveAsPrefabAsset(go, FxDir + "/" + name + ".prefab");
        Object.DestroyImmediate(go);
        return p;
    }

    static Color C(string hex, float a = 1f) { ColorUtility.TryParseHtmlString(hex, out var c); c.a = a; return c; }

    static LightningBurst Lightning(Transform parent, string name, int arcs, Vector2 length, float cone, float width, float duration, float flicker, Color a, Color b, float dropout = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lb = go.AddComponent<LightningBurst>();
        lb.material = mBeamAlpha; lb.arcs = arcs; lb.length = length; lb.coneAngle = cone; lb.width = width; lb.duration = duration; lb.flickerRate = flicker; lb.dropout = dropout;
        lb.segments = 7; lb.jitter = 0.2f; lb.forkChance = 0.45f;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0.35f, 1) });
        lb.color = g;
        return lb;
    }

    // ================================================================ muzzles

    static GameObject MuzzleCannon()
    {
        var r = Root("VFX_Muzzle_Cannon");
        new PS(r.transform, "Flash", mFlash, false).Main(0.07f, 1.5f, 0, C("#FFD27A")).Rot().Burst(1).Fade(C("#FFE6A8"), C("#FF7A1E"), 0.5f);
        new PS(r.transform, "Burst", mFire, false).Main(0.08f, 0.45f, 0, C("#FFB347")).Speed(4, 8).Cone(10).Burst(3).Stretch(1.6f, 0.05f).Fade(C("#FFC060"), C("#FF5A10"));
        new PS(r.transform, "Sparks", mSpark, true).Main(0.25f, 0.06f, 0, C("#FFD080")).Speed(6, 12).Life(0.12f, 0.28f).Cone(22).Burst(6).Gravity(0.6f).Stretch(2.5f, 0.04f).Fade(C("#FFE0A0"), C("#FF6A10"), 0.4f);
        new PS(r.transform, "Smoke", mSmoke, true).Main(0.7f, 0.5f, 0, C("#8A8580", 0.35f)).Speed(0.5f, 1.4f).Life(0.5f, 0.9f).Rot().Cone(20).Burst(2).Grow(0.6f, 1.6f).Fade(C("#9A948E", 0.35f), C("#6E6A66", 0f));
        return Save(r, "VFX_Muzzle_Cannon");
    }

    static GameObject MuzzleTwin()
    {
        var r = Root("VFX_Muzzle_TwinCannon");
        new PS(r.transform, "Flash", mFlash, false).Main(0.08f, 1.8f, 0, C("#FFC66A")).Rot().Burst(1).Fade(C("#FFE0A0"), C("#FF6A10"), 0.5f);
        new PS(r.transform, "SideFlare", mSpark, false).Main(0.07f, 0.5f, 0, C("#FFB050")).Speed(5, 7).Cone(70).Burst(4).Stretch(1.2f, 0.06f).Fade(C("#FFC870"), C("#FF5A10"));
        new PS(r.transform, "Sparks", mSpark, true).Main(0.3f, 0.06f, 0, C("#FFD080")).Speed(7, 13).Life(0.15f, 0.3f).Cone(28).Burst(8).Gravity(0.6f).Stretch(2.5f, 0.04f).Fade(C("#FFE0A0"), C("#FF6A10"), 0.4f);
        new PS(r.transform, "Smoke", mSmoke, true).Main(0.9f, 0.6f, 0, C("#7E7A76", 0.4f)).Speed(0.6f, 1.6f).Life(0.7f, 1.1f).Rot().Cone(30).Burst(3).Grow(0.6f, 2f).Fade(C("#8E8984", 0.4f), C("#5E5A57", 0f));
        return Save(r, "VFX_Muzzle_TwinCannon");
    }

    static GameObject MuzzleGatling()
    {
        var r = Root("VFX_Muzzle_Gatling");
        new PS(r.transform, "Flash", mFlash, false).Main(0.04f, 0.8f, 0, C("#FFE08A")).Rot().Burst(1).Fade(C("#FFF0C0"), C("#FFA030"), 0.6f);
        new PS(r.transform, "Burst", mFire, false).Main(0.04f, 0.22f, 0, C("#FFC05A")).Speed(6, 9).Cone(6).Burst(1).Stretch(2.2f, 0.04f).Fade(C("#FFD070"), C("#FF7020"));
        new PS(r.transform, "Sparks", mSpark, true).Main(0.15f, 0.035f, 0, C("#FFE0A0")).Speed(8, 14).Life(0.08f, 0.16f).Cone(18).Burst(2).Stretch(2.5f, 0.03f).Fade(C("#FFF0C0"), C("#FF8020"), 0.5f);
        // light haze that builds up during sustained fire (only some shots emit)
        new PS(r.transform, "Haze", mSmoke, true).Main(0.8f, 0.35f, 0, C("#A09A94", 0.18f)).Speed(0.3f, 0.8f).Life(0.6f, 1f).Rot().Cone(25).Burst(1, 0, 0.35f).Grow(0.8f, 2.2f).Fade(C("#A8A29C", 0.18f), C("#7A7672", 0f)).Max(40);
        return Save(r, "VFX_Muzzle_Gatling");
    }

    static GameObject MuzzleRocket()
    {
        var r = Root("VFX_Muzzle_Rocket");
        new PS(r.transform, "Flash", mFlash, false).Main(0.1f, 2.4f, 0, C("#FFF2D0")).Rot().Burst(1).Fade(C("#FFFFFF"), C("#FF8A2A"), 0.4f);
        new PS(r.transform, "Blast", mFire, false).Main(0.14f, 0.7f, 0, C("#FFB040")).Speed(5, 9).Life(0.1f, 0.18f).Cone(14).Burst(5).Stretch(1.4f, 0.05f).Fade(C("#FFD080"), C("#FF4A10"));
        new PS(r.transform, "Sparks", mSpark, true).Main(0.35f, 0.07f, 0, C("#FFD080")).Speed(6, 14).Life(0.2f, 0.4f).Cone(30).Burst(10).Gravity(0.8f).Stretch(2.5f, 0.04f).Fade(C("#FFE6B0"), C("#FF5A10"), 0.4f);
        // backblast smoke lingers around the launcher
        new PS(r.transform, "Smoke", mSmoke, true).Main(1.3f, 0.8f, 0, C("#8C8782", 0.45f)).Speed(0.4f, 1.6f).Life(1f, 1.6f).Rot().Cone(45).Burst(5).Grow(0.7f, 2.6f).Fade(C("#9A948E", 0.45f), C("#605C59", 0f)).Drag(2f);
        return Save(r, "VFX_Muzzle_Rocket");
    }

    static GameObject MuzzleElectric()
    {
        var r = Root("VFX_Muzzle_Electric");
        new PS(r.transform, "Flash", mFlash, false).Main(0.08f, 1.8f, 0, C("#CFF4FF")).Rot().Burst(1).Fade(C("#FFFFFF"), C("#3AB8FF"), 0.4f);
        new PS(r.transform, "Body", mDotAlpha, false).Main(0.14f, 1.1f, 0, C("#3FB4FF", 0.85f)).Burst(1).Grow(1.1f, 0.4f).Fade(C("#8FDCFF", 0.85f), C("#1E64FF", 0f));
        new PS(r.transform, "Core", mGlow, false).Main(0.12f, 0.9f, 0, C("#7FDcff")).Burst(1).Grow(1.2f, 0.3f).Fade(C("#BFF0FF"), C("#2A90FF"));
        new PS(r.transform, "Sparks", mSpark, true).Main(0.2f, 0.04f, 0, C("#BFEFFF")).Speed(5, 11).Life(0.1f, 0.22f).Cone(40).Burst(7).Stretch(2.2f, 0.03f).Fade(C("#E6FAFF"), C("#3AA0FF"), 0.5f);
        Lightning(r.transform, "Arcs", 4, new Vector2(0.9f, 1.8f), 40f, 0.09f, 0.14f, 40f, C("#F4FCFF"), C("#2E9BFF"));
        return Save(r, "VFX_Muzzle_Electric");
    }

    // ================================================================ impacts / explosion

    static GameObject ImpactSpark(string name, Color hot, Color cool, float scale)
    {
        var r = Root(name);
        new PS(r.transform, "Flash", mFlash, false).Main(0.05f, 0.5f * scale, 0, hot).Rot().Burst(1).Fade(hot, cool, 0.5f);
        new PS(r.transform, "Sparks", mSpark, true).Main(0.25f, 0.04f * scale, 0, hot).Speed(3, 8).Life(0.1f, 0.25f).Cone(55).Burst((int)(6 * scale)).Gravity(1f).Stretch(2f, 0.03f).Fade(hot, cool, 0.4f);
        new PS(r.transform, "Puff", mSmoke, true).Main(0.45f, 0.35f * scale, 0, C("#A49C92", 0.3f)).Speed(0.3f, 0.9f).Rot().Cone(40).Burst(1).Grow(0.6f, 1.6f).Fade(C("#B0A79C", 0.3f), C("#8A8279", 0f));
        r.AddComponent<PooledVfx>().lifetime = 0.6f;
        return Save(r, name);
    }

    static GameObject ImpactElectric()
    {
        var r = Root("VFX_Impact_Electric");
        new PS(r.transform, "Body", mDotAlpha, false).Main(0.25f, 1.8f, 0, C("#3AA8FF", 0.8f)).Burst(1).Grow(0.6f, 1.2f).Fade(C("#7FD0FF", 0.8f), C("#1A5CFF", 0f));
        new PS(r.transform, "Flash", mFlash, false).Main(0.09f, 2.4f, 0, C("#D8F6FF")).Rot().Burst(1).Fade(C("#FFFFFF"), C("#3AA8FF"), 0.4f);
        new PS(r.transform, "Sparks", mSpark, true).Main(0.3f, 0.04f, 0, C("#CFF2FF")).Speed(4, 10).Life(0.12f, 0.3f).Sphere(0.1f).Burst(10).Gravity(0.5f).Stretch(2f, 0.03f).Fade(C("#E8FBFF"), C("#2F8CFF"), 0.4f);
        new PS(r.transform, "Residual", mGlow, false).Main(0.35f, 0.9f, 0, C("#5FC8FF", 0.6f)).Burst(1).Grow(1f, 0.4f).Fade(C("#8FDcff", 0.6f), C("#1E70FF", 0f));
        Lightning(r.transform, "Discharge", 6, new Vector2(1.2f, 2.4f), 180f, 0.1f, 0.32f, 30f, C("#F4FCFF"), C("#2E9BFF"));
        r.AddComponent<PooledVfx>().lifetime = 0.5f;
        return Save(r, "VFX_Impact_Electric");
    }

    static GameObject Explosion()
    {
        var r = Root("VFX_Explosion_Rocket");
        new PS(r.transform, "Flash", mFlash, false).Main(0.12f, 5f, 0, C("#FFF6E0")).Rot().Burst(1).Fade(C("#FFFFFF"), C("#FFA040"), 0.3f);
        new PS(r.transform, "Fireball", mFire, true).Main(0.55f, 1.8f, 0, C("#FFB040")).Life(0.35f, 0.65f).Size(1.4f, 2.4f).Speed(1.5f, 4.5f).Rot().Sphere(0.4f).Burst(14).Grow(0.7f, 1.9f).Fade(C("#FFE2A0"), C("#C8300A"), 0.25f).Drag(3f);
        new PS(r.transform, "Core", mGlow, false).Main(0.3f, 4f, 0, C("#FFC870")).Burst(1).Grow(0.6f, 1.3f).Fade(C("#FFE6B0"), C("#FF6A10"));
        new PS(r.transform, "Sparks", mSpark, true).Main(0.6f, 0.09f, 0, C("#FFD890")).Life(0.3f, 0.7f).Speed(8, 18).Hemi(0.3f).Burst(26).Gravity(1.4f).Stretch(3f, 0.03f).Fade(C("#FFF0C0"), C("#FF5A10"), 0.5f);
        new PS(r.transform, "Smoke", mSmoke, true).Main(1.8f, 2.2f, 0, C("#4A4542", 0.6f)).Life(1.4f, 2.1f).Size(1.8f, 2.8f).Speed(0.8f, 2.4f).Rot().Hemi(0.6f).Burst(9, 0.06f).Grow(0.6f, 2.3f).Fade(C("#5A5450", 0.6f), C("#2E2B29", 0f), 0.35f).Up(0.6f, 1.4f).Drag(1.5f);
        new PS(r.transform, "Debris", mDebris, true).Main(1f, 0.16f, 0, C("#3A2A20")).Life(0.7f, 1.2f).Size(0.1f, 0.22f).Speed(6, 12).Hemi(0.3f).Burst(12).Gravity(2.5f).Fade(C("#3A2A20"), C("#2A1E16", 0f), 0.7f);
        new PS(r.transform, "Shockwave", mRing, true).Main(0.35f, 1f, 0, C("#FFC070", 0.8f)).Burst(1).Flat().Grow(1f, 8f).Fade(C("#FFD090", 0.8f), C("#FF6A20", 0f), 0.1f);
        new PS(r.transform, "Dust", mSmoke, true).Main(1.2f, 1.6f, 0, C("#C88A5A", 0.5f)).Life(0.9f, 1.4f).Speed(4, 7).Rot().Cone(88, 0.6f).Burst(10).Grow(0.8f, 2.2f).Fade(C("#D29766", 0.5f), C("#A06A44", 0f)).Drag(4f);
        // dust ring travels along the ground: emit sideways from a flat cone
        var dust = r.transform.Find("Dust"); dust.localRotation = Quaternion.Euler(-90, 0, 0);
        r.AddComponent<PooledVfx>().lifetime = 2.3f;
        return Save(r, "VFX_Explosion_Rocket");
    }

    // ================================================================ projectiles

    static void TracerBullet(string prefabPath, Color head, Color tail, float width, float time, bool glow, GameObject impact)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        foreach (var mr in root.GetComponents<MeshRenderer>()) mr.enabled = false;
        foreach (Transform c in root.transform) if (c.name == "Tracer" || c.name == "Glow") Object.DestroyImmediate(c.gameObject);
        // the bullet root carries a squashed scale; tracer children counter it
        Vector3 inv = new Vector3(1f / root.transform.localScale.x, 1f / root.transform.localScale.y, 1f / root.transform.localScale.z);
        var tr = new GameObject("Tracer").AddComponent<TrailRenderer>();
        tr.transform.SetParent(root.transform, false); tr.transform.localScale = inv;
        tr.sharedMaterial = mBeam; tr.time = time; tr.minVertexDistance = 0.1f; tr.widthMultiplier = width;
        tr.widthCurve = AnimationCurve.Linear(0, 1, 1, 0.1f);
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(head, 0), new GradientColorKey(tail, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        tr.colorGradient = g; tr.shadowCastingMode = ShadowCastingMode.Off; tr.receiveShadows = false; tr.numCapVertices = 0; tr.alignment = LineAlignment.View;
        if (glow)
        {
            var gp = new PS(root.transform, "Glow", mGlow, false).Main(10f, 0.55f, 0, head, 1).Burst(1);
            gp.ps.transform.localScale = inv;
            var m = gp.ps.main; m.playOnAwake = true; m.loop = false; m.duration = 10f;
        }
        var bullet = root.GetComponent<Bullet>();
        if (bullet != null) bullet.impactEffect = impact;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static GameObject ElectricBolt(GameObject impact)
    {
        const string path = "Assets/Prefeb/ElectricBolt.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset("Assets/Prefeb/basicbulle.prefab", path);
        var root = PrefabUtility.LoadPrefabContents(path);
        root.name = "ElectricBolt";
        foreach (var mr in root.GetComponents<MeshRenderer>()) mr.enabled = false;
        for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
        Vector3 inv = new Vector3(1f / root.transform.localScale.x, 1f / root.transform.localScale.y, 1f / root.transform.localScale.z);

        var body = new PS(root.transform, "Body", mDotAlpha, false).Main(10f, 1.1f, 0, C("#2F9CFF", 0.9f), 1).Burst(1);
        body.ps.transform.localScale = inv; var bm = body.ps.main; bm.playOnAwake = true; bm.duration = 10f;
        var core = new PS(root.transform, "Core", mGlow, false).Main(10f, 1.2f, 0, C("#9FE4FF"), 1).Burst(1);
        core.ps.transform.localScale = inv; var cm = core.ps.main; cm.playOnAwake = true; cm.duration = 10f;
        var hot = new PS(root.transform, "HotCore", mFlash, false).Main(10f, 0.8f, 0, C("#FFFFFF"), 1).Burst(1).Rot();
        hot.ps.transform.localScale = inv; var hm = hot.ps.main; hm.playOnAwake = true; hm.duration = 10f;
        var arcs = Lightning(root.transform, "Arcs", 3, new Vector2(0.5f, 1.0f), 180f, 0.07f, 0f, 22f, C("#F0FBFF"), C("#2E9BFF"), 0.25f);
        arcs.transform.localScale = inv;
        var tr = new GameObject("Tracer").AddComponent<TrailRenderer>();
        tr.transform.SetParent(root.transform, false); tr.transform.localScale = inv;
        tr.sharedMaterial = mBeamAlpha; tr.time = 0.16f; tr.minVertexDistance = 0.1f; tr.widthMultiplier = 0.55f; tr.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(C("#CFF4FF"), 0), new GradientColorKey(C("#2F8CFF"), 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
        tr.colorGradient = g; tr.shadowCastingMode = ShadowCastingMode.Off; tr.receiveShadows = false; tr.alignment = LineAlignment.View;

        var b = root.GetComponent<Bullet>();
        b.speed = 55f; b.lifeTime = 0.9f; b.useHoming = true; b.turnSpeed = 6f;
        b.impactEffect = impact; b.electricImpactSound = true;
        var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        return saved;
    }

    static void RocketVisual()
    {
        const string path = "Assets/Prefeb/Rocket.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        foreach (var mr in root.GetComponents<MeshRenderer>()) mr.enabled = false;
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var c = root.transform.GetChild(i);
            // the nose sphere carries a collider: keep it, only hide its placeholder mesh
            if (c.GetComponent<Collider>() != null) { foreach (var r in c.GetComponents<Renderer>()) r.enabled = false; continue; }
            Object.DestroyImmediate(c.gameObject);
        }
        Vector3 inv = new Vector3(1f / root.transform.localScale.x, 1f / root.transform.localScale.y, 1f / root.transform.localScale.z);

        // body: reuse the missile mesh modelled on the rocket car's launcher
        var car = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/rocket launcher.prefab");
        MeshFilter src = null;
        foreach (var mf in car.GetComponentsInChildren<MeshFilter>(true)) if (mf.name == "Rocket_1") src = mf;
        var body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = inv;
        if (src != null)
        {
            body.AddComponent<MeshFilter>().sharedMesh = src.sharedMesh;
            var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterial = src.GetComponent<MeshRenderer>().sharedMaterial; mr.shadowCastingMode = ShadowCastingMode.Off;
            var bnd = src.sharedMesh.bounds; float len = Mathf.Max(bnd.size.z, 0.01f);
            float k = 0.9f / len; // ~0.9 m long in flight, readable from the gameplay camera
            body.transform.localScale = Vector3.Scale(inv, Vector3.one * k);
            body.transform.localPosition = Vector3.Scale(inv, -bnd.center * k);
        }

        var fx = new GameObject("Exhaust").transform; fx.SetParent(root.transform, false); fx.localScale = inv;
        fx.localPosition = new Vector3(0, 0, -0.5f / root.transform.localScale.z);
        fx.localRotation = Quaternion.Euler(0, 180, 0);
        var flame = new PS(root.transform, "Flame", mFire, false).Main(0.12f, 0.6f, 0, C("#FFB040")).Life(0.08f, 0.14f).Size(0.5f, 0.8f).Speed(2, 4).Cone(6).Loop(70).Rot().Fade(C("#FFF0C0"), C("#FF4A10"), 0.2f).Grow(1f, 0.3f);
        var core = new PS(root.transform, "HotCore", mGlow, false).Main(0.06f, 0.55f, 0, C("#FFF2D8")).Loop(30).Fade(C("#FFFFFF"), C("#FFB050"));
        var smoke = new PS(root.transform, "SmokeTrail", mSmoke, true).Main(1.0f, 0.6f, 0, C("#9A948E", 0.5f)).Life(0.8f, 1.2f).Speed(0.1f, 0.4f).Rot().Distance(7f).Grow(0.7f, 2.6f).Fade(C("#A8A29C", 0.45f), C("#6E6A66", 0f), 0.15f).Max(120);
        var embers = new PS(root.transform, "Embers", mSpark, true).Main(0.3f, 0.05f, 0, C("#FFC070")).Life(0.15f, 0.35f).Speed(1, 3).Cone(25).Loop(25).Stretch(1.5f, 0.05f).Fade(C("#FFE0A0"), C("#FF5010"), 0.4f);
        foreach (var p in new[] { flame, core, smoke, embers })
        {
            p.ps.transform.localScale = inv;
            p.ps.transform.localPosition = new Vector3(0, 0, -0.48f / root.transform.localScale.z);
            p.ps.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var m = p.ps.main; m.playOnAwake = true; m.duration = 1f;
        }
        Object.DestroyImmediate(fx.gameObject);
        var tr = new GameObject("Trail").AddComponent<TrailRenderer>();
        tr.transform.SetParent(root.transform, false); tr.transform.localScale = inv; tr.transform.localPosition = new Vector3(0, 0, -0.45f / root.transform.localScale.z);
        tr.sharedMaterial = mBeam; tr.time = 0.22f; tr.widthMultiplier = 0.3f; tr.widthCurve = AnimationCurve.Linear(0, 1, 1, 0); tr.minVertexDistance = 0.15f;
        var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(C("#FFE0A0"), 0), new GradientColorKey(C("#FF5010"), 1) }, new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0, 1) });
        tr.colorGradient = g; tr.shadowCastingMode = ShadowCastingMode.Off; tr.receiveShadows = false;

        var rb = root.GetComponent<RocketBullet>();
        rb.explosionEffect = AssetDatabase.LoadAssetAtPath<GameObject>(FxDir + "/VFX_Explosion_Rocket.prefab");
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ================================================================ weapon hookup

    static void SetGuns(string prefabPath, GameObject muzzle, AutoGun.ShotSound sound, System.Action<AutoGun> extra = null)
    {
        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        foreach (var gun in root.GetComponentsInChildren<AutoGun>(true))
        {
            gun.muzzleFlashPrefab = muzzle;
            gun.shotSound = sound;
            extra?.Invoke(gun);
        }
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void FlamethrowerExtras()
    {
        const string path = "Assets/Prefeb/FlameThrower.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var ft = root.GetComponentsInChildren<FlameThrower>(true);
        ParticleSystem fx = null;
        foreach (var f in ft) if (f.flameEffect != null) fx = f.flameEffect;
        if (fx != null)
        {
            for (int i = fx.transform.childCount - 1; i >= 0; i--)
            {
                var c = fx.transform.GetChild(i);
                if (c.name == "Embers" || c.name == "HeatSmoke" || c.name == "PilotGlow") Object.DestroyImmediate(c.gameObject);
            }
            var embers = new PS(fx.transform, "Embers", mSpark, true).Main(0.6f, 0.05f, 0, C("#FFB050")).Life(0.4f, 0.8f).Speed(4, 8).Cone(14).Loop(28).Gravity(-0.15f).Stretch(1.8f, 0.04f).Fade(C("#FFE0A0"), C("#FF3A08"), 0.5f).Max(60);
            var smoke = new PS(fx.transform, "HeatSmoke", mSmoke, true).Main(1.2f, 0.6f, 0, C("#3E3A38", 0.35f)).Life(0.9f, 1.4f).Speed(3, 5).Rot().Cone(16).Loop(10).Grow(0.8f, 2.6f).Fade(C("#4A4644", 0.35f), C("#2A2826", 0f), 0.3f).Up(0.4f, 1f).Drag(2f).Max(30);
            foreach (var p in new[] { embers.ps, smoke.ps })
            {
                p.transform.localPosition = Vector3.zero;
                // the flame system points along its own forward; emit the same way
                p.transform.localRotation = Quaternion.identity;
                var m = p.main; m.playOnAwake = false; m.duration = 1f;
            }
        }
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }

    static void TeslaCar(GameObject bolt, GameObject muzzle)
    {
        var root = PrefabUtility.LoadPrefabContents(DrillexProgressionSetup.ElectricPrefabPath);
        foreach (var gun in root.GetComponentsInChildren<AutoGun>(true))
        {
            gun.weaponType = AutoGun.WeaponType.Electric;
            gun.bulletPrefab = bolt;
            gun.muzzleFlashPrefab = muzzle;
            gun.shotSound = AutoGun.ShotSound.Electric;
            // first-time electric stats: between the Gatling and Rocket in damage per second
            gun.baseDamage = 28f; gun.fireRate = 0.7f; gun.range = 32f; gun.rotationSpeed = 9f;
        }
        // idle coil crackle on the two top emitters (cheap: 2 line renderers each, low flicker rate)
        var emitters = new System.Collections.Generic.List<Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == "VFX_Arc_Top_L" || t.name == "VFX_Arc_Top_R") emitters.Add(t);
        foreach (var t in emitters)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
            Lightning(t, "IdleArc", 1, new Vector2(0.12f, 0.28f), 70f, 0.02f, 0f, 14f, C("#E8FAFF"), C("#4AB4FF"), 0.55f).transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var glow = new PS(t, "CoilGlow", mGlow, false).Main(10f, 0.35f, 0, C("#5FC8FF", 0.8f), 1).Burst(1);
            var gm = glow.ps.main; gm.playOnAwake = true; gm.loop = true; gm.duration = 10f;
        }
        PrefabUtility.SaveAsPrefabAsset(root, DrillexProgressionSetup.ElectricPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // ================================================================ entry

    [MenuItem("Drillex/VFX/Build Weapon VFX And Hook Up Weapons")]
    public static void Build()
    {
        Materials();
        var mCannon = MuzzleCannon();
        var mTwin = MuzzleTwin();
        var mGat = MuzzleGatling();
        var mRocket = MuzzleRocket();
        var mElec = MuzzleElectric();
        var iCannon = ImpactSpark("VFX_Impact_Cannon", C("#FFD080"), C("#FF5A10"), 2f);
        var iGat = ImpactSpark("VFX_Impact_Gatling", C("#FFE6A0"), C("#FF8020"), 1.2f);
        var iElec = ImpactElectric();
        Explosion();

        TracerBullet("Assets/Prefeb/basicbulle.prefab", C("#FFD27A"), C("#FF6A10"), 0.3f, 0.1f, true, iCannon);
        TracerBullet("Assets/Prefeb/Machinebullet.prefab", C("#FFF0B0"), C("#FFA030"), 0.14f, 0.07f, false, iGat);
        var bolt = ElectricBolt(iElec);
        RocketVisual();

        SetGuns("Assets/Prefeb/Basic Gun.prefab", mCannon, AutoGun.ShotSound.Basic);
        SetGuns("Assets/Prefeb/Double  barrel gun.prefab", mTwin, AutoGun.ShotSound.Double);
        SetGuns("Assets/Prefeb/mini machine gun.prefab", mGat, AutoGun.ShotSound.MachineGun);
        SetGuns("Assets/Prefeb/rocket launcher.prefab", mRocket, AutoGun.ShotSound.Rocket);
        SetGuns("Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Train.prefab", mCannon, AutoGun.ShotSound.Basic);
        FlamethrowerExtras();
        TeslaCar(bolt, mElec);

        AssetDatabase.SaveAssets();
        Debug.Log("Drillex weapon VFX built");
    }
}
