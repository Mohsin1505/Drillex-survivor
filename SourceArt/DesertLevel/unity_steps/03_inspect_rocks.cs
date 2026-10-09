using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        foreach (var p in new[] { "RockZ1", "RockZ1(2) 1", "RockZ2 1", "RockZ2(2)" })
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/" + p + ".prefab");
            sb.AppendLine(p + " tag=" + go.tag + " layer=" + LayerMask.LayerToName(go.layer) + " scale=" + go.transform.localScale);
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                var comps = string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name));
                var r = t.GetComponent<Renderer>();
                var mf = t.GetComponent<MeshFilter>();
                sb.AppendLine("  " + t.name + " [" + comps + "] lpos=" + t.localPosition + " lscl=" + t.localScale + (mf ? " mesh=" + mf.sharedMesh.name + " b=" + mf.sharedMesh.bounds.size : "") +
                    (r ? " mats=" + string.Join("|", r.sharedMaterials.Select(m => m ? m.name + "(" + m.shader.name + ")" : "null")) : ""));
                var col = t.GetComponent<Collider>();
                if (col is SphereCollider sc) sb.AppendLine("    sphere r=" + sc.radius + " trig=" + sc.isTrigger);
                if (col is BoxCollider bc) sb.AppendLine("    box " + bc.size + " trig=" + bc.isTrigger);
            }
            var rock = go.GetComponent<Rock>();
            if (rock) sb.AppendLine("  Rock json " + EditorJsonUtility.ToJson(rock));
        }
        result.Log(sb.ToString());
    }
}
