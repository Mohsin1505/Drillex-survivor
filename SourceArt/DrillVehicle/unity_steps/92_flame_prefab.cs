using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// Replace the placeholder visuals in Assets/Prefeb/FlameThrower.prefab with the flamethrower car model.
// Kept: root trigger BoxCollider, Rigidbody, TrainDamagePoint, tag/layer, both FlameThrower components
// (same settings) and the existing vfx_Flamethrower_01 particle effect, which moves to the nozzle socket.
internal class CommandScript : IRunCommand
{
    const float RootHeight = 0.57f;

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string path = "Assets/Prefeb/FlameThrower.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        sb.AppendLine("before: " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)));

        Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(root.GetComponent<MeshFilter>());
        var oldPivot = root.transform.Find("pivot");
        var oldPivotFlame = oldPivot.GetComponent<FlameThrower>();
        var oldShootFlame = oldPivot.GetComponentsInChildren<FlameThrower>(true).First(f => f.transform != oldPivot);
        var vfx = oldPivot.GetComponentsInChildren<ParticleSystem>(true).First(p => p.name == "vfx_Flamethrower_01").transform;

        var col = root.GetComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.60f - RootHeight, 0f);
        col.size = new Vector3(1.10f, 0.95f, 1.85f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_FlameCar/Meshes/SM_DRL_FlameCar.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_FlameCar/Materials/M_DRL_FlameCar.mat");
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
        Transform Find(string n) => vis.GetComponentsInChildren<Transform>(true).First(t => t.name == n);
        var turret = Find("Turret");
        var muzzle = Find("VFX_Muzzle");

        // flame effect: keep its orientation/scale relative to the aiming pivot, move it to the nozzle
        Quaternion relRot = Quaternion.Inverse(oldPivot.rotation) * vfx.rotation;
        Vector3 lossy = vfx.lossyScale;
        vfx.SetParent(muzzle, true);
        vfx.localPosition = Vector3.zero;
        vfx.rotation = turret.rotation * relRot;
        var ls = vfx.parent.lossyScale;
        vfx.localScale = new Vector3(lossy.x / ls.x, lossy.y / ls.y, lossy.z / ls.z);

        // both FlameThrower components, same settings, same effect
        var f1 = turret.gameObject.AddComponent<FlameThrower>();
        EditorUtility.CopySerialized(oldPivotFlame, f1);
        f1.flameEffect = vfx.GetComponent<ParticleSystem>();
        var f2 = muzzle.gameObject.AddComponent<FlameThrower>();
        EditorUtility.CopySerialized(oldShootFlame, f2);
        f2.flameEffect = vfx.GetComponent<ParticleSystem>();

        var roller = root.AddComponent<TrainWheelRoller>();
        roller.wheels = vis.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Axle_")).OrderBy(t => t.name).ToArray();

        oldPivot.gameObject.SetActive(false);   // placeholder barrel switched off, not deleted

        PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.AppendLine("after: " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(t => t.name + (t.gameObject.activeSelf ? "" : "(off)"))));
        sb.AppendLine("FlameThrower on " + f1.name + " range " + f1.range + " dps " + f1.damagePerSecond + " angle " + f1.flameAngle + " ; second on " + f2.name);
        sb.AppendLine("vfx under " + vfx.parent.name + " localScale " + vfx.localScale.ToString("F2") + " fwd·turretFwd " + Vector3.Dot(vfx.forward, turret.forward).ToString("F2"));
        sb.AppendLine("renderers " + vis.GetComponentsInChildren<MeshRenderer>(true).Length + " wheels " + roller.wheels.Length);
        PrefabUtility.UnloadPrefabContents(root);
        result.Log(sb.ToString());
    }
}
