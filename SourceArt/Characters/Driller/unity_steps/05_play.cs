using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.Log("already playing"); return; }
        if (EditorSceneManager.GetActiveScene().name != "MainMenu") { result.Log("not on MainMenu"); return; }
        PlayerPrefs.DeleteKey("crew.name"); PlayerPrefs.DeleteKey("crew.colourway"); PlayerPrefs.DeleteKey("crew.skin"); PlayerPrefs.DeleteKey("crew.headgear");
        Application.runInBackground = true;
        EditorApplication.isPlaying = true;
        result.Log("entering play mode");
    }
}
