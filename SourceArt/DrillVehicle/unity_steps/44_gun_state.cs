using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var car = GameObject.Find("Basic Gun(Clone)");
        if (!car) { result.Log("no gun car"); return; }
        var guns = car.GetComponentsInChildren<AutoGun>(false);
        var turret = guns[0].transform;
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        var near = enemies.OrderBy(e => Vector3.Distance(e.transform.position, car.transform.position)).FirstOrDefault();
        var bullets = Object.FindObjectsByType<Bullet>(FindObjectsInactive.Exclude);
        result.Log("t {0} car {1} dist-to-head {2} activeAutoGuns {3} turretYaw {4} carYaw {5} enemies {6} nearest {7} bullets {8} dmgPoint {9} trigger {10}",
            Time.time.ToString("F1"), car.transform.position.ToString("F1"), Vector3.Distance(car.transform.position, tm.transform.position).ToString("F2"),
            guns.Length, turret.eulerAngles.y.ToString("F0"), car.transform.eulerAngles.y.ToString("F0"), enemies.Length,
            near ? Vector3.Distance(near.transform.position, car.transform.position).ToString("F1") : "-", bullets.Length,
            car.GetComponent<TrainDamagePoint>() != null, car.GetComponent<BoxCollider>().isTrigger);
        if (bullets.Length > 0) { var b = bullets[0].transform; result.Log("bullet pos {0} fwd {1}", b.position.ToString("F1"), b.forward.ToString("F2")); }
    }
}
