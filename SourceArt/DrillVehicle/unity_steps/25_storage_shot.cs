using UnityEngine;
using UnityEditor;
using System.Linq;
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
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var blocks = Object.FindObjectsByType<StorageBlock>(FindObjectsInactive.Exclude).Select(b => b.transform).ToList();
        Vector3 mid = tm.transform.position;
        foreach (var b in blocks) mid += b.position;
        mid /= (blocks.Count + 1);
        var go = new GameObject("tmp_review_cam");
        var c = go.AddComponent<Camera>();
        c.CopyFrom(Camera.main);
        c.fieldOfView = 35;
        var t = tm.transform;
        go.transform.position = mid + t.right * 6.5f + t.forward * 1.5f + Vector3.up * 3.2f;
        go.transform.LookAt(mid + Vector3.up * 0.1f);
        Shot(c, @"D:\Drillex_StorageBlock\_preview\unity_storage_train.png");
        go.transform.position = blocks[0].position + t.right * 2.6f - t.forward * 2.2f + Vector3.up * 1.6f;
        go.transform.LookAt(blocks[0].position + Vector3.up * 0.0f);
        Shot(c, @"D:\Drillex_StorageBlock\_preview\unity_storage_close.png");
        Object.DestroyImmediate(go);
        result.Log("shots saved at time {0}", Time.time);
    }
}
