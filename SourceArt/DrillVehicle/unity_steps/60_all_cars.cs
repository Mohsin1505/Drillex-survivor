using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode: unpause, attach one of each car through the shop path, report and screenshot.
internal class CommandScript : IRunCommand
{
    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1600, 600, 24);
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(1600, 600, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 600), 0, 0); tex.Apply();
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
        if (Object.FindObjectsByType<TrainWheelRoller>(FindObjectsInactive.Exclude).Length == 0)
        {
            tm.AddTrainBlock(um.storageBlockPrefab);
            tm.AddTrainBlock(um.basicGunPrefab);
            tm.AddTrainBlock(um.dualGunPrefab);
            tm.AddTrainBlock(um.storageBlockPrefab);
            result.Log("attached 4 cars");
            return;
        }
        var cars = Object.FindObjectsByType<TrainWheelRoller>(FindObjectsInactive.Exclude).Select(r => r.transform).ToList();
        foreach (var c in cars)
            result.Log("{0} dist-to-head {1} damagePoint {2}", c.name, Vector3.Distance(c.position, tm.transform.position).ToString("F2"), c.GetComponent<TrainDamagePoint>() != null);
        result.Log("storage total {0}", tm.GetComponent<TrainStorageManager>().totalStorage);
        Vector3 mid = tm.transform.position - tm.transform.forward * 4f;
        var go = new GameObject("tmp_review_cam");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(Camera.main);
        cam.fieldOfView = 30;
        go.transform.position = mid + tm.transform.right * 9f + Vector3.up * 3.5f + tm.transform.forward * 1f;
        go.transform.LookAt(mid + Vector3.up * 0.3f);
        Shot(cam, "D:/Drillex/SourceArt/_check/unity_train_side.png");
        go.transform.position = mid + tm.transform.right * 3.2f + Vector3.up * 1.7f + tm.transform.forward * 1.2f;
        go.transform.LookAt(mid + Vector3.up * 0.6f + tm.transform.forward * 1.0f);
        Shot(cam, "D:/Drillex/SourceArt/_check/unity_train_close.png");
        Object.DestroyImmediate(go);
        result.Log("screenshots saved at t {0}", Time.time);
    }
}
