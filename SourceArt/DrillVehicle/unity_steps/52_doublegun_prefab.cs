using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// Replace the placeholder visuals in "Assets/Prefeb/Double  barrel gun.prefab" with the double-barrel car.
// The prefab had two AutoGun components (one per placeholder gun); both are kept with their settings and
// now sit on the one turret, each firing from its own barrel's muzzle socket.
internal class CommandScript : IRunCommand
{
    const float RootHeight = 0.57f;

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string path = "Assets/Prefeb/Double  barrel gun.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        sb.AppendLine("before: " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(t => t.name)));

        Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(root.GetComponent<MeshFilter>());
        var oldGuns = root.transform.Cast<Transform>().Select(t => t.GetComponent<AutoGun>()).Where(g => g != null)
            .OrderBy(g => g.transform.localPosition.x).ToArray();   // left (-x) first

        var col = root.GetComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.60f - RootHeight, 0f);
        col.size = new Vector3(1.10f, 0.95f, 1.85f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_DoubleGunCar/Meshes/SM_DRL_DoubleGunCar.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_DoubleGunCar/Materials/M_DRL_DoubleGunCar.mat");
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
        var muzzles = new[] { Find("VFX_Muzzle_L"), Find("VFX_Muzzle_R") };
        for (int i = 0; i < oldGuns.Length; i++)
        {
            var gun = turret.gameObject.AddComponent<AutoGun>();
            EditorUtility.CopySerialized(oldGuns[i], gun);
            gun.shootPoint = muzzles[Mathf.Min(i, muzzles.Length - 1)];
            sb.AppendLine("AutoGun " + i + ": type " + gun.weaponType + " dmg " + gun.baseDamage + " range " + gun.range + " rate " + gun.fireRate + " bullet " + (gun.bulletPrefab ? gun.bulletPrefab.name : "null") + " -> " + gun.shootPoint.name);
            oldGuns[i].gameObject.SetActive(false);   // placeholder cylinder gun switched off, not deleted
        }

        var roller = root.AddComponent<TrainWheelRoller>();
        roller.wheels = vis.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Axle_")).OrderBy(t => t.name).ToArray();

        PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.AppendLine("after: " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(t => t.name + (t.gameObject.activeSelf ? "" : "(off)"))));
        sb.AppendLine("renderers " + vis.GetComponentsInChildren<MeshRenderer>(true).Length + " materials " + vis.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.sharedMaterial).Distinct().Count() + " wheels " + roller.wheels.Length);
        PrefabUtility.UnloadPrefabContents(root);
        result.Log(sb.ToString());
    }
}
