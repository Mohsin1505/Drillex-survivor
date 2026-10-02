using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;

// Step 31: make the drill vehicle the live head in Lev1 (scene overrides kept).
// prefab for DRL_DrillVehicle_Train (scene overrides are kept). Lev1 itself is not modified.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string src = "Assets/Scenes/Lev1.unity";
        const string dst = "Assets/Scenes/Lev1.unity";
        if (false)
            AssetDatabase.CopyAsset(src, dst);

        // discard the temporary edits made in the open menu scene and open the test scene
        var scene = EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);

        var newTrain = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_DrillVehicle/Prefabs/DRL_DrillVehicle_Train.prefab");
        var tm = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TrainMovement>(true)).First();
        var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(tm.gameObject);
        sb.AppendLine("train instance " + inst.name + " at " + inst.transform.position);
        PrefabUtility.ReplacePrefabAssetOfPrefabInstance(inst, newTrain, InteractionMode.AutomatedAction);
        inst.name = "DRL_DrillVehicle_Train";

        var camTarget = inst.GetComponentsInChildren<Transform>(true).First(t => t.name == "Socket_CameraTarget");
        foreach (var cam in Object.FindObjectsByType<CameraMovement>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(cam, "camera target");
            cam.target = camTarget;
            sb.AppendLine("camera target -> " + camTarget.name);
        }

        // sanity report
        var drill = inst.GetComponentInChildren<Drill>(false);
        var gun = inst.GetComponentsInChildren<AutoGun>(false);
        sb.AppendLine("active Drill on: " + (drill ? drill.name : "none") + " trainMovement=" + (drill && drill.trainMovement ? drill.trainMovement.name : "null"));
        foreach (var g in gun) sb.AppendLine("active AutoGun on: " + g.name + " shootPoint=" + (g.shootPoint ? g.shootPoint.name : "null") + " bullet=" + (g.bulletPrefab ? g.bulletPrefab.name : "null"));
        var r = inst.GetComponentsInChildren<Renderer>(false).Where(x => x is MeshRenderer).ToArray();
        var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds);
        sb.AppendLine("visual bounds size " + b.size.ToString("F2") + " min.y " + b.min.y.ToString("F2") + " (ground expected ~0) center " + b.center.ToString("F2"));
        sb.AppendLine("root pos " + inst.transform.position.ToString("F2"));

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log(sb.ToString());
    }
}
