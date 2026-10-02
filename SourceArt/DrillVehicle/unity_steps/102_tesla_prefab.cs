using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// New weapon car: Assets/Prefeb/Tesla Cannon.prefab.
// There is no Tesla placeholder in the project, so this starts from a copy of Basic Gun.prefab
// (same root components: trigger BoxCollider, kinematic Rigidbody, TrainDamagePoint, tag Drill,
// TrainWheelRoller) and uses the existing AutoGun with the Basic gun's settings on the Tesla turret.
internal class CommandScript : IRunCommand
{
    const float RootHeight = 0.57f;

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string src = "Assets/Prefeb/Basic Gun.prefab";
        const string path = "Assets/Prefeb/Tesla Cannon.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset(src, path);

        var root = PrefabUtility.LoadPrefabContents(path);
        root.name = "Tesla Cannon";
        var oldGun = root.transform.Find("gun").GetComponent<AutoGun>();   // original Basic gun settings
        var settings = new GameObject("tmp").AddComponent<AutoGun>();
        EditorUtility.CopySerialized(oldGun, settings);
        foreach (var n in new[] { "Visual", "gun", "Point Light" })
        {
            var t = root.transform.Find(n);
            if (t) Object.DestroyImmediate(t.gameObject);
        }

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_TeslaCar/Meshes/SM_DRL_TeslaCar.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_TeslaCar/Materials/M_DRL_TeslaCar.mat");
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

        var gun = Find("Turret").gameObject.AddComponent<AutoGun>();
        EditorUtility.CopySerialized(settings, gun);
        gun.shootPoint = Find("VFX_Muzzle");
        Object.DestroyImmediate(settings.gameObject);

        var roller = root.GetComponent<TrainWheelRoller>();
        if (roller == null) roller = root.AddComponent<TrainWheelRoller>();
        roller.wheels = vis.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Axle_")).OrderBy(t => t.name).ToArray();

        var col = root.GetComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.60f - RootHeight, 0f);
        col.size = new Vector3(1.10f, 0.95f, 1.85f);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.AppendLine("saved " + path + " components " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(t => t.name)));
        sb.AppendLine("AutoGun on " + gun.name + " type " + gun.weaponType + " dmg " + gun.baseDamage + " range " + gun.range + " rate " + gun.fireRate + " bullet " + (gun.bulletPrefab ? gun.bulletPrefab.name : "null") + " shoot " + gun.shootPoint.name);
        sb.AppendLine("sockets " + string.Join(",", vis.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("VFX_")).Select(t => t.name)));
        sb.AppendLine("renderers " + vis.GetComponentsInChildren<MeshRenderer>(true).Length + " wheels " + roller.wheels.Length);
        PrefabUtility.UnloadPrefabContents(root);
        result.Log(sb.ToString());
    }
}
