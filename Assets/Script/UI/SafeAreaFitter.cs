using UnityEngine;

// Keeps HUD content inside the device safe area (notches, rounded corners, home indicator).
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    Rect applied;
    Vector2Int screen;

    void OnEnable() { Apply(); }

    void Update()
    {
        if (applied != Screen.safeArea || screen.x != Screen.width || screen.y != Screen.height) Apply();
    }

    void Apply()
    {
        var rt = (RectTransform)transform;
        Rect sa = Screen.safeArea;
        applied = sa;
        screen = new Vector2Int(Screen.width, Screen.height);
        if (Screen.width <= 0 || Screen.height <= 0) return;
        rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
