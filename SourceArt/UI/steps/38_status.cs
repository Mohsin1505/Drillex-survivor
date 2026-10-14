using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("STATUS playing={0} compiling={1} updating={2}", EditorApplication.isPlaying, EditorApplication.isCompiling, EditorApplication.isUpdating);
    }
}
