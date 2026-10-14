using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Debug.ClearDeveloperConsole();
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
        logEntries?.GetMethod("Clear")?.Invoke(null, null);
        EditorApplication.isPlaying = true;
        result.Log("entering play mode");
    }
}
