using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var car = GameObject.Find("Double  barrel gun(Clone)");
        if (!car) { result.Log("no double gun car"); return; }
        var guns = car.GetComponentsInChildren<AutoGun>(false);
        var turret = guns[0].transform;
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        var near = enemies.OrderBy(e => Vector3.Distance(e.transform.position, car.transform.position)).FirstOrDefault();
        var bullets = Object.FindObjectsByType<Bullet>(FindObjectsInactive.Exclude);
        result.Log("t {0} dist-to-head {1} activeAutoGuns {2} on {3} turretYaw {4} enemies {5} nearest {6} bullets {7} dmgPoint {8} trigger {9}",
            Time.time.ToString("F1"), Vector3.Distance(car.transform.position, tm.transform.position).ToString("F2"),
            guns.Length, string.Join("+", guns.Select(g => g.name)), turret.eulerAngles.y.ToString("F0"), enemies.Length,
            near ? Vector3.Distance(near.transform.position, car.transform.position).ToString("F1") : "-", bullets.Length,
            car.GetComponent<TrainDamagePoint>() != null, car.GetComponent<BoxCollider>().isTrigger);
        var m = guns.Select(g => g.shootPoint).ToArray();
        result.Log("muzzles {0} / {1} angle-between {2} separation {3} both-children-of-turret {4}",
            m[0].position.ToString("F2"), m[1].position.ToString("F2"), Vector3.Angle(m[0].forward, m[1].forward).ToString("F2"),
            Vector3.Distance(m[0].position, m[1].position).ToString("F2"), m.All(x => x.IsChildOf(turret)));
        foreach (var b in bullets.Take(2)) result.Log("bullet pos {0}", b.transform.position.ToString("F1"));
    }
}
