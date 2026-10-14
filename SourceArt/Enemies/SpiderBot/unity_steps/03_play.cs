using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.Log("already playing"); return; }
        var s = EditorSceneManager.GetActiveScene();
        if (s.name != "Lev1" || s.isDirty) { result.Log("SKIP scene {0} dirty {1}", s.name, s.isDirty); return; }
        Application.runInBackground = true;
        EditorApplication.isPlaying = true;
        result.Log("entering play mode");
    }
}
