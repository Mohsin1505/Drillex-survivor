using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        DrillexCrewSetup.Run();
        var mi = (ModelImporter)AssetImporter.GetAtPath(DrillexCrewSetup.FbxPath);
        var objs = AssetDatabase.LoadAllAssetsAtPath(DrillexCrewSetup.FbxPath);
        var avatar = objs.OfType<Avatar>().FirstOrDefault();
        result.Log("avatar {0} valid {1} human {2}", avatar, avatar && avatar.isValid, avatar && avatar.isHuman);
        result.Log("clips: {0}", string.Join(", ", objs.OfType<AnimationClip>().Select(c => c.name + ":" + c.length.ToString("0.00") + (c.isLooping ? "L" : ""))));
        result.Log("meshes: {0}", string.Join(", ", objs.OfType<UnityEngine.Mesh>().Select(m => m.name + " v" + m.vertexCount + " t" + m.triangles.Length / 3 + " b" + m.bounds.size)));
        var p = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexCrewSetup.PrefabPath);
        result.Log("prefab {0}", p);
    }
}
