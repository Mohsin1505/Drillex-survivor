using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;
using UnityEngine.Rendering.Universal;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        var camMove = Object.FindAnyObjectByType<CameraMovement>();
        sb.AppendLine("CameraMovement json: " + EditorJsonUtility.ToJson(camMove));
        var sp = Object.FindAnyObjectByType<EnemySpawner>();
        var so = new SerializedObject(sp);
        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = it.propertyType == SerializedPropertyType.Generic && it.depth < 3;
            if (it.propertyType == SerializedPropertyType.ObjectReference)
                sb.AppendLine("  " + it.propertyPath + " = " + (it.objectReferenceValue ? it.objectReferenceValue.name : "null"));
            else if (it.propertyType == SerializedPropertyType.LayerMask) sb.AppendLine("  " + it.propertyPath + " mask=" + it.intValue);
            else if (it.propertyType == SerializedPropertyType.Float || it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.String)
                sb.AppendLine("  " + it.propertyPath + " = " + (it.propertyType == SerializedPropertyType.Float ? it.floatValue.ToString() : it.propertyType == SerializedPropertyType.String ? it.stringValue : it.intValue.ToString()));
        }
        foreach (var n in new[] { "FirstZone", "Zone 2", "Zone 2 (1)", "Plane (1)" })
        {
            var g = GameObject.Find(n);
            var r = g.GetComponent<Renderer>();
            sb.AppendLine(n + " layer=" + LayerMask.LayerToName(g.layer) + " mat=" + (r.sharedMaterial ? r.sharedMaterial.name + " shader=" + r.sharedMaterial.shader.name : "null"));
        }
        var walls = GameObject.Find("walls");
        sb.AppendLine("walls renderers enabled: " + string.Join(",", walls.GetComponentsInChildren<Renderer>().Select(r => r.enabled)));
        var sun = GameObject.Find("Directional Light").GetComponent<Light>();
        sb.AppendLine("sun int=" + sun.intensity + " col=" + sun.color + " shadows=" + sun.shadows + " temp=" + sun.useColorTemperature);
        sb.AppendLine("fog col=" + RenderSettings.fogColor + " dens=" + RenderSettings.fogDensity + " amb=" + RenderSettings.ambientLight + " skymat shader=" + (RenderSettings.skybox ? RenderSettings.skybox.shader.name : ""));
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            var a = QualitySettings.GetRenderPipelineAssetAt(i) as UniversalRenderPipelineAsset;
            sb.AppendLine("quality " + i + " " + QualitySettings.names[i] + " asset=" + (a ? a.name + " shadowDist=" + a.shadowDistance + " cascades=" + a.shadowCascadeCount + " hdr=" + a.supportsHDR + " msaa=" + a.msaaSampleCount + " softShadows=" + a.supportsSoftShadows : "null"));
        }
        sb.AppendLine("current quality " + QualitySettings.GetQualityLevel());
        sb.AppendLine("Android default quality: " + string.Join(",", QualitySettings.names));
        for (int l = 0; l < 32; l++) { var n = LayerMask.LayerToName(l); if (!string.IsNullOrEmpty(n)) sb.Append(l + ":" + n + " "); }
        sb.AppendLine();
        sb.AppendLine("build target " + EditorUserBuildSettings.activeBuildTarget);
        result.Log(sb.ToString());
    }
}
