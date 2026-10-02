using UnityEngine;
using UnityEditor;
using System.Linq;
// Render the gameplay camera plus a closer chase view of the new train to PNGs.
internal class CommandScript : IRunCommand
{
    static void Shot(Camera cam, string path)
    {
        var rt = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = prev;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
    }

    public void Execute(ExecutionResult result)
    {
        string dir = @"D:\Drillex\SourceArt\DrillVehicle\_preview\";
        Shot(Camera.main, dir + "unity_game.png");
        var t = GameObject.Find("DRL_DrillVehicle_Train").transform;
        var go = new GameObject("tmp_review_cam");
        var c = go.AddComponent<Camera>();
        c.CopyFrom(Camera.main);
        c.fieldOfView = 40;
        go.transform.position = t.position + t.right * 4.5f + t.forward * 3.5f + Vector3.up * 2.6f;
        go.transform.LookAt(t.position + t.forward * 0.4f + Vector3.up * 0.2f);
        Shot(c, dir + "unity_chase.png");
        Object.DestroyImmediate(go);
        result.Log("saved; time {0}", Time.time);
    }
}
