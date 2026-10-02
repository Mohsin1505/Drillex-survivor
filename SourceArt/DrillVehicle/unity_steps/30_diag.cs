using UnityEngine;
using UnityEditor;
using System.Linq;
// Read-only diagnosis of the open scene's train head.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        result.Log("open scene {0} dirty {1} playing {2}", s.path, s.isDirty, EditorApplication.isPlaying);
        foreach (var tm in Object.FindObjectsByType<TrainMovement>(FindObjectsInactive.Include))
        {
            var src = PrefabUtility.GetCorrespondingObjectFromSource(tm.gameObject);
            result.Log("train '{0}' active {1} source {2}", tm.name, tm.gameObject.activeInHierarchy, src ? AssetDatabase.GetAssetPath(src) : "none");
            foreach (Transform c in tm.transform) result.Log("  child {0} active {1}", c.name, c.gameObject.activeSelf);
        }
    }
}
