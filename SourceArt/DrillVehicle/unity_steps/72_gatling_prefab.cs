using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Text;

// Replace the placeholder visuals in Assets/Prefeb/mini machine gun.prefab with the Gatling gun car model.
// Kept: root BoxCollider (trigger), Rigidbody, TrainDamagePoint, tag/layer, and the AutoGun settings
// (moved from the old placeholder gun to the new turret, firing from VFX_Muzzle).
internal class CommandScript : IRunCommand
{
    const float RootHeight = 0.57f;

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        const string path = "Assets/Prefeb/mini machine gun.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        sb.AppendLine("before: " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(t => t.name)));

        // old placeholder visuals
        Object.DestroyImmediate(root.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(root.GetComponent<MeshFilter>());
        var oldGunPivot = root.transform.Find("gun");
        var oldGun = oldGunPivot.GetComponent<AutoGun>();

        var col = root.GetComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.60f - RootHeight, 0f);
        col.size = new Vector3(1.10f, 0.95f, 1.85f);

        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Vehicles/DRL_GatlingCar/Meshes/SM_DRL_GatlingCar.fbx");
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Vehicles/DRL_GatlingCar/Materials/M_DRL_GatlingCar.mat");
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

        // turret gun: same AutoGun component/settings as before, on the new turret pivot
        var turret = vis.GetComponentsInChildren<Transform>(true).First(t => t.name == "Turret");
        var gun = turret.gameObject.AddComponent<AutoGun>();
        EditorUtility.CopySerialized(oldGun, gun);
        gun.shootPoint = vis.GetComponentsInChildren<Transform>(true).First(t => t.name == "VFX_Muzzle");

        // wheels roll from movement
        var roller = root.AddComponent<TrainWheelRoller>();
        roller.wheels = vis.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Axle_")).OrderBy(t => t.name).ToArray();

        // the old cylinder gun is switched off (not deleted); emissive lamps replace the per-car point light
        oldGunPivot.gameObject.SetActive(false);
        var pl = root.GetComponentsInChildren<Light>(true).FirstOrDefault();
        if (pl) pl.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        sb.AppendLine("after: " + string.Join(",", root.GetComponents<Component>().Select(c => c.GetType().Name)) + " children " + string.Join(",", root.transform.Cast<Transform>().Select(t => t.name + (t.gameObject.activeSelf ? "" : "(off)"))));
        sb.AppendLine("AutoGun on " + gun.name + " type " + gun.weaponType + " dmg " + gun.baseDamage + " range " + gun.range + " rate " + gun.fireRate + " bullet " + (gun.bulletPrefab ? gun.bulletPrefab.name : "null") + " shoot " + gun.shootPoint.name);
        sb.AppendLine("renderers " + vis.GetComponentsInChildren<MeshRenderer>(true).Length + " wheels " + roller.wheels.Length);
        PrefabUtility.UnloadPrefabContents(root);
        result.Log(sb.ToString());
    }
}
