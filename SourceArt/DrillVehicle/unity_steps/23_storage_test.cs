using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode only: unpause, buy storage cars through the normal upgrade path, report state.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true;
        foreach (var h in Object.FindObjectsByType<HelpPanelManager>(FindObjectsInactive.Include)) h.CloseHelp();
        Time.timeScale = 1f;
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        var sm = tm.GetComponent<TrainStorageManager>();
        int before = Object.FindObjectsByType<StorageBlock>(FindObjectsInactive.Exclude).Length;
        result.Log("before: blocks {0} totalStorage {1}", before, sm.totalStorage);
        var um = Object.FindAnyObjectByType<UpgradeManager>();
        um.BuyStorageBlock();
        um.BuyStorageBlock();
        int after = Object.FindObjectsByType<StorageBlock>(FindObjectsInactive.Exclude).Length;
        if (after == before)
        {
            result.Log("BuyStorageBlock did not add (locked or cost) - adding through TrainMovement.AddTrainBlock like the shop does");
            tm.AddTrainBlock(um.storageBlockPrefab);
            tm.AddTrainBlock(um.storageBlockPrefab);
        }
        result.Log("after: blocks {0} totalStorage {1}", Object.FindObjectsByType<StorageBlock>(FindObjectsInactive.Exclude).Length, sm.totalStorage);
    }
}
