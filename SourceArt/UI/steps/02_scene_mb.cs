using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Text;
internal class CommandScript : IRunCommand
{
    static string PathOf(Transform t){ return t.parent ? PathOf(t.parent) + "/" + t.name : t.name; }
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        foreach (var path in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/Lev1.unity"})
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            sb.AppendLine("=== " + path);
            foreach (var root in s.GetRootGameObjects())
            {
                if (root.transform.childCount > 200) { sb.AppendLine("ROOT big " + root.name); }
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var ns = mb.GetType().Namespace ?? "";
                    if (ns.StartsWith("UnityEngine") || ns.StartsWith("TMPro")) {
                        if (mb is Button b) {
                            sb.Append("BTN " + PathOf(b.transform) + " -> ");
                            for (int i=0;i<b.onClick.GetPersistentEventCount();i++) sb.Append((b.onClick.GetPersistentTarget(i)? b.onClick.GetPersistentTarget(i).name:"null") + "." + b.onClick.GetPersistentMethodName(i) + " ");
                            sb.AppendLine();
                        }
                        continue;
                    }
                    sb.Append(PathOf(mb.transform) + " |" + mb.GetType().Name + "{");
                    var so = new SerializedObject(mb); var it = so.GetIterator(); it.NextVisible(true);
                    while (it.NextVisible(false))
                    {
                        if (it.propertyType == SerializedPropertyType.ObjectReference) sb.Append(it.name + "=" + (it.objectReferenceValue ? it.objectReferenceValue.name : "null") + ";");
                        else if (it.propertyType == SerializedPropertyType.Float) sb.Append(it.name + "=" + it.floatValue + ";");
                        else if (it.propertyType == SerializedPropertyType.Integer) sb.Append(it.name + "=" + it.intValue + ";");
                        else if (it.propertyType == SerializedPropertyType.String) sb.Append(it.name + "='" + it.stringValue + "';");
                    }
                    sb.AppendLine("}");
                }
            }
            if (EditorSceneManager.GetActiveScene() != s) EditorSceneManager.CloseScene(s, true);
        }
        System.IO.File.WriteAllText(@"D:\Drillex\SourceArt\UI\scene_mb.txt", sb.ToString());
        result.Log("ok");
    }
}
