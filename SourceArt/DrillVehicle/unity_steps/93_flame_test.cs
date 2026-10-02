using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode: attach the flamethrower car through the shop path, report turret/flame/wheels, screenshot while firing.
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
        var car = GameObject.Find("FlameThrower(Clone)");
        if (car == null)
        {
            tm.AddTrainBlock(um.storageBlockPrefab);
            tm.AddTrainBlock(um.flameThrowerPrefab);
            result.Log("attached storage + flamethrower car at t {0}", Time.time);
            return;
        }
        var flames = car.GetComponentsInChildren<FlameThrower>(false);
        var turret = flames.First(f => f.name == "Turret").transform;
        var ps = flames[0].flameEffect;
        var wheel = car.GetComponent<TrainWheelRoller>().wheels[0];
        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        var near = enemies.OrderBy(e => Vector3.Distance(e.transform.position, car.transform.position)).FirstOrDefault();
        float dist = near ? Vector3.Distance(near.transform.position, car.transform.position) : -1f;
        result.Log("t {0} dist-to-head {1} FlameThrowers {2} on {3} turretYaw {4} enemies {5} nearest {6} flamePlaying {7} particles {8} wheelX {9}",
            Time.time.ToString("F1"), Vector3.Distance(car.transform.position, tm.transform.position).ToString("F2"), flames.Length,
            string.Join("+", flames.Select(f => f.name)), turret.eulerAngles.y.ToString("F0"), enemies.Length, dist.ToString("F1"),
            ps.isPlaying, ps.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount), wheel.localEulerAngles.x.ToString("F0"));
        if (ps.isPlaying && ps.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount) > 5)
        {
            var go = new GameObject("tmp_review_cam");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(Camera.main);
            cam.fieldOfView = 45;
            var t = car.transform;
            go.transform.position = t.position + t.right * 3.4f - t.forward * 2.4f + Vector3.up * 2.0f;
            go.transform.LookAt(t.position + turret.forward * 1.2f + Vector3.up * 0.5f);
            Shot(cam, "D:/Drillex/SourceArt/FlameCar/_preview/unity_flame_close.png");
            go.transform.position = t.position + t.right * 8f + t.forward * 1.5f + Vector3.up * 3.5f;
            go.transform.LookAt(t.position + t.forward * 1.5f + Vector3.up * 0.3f);
            Shot(cam, "D:/Drillex/SourceArt/FlameCar/_preview/unity_flame_train.png");
            Object.DestroyImmediate(go);
            result.Log("SHOTS saved while flaming");
        }
    }
}
