using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    static void Shot(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
        var prev = cam.targetTexture;
        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = prev; RenderTexture.active = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
    }
    public void Execute(ExecutionResult result)
    {
        string dir = @"D:\Drillex\SourceArt\DesertLevel\_unity_shots\";
        string tag = System.IO.File.Exists(dir + "tag.txt") ? System.IO.File.ReadAllText(dir + "tag.txt").Trim() : "shot";
        var cam = Camera.main;
        Shot(cam, dir + tag + "_game.png", 1920, 1080);
        ScreenCapture.CaptureScreenshot(dir + tag + "_gameview_ui.png");
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        result.Log("t={0} train {1} cam {2} | gameview tris {3} verts {4} drawcalls {5} setpass {6} shadowcasters {7}", Time.time, tm.transform.position, cam.transform.position, UnityStats.triangles, UnityStats.vertices, UnityStats.drawCalls, UnityStats.setPassCalls, UnityStats.shadowCasters);
    }
}
