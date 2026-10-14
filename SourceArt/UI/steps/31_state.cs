using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
        result.Log("playing {0} paused {1} frame {2} t {3} scale {4} scene {5}", EditorApplication.isPlaying, EditorApplication.isPaused, Time.frameCount, Time.time, Time.timeScale, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
