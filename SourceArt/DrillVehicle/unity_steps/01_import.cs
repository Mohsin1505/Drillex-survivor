using UnityEngine;
using UnityEditor;
using System.Text;

// Step 1: import settings for the drill vehicle textures and models, and the two URP/Lit materials.
internal class CommandScript : IRunCommand
{
    const string Root = "Assets/Art/Vehicles/DRL_DrillVehicle";

    void Tex(string path, bool srgb, bool normal, int max, int mobileMax)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.alphaIsTransparency = false;
        ti.mipmapEnabled = true;
        ti.maxTextureSize = max;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.anisoLevel = 4;
        foreach (var plat in new[] { "Android", "iPhone" })
        {
            var s = ti.GetPlatformTextureSettings(plat);
            s.overridden = true;
            s.maxTextureSize = mobileMax;
            s.format = TextureImporterFormat.ASTC_6x6;
            ti.SetPlatformTextureSettings(s);
        }
        ti.SaveAndReimport();
    }

    void Model(string path)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(path);
        mi.globalScale = 1f;
        mi.useFileScale = true;
        mi.bakeAxisConversion = true;
        mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.importAnimation = false;
        mi.animationType = ModelImporterAnimationType.None;
        mi.importCameras = false;
        mi.importLights = false;
        mi.importBlendShapes = false;
        mi.isReadable = false;
        mi.meshCompression = ModelImporterMeshCompression.Off;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.addCollider = false;
        mi.generateSecondaryUV = false;
        mi.SaveAndReimport();
    }

    void Mat(string name, string set, bool emission)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        string t = Root + "/Textures/" + set + "_";
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Mask.png");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Normal.png"));
        m.SetFloat("_BumpScale", 1f);
        m.EnableKeyword("_NORMALMAP");
        // Mask texture: R metallic, G occlusion, A smoothness.
        // URP/Lit reads metallic from R and smoothness from A of _MetallicGlossMap, occlusion from G.
        m.SetTexture("_MetallicGlossMap", mask);
        m.SetFloat("_Metallic", 1f);
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetTexture("_OcclusionMap", mask);
        m.SetFloat("_OcclusionStrength", 1f);
        m.EnableKeyword("_OCCLUSIONMAP");
        if (emission)
        {
            m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Emission.png"));
            m.SetColor("_EmissionColor", Color.white * 2.5f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.SetColor("_EmissionColor", Color.black);
            m.DisableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
    }

    public void Execute(ExecutionResult result)
    {
        if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
        if (!AssetDatabase.IsValidFolder(Root + "/Prefabs")) AssetDatabase.CreateFolder(Root, "Prefabs");

        string t = Root + "/Textures/";
        Tex(t + "T_DRL_DrillVehicle_BaseColor.png", true, false, 2048, 1024);
        Tex(t + "T_DRL_DrillVehicle_Normal.png", false, true, 2048, 1024);
        Tex(t + "T_DRL_DrillVehicle_Mask.png", false, false, 2048, 1024);
        Tex(t + "T_DRL_DrillVehicle_Emission.png", true, false, 1024, 512);
        Tex(t + "T_DRL_Drill_Standard_BaseColor.png", true, false, 1024, 512);
        Tex(t + "T_DRL_Drill_Standard_Normal.png", false, true, 1024, 512);
        Tex(t + "T_DRL_Drill_Standard_Mask.png", false, false, 1024, 512);

        Model(Root + "/Meshes/SM_DRL_DrillVehicle.fbx");
        Model(Root + "/Meshes/SM_DRL_Drill_Standard.fbx");

        Mat("M_DRL_DrillVehicle", "T_DRL_DrillVehicle", true);
        Mat("M_DRL_Drill_Standard", "T_DRL_Drill_Standard", false);
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        foreach (var p in new[] { Root + "/Meshes/SM_DRL_DrillVehicle.fbx", Root + "/Meshes/SM_DRL_Drill_Standard.fbx" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            sb.AppendLine("== " + p);
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
            {
                var mf = tr.GetComponent<MeshFilter>();
                sb.AppendLine(string.Format("{0} parent={1} world={2} localRot={3} scale={4} {5}", tr.name,
                    tr.parent ? tr.parent.name : "-", tr.position.ToString("F3"), tr.localEulerAngles.ToString("F1"),
                    tr.localScale.ToString("F2"),
                    mf && mf.sharedMesh ? ("mesh " + mf.sharedMesh.name + " b=" + mf.sharedMesh.bounds.size.ToString("F2") + " c=" + mf.sharedMesh.bounds.center.ToString("F2")) : ""));
            }
        }
        result.Log(sb.ToString());
    }
}
