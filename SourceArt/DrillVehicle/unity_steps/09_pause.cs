using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("paused {0} frame {1} realtime {2} runInBg {3} focused {4}", EditorApplication.isPaused, Time.frameCount, Time.realtimeSinceStartup, Application.runInBackground, UnityEditorInternal.InternalEditorUtility.isApplicationActive);
        EditorApplication.isPaused = false;
        Application.runInBackground = true;
    }
}
