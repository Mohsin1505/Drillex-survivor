using UnityEngine;
using UnityEditor;
// Captures the Game view (including Screen Space Overlay UI) on the next rendered frame.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        string name = System.IO.File.Exists(@"D:\Drillex\SourceArt\UI\_preview\shot_name.txt") ? System.IO.File.ReadAllText(@"D:\Drillex\SourceArt\UI\_preview\shot_name.txt").Trim() : "game";
        string path = @"D:\Drillex\SourceArt\UI\_preview\" + name + ".png";
        ScreenCapture.CaptureScreenshot(path, 1);
        EditorApplication.QueuePlayerLoopUpdate();
        result.Log("capture requested {0} at frame {1}", path, Time.frameCount);
    }
}
