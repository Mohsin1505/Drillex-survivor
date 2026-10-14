using UnityEngine;
using UnityEditor;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var c = GameObject.Find("Canvas");
        foreach (Transform t in c.transform) result.Log("{0} active={1}", t.name, t.gameObject.activeSelf);
        var hp = Object.FindAnyObjectByType<TrainHealth>();
        result.Log("hp {0}/{1} paused={2} scale={3}", hp.CurrentHealth, hp.maxHealth, PauseManager.IsPaused, Time.timeScale);
    }
}
