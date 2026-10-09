using UnityEngine;
using UnityEditor;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        Application.runInBackground = true;
        foreach (var h in Object.FindObjectsByType<HelpPanelManager>(FindObjectsInactive.Include)) h.CloseHelp();
        Time.timeScale = 1f;
        // screenshot session only (play mode, not saved): no enemies so the train survives the drive
        foreach (var z in Object.FindAnyObjectByType<EnemySpawner>().zones) z.maxEnemiesAlive = 0;
        var tm = Object.FindAnyObjectByType<TrainMovement>();
        result.Log("playing {0} t={1} train {2} cam {3} {4}", EditorApplication.isPlaying, Time.time, tm.transform.position, Camera.main.transform.position, Camera.main.transform.eulerAngles);
    }
}
