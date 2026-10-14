using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        int n = 0;
        foreach (var w in Object.FindObjectsByType<SmallWorm>(FindObjectsSortMode.None))
            if (w.GetComponentInChildren<SpiderBotVisual>() != null && n < 4) { w.TakeDamage(99999f); n++; }
        result.Log("killed {0}", n);
    }
}
