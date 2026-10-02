using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text;

// Desert kit: palette texture + one shared URP/Lit material, model import settings for every SM_*.fbx,
// and one static prefab per asset (Prefabs/<COLLECTION>/<NAME>.prefab). Decorative: no colliders.
internal class CommandScript : IRunCommand
{
    const string Root = "Assets/Art/Environment/DesertKit";

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        AssetDatabase.Refresh();
        foreach (var f in new[] { "Materials", "Prefabs" })
            if (!AssetDatabase.IsValidFolder(Root + "/" + f)) AssetDatabase.CreateFolder(Root, f);

        // palette: 16 px swatches sampled at their centres -> point filter, mips ok, near-lossless compression
        string texPath = Root + "/Textures/T_DesertKit_Palette.png";
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        ti.textureType = TextureImporterType.Default;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = true;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 256;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        foreach (var plat in new[] { "Android", "iPhone" })
        {
            var s = ti.GetPlatformTextureSettings(plat);
            s.overridden = true;
            s.maxTextureSize = 256;
            s.format = TextureImporterFormat.ASTC_4x4;
            ti.SetPlatformTextureSettings(s);
        }
        ti.SaveAndReimport();

        string mpath = Root + "/Materials/M_DesertKit.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mpath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, mpath);
        }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.1f);
        m.SetFloat("_EnvironmentReflections", 0f);
        m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);

        var fbxs = AssetDatabase.FindAssets("t:Model", new[] { Root + "/Meshes" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        int tris = 0;
        foreach (var path in fbxs)
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
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;   // no normal map on the kit
            mi.addCollider = false;
            mi.SaveAndReimport();

            string coll = Path.GetFileName(Path.GetDirectoryName(path));
            string name = Path.GetFileNameWithoutExtension(path).Substring(3);   // drop "SM_"
            string pdir = Root + "/Prefabs/" + coll;
            if (!AssetDatabase.IsValidFolder(pdir)) AssetDatabase.CreateFolder(Root + "/Prefabs", coll);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = new GameObject(name);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, go.transform);
            vis.name = "Visual";
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = m;
                r.shadowCastingMode = coll == "VEGETATION" ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ContributeGI);
            PrefabUtility.SaveAsPrefabAsset(go, pdir + "/" + name + ".prefab");
            Object.DestroyImmediate(go);

            var mf = model.GetComponentInChildren<MeshFilter>();
            int t3 = mf.sharedMesh.triangles.Length / 3;
            tris += t3;
            var root = model.transform;
            sb.AppendLine(string.Format("{0}/{1} tris={2} b={3} c={4} rot={5}", coll, name, t3,
                mf.sharedMesh.bounds.size.ToString("F2"), mf.sharedMesh.bounds.center.ToString("F2"), mf.transform.localEulerAngles.ToString("F0")));
        }
        AssetDatabase.SaveAssets();
        result.Log("models " + fbxs.Length + " total tris " + tris + "\n" + sb);
    }
}
