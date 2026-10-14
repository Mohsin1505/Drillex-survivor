using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
internal class CommandScript : IRunCommand
{
    static void Call(string type, string method)
    {
        var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(type)).First(x => x != null);
        t.GetMethod(method).Invoke(null, null);
    }
    public void Execute(ExecutionResult result)
    {
        Call("DrillexHudBuilder", "OpenMenu");
        result.Log("done DrillexHudBuilder.OpenMenu");
    }
}
