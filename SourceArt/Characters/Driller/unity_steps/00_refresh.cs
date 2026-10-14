using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        result.Log("refreshed");
    }
}