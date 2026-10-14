using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Persistent black fade used for every scene change (menu -> level, restart, back to menu).
// Uses unscaled time and always restores Time.timeScale = 1 before loading.
public class SceneTransition : MonoBehaviour
{
    static SceneTransition instance;
    CanvasGroup group;
    bool busy;

    static SceneTransition Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[SceneTransition]");
                DontDestroyOnLoad(go);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                go.AddComponent<CanvasScaler>();
                var img = new GameObject("Fade").AddComponent<Image>();
                img.transform.SetParent(go.transform, false);
                img.color = new Color(0.02f, 0.02f, 0.025f, 1f);
                var rt = img.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                instance = go.AddComponent<SceneTransition>();
                instance.group = go.AddComponent<CanvasGroup>();
                instance.group.alpha = 0f;
                instance.group.blocksRaycasts = false;
            }
            return instance;
        }
    }

    public static bool IsBusy => instance != null && instance.busy;

    public static void Load(string sceneName)
    {
        Instance.StartCoroutine(Instance.Run(() => SceneManager.LoadSceneAsync(sceneName)));
    }

    public static void Reload()
    {
        int index = SceneManager.GetActiveScene().buildIndex;
        Instance.StartCoroutine(Instance.Run(() => SceneManager.LoadSceneAsync(index)));
    }

    IEnumerator Run(System.Func<AsyncOperation> load)
    {
        if (busy) yield break;
        busy = true;
        group.blocksRaycasts = true;
        for (float t = group.alpha; t < 1f; t += Time.unscaledDeltaTime / 0.35f) { group.alpha = t; yield return null; }
        group.alpha = 1f;
        Time.timeScale = 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.SetPaused(false);
        var op = load();
        while (op != null && !op.isDone) yield return null;
        yield return null;
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.45f) { group.alpha = t; yield return null; }
        group.alpha = 0f;
        group.blocksRaycasts = false;
        busy = false;
    }
}
