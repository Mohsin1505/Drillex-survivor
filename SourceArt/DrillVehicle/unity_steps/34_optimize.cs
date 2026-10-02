using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// Mobile optimization pass for the drill head and the storage car:
//  - mesh compression + verified import settings on both FBX files
//  - storage car: materials on the new LOD1 meshes + LODGroup
//  - head light: keep the headlight (it lights the dark level) but no realtime shadows
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        foreach (var p in new[] { "Assets/Art/Vehicles/DRL_DrillVehicle/Meshes/SM_DRL_DrillVehicle.fbx",
                                  "Assets/Art/Vehicles/DRL_DrillVehicle/Meshes/SM_DRL_Drill_Standard.fbx",
                                  "Assets/Art/Vehicles/DRL_StorageBlock/Meshes/SM_DRL_StorageBlock.fbx" })
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(p);
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.isReadable = false;
            mi.addCollider = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.SaveAndReimport();
            sb.AppendLine(System.IO.Path.GetFileName(p) + ": compression=" + mi.meshCompression + " readWrite=" + mi.isReadable + " colliders=" + mi.addCollider + " anim=" + mi.animationType);
        }

        // ---- storage car LOD ----
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_StorageBlock/Materials/M_DRL_StorageBlock.mat");
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefeb/Storage.prefab");
        var vis = root.transform.Find("Visual").gameObject;
        foreach (var r in vis.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = mat;
        var all = vis.GetComponentsInChildren<MeshRenderer>(true);
        var lod0 = all.Where(r => !r.name.EndsWith("_L1")).Cast<Renderer>().ToArray();
        var lod1 = all.Where(r => r.name.EndsWith("_L1")).Cast<Renderer>().ToArray();
        var group = vis.GetComponent<LODGroup>();
        if (group == null) group = vis.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.04f, lod0), new LOD(0.005f, lod1) });
        group.fadeMode = LODFadeMode.None;
        group.RecalculateBounds();
        // roll both LOD levels of each wheelset (LOD1 wheels are children of the LOD0 axles)
        sb.AppendLine("storage LOD0 renderers " + lod0.Length + " (" + string.Join(",", lod0.Select(x => x.name)) + "), LOD1 " + lod1.Length);
        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefeb/Storage.prefab");
        PrefabUtility.UnloadPrefabContents(root);

        // ---- head light ----
        var train = PrefabUtility.LoadPrefabContents("Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Train.prefab");
        var light = train.GetComponentsInChildren<Light>(true).First();
        sb.AppendLine("head light before: shadows=" + light.shadows + " renderMode=" + light.renderMode + " range=" + light.range);
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        PrefabUtility.SaveAsPrefabAsset(train, "Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Train.prefab");
        PrefabUtility.UnloadPrefabContents(train);
        sb.AppendLine("head light after: shadows=None");

        // ---- texture audit ----
        foreach (var g in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Vehicles" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            var a = ti.GetPlatformTextureSettings("Android");
            sb.AppendLine(System.IO.Path.GetFileName(p) + ": max " + ti.maxTextureSize + " mip " + ti.mipmapEnabled + " rw " + ti.isReadable + " android " + (a.overridden ? a.format + "/" + a.maxTextureSize : "none"));
        }
        AssetDatabase.SaveAssets();
        result.Log(sb.ToString());
    }
}
