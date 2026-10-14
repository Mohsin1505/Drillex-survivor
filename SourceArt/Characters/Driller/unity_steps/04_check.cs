using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = EditorSceneManager.GetActiveScene();
        result.Log("playing {0} scene {1} dirty {2}", EditorApplication.isPlaying, s.name, s.isDirty);
        var ctl = Object.FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
        if (ctl) foreach (var b in ctl.buttons) result.Log("btn {0} y {1}", b ? b.name : "null", b ? b.anchoredPosition.y : 0);
        var c = GameObject.Find("CrewMember"); result.Log("crew {0} {1}", c ? c.transform.position.ToString() : "none", c ? c.transform.eulerAngles.y : 0);
    }
}
