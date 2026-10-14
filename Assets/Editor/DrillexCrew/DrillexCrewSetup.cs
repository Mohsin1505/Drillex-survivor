using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Driller crew character: import settings (humanoid, clips), palette textures, URP Lit material,
// animator controller and the DRL_Driller prefab. Safe to re-run after re-exporting the FBX from
// SourceArt/Characters/Driller/build_driller.py.
public static class DrillexCrewSetup
{
    public const string Root = "Assets/Art/Characters/DRL_Driller";
    public const string FbxPath = Root + "/Meshes/SK_DRL_Driller.fbx";
    public const string PrefabPath = Root + "/Prefabs/DRL_Driller.prefab";
    const string MatPath = Root + "/Materials/M_DRL_Driller.mat";
    const string ControllerPath = Root + "/Animation/AC_DRL_Driller.controller";
    static readonly string[] Headgear = { "HG_HardHat", "HG_Welder", "HG_Goggles" };

    [MenuItem("Drillex/Crew/Rebuild Driller Assets")]
    public static void Run()
    {
        foreach (var f in new[] { "Materials", "Textures", "Prefabs", "Animation" })
            if (!AssetDatabase.IsValidFolder(Root + "/" + f)) AssetDatabase.CreateFolder(Root, f);

        ImportModel();
        var mat = BuildMaterial();
        var ctrl = BuildController();
        BuildPrefab(mat, ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("Driller crew assets rebuilt");
    }

    // ---------------------------------------------------------------- model
    static void ImportModel()
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(FbxPath);
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.bakeAxisConversion = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        mi.importVisibility = false;
        mi.isReadable = false;
        mi.meshCompression = ModelImporterMeshCompression.Low;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.None; // no normal map
        mi.addCollider = false;
        mi.skinWeights = ModelImporterSkinWeights.Standard;
        mi.optimizeMeshVertices = true;
        mi.optimizeMeshPolygons = true;
        mi.animationType = ModelImporterAnimationType.Human;
        mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importAnimation = true;
        mi.animationCompression = ModelImporterAnimationCompression.Optimal;
        mi.resampleCurves = true;
        mi.SaveAndReimport();

        // the model is authored in an A-pose; the avatar's reference pose must be a T-pose
        EnforceTPose(mi);

        var clips = mi.defaultClipAnimations;
        var list = new List<ModelImporterClipAnimation>();
        foreach (var c in clips)
        {
            string n = c.takeName.Contains("|") ? c.takeName.Substring(c.takeName.LastIndexOf('|') + 1) : c.takeName;
            if (!new[] { "Idle", "Wave", "Cheer", "Drive" }.Contains(n)) continue;
            c.name = n;
            c.loopTime = n == "Idle" || n == "Drive";
            c.loopPose = n == "Idle";
            c.lockRootRotation = true; c.lockRootHeightY = false; c.lockRootPositionXZ = true;
            c.keepOriginalOrientation = true; c.keepOriginalPositionY = true; c.keepOriginalPositionXZ = true;
            c.heightFromFeet = false;
            list.Add(c);
        }
        mi.clipAnimations = list.ToArray();
        mi.SaveAndReimport();
    }

