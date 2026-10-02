using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var hw = tm.GetComponentsInChildren<Transform>().First(t => t.name == "Wheel_L1");
        result.Log("t {0} fps~{1} head wheel x {2}", Time.time.ToString("F2"), (1f / Time.smoothDeltaTime).ToString("F0"), hw.localEulerAngles.x.ToString("F1"));
        foreach (var b in Object.FindObjectsByType<TrainWheelRoller>(FindObjectsInactive.Exclude))
            result.Log("{0} axle1 rot {1}", b.name, b.wheels[0].localEulerAngles.ToString("F1"));
    }
}
