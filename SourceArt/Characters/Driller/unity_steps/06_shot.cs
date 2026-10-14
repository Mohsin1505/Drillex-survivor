using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string name = System.IO.File.ReadAllText(@"D:\Drillex\SourceArt\Characters\Driller\_preview\shot_name.txt").Trim();
        ScreenCapture.CaptureScreenshot(@"D:\Drillex\SourceArt\Characters\Driller\_preview\" + name + ".png", 1);
        EditorApplication.QueuePlayerLoopUpdate();
        var hp = Object.FindAnyObjectByType<HelpPanelManager>(FindObjectsInactive.Include);
        result.Log("capture {0} playing {1} t {2} timescale {3}", name, EditorApplication.isPlaying, Time.time, Time.timeScale);
    }
}
