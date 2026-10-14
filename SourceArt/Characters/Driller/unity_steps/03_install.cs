using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = EditorSceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || s.isDirty) { result.Log("SKIP playing {0} dirty {1} scene {2}", EditorApplication.isPlaying, s.isDirty, s.name); return; }
        string prev = s.path;
        DrillexCrewMenu.InstallInMainMenu();
        var ctl = Object.FindAnyObjectByType<MainMenuController>();
        foreach (var b in ctl.buttons) result.Log("btn {0} y {1}", b ? b.name : "null", b ? b.anchoredPosition.y : 0);
        var cam = Camera.main; var rig = cam.GetComponent<MenuCameraRig>(); rig.Snap();
        // render the menu camera to a png for review
        var rt = new RenderTexture(1920, 1080, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply();
        System.IO.File.WriteAllBytes(@"D:\Drillex\SourceArt\Characters\Driller\_preview\unity_menu_stage.png", tex.EncodeToPNG());
        cam.targetTexture = null; RenderTexture.active = null; Object.DestroyImmediate(rt);
        result.Log("installed; previous scene {0}", prev);
    }
}
