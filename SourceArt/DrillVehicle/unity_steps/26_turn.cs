using UnityEngine;
using UnityEditor;
// Play-mode only: steer the head 45 degrees to check that the cars follow the path.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        tm.transform.Rotate(Vector3.up, 45f);
        result.Log("head yaw {0}", tm.transform.eulerAngles.y);
    }
}
