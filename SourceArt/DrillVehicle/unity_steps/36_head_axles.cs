using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// Drill head: 8 wheel renderers were merged into 4 axle wheel-pairs (shared mesh).
// Re-point materials, the wheel animator and the LODGroup on DRL_DrillVehicle_Visual.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string path = "Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Visual.prefab";
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_DrillVehicle/Materials/M_DRL_DrillVehicle.mat");
        var root = PrefabUtility.LoadPrefabContents(path);

        // drop overrides left on the removed Wheel_* objects
        PrefabUtility.RemoveUnusedOverrides(new[] { root }, InteractionMode.AutomatedAction);

        var drillRenderers = root.GetComponentsInChildren<Drill>(true).SelectMany(d => d.GetComponentsInChildren<Renderer>(true)).ToList();
        var mine = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => !drillRenderers.Contains(r)).ToArray();
        foreach (var r in mine) r.sharedMaterial = mat;

        var axles = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Axle_")).OrderBy(t => t.name).ToArray();
        var anim = root.GetComponent<DrillVehicleAnimator>();
        anim.wheels = axles;

        var lod0 = mine.Where(r => !r.name.EndsWith("_L1")).Cast<Renderer>().ToArray();
        var lod1 = mine.Where(r => r.name.EndsWith("_L1") || r.name.StartsWith("Axle_")).Cast<Renderer>().ToArray();
        var group = root.GetComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(0.05f, lod0), new LOD(0.006f, lod1) });
        group.RecalculateBounds();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.AppendLine("wheels -> " + string.Join(",", axles.Select(a => a.name)));
        sb.AppendLine("LOD0 " + string.Join(",", lod0.Select(r => r.name)));
        sb.AppendLine("LOD1 " + string.Join(",", lod1.Select(r => r.name)));
        sb.AppendLine("leftover Wheel_ objects: " + root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Wheel_")));
        PrefabUtility.UnloadPrefabContents(root);
        result.Log(sb.ToString());
    }
}
