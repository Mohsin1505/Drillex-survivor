using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using System.Text;
using System.Linq;

internal class CommandScript : IRunCommand
{
    static void Dump(Transform t, int d, StringBuilder sb)
    {
        var rt = t as RectTransform;
        sb.Append(new string(' ', d * 2)).Append(t.name);
        if (!t.gameObject.activeSelf) sb.Append(" [off]");
        if (rt) sb.AppendFormat(" a{0}-{1} p{2} s{3}", rt.anchorMin, rt.anchorMax, rt.anchoredPosition, rt.sizeDelta);
        foreach (var c in t.GetComponents<Component>())
        {
            if (c is Transform) continue;
            if (c == null) { sb.Append(" |MISSING"); continue; }
            sb.Append(" |").Append(c.GetType().Name);
            if (c is UnityEngine.UI.Image im) sb.AppendFormat("({0},{1},{2})", im.sprite ? im.sprite.name : "-", ColorUtility.ToHtmlStringRGBA(im.color), im.type);
            if (c is TMPro.TMP_Text tx) sb.AppendFormat("(\"{0}\",{1},{2},{3})", tx.text.Replace("\n","\n").Substring(0, Mathf.Min(40, tx.text.Length)), tx.font ? tx.font.name : "-", tx.fontSize, ColorUtility.ToHtmlStringRGBA(tx.color));
            if (c is UnityEngine.UI.Text lt) sb.AppendFormat("(LEGACY \"{0}\")", lt.text);
            if (c is CanvasScaler cs) sb.AppendFormat("({0},{1},{2})", cs.uiScaleMode, cs.referenceResolution, cs.matchWidthOrHeight);
        }
        sb.AppendLine();
        foreach (Transform ch in t) Dump(ch, d + 1, sb);
    }
    public void Execute(ExecutionResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ACTIVE " + EditorSceneManager.GetActiveScene().path + " dirty=" + EditorSceneManager.GetActiveScene().isDirty + " playing=" + EditorApplication.isPlaying);
        foreach (var path in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/Lev1.unity"})
        {
            var s = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            sb.AppendLine("=== " + path);
            foreach (var root in s.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Canvas>(true).Where(c => c.isRootCanvas))
                { sb.AppendLine("CANVAS " + c.renderMode); Dump(c.transform, 0, sb); }
            if (EditorSceneManager.GetActiveScene() != s) EditorSceneManager.CloseScene(s, true);
        }
        System.IO.File.WriteAllText(@"D:\Drillex\SourceArt\UI\ui_dump.txt", sb.ToString());
        result.Log("ok " + sb.Length);
    }
}
