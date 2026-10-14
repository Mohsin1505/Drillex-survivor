using UnityEngine;

// Editor-only convenience: keep Play mode running when the Game view loses focus
// (e.g. while tools or a second monitor are in use). Has no effect in builds.
static class EditorPlayInBackground
{
#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Enable() { Application.runInBackground = true; }
#endif
}
