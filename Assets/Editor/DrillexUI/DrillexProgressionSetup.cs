using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Wires the Electric Gun Block (Tesla Cannon car) into the scene's UpgradeManager.
// Progression rule: Mini MG (8) -> Flamethrower (10) -> ELECTRIC (11) -> Rocket (12).
// Existing values are only touched the first time (when no electric prefab is assigned yet).
public static class DrillexProgressionSetup
{
    public const string ElectricPrefabPath = "Assets/Prefeb/Tesla Cannon.prefab";

    [MenuItem("Drillex/Gameplay/Configure Electric Block Progression")]
    public static void Configure()
    {
        var um = Object.FindAnyObjectByType<UpgradeManager>(FindObjectsInactive.Include);
        if (um == null) { Debug.LogWarning("No UpgradeManager in the open scene"); return; }
        var so = new SerializedObject(um);
        var prefabProp = so.FindProperty("electricGunPrefab");
        if (prefabProp.objectReferenceValue == null)
        {
            prefabProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ElectricPrefabPath);
            int rocket = so.FindProperty("rocketGunUnlockLevel").intValue;
            int flame = so.FindProperty("flameThrowerUnlockLevel").intValue;
            int mg = so.FindProperty("machineGunUnlockLevel").intValue;
            // after the Mini MG / Flamethrower tier, strictly before the Rocket Launcher
            int level = Mathf.Clamp(Mathf.Max(mg, flame) + 1, 1, rocket - 1);
            so.FindProperty("electricGunUnlockLevel").intValue = level;
            int rocketCost = so.FindProperty("rocketGunCost").intValue;
            int flameCost = so.FindProperty("flameThrowerCost").intValue;
            so.FindProperty("electricGunCost").intValue = Mathf.RoundToInt((rocketCost + flameCost) * 0.5f / 50f) * 50;
            so.FindProperty("electricGunBonus").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(um.gameObject.scene);
        }
        Debug.LogFormat("Electric block: unlock Lv {0}, cost {1} (rocket Lv {2})", um.electricGunUnlockLevel, um.electricGunCost, um.rocketGunUnlockLevel);
    }
}
