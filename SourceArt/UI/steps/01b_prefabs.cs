using UnityEngine;
using UnityEditor;
using System.Text;
internal class CommandScript : IRunCommand
{
    static void Dump(Transform t, int d, StringBuilder sb)
    {
        sb.Append(new string(' ', d * 2)).Append(t.name).AppendFormat(" lp{0} lr{1} ls{2}", t.localPosition, t.localEulerAngles, t.localScale);
        if (!t.gameObject.activeSelf) sb.Append(" [off]");
        sb.Append(" layer=" + LayerMask.LayerToName(t.gameObject.layer) + " tag=" + t.tag);
        foreach (var c in t.GetComponents<Component>())
        {
            if (c is Transform) continue;
            if (c == null) { sb.Append(" |MISSING"); continue; }
            sb.Append(" |").Append(c.GetType().Name);
            if (c is MonoBehaviour mb)
            {
                var so = new SerializedObject(mb); var it = so.GetIterator(); it.NextVisible(true);
                sb.Append("{");
                while (it.NextVisible(false))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference) sb.Append(it.name + "=" + (it.objectReferenceValue ? it.objectReferenceValue.name : "null") + ";");
                    else if (it.propertyType == SerializedPropertyType.Float) sb.Append(it.name + "=" + it.floatValue + ";");
                    else if (it.propertyType == SerializedPropertyType.Integer) sb.Append(it.name + "=" + it.intValue + ";");
                    else if (it.propertyType == SerializedPropertyType.Enum) sb.Append(it.name + "=" + it.enumDisplayNames[it.enumValueIndex] + ";");
                    else if (it.propertyType == SerializedPropertyType.Boolean) sb.Append(it.name + "=" + it.boolValue + ";");
                    else if (it.isArray && it.propertyType == SerializedPropertyType.Generic) { sb.Append(it.name + "=["); for (int i=0;i<it.arraySize;i++){var e=it.GetArrayElementAtIndex(i); sb.Append(e.propertyType==SerializedPropertyType.ObjectReference&&e.objectReferenceValue?e.objectReferenceValue.name:"?").Append(",");} sb.Append("];"); }
                }
                sb.Append("}");
            }
            if (c is ParticleSystem ps) sb.AppendFormat("(max{0} rate{1} life{2} mat {3})", ps.main.maxParticles, ps.emission.rateOverTime.constant, ps.main.startLifetime.constant, c.GetComponent<ParticleSystemRenderer>().sharedMaterial ? c.GetComponent<ParticleSystemRenderer>().sharedMaterial.name : "-");
            if (c is MeshRenderer mr) sb.Append("(" + (mr.sharedMaterial ? mr.sharedMaterial.name + "/" + mr.sharedMaterial.shader.name : "-") + ")");
            if (c is Light l) sb.Append("(" + l.type + " " + l.range + ")");
            if (c is TrailRenderer tr) sb.Append("(" + tr.time + " " + (tr.sharedMaterial?tr.sharedMaterial.name:"-") + ")");
        }
        sb.AppendLine();
        foreach (Transform ch in t) Dump(ch, d + 1, sb);
    }
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[]{"Assets/Prefeb","Assets/fx"}))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (!(p.EndsWith("/Rocket.prefab")||p.Contains("vfx_Explosion")||p.Contains("vfx_MuzzleFlash")||p.Contains("vfx_Lightning")||p.Contains("vfx_Electricity")||p.Contains("vfx_Impact")||p.Contains("vfx_Sparks")||p.Contains("vfx_Smoke"))) continue;
            sb.AppendLine("##### " + p);
            Dump(AssetDatabase.LoadAssetAtPath<GameObject>(p).transform, 0, sb);
        }
        System.IO.File.WriteAllText(@"D:\Drillex\SourceArt\UI\prefab_dump2.txt", sb.ToString());
        result.Log("ok " + sb.Length);
    }
}
