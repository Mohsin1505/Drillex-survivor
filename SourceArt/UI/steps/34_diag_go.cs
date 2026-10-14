using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var go = Object.FindAnyObjectByType<GameOverButtons>(FindObjectsInactive.Include);
        var img = go.GetComponent<UnityEngine.UI.Image>();
        var cg = go.GetComponent<CanvasGroup>();
        result.Log("img enabled {0} color {1} sprite {2} alpha cg {3} rt {4} {5}", img.enabled, img.color, img.sprite != null, cg ? cg.alpha : -1, ((RectTransform)go.transform).rect, go.transform.parent.name);
        result.Log("canvas render alpha {0}", img.canvasRenderer.GetAlpha());
    }
}
