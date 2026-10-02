using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// Player train stays close to the camera: no LODs. Remove LODGroups and stale overrides
// from the drill module, head visual and storage car prefabs (LOD1 meshes were removed from the FBX files).
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        foreach (var path in new[] { "Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_Drill_Standard.prefab",
                                     "Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Visual.prefab",
                                     "Assets/Prefeb/Storage.prefab" })
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            int removed = 0;
            foreach (var g in root.GetComponentsInChildren<LODGroup>(true))
            {
                // only the group owned by this prefab; nested drill groups are handled in their own prefab
                if (PrefabUtility.IsPartOfPrefabInstance(g) && !PrefabUtility.IsAddedComponentOverride(g)) continue;
                Object.DestroyImmediate(g);
                removed++;
            }
            var instances = root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject)
                .Where(go => PrefabUtility.IsAnyPrefabInstanceRoot(go)).ToArray();
            if (instances.Length > 0) PrefabUtility.RemoveUnusedOverrides(instances, InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            var leftover = root.GetComponentsInChildren<Transform>(true).Count(t => t.name.EndsWith("_L1"));
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
            sb.AppendLine(path + ": LODGroups removed " + removed + ", _L1 objects left " + leftover + ", renderers " + renderers);
            PrefabUtility.UnloadPrefabContents(root);
        }
        var trainLods = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Train.prefab").GetComponentsInChildren<LODGroup>(true).Length;
        var storageLods = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Storage.prefab").GetComponentsInChildren<LODGroup>(true).Length;
        sb.AppendLine("LODGroups remaining: train " + trainLods + ", storage " + storageLods);
        result.Log(sb.ToString());
    }
}
