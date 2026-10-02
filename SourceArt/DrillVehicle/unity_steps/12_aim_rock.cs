using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode only: point the train at the nearest rock so the drill hits it.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var t = GameObject.Find("DRL_DrillVehicle_Train").transform;
        var rock = Object.FindObjectsByType<Rock>(FindObjectsInactive.Exclude).OrderBy(r => Vector3.Distance(r.transform.position, t.position)).First();
        var col = rock.GetComponent<Collider>();
        Vector3 target = col.bounds.center; target.y = t.position.y;
        Vector3 dir = (target - t.position); dir.y = 0;
        t.rotation = Quaternion.LookRotation(dir.normalized);
        Vector3 edge = col.ClosestPoint(t.position);
        t.position = new Vector3(edge.x, t.position.y, edge.z) - t.forward * 3f;
        result.Log("aimed at {0} bounds {1}", rock.name, col.bounds.size);
    }
}
