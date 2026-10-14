using UnityEngine;
using UnityEditor;
using UnityEditor.Compilation;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("compiling {0} updating {1}", EditorApplication.isCompiling, EditorApplication.isUpdating);
        var t = System.Type.GetType("DrillexCrewSetup, Assembly-CSharp-Editor");
        result.Log("type {0}", t);
        CompilationPipeline.RequestScriptCompilation();
    }
}
