using UnityEngine;
using UnityEditor;
using System.Text;
using System.Linq;

internal class CommandScript : IRunCommand
{
    void Dump(Transform t, int d, StringBuilder sb)
    {
        var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c =>
        {
            string e = c.GetType().Name;
            if (c is MeshFilter mf && mf.sharedMesh) e += "(" + mf.sharedMesh.name + ")";
            if (c is BoxCollider bc) e += "(c" + bc.center + " s" + bc.size + (bc.isTrigger ? " trig" : "") + ")";
            if (c is Light l) e += "(" + l.type + " r" + l.range + " i" + l.intensity + " sh" + l.shadows + ")";
            if (c is MonoBehaviour mb)
            {
                var so = new SerializedObject(mb); var it = so.GetIterator(); var refs = new System.Collections.Generic.List<string>();
                while (it.NextVisible(true)) if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script") refs.Add(it.name + "=" + (it.objectReferenceValue ? it.objectReferenceValue.name : "null"));
                e += "{" + string.Join(";", refs) + "}";
            }
            return e;
        });
        sb.AppendLine(new string(' ', d * 2) + t.name + " [" + t.tag + "] p" + t.localPosition.ToString("F2") + " r" + t.localEulerAngles.ToString("F0") + " s" + t.localScale.ToString("F2") + " :: " + string.Join(", ", comps));
        foreach (Transform c in t) Dump(c, d + 1, sb);
    }

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        foreach (var p in new[] { "Assets/Prefeb/Basic Gun.prefab" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            sb.AppendLine("== " + p);
            Dump(go.transform, 0, sb);
        }
        var g = AssetDatabase.AssetPathToGUID("Assets/Prefeb/Basic Gun.prefab");
        foreach (var path in AssetDatabase.FindAssets("t:Prefab t:Scene").Select(AssetDatabase.GUIDToAssetPath))
            if (path.EndsWith(".unity") || path.EndsWith(".prefab"))
                if (System.IO.File.ReadAllText(path).Contains(g)) sb.AppendLine("referenced by " + path);
        result.Log(sb.ToString());
    }
}
