using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var sm = tm.GetComponent<TrainStorageManager>();
        result.Log("time {0} head {1} totalStorage {2} orb {3}", Time.time, tm.transform.position.ToString("F2"), sm.totalStorage, sm.currentOrb);
        foreach (var b in Object.FindObjectsByType<StorageBlock>(FindObjectsInactive.Exclude))
        {
            var c = b.GetComponent<BoxCollider>();
            result.Log("{0} pos {1} rot {2} dist-to-head {3} collider {4} damagePoint {5}", b.name, b.transform.position.ToString("F2"), b.transform.eulerAngles.y.ToString("F0"),
                Vector3.Distance(b.transform.position, tm.transform.position).ToString("F2"), c.bounds.size.ToString("F2"), b.GetComponent<TrainDamagePoint>() != null);
        }
    }
}
