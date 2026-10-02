using UnityEngine;
using UnityEditor;
using System.Linq;
// Play-mode snapshot of rendering stats (Game view) + train renderer/LOD state.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        result.Log("t {0} editor fps {1} | drawCalls {2} setPass {3} tris {4} verts {5} shadowCasters {6} instancedDC {7} texMB {8}",
            Time.time.ToString("F1"), (1f / Time.smoothDeltaTime).ToString("F0"), UnityStats.drawCalls, UnityStats.setPassCalls,
            UnityStats.triangles, UnityStats.vertices, UnityStats.shadowCasters, UnityStats.instancedBatchedDrawCalls,
            (UnityStats.usedTextureMemorySize / (1024f * 1024f)).ToString("F0"));
        var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude);
        result.Log("realtime lights {0}: {1}", lights.Length, string.Join(", ", lights.Select(l => l.name + "(" + l.type + ", shadows " + l.shadows + ")")));
        var cam = Camera.main;
        foreach (var g in Object.FindObjectsByType<LODGroup>(FindObjectsInactive.Exclude).Take(6))
        {
            var lods = g.GetLODs();
            string active = "culled";
            for (int i = 0; i < lods.Length; i++)
                if (lods[i].renderers.Length > 0 && lods[i].renderers[0] != null && lods[i].renderers[0].isVisible) { active = "LOD" + i; break; }
            result.Log("LODGroup {0}/{1}: {2} dist {3}", g.transform.root.name, g.name, active, Vector3.Distance(cam.transform.position, g.transform.position).ToString("F1"));
        }
    }
}
