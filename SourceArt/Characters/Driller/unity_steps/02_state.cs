using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var t = System.Type.GetType("DrillexCrewSetup, Assembly-CSharp-Editor");
        result.Log("playing {0} compiling {1} crewType {2} scene {3}", EditorApplication.isPlaying, EditorApplication.isCompiling, t, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
