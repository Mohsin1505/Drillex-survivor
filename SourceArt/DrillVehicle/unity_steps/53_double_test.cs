using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode: unpause, attach a basic gun car exactly like the shop does, report state.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true;
        foreach (var h in Object.FindObjectsByType<HelpPanelManager>(FindObjectsInactive.Include)) h.CloseHelp();
        Time.timeScale = 1f;
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var um = Object.FindAnyObjectByType<UpgradeManager>();
        if (Object.FindObjectsByType<TrainWheelRoller>(FindObjectsInactive.Exclude).Count(r => r.name.StartsWith("Double")) == 0)
        {
            tm.AddTrainBlock(um.storageBlockPrefab);
            tm.AddTrainBlock(um.dualGunPrefab);
        }
        result.Log("added; time {0}", Time.time);
    }
}
