using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; result.Log("stopping"); return; }
        EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity");
        result.Log("back on Lev1");
    }
}
