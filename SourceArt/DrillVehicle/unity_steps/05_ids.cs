using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var t = GameObject.Find("DRL_DrillVehicle_Train");
        result.Log("train id {0}", t.GetInstanceID());
        result.Log("cam id {0}", Camera.main ? Camera.main.GetInstanceID() : 0);
        result.Log("cam pos {0} target dist", Camera.main.transform.position);
    }
}
