using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = EditorSceneManager.GetActiveScene();
        if (s.path != "Assets/Scenes/Lev1.unity") EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
        result.Log("entering play mode");
    }
}
