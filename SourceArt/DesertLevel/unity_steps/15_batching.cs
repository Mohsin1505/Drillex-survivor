using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.iOS, NamedBuildTarget.Standalone })
        {
            bool before = PlayerSettings.GetStaticBatchingForPlatform(t == NamedBuildTarget.Android ? BuildTarget.Android : t == NamedBuildTarget.iOS ? BuildTarget.iOS : BuildTarget.StandaloneWindows64);
            sb.Append(t.TargetName + " static batching was " + before + "; ");
        }
        result.Log(sb.ToString());
    }
}
