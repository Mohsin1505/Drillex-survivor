using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("playing {0} time {1}", EditorApplication.isPlaying, Time.time);
        var t = GameObject.Find("DRL_DrillVehicle_Train");
        if (t == null) { result.Log("train not found"); return; }
        var tm = t.GetComponent<TrainMovement>();
        result.Log("train pos {0} speed {1}", t.transform.position, tm.currentSpeed);
        var turret = t.GetComponentsInChildren<Transform>().First(x => x.name == "Turret");
        var rotor = t.GetComponentsInChildren<Transform>().First(x => x.name == "DrillRotor");
        var w = t.GetComponentsInChildren<Transform>().First(x => x.name == "Wheel_L1");
        result.Log("turret yaw {0} rotor z {1} wheel x {2}", turret.eulerAngles.y, rotor.localEulerAngles.z, w.localEulerAngles.x);
        var lod = t.GetComponentInChildren<LODGroup>();
        result.Log("cam {0}", Camera.main ? Camera.main.transform.position.ToString() : "none");
    }
}
