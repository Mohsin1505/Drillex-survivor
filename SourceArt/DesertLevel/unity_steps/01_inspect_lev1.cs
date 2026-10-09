using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Linq;
using System.Text;

internal class CommandScript : IRunCommand
{
    static string Desc(Transform t)
    {
        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
        var r = t.GetComponent<Renderer>();
        string b = r ? " bounds c=" + r.bounds.center.ToString("F1") + " s=" + r.bounds.size.ToString("F1") : "";
        return t.name + " pos=" + t.position.ToString("F1") + " rot=" + t.eulerAngles.ToString("F0") + " scl=" + t.localScale.ToString("F1") + " [" + comps + "]" + b + (t.gameObject.activeSelf ? "" : " (inactive)");
    }

    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        foreach (var go in scene.GetRootGameObjects())
        {
            sb.AppendLine(Desc(go.transform));
            foreach (Transform c in go.transform)
            {
                sb.AppendLine("   " + Desc(c));
                if (c.childCount > 0 && c.childCount < 12)
                    foreach (Transform cc in c) sb.AppendLine("      " + Desc(cc));
                else if (c.childCount >= 12) sb.AppendLine("      (" + c.childCount + " children)");
            }
        }
        sb.AppendLine("RenderSettings fog=" + RenderSettings.fog + " mode=" + RenderSettings.fogMode + " amb=" + RenderSettings.ambientMode + " sky=" + (RenderSettings.skybox ? RenderSettings.skybox.name : "null"));
        var cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in cams) sb.AppendLine("CAM " + c.name + " fov=" + c.fieldOfView + " ortho=" + c.orthographic + " far=" + c.farClipPlane + " pos=" + c.transform.position.ToString("F1") + " rot=" + c.transform.eulerAngles.ToString("F0"));
        var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        sb.AppendLine("pipeline " + (rp ? rp.name : "null"));
        var vols = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var v in vols) sb.AppendLine("VOLUME " + v.name + " profile=" + (v.sharedProfile ? v.sharedProfile.name + " comps=" + string.Join(",", v.sharedProfile.components.Select(c => c.GetType().Name)) : "null"));
        result.Log(sb.ToString());
    }
}
