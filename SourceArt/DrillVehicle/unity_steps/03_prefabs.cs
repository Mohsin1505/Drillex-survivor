using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Step 3: prefabs.
//   DRL_Drill_Standard.prefab         swappable drill module (variant of its FBX)
//   DRL_DrillVehicle_Visual.prefab    art-only vehicle with the drill nested in DrillSocket
//   DRL_DrillVehicle_Train.prefab     test version of Train.prefab (prefab variant) using the new art
// Train.prefab itself is not modified.
internal class CommandScript : IRunCommand
{
    const string Root = "Assets/Art/Vehicles/DRL_DrillVehicle";

    static Transform Find(Transform t, string name)
    {
        return t.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
    }

    static void SetMaterial(GameObject go, Material m)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            r.receiveShadows = true;
        }
    }

    static void SetLods(GameObject go, string[] lod0, string[] lod1, float lod0Height, float lod1Height)
    {
        var all = go.GetComponentsInChildren<MeshRenderer>(true);
        Renderer[] Pick(string[] names) => all.Where(r => names.Contains(r.name) || names.Any(n => n.EndsWith("*") && r.name.StartsWith(n.TrimEnd('*')))).Cast<Renderer>().ToArray();
        var group = go.GetComponent<LODGroup>();
        if (group == null) group = go.AddComponent<LODGroup>();
        group.SetLODs(new[] { new LOD(lod0Height, Pick(lod0)), new LOD(lod1Height, Pick(lod1)) });
        group.fadeMode = LODFadeMode.None;
        group.RecalculateBounds();
    }

    public void Execute(ExecutionResult result)
    {
        var log = new StringBuilder();
        // remove leftovers from an interrupted earlier run (temporary instances only)
        foreach (var g in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == "DRL_Drill_Standard" || g.name == "DRL_DrillVehicle_Visual" || g.name == "DRL_DrillVehicle_Train")
            {
                log.AppendLine("removed leftover " + g.name);
                Object.DestroyImmediate(g);
            }
        var mVehicle = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_DRL_DrillVehicle.mat");
        var mDrill = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/M_DRL_Drill_Standard.mat");

        // ---------------- drill module ----------------
        string drillPath = Root + "/Prefabs/DRL_Drill_Standard.prefab";
        var drillModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Meshes/SM_DRL_Drill_Standard.fbx");
        var drill = (GameObject)PrefabUtility.InstantiatePrefab(drillModel);
        drill.name = "DRL_Drill_Standard";
        SetMaterial(drill, mDrill);
        drill.tag = "Drill";
        var col = drill.AddComponent<BoxCollider>();      // same setup as the old Drill cube
        col.center = new Vector3(0f, 0f, 0.43f);
        col.size = new Vector3(0.84f, 0.84f, 0.86f);
        var rb = drill.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        drill.AddComponent<Drill>();
        drill.AddComponent<TrainDamagePoint>();
        SetLods(drill, new[] { "DrillBase", "DrillSections", "DrillTip" },
                       new[] { "DrillBase_L1", "DrillSections_L1", "DrillTip_L1" }, 0.03f, 0.004f);
        var drillPrefab = PrefabUtility.SaveAsPrefabAsset(drill, drillPath);
        Object.DestroyImmediate(drill);
        log.AppendLine("saved " + drillPath);

        // ---------------- visual vehicle ----------------
        string visualPath = Root + "/Prefabs/DRL_DrillVehicle_Visual.prefab";
        var vehModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Meshes/SM_DRL_DrillVehicle.fbx");
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(vehModel);
        vis.name = "DRL_DrillVehicle_Visual";
        SetMaterial(vis, mVehicle);
        var socket = Find(vis.transform, "DrillSocket");
        var d = (GameObject)PrefabUtility.InstantiatePrefab(drillPrefab, socket);
        d.transform.localPosition = Vector3.zero;
        d.transform.localRotation = Quaternion.identity;
        d.transform.localScale = Vector3.one;
        var wheels = vis.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Wheel_")).OrderBy(t => t.name).ToArray();
        SetLods(vis, new[] { "VehicleBody", "Turret", "Cannon", "Wheel_*" },
                     new[] { "VehicleBody_L1", "Turret_L1", "Cannon_L1", "Wheel_*" }, 0.05f, 0.006f);
        var anim = vis.AddComponent<DrillVehicleAnimator>();
        anim.drillSocket = socket;
        anim.wheels = wheels;
        var visualPrefab = PrefabUtility.SaveAsPrefabAsset(vis, visualPath);
        Object.DestroyImmediate(vis);
        log.AppendLine("saved " + visualPath + " wheels=" + wheels.Length);

        // ---------------- test train (variant of Train.prefab) ----------------
        string trainPath = Root + "/Prefabs/DRL_DrillVehicle_Train.prefab";
        var trainBase = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/Train.prefab");
        var train = (GameObject)PrefabUtility.InstantiatePrefab(trainBase);
        train.name = "DRL_DrillVehicle_Train";
        const float rootHeight = 0.57f;   // Train root sits 0.57 m above the ground

        var oldDrill = train.transform.Find("Drill");
        var oldGun = oldDrill.GetComponentInChildren<AutoGun>(true);

        var v = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, train.transform);
        v.name = "Visual";
        v.transform.localPosition = new Vector3(0f, -rootHeight, 0f);
        v.transform.localRotation = Quaternion.identity;

        // hull collider: takes damage, tagged Drill for safe zones / upgrade stations / enemy targeting
        var hull = new GameObject("HullCollider");
        hull.transform.SetParent(train.transform, false);
        hull.tag = "Drill";
        var hc = hull.AddComponent<BoxCollider>();
        hc.center = new Vector3(0f, 0.55f - rootHeight, -0.02f);
        hc.size = new Vector3(1.05f, 0.95f, 1.75f);
        var hrb = hull.AddComponent<Rigidbody>();
        hrb.isKinematic = true;
        hrb.useGravity = false;
        hrb.constraints = RigidbodyConstraints.FreezeRotation;
        hull.AddComponent<TrainDamagePoint>();

        // turret gun: same AutoGun settings as the old head gun, firing from the cannon muzzle
        var turret = Find(v.transform, "Turret");
        var gun = turret.gameObject.AddComponent<AutoGun>();
        EditorUtility.CopySerialized(oldGun, gun);
        gun.shootPoint = Find(v.transform, "VFX_CannonMuzzle");

        // drill module talks to this train's movement
        var drillComp = v.GetComponentInChildren<Drill>(true);
        drillComp.trainMovement = train.GetComponent<TrainMovement>();
        EditorUtility.SetDirty(drillComp);

        // reuse the existing light / trap deployer, moved to the new sockets
        var light = train.transform.Find("Spot Light");
        light.localPosition = new Vector3(0f, 0.95f - rootHeight, 0.80f);
        var trap = train.transform.Find("trapdeployer");
        trap.localPosition = new Vector3(0f, 0.30f - rootHeight, -1.00f);

        // old placeholder head (cube, its collider, Drill script and gun) is switched off, not deleted
        oldDrill.gameObject.SetActive(false);

        PrefabUtility.SaveAsPrefabAsset(train, trainPath);
        Object.DestroyImmediate(train);
        log.AppendLine("saved " + trainPath);

        AssetDatabase.SaveAssets();
        result.Log(log.ToString());
    }
}