    // Rotates the upper arms of the bind pose to horizontal and stores it as the avatar skeleton
    // (what "Enforce T-Pose" does in the avatar configurator).
    static void EnforceTPose(ModelImporter mi)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var go = Object.Instantiate(model);
        try
        {
            var all = go.GetComponentsInChildren<Transform>(true);
            Transform T(string n) => all.FirstOrDefault(t => t.name == n);
            foreach (var side in new[] { "Left", "Right" })
            {
                var ua = T(side + "UpperArm"); var la = T(side + "LowerArm");
                if (ua == null || la == null) continue;
                Vector3 dir = (la.position - ua.position).normalized;
                Vector3 target = new Vector3(Mathf.Sign(dir.x), 0f, 0f);
                ua.rotation = Quaternion.FromToRotation(dir, target) * ua.rotation;
            }
            var hd = mi.humanDescription;
            var bones = new List<SkeletonBone>();
            foreach (var t in all)
                bones.Add(new SkeletonBone { name = t == go.transform ? model.name : t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale });
            hd.skeleton = bones.ToArray();
            // make sure every human bone is mapped by name (names follow Unity's HumanBodyBones)
            var human = new List<HumanBone>();
            foreach (var hn in HumanTrait.BoneName)
            {
                string boneName = hn.Replace(" ", "");
                if (T(boneName) != null) human.Add(new HumanBone { humanName = hn, boneName = boneName, limit = new HumanLimit { useDefaultValues = true } });
            }
            hd.human = human.ToArray();
            hd.hasTranslationDoF = false;
            mi.humanDescription = hd;
            mi.SaveAndReimport();
        }
        finally { Object.DestroyImmediate(go); }
    }

    // ---------------------------------------------------------------- textures + material
    static Texture2D SaveTex(Texture2D t, string file, bool srgb)
    {
        string path = Root + "/Textures/" + file;
        File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.mipmapEnabled = false;      // flat palette cells: no mips needed, no cell bleeding
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.isReadable = false;
        ti.maxTextureSize = 64;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        foreach (var plat in new[] { "Android", "iPhone" })
        {
            var s = ti.GetPlatformTextureSettings(plat);
            s.overridden = true; s.maxTextureSize = 64;
            s.format = TextureImporterFormat.ASTC_4x4; // 4x4 blocks never straddle the 8 px cells
            ti.SetPlatformTextureSettings(s);
        }
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static Material BuildMaterial()
    {
        var baseTex = SaveTex(CrewPalette.BaseColor(CrewPalette.Colourways[0], CrewPalette.SkinTones[1]), "T_Driller_BaseColor.png", true);
        var emi = SaveTex(CrewPalette.Emission(), "T_Driller_Emission.png", true);
        var mask = SaveTex(CrewPalette.Mask(), "T_Driller_Mask.png", false);

        var m = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, MatPath); }
        m.SetTexture("_BaseMap", baseTex);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_MetallicGlossMap", mask);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetFloat("_Metallic", 1f);
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.SetTexture("_EmissionMap", emi);
        m.SetColor("_EmissionColor", Color.white * 2.2f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        m.enableInstancing = false;
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------------------------------------------------------- animator
    static AnimatorController BuildController()
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")).ToDictionary(c => c.name);
        AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ac.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Cheer", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Driving", AnimatorControllerParameterType.Bool);
        var sm = ac.layers[0].stateMachine;
        AnimatorState S(string n, Vector3 p) { var s = sm.AddState(n, p); s.motion = clips.TryGetValue(n, out var c) ? c : null; return s; }
        var idle = S("Idle", new Vector3(300, 0));
        var wave = S("Wave", new Vector3(300, 120));
        var cheer = S("Cheer", new Vector3(560, 120));
        var drive = S("Drive", new Vector3(560, 0));
        sm.defaultState = idle;

        void Back(AnimatorState from)
        {
            var t = from.AddTransition(idle); t.hasExitTime = true; t.exitTime = 0.92f; t.duration = 0.2f;
        }
        var tw = sm.AddAnyStateTransition(wave); tw.AddCondition(AnimatorConditionMode.If, 0, "Wave"); tw.duration = 0.2f; tw.canTransitionToSelf = false;
        var tc = sm.AddAnyStateTransition(cheer); tc.AddCondition(AnimatorConditionMode.If, 0, "Cheer"); tc.duration = 0.12f; tc.canTransitionToSelf = false;
        Back(wave); Back(cheer);
        var td = idle.AddTransition(drive); td.AddCondition(AnimatorConditionMode.If, 0, "Driving"); td.duration = 0.3f; td.hasExitTime = false;
        var tu = drive.AddTransition(idle); tu.AddCondition(AnimatorConditionMode.IfNot, 0, "Driving"); tu.duration = 0.3f; tu.hasExitTime = false;
        EditorUtility.SetDirty(ac);
        return ac;
    }

    // ---------------------------------------------------------------- prefab
    static void BuildPrefab(Material mat, RuntimeAnimatorController ctrl)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        var root = new GameObject("DRL_Driller");
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        visual.name = "Visual";
        var anim = visual.GetComponent<Animator>();
        if (anim == null) anim = visual.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var app = root.AddComponent<CrewAppearance>();
        app.animator = anim;
        var smrs = visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var r in smrs)
        {
            r.sharedMaterial = mat;
            r.updateWhenOffscreen = false;
            r.quality = SkinQuality.Bone4;
            r.skinnedMotionVectors = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
            r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        app.body = smrs.FirstOrDefault(r => r.name == "Body");
        app.headgear = Headgear.Select(n => smrs.FirstOrDefault(r => r.name == n)).ToArray();
        for (int i = 0; i < app.headgear.Length; i++) if (app.headgear[i]) app.headgear[i].gameObject.SetActive(i == 0);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
    }
}
