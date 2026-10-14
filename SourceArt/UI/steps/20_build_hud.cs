using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
internal class CommandScript : IRunCommand
{
    // Editor builders live in Assembly-CSharp-Editor, which this dynamic script can't reference directly.
    static void Call(string type, string method)
    {
        var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(type)).First(x => x != null);
        t.GetMethod(method).Invoke(null, null);
    }
    public void Execute(ExecutionResult result)
    {
        Call("DrillexSettingsBuilder", "Build");
        Call("DrillexHudBuilder", "Build");
        result.Log("built HUD");
    }
}
