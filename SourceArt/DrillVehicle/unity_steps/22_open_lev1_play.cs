using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
        logEntries?.GetMethod("Clear")?.Invoke(null, null);
        EditorApplication.isPlaying = true;
        result.Log("Lev1 opened, entering play mode");
    }
}
