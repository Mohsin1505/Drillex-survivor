using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
// Edit-mode estimate of what the gameplay camera draws from a pose: triangles per group / per prefab (frustum + far clip + LODGroup cull ratio).
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var cam = GameObject.Find("Main Camera").GetComponent<Camera>();
        var savePos = cam.transform.position; var saveRot = cam.transform.rotation;
        cam.transform.position = new Vector3(0f, 7.11f, -253f);
        cam.transform.rotation = Quaternion.Euler(23.4f, 0f, 0f);
        var planes = GeometryUtility.CalculateFrustumPlanes(cam);
        var byGroup = new Dictionary<string, long>();
        var byPrefab = new Dictionary<string, long>();
        long total = 0; int count = 0;
        var root = GameObject.Find("DESERT_ENVIRONMENT").transform;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
            float dist = Vector3.Distance(cam.transform.position, r.bounds.center);
            if (dist - r.bounds.extents.magnitude > cam.farClipPlane) continue;
            var lg = r.GetComponentInParent<LODGroup>();
            if (lg)
            {
                float size = lg.size * lg.transform.lossyScale.x;
                float rel = size / (2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
                if (rel < lg.GetLODs()[0].screenRelativeTransitionHeight) continue;
            }
            int t = r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
            var grp = r.transform; while (grp.parent != root) grp = grp.parent;
            var inst = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
            string pn = inst ? PrefabUtility.GetCorrespondingObjectFromSource(inst).name : r.name;
            byGroup[grp.name] = (byGroup.TryGetValue(grp.name, out var a) ? a : 0) + t;
            byPrefab[pn] = (byPrefab.TryGetValue(pn, out var b) ? b : 0) + t;
            total += t; count++;
        }
        cam.transform.position = savePos; cam.transform.rotation = saveRot;
        result.Log("visible kit renderers " + count + " tris " + total + "\nGROUPS " + string.Join(", ", byGroup.OrderByDescending(k => k.Value).Select(k => k.Key + "=" + k.Value)) +
                   "\nTOP " + string.Join(", ", byPrefab.OrderByDescending(k => k.Value).Take(14).Select(k => k.Key + "=" + k.Value)));
    }
}
