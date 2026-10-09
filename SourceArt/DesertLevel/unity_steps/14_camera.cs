using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
// Gameplay camera framing for the desert level: 3/4 chase view (train lower-middle, route + canyon ahead).
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Lev1.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        var cm = Object.FindAnyObjectByType<CameraMovement>();
        cm.startPitch = 24f;
        cm.distance = 16f;
        EditorUtility.SetDirty(cm);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        result.Log("camera startPitch {0} distance {1} target {2}", cm.startPitch, cm.distance, cm.target ? cm.target.name : "null");
    }
}
