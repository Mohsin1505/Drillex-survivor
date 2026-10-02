using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode only: close the help panel (unpauses) so the train drives, like a player pressing close.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        foreach (var h in Object.FindObjectsByType<HelpPanelManager>(FindObjectsInactive.Include)) h.CloseHelp();
        Time.timeScale = 1f;
        result.Log("timeScale {0}", Time.timeScale);
    }
}
