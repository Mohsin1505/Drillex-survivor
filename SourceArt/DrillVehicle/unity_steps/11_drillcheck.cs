using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var t = GameObject.Find("DRL_DrillVehicle_Train").transform;
        var tm = t.GetComponent<TrainMovement>();
        var th = t.GetComponent<TrainHealth>();
        var rocks = Object.FindObjectsByType<Rock>(FindObjectsInactive.Exclude);
        var nearest = rocks.OrderBy(r => Vector3.Distance(r.transform.position, t.position)).FirstOrDefault();
        result.Log("time {0} pos {1} speed {2} rocks {3} nearest {4} dist {5}", Time.time, t.position, tm.currentSpeed, rocks.Length,
            nearest ? nearest.name : "-", nearest ? Vector3.Distance(nearest.transform.position, t.position) : -1);
        var blocks = Object.FindObjectsByType<StorageBlock>(FindObjectsInactive.Exclude).Length;
        result.Log("storage blocks {0} orbs {1}", blocks, Manager.instance ? Manager.instance.permanentOrb : -1);
    }
}
