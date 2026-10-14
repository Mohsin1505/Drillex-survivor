using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Spider Robot enemy: import settings, the four colour-variant materials, animator, the shared
// visual prefab, the energy bolt, and gameplay prefab variants of the existing monsters
// (Red = lightMoster, Blue = fast lightMoster, Green = MediumMoster, Purple = HeavyMoster).
// Gameplay stays on SmallWorm; the variants only swap the placeholder capsule for the spider.
// Re-run after re-exporting SourceArt/Enemies/SpiderBot/build_spiderbot.py.
public static class DrillexSpiderSetup
{
    public const string Root = "Assets/Art/Enemies/DRL_SpiderBot";
    public const string FbxPath = Root + "/Meshes/SK_DRL_SpiderBot.fbx";
    public const string VisualPath = Root + "/Prefabs/DRL_SpiderBot_Visual.prefab";
    public const string BoltPath = Root + "/Prefabs/VFX_SpiderBolt.prefab";
    const string ControllerPath = Root + "/Animation/AC_DRL_SpiderBot.controller";
    public static readonly string[] Variants = { "Red", "Blue", "Green", "Purple" };
    static readonly Dictionary<string, string> Glow = new Dictionary<string, string>
    { { "Red", "#FFB020" }, { "Blue", "#3FD0FF" }, { "Green", "#66FF3A" }, { "Purple", "#E85BFF" } };

    public static string EnemyPath(string v) => Root + "/Prefabs/DRL_SpiderBot_" + v + ".prefab";

    [MenuItem("Drillex/Enemies/Rebuild Spider Robot Assets")]
    public static void Run()
    {
        foreach (var f in new[] { "Materials", "Prefabs", "Animation" })
            if (!AssetDatabase.IsValidFolder(Root + "/" + f)) AssetDatabase.CreateFolder(Root, f);
        Textures();
        ImportModel();
        var mats = Materials();
        var ctrl = Controller();
        var bolt = Bolt();
        Visual(mats["Red"], ctrl, bolt);
        foreach (var v in Variants) EnemyVariant(v, mats[v]);
        AssetDatabase.SaveAssets();
        Debug.Log("Spider Robot assets rebuilt");
    }

