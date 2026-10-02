using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;

// Replace the placeholder cube visuals in Assets/Prefeb/Storage.prefab with the storage car model.
// Root components (StorageBlock, TrainDamagePoint, Rigidbody, BoxCollider), tag and layer are kept.
internal class CommandScript : IRunCommand
{
    const float RootHeight = 0.57f;   // blocks spawn at the train head's height (0.57 m above ground)

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string path = "Assets/Prefeb/Storage.prefab";

        // scene instances that might carry overrides
        foreach (var sp in new[] { "Assets/Scenes/Lev1.unity", "Assets/Scenes/MainMenu.unity" })
        {
            var text = System.IO.File.ReadAllText(sp);
            var guid = AssetDatabase.AssetPathToGUID(path);
            sb.AppendLine(sp + " references Storage.prefab: " + text.Contains(guid));
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        sb.AppendLine("before: scale " + root.transform.localScale + " comps " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)));
        var col = root.GetComponent<BoxCollider>();
        sb.AppendLine("before collider world size " + Vector3.Scale(col.size, root.transform.localScale) + " trigger " + col.isTrigger);

        root.transform.localScale = Vector3.one;
        Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(root.GetComponent<MeshFilter>());
        var t = root.transform.Find("t");
        if (t) Object.DestroyImmediate(t.gameObject);

        // collider fitted to the new car (body + pillars, wheels excluded)
        col.center = new Vector3(0f, 0.60f - RootHeight, 0f);
        col.size = new Vector3(1.10f, 0.95f, 1.85f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_StorageBlock/Meshes/SM_DRL_StorageBlock.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_StorageBlock/Materials/M_DRL_StorageBlock.mat");
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
        vis.name = "Visual";
        vis.transform.localPosition = new Vector3(0f, -RootHeight, 0f);
        vis.transform.localRotation = Quaternion.identity;
        vis.transform.localScale = Vector3.one;
        foreach (var r in vis.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.AppendLine("after: comps " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(c => c.name)));
        PrefabUtility.UnloadPrefabContents(root);
        result.Log(sb.ToString());
    }
}
