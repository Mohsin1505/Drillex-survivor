using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public GameObject pausePanel;

    [Header("Optional")]
    public SettingsPanel settingsPanel;

    [Tooltip("HUD elements hidden while paused (pause button, joystick, ability button).")]
    public CanvasGroup hudGroup;

    public static bool IsPaused { get; private set; }

    // timeScale to restore on resume. Pausing over the upgrade screen (timeScale 0) keeps it at 0;
    // if that screen closes while paused, UpgradeStation raises it back to 1 via SetResumeTimeScale.
    private static float previousTimeScale = 1f;

    public static void SetResumeTimeScale(float value)
    {
        previousTimeScale = value;
    }

    void Start()
    {
        pausePanel.SetActive(false);
        IsPaused = false;
    }

    void OnDestroy()
    {
        if (IsPaused)
        {
            IsPaused = false;
            if (AudioManager.Instance != null) AudioManager.Instance.SetPaused(false);
        }
    }

    void Update()
    {
        // Escape / Android back button toggles pause
        if (Input.GetKeyDown(KeyCode.Escape) && !SceneTransition.IsBusy)
        {
            if (settingsPanel != null && settingsPanel.gameObject.activeSelf) settingsPanel.Close();
            else if (IsPaused) ResumeGame();
            else if (Time.timeScale > 0f) PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPaused || SceneTransition.IsBusy)
            return;

        IsPaused = true;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        MobileInput.Steer = 0f;

        if (AudioManager.Instance != null) AudioManager.Instance.SetPaused(true);
        AudioManager.Ui(l => l.uiOpen, 0.8f);
        SetHud(false);

        pausePanel.SetActive(true);
    }

    public void ResumeGame()
    {
        if (!IsPaused)
            return;

        IsPaused = false;
        if (settingsPanel != null) settingsPanel.gameObject.SetActive(false);

        AudioManager.Ui(l => l.uiClose, 0.8f);
        UIPanel panel = pausePanel.GetComponent<UIPanel>();
        if (panel != null) panel.Hide();
        else pausePanel.SetActive(false);

        Time.timeScale = previousTimeScale;
        if (AudioManager.Instance != null) AudioManager.Instance.SetPaused(false);
        SetHud(true);
    }

    // Reloads the current level. There is no persistent save system, so this resets the
    // level to its scene state exactly like the existing Abandon flow, without clearing PlayerPrefs.
    public void RestartLevel()
    {
        IsPaused = false;
        SceneTransition.Reload();
    }

    public void OpenSettings()
    {
        if (settingsPanel != null) settingsPanel.Open();
    }

    public void MainMenu()
    {
        IsPaused = false;
        SceneTransition.Load("MainMenu");
    }

    void SetHud(bool visible)
    {
        if (hudGroup == null) return;
        hudGroup.interactable = visible;
        hudGroup.blocksRaycasts = visible;
        UITween.Fade(hudGroup, visible ? 1f : 0.35f, 0.2f);
    }
}
