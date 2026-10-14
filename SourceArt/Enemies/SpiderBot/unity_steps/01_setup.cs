using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.Log("SKIP: playing"); return; }
        DrillexSpiderSetup.Run();
        var objs = AssetDatabase.LoadAllAssetsAtPath(DrillexSpiderSetup.FbxPath);
        result.Log("clips: {0}", string.Join(", ", objs.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).Select(c => c.name + ":" + c.length.ToString("0.00") + (c.isLooping ? "L" : ""))));
        result.Log("meshes: {0}", string.Join(", ", objs.OfType<UnityEngine.Mesh>().Select(m => m.name + " v" + m.vertexCount + " t" + m.triangles.Length / 3 + " bones" + m.bindposes.Length)));
        var vis = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSpiderSetup.VisualPath);
        result.Log("visual muzzle {0}", vis.GetComponent<SpiderBotVisual>().muzzle);
        foreach (var v in DrillexSpiderSetup.Variants) { var p = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSpiderSetup.EnemyPath(v)); result.Log("{0}: {1} variantOf {2}", v, p, PrefabUtility.GetCorrespondingObjectFromSource(p)); }
    }
}
