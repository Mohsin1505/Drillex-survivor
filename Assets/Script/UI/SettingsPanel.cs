using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Sound & music settings. Bound to the persistent AudioManager, which saves to PlayerPrefs
// on every change and restores at launch. Used by both the main menu and the pause menu.
public class SettingsPanel : MonoBehaviour
{
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;
    public Toggle musicToggle;
    public Toggle sfxToggle;
    public TMP_Text masterValue;
    public TMP_Text musicValue;
    public TMP_Text sfxValue;

    bool binding;

    void Awake()
    {
        masterSlider.onValueChanged.AddListener(v => { if (!binding) AudioManager.Instance?.SetMaster(v); Refresh(); });
        musicSlider.onValueChanged.AddListener(v => { if (!binding) AudioManager.Instance?.SetMusic(v); Refresh(); });
        sfxSlider.onValueChanged.AddListener(v => { if (!binding) { AudioManager.Instance?.SetSfx(v); PreviewSfx(); } Refresh(); });
        musicToggle.onValueChanged.AddListener(on => { if (!binding) { AudioManager.Instance?.SetMusicOn(on); AudioManager.PlayUiClick(); } Refresh(); });
        sfxToggle.onValueChanged.AddListener(on => { if (!binding) { AudioManager.Instance?.SetSfxOn(on); AudioManager.PlayUiClick(); } Refresh(); });
    }

    void OnEnable() { Bind(); }

    float lastPreview;

    void PreviewSfx()
    {
        if (Time.unscaledTime - lastPreview < 0.15f) return;
        lastPreview = Time.unscaledTime;
        AudioManager.Ui(l => l.orbPickup, 0.8f);
    }

    void Bind()
    {
        var a = AudioManager.Instance;
        if (a == null) return;
        binding = true;
        masterSlider.value = a.MasterVolume;
        musicSlider.value = a.MusicVolume;
        sfxSlider.value = a.SfxVolume;
        musicToggle.isOn = a.MusicOn;
        sfxToggle.isOn = a.SfxOn;
        binding = false;
        Refresh();
    }

    void Refresh()
    {
        if (masterValue) masterValue.text = Mathf.RoundToInt(masterSlider.value * 100f) + "%";
        if (musicValue) musicValue.text = Mathf.RoundToInt(musicSlider.value * 100f) + "%";
        if (sfxValue) sfxValue.text = Mathf.RoundToInt(sfxSlider.value * 100f) + "%";
        // dim the volume slider of a muted channel so the state reads at a glance
        SetDim(musicSlider, !musicToggle.isOn);
        SetDim(sfxSlider, !sfxToggle.isOn);
    }

    static void SetDim(Slider s, bool dim)
    {
        var g = s.GetComponent<CanvasGroup>();
        if (g == null) g = s.gameObject.AddComponent<CanvasGroup>();
        g.alpha = dim ? 0.4f : 1f;
    }

    public void Open()
    {
        gameObject.SetActive(true);
        AudioManager.Ui(l => l.uiOpen, 0.8f);
    }

    public void Close()
    {
        AudioManager.Ui(l => l.uiClose, 0.8f);
        var p = GetComponent<UIPanel>();
        if (p != null) p.Hide(); else gameObject.SetActive(false);
    }
}