    // ---------------------------------------------------------------- import
    static void Textures()
    {
        foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Textures" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool mask = path.Contains("_Mask");
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = !mask;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = 512;
            ti.isReadable = false;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.anisoLevel = 1;
            foreach (var plat in new[] { "Android", "iPhone" })
            {
                var s = ti.GetPlatformTextureSettings(plat);
                s.overridden = true; s.maxTextureSize = 512; s.format = TextureImporterFormat.ASTC_6x6;
                ti.SetPlatformTextureSettings(s);
            }
            ti.SaveAndReimport();
        }
    }

    static void ImportModel()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        mi.globalScale = 1f; mi.useFileScale = true; mi.bakeAxisConversion = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importCameras = false; mi.importLights = false; mi.importBlendShapes = false; mi.importVisibility = false;
        mi.isReadable = false;
        mi.meshCompression = ModelImporterMeshCompression.Medium;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.None;
        mi.addCollider = false;
        // mechanical, rigidly skinned: one bone per vertex is exact and the cheapest skinning
        mi.skinWeights = ModelImporterSkinWeights.Custom;
        mi.maxBonesPerVertex = 1;
        mi.animationType = ModelImporterAnimationType.Generic;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        // crowds: strip the bone GameObjects, keep only the eye exposed as the bolt socket
        mi.optimizeGameObjects = true;
        mi.extraExposedTransformPaths = new[] { "SpiderBot_Rig/Root/Body/Eye" };
        mi.importAnimation = true;
        mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        mi.SaveAndReimport();

        var list = new List<ModelImporterClipAnimation>();
        foreach (var c in mi.defaultClipAnimations)
        {
            string n = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
            if (!new[] { "Idle", "Roam", "Walk", "Attack", "Death" }.Contains(n)) continue;
            c.name = n;
            c.loopTime = n == "Idle" || n == "Roam" || n == "Walk";
            c.loopPose = false; // cycles are authored to loop exactly
            c.lockRootRotation = false; c.lockRootHeightY = false; c.lockRootPositionXZ = false;
            list.Add(c);
        }
        mi.clipAnimations = list.ToArray();
        mi.SaveAndReimport();
    }

    // ---------------------------------------------------------------- materials
    static Dictionary<string, Material> Materials()
    {
        var mats = new Dictionary<string, Material>();
        var emi = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_SpiderBot_Emission.png");
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_SpiderBot_Mask.png");
        foreach (var v in Variants)
        {
            string path = Root + "/Materials/M_SpiderBot_" + v + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/T_SpiderBot_" + v + "_BaseColor.png"));
            m.SetColor("_BaseColor", Color.white);
            m.SetTexture("_MetallicGlossMap", mask);
            m.EnableKeyword("_METALLICSPECGLOSSMAP");
            m.SetFloat("_Metallic", 1f); m.SetFloat("_Smoothness", 1f); m.SetFloat("_SmoothnessTextureChannel", 0f);
            m.SetTexture("_EmissionMap", emi);
            ColorUtility.TryParseHtmlString(Glow[v], out var g);
            m.SetColor("_EmissionColor", g * 2.2f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            mats[v] = m;
        }
        return mats;
    }

    // ---------------------------------------------------------------- animator
    static AnimatorController Controller()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        var sm = ac.layers[0].stateMachine;

        // ground speeds the in-place cycles were authored for (stride / half-cycle)
        var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(clips["Idle"], 0f);
        tree.AddChild(clips["Roam"], 1.05f);
        tree.AddChild(clips["Walk"], 3.0f);
        sm.defaultState = loco;
        loco.writeDefaultValues = true;

        var attack = sm.AddState("Attack", new Vector3(300, 120)); attack.motion = clips["Attack"];
        var death = sm.AddState("Death", new Vector3(560, 0)); death.motion = clips["Death"];
        var ta = loco.AddTransition(attack); ta.AddCondition(AnimatorConditionMode.If, 0, "Attack"); ta.hasExitTime = false; ta.duration = 0.08f;
        var tb = attack.AddTransition(loco); tb.hasExitTime = true; tb.exitTime = 0.85f; tb.duration = 0.15f;
        var td = sm.AddAnyStateTransition(death); td.AddCondition(AnimatorConditionMode.If, 0, "Death"); td.duration = 0.05f; td.canTransitionToSelf = false;
        EditorUtility.SetDirty(ac);
        return ac;
    }

    // ---------------------------------------------------------------- bolt
    static GameObject Bolt()
    {
        var go = new GameObject("VFX_SpiderBolt");
        var trail = go.AddComponent<TrailRenderer>();
        trail.time = 0.12f; trail.minVertexDistance = 0.15f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0, 0.32f), new Keyframe(1, 0f));
        trail.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/M_VFX_Beam_Add.mat");
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; trail.receiveShadows = false;
        trail.numCapVertices = 2;
        var head = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.DestroyImmediate(head.GetComponent<Collider>());
        head.name = "Head"; head.transform.SetParent(go.transform, false); head.transform.localScale = Vector3.one * 0.55f;
        var hr = head.GetComponent<MeshRenderer>();
        hr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/Materials/M_VFX_Glow_Add.mat");
        hr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; hr.receiveShadows = false;
        head.AddComponent<BillboardUI>();
        var b = go.AddComponent<SpiderBolt>();
        b.trail = trail; b.head = head.transform;
        b.impactVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/VFX/Weapons/VFX_Impact_Electric.prefab");
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, BoltPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ---------------------------------------------------------------- shared visual
    static void Visual(Material red, RuntimeAnimatorController ctrl, GameObject bolt)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        go.name = "DRL_SpiderBot_Visual";
        var anim = go.GetComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
        smr.sharedMaterial = red;
        smr.quality = SkinQuality.Bone1;
        smr.updateWhenOffscreen = false;
        smr.skinnedMotionVectors = false;
        smr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        var vis = go.AddComponent<SpiderBotVisual>();
        vis.animator = anim; vis.body = smr;
        vis.muzzle = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Eye");
        vis.boltPrefab = bolt;
        vis.deathVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/VFX/Weapons/VFX_Impact_Electric.prefab");
        vis.walkClipSpeed = 3.0f;
        PrefabUtility.SaveAsPrefabAsset(go, VisualPath);
        Object.DestroyImmediate(go);
    }

    // ---------------------------------------------------------------- gameplay variants
    static void EnemyVariant(string v, Material mat)
    {
        string basePath = v == "Green" ? "Assets/Prefeb/MediumMoster.prefab" : v == "Purple" ? "Assets/Prefeb/HeavyMoster.prefab" : "Assets/Prefeb/lightMoster.prefab";
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        inst.name = "DRL_SpiderBot_" + v;
        inst.transform.position = Vector3.zero; inst.transform.rotation = Quaternion.identity;

        // hide the placeholder capsule and its block "eyes" (kept in the base prefab, untouched)
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
            if (r.GetComponentInParent<Canvas>(true) == null) r.enabled = false;

        // feet on the ground: the root sits at the collider centre, spawned enemyHeightOffset above the sand
        // (collider bounds are empty outside a physics scene, so read the shape directly)
        float bottom = -1f;
        if (inst.GetComponent<Collider>() is BoxCollider bc) bottom = bc.center.y - bc.size.y * 0.5f;
        else if (inst.GetComponent<Collider>() is CapsuleCollider cc) bottom = cc.center.y - cc.height * 0.5f;
        var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPath);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, inst.transform);
        visual.name = "SpiderVisual";
        visual.transform.localPosition = new Vector3(0, bottom, 0);
        visual.transform.localRotation = Quaternion.identity;
        float scale = v == "Green" ? 1.12f : v == "Purple" ? 1.25f : 1f;
        visual.transform.localScale = Vector3.one * scale;
        var vis = visual.GetComponent<SpiderBotVisual>();
        vis.body.sharedMaterial = mat;
        ColorUtility.TryParseHtmlString(Glow[v], out var g); vis.glow = g;

        // ranged: hang back and shoot instead of hugging the train
        var worm = inst.GetComponent<SmallWorm>();
        worm.attackDistance = 6f;
        worm.trainAttackRange = 7f;
        if (v == "Blue") { worm.moveSpeed = 9f; worm.maxHealth = 70f; }

        PrefabUtility.SaveAsPrefabAsset(inst, EnemyPath(v));
        Object.DestroyImmediate(inst);
    }
}
