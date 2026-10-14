using System.Collections.Generic;
using UnityEngine;

// Main menu presentation: staggered entrance, settings panel, platform-aware Exit button.
// Scene loading itself stays in SceneLoader.StartGame().
public class MainMenuController : MonoBehaviour
{
    public RectTransform title;
    public CanvasGroup titleGroup;
    public List<RectTransform> buttons = new List<RectTransform>();
    public GameObject exitButton;
    public SettingsPanel settingsPanel;
    public CanvasGroup footer;

    void Start()
    {
        Time.timeScale = 1f;
        // Exit makes no sense on iOS / WebGL, where apps don't quit themselves.
        if (exitButton != null)
            exitButton.SetActive(Application.platform != RuntimePlatform.IPhonePlayer && Application.platform != RuntimePlatform.WebGLPlayer);

        if (title != null)
        {
            Vector2 home = title.anchoredPosition;
            UITween.Move(title, home + new Vector2(-80f, 0f), home, 0.7f, UITween.Ease.OutCubic, null, 0.15f);
        }
        if (titleGroup != null) { titleGroup.alpha = 0f; UITween.Fade(titleGroup, 1f, 0.6f, UITween.Ease.OutCubic, null, 0.15f); }

        float delay = 0.35f;
        foreach (var b in buttons)
        {
            if (b == null || !b.gameObject.activeSelf) continue;
            var g = b.GetComponent<CanvasGroup>();
            if (g == null) g = b.gameObject.AddComponent<CanvasGroup>();
            g.alpha = 0f;
            Vector2 home = b.anchoredPosition;
            var rb = b;
            UITween.Run(g, 0.5f, k => { if (rb) { rb.anchoredPosition = Vector2.LerpUnclamped(home + new Vector2(-120f, 0f), home, k); g.alpha = Mathf.Clamp01(k * 1.4f); } }, UITween.Ease.OutCubic, null, delay);
            delay += 0.08f;
        }
        if (footer != null) { footer.alpha = 0f; UITween.Fade(footer, 1f, 0.6f, UITween.Ease.OutCubic, null, delay + 0.1f); }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && settingsPanel != null && settingsPanel.gameObject.activeSelf)
            settingsPanel.Close();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.Open();
    }
}
