using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
// Lev1: zone(s) that spawn the light capsule monster now spawn the Red spider (a variant of it).
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        if (EditorApplication.isPlaying) { result.Log("SKIP: playing"); return; }
        var s = EditorSceneManager.GetActiveScene();
        if (s.isDirty) { result.Log("SKIP: {0} has unsaved changes", s.name); return; }
        if (s.name != "Lev1") s = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        var light = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/lightMoster.prefab");
        var red = AssetDatabase.LoadAssetAtPath<GameObject>(DrillexSpiderSetup.EnemyPath("Red"));
        var sp = Object.FindAnyObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        Undo.RecordObject(sp, "Spawn red spider");
        foreach (var z in sp.zones)
        {
            for (int i = 0; i < z.enemies.Length; i++) if (z.enemies[i] == light) z.enemies[i] = red;
            result.Log("zone {0}: {1}", z.zoneName, string.Join(", ", System.Array.ConvertAll(z.enemies, e => e ? e.name : "null")));
        }
        EditorUtility.SetDirty(sp);
        EditorSceneManager.MarkSceneDirty(s);
        EditorSceneManager.SaveScene(s);
        result.Log("saved Lev1");
    }
}
