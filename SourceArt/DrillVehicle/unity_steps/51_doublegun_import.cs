using UnityEngine;
using UnityEditor;
using System.Text;

// Double gun car: import settings, URP/Lit material (mask: R metallic, G AO, A smoothness).
internal class CommandScript : IRunCommand
{
    const string Root = "Assets/Art/Vehicles/DRL_DoubleGunCar";

    void Tex(string path, bool srgb, bool normal, int max, int mobileMax)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = srgb;
        ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.mipmapEnabled = true;
        ti.maxTextureSize = max;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.anisoLevel = 2;
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

    public void Execute(ExecutionResult result)
    {
        if (!AssetDatabase.IsValidFolder(Root + "/Materials")) AssetDatabase.CreateFolder(Root, "Materials");
        string t = Root + "/Textures/T_DRL_DoubleGunCar_";
        Tex(t + "BaseColor.png", true, false, 1024, 1024);
        Tex(t + "Normal.png", false, true, 1024, 1024);
        Tex(t + "Mask.png", false, false, 1024, 1024);
        Tex(t + "Emission.png", true, false, 1024, 512);

        var mi = (ModelImporter)AssetImporter.GetAtPath(Root + "/Meshes/SM_DRL_DoubleGunCar.fbx");
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
        mi.meshCompression = ModelImporterMeshCompression.Medium;
        mi.importNormals = ModelImporterNormals.Import;
        mi.importTangents = ModelImporterTangents.CalculateMikk;
        mi.addCollider = false;
        mi.SaveAndReimport();

        string mpath = Root + "/Materials/M_DRL_DoubleGunCar.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mpath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, mpath);
        }
        var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Mask.png");
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Normal.png"));
        m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap", mask);
        m.SetFloat("_Metallic", 1f);
        m.SetFloat("_Smoothness", 1f);
        m.SetFloat("_SmoothnessTextureChannel", 0f);
        m.EnableKeyword("_METALLICSPECGLOSSMAP");
        m.SetTexture("_OcclusionMap", mask);
        m.SetFloat("_OcclusionStrength", 1f);
        m.EnableKeyword("_OCCLUSIONMAP");
        m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(t + "Emission.png"));
        m.SetColor("_EmissionColor", Color.white * 2.5f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Meshes/SM_DRL_DoubleGunCar.fbx");
        foreach (var tr in go.GetComponentsInChildren<Transform>(true))
        {
            var mf = tr.GetComponent<MeshFilter>();
            sb.AppendLine(string.Format("{0} pos={1} rot={2} {3}", tr.name, tr.localPosition.ToString("F2"), tr.localEulerAngles.ToString("F0"),
                mf ? "mesh b=" + mf.sharedMesh.bounds.size.ToString("F2") + " c=" + mf.sharedMesh.bounds.center.ToString("F2") : ""));
        }
        result.Log(sb.ToString());
    }
}
