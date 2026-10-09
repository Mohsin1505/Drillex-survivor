using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System.Text;

// Mineable rocks (RockZ*) restyled as faceted sandstone ore boulders from the DesertKit.
// Visual swap only: the root keeps Rock, its collider, tag, layer and health bar; its old MeshRenderer is switched off
// and a kit boulder ("Visual_Ore") fills the same bounds. Slightly see-through so the train stays visible while drilling.
internal class CommandScript : IRunCommand
{
    const string Dir = "Assets/Art/Environment/DesertLevel/Materials";

    Material Ore(string name, Color tint, float alpha, Color emission)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/DesertKit/Textures/T_DesertKit_Palette.png"));
        m.SetColor("_BaseColor", new Color(tint.r, tint.g, tint.b, alpha));
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.25f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", 1f);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_Cull", 2f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = 3000;
        m.SetShaderPassEnabled("DepthOnly", false);
        m.SetShaderPassEnabled("ShadowCaster", true);
        if (emission.maxColorComponent > 0f)
        {
            m.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Environment/DesertKit/Textures/T_DesertKit_Palette.png"));
            m.SetColor("_EmissionColor", emission);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    public void Execute(ExecutionResult result)
    {
        Directory.CreateDirectory(Dir);
        // faint self-glow marks them as mineable ore vs scenery boulders. zone 1: golden sandstone; zone 2: deeper red; zone 3: dark ore with ember glow
        var mats = new[] {
            Ore("M_Ore_Z1", new Color(1.00f, 0.90f, 0.78f), 0.86f, new Color(0.30f, 0.20f, 0.05f)),
            Ore("M_Ore_Z2", new Color(0.95f, 0.68f, 0.58f), 0.86f, new Color(0.40f, 0.14f, 0.05f)),
            Ore("M_Ore_Z3", new Color(0.70f, 0.50f, 0.46f), 0.88f, new Color(0.55f, 0.16f, 0.04f)) };
        var sb = new StringBuilder();
        var paths = AssetDatabase.FindAssets("RockZ t:Prefab", new[] { "Assets/Prefeb" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
        foreach (var path in paths)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            int zone = file.StartsWith("RockZ3") ? 2 : file.StartsWith("RockZ2") ? 1 : 0;
            bool variantB = file.Contains("(2)");
            string boulder = zone == 0 ? (variantB ? "BOULDER_05" : "BOULDER_03") : zone == 1 ? (variantB ? "BOULDER_02" : "BOULDER_01") : (variantB ? "BOULDER_04" : "BOULDER_03");
            var kit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Environment/DesertKit/Prefabs/BOULDERS/" + boulder + ".prefab");

            var root = PrefabUtility.LoadPrefabContents(path);
            var oldMr = root.GetComponent<MeshRenderer>();
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            var old = root.transform.Find("Visual_Ore");
            if (old) Object.DestroyImmediate(old.gameObject);
            oldMr.enabled = false;

            var vis = (GameObject)PrefabUtility.InstantiatePrefab(kit, root.transform);
            vis.name = "Visual_Ore";
            var kb = kit.GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            var tb = mesh.bounds;
            // fill the original rock's footprint; sit slightly lower so the base is buried in the sand
            vis.transform.localRotation = Quaternion.identity;
            vis.transform.localScale = new Vector3(tb.size.x / kb.size.x, tb.size.y / kb.size.y * 1.05f, tb.size.z / kb.size.z);
            vis.transform.localPosition = new Vector3(tb.center.x, tb.center.y - tb.extents.y - 0.04f, tb.center.z);
            foreach (var r in vis.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = mats[zone];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (var t in vis.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            var w = Vector3.Scale(tb.size, root.transform.localScale);
            sb.AppendLine(file + " zone" + (zone + 1) + " -> " + boulder + " world size " + w.ToString("F1") + " collider " + root.GetComponent<Collider>().GetType().Name);
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        result.Log(sb.ToString());
    }
}
