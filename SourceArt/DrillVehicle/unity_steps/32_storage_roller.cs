using UnityEngine;
using UnityEditor;
using System.Linq;

// Add TrainWheelRoller to Storage.prefab, driving the 4 wheelsets of the storage car model.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        const string path = "Assets/Prefeb/Storage.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var roller = root.GetComponent<TrainWheelRoller>();
        if (roller == null) roller = root.AddComponent<TrainWheelRoller>();
        roller.wheels = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Axle_")).OrderBy(t => t.name).ToArray();
        roller.wheelRadius = 0.17f;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        result.Log("wheels: {0}", string.Join(",", roller.wheels.Select(w => w.name)));
        PrefabUtility.UnloadPrefabContents(root);
    }
}
