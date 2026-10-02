using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode: attach the machine gun (Gatling) car through the shop path, then report turret/firing/wheels.
internal class CommandScript : IRunCommand
{
    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }

    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true;
        foreach (var h in Object.FindObjectsByType<HelpPanelManager>(FindObjectsInactive.Include)) h.CloseHelp();
        Time.timeScale = 1f;
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var um = Object.FindAnyObjectByType<UpgradeManager>();
        var car = GameObject.Find("rocket launcher(Clone)");
        if (car == null)
        {
            tm.AddTrainBlock(um.storageBlockPrefab);
            tm.AddTrainBlock(um.rocketGunPrefab);
            result.Log("attached storage + rocket launcher car at t {0}", Time.time);
            return;
        }
        var gun = car.GetComponentInChildren<AutoGun>(false);
        var wheel = car.GetComponent<TrainWheelRoller>().wheels[0];
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        var near = enemies.OrderBy(e => Vector3.Distance(e.transform.position, car.transform.position)).FirstOrDefault();
        var bullets = Object.FindObjectsByType<RocketBullet>(FindObjectsInactive.Exclude);
        var nearMuzzle = bullets.Count(b => Vector3.Distance(b.transform.position, gun.shootPoint.position) < 3f);
        result.Log("t {0} dist-to-head {1} AutoGun on {2} ({3}, rate {4}) turretYaw {5} enemies {6} nearest {7} bullets {8} (near muzzle {9}) muzzle {10} muzzleFwd·turretFwd {11} wheelX {12}",
            Time.time.ToString("F1"), Vector3.Distance(car.transform.position, tm.transform.position).ToString("F2"), gun.name, gun.weaponType, gun.fireRate,
            gun.transform.eulerAngles.y.ToString("F0"), enemies.Length, near ? Vector3.Distance(near.transform.position, car.transform.position).ToString("F1") : "-",
            bullets.Length, nearMuzzle, gun.shootPoint.position.ToString("F2"), Vector3.Dot(gun.shootPoint.forward, gun.transform.forward).ToString("F2"),
            wheel.localEulerAngles.x.ToString("F0"));
        if (near && Vector3.Distance(near.transform.position, car.transform.position) < 20f)
        {
            var go = new GameObject("tmp_review_cam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(Camera.main);
            cam.fieldOfView = 38;
            var t = car.transform;
            go.transform.position = t.position + t.right * 2.8f - t.forward * 2.0f + Vector3.up * 1.7f;
            go.transform.LookAt(t.position + Vector3.up * 0.5f);
            Shot(cam, "D:/Drillex/SourceArt/RocketCar/_preview/unity_rocket_close.png");
            go.transform.position = t.position + t.right * 7.5f + t.forward * 1.5f + Vector3.up * 3.2f;
            go.transform.LookAt(t.position + t.forward * 1.5f + Vector3.up * 0.3f);
            Shot(cam, "D:/Drillex/SourceArt/RocketCar/_preview/unity_rocket_train.png");
            Object.DestroyImmediate(go);
            result.Log("SHOTS saved");
        }
    }
}
