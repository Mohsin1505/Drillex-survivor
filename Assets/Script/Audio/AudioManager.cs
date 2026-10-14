using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// The one persistent audio owner for Drillex. Created automatically before the first scene loads,
// survives scene changes, and never duplicates. Owns music (crossfading pair of sources), a small
// pooled set of SFX sources, and the player's saved volume settings.
//
// Volume model (no AudioMixer needed for this scope):
//   Master -> AudioListener.volume
//   Music  -> the two music sources
//   SFX    -> every pooled SFX source and anything that asks for SfxScale (looping weapon audio)
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public static event System.Action SettingsChanged;

    const string KeyMaster = "drx.audio.master";
    const string KeyMusic = "drx.audio.music";
    const string KeySfx = "drx.audio.sfx";
    const string KeyMusicOn = "drx.audio.musicOn";
    const string KeySfxOn = "drx.audio.sfxOn";

    public AudioLibrary Library { get; private set; }

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 0.8f;
    public float SfxVolume { get; private set; } = 0.9f;
    public bool MusicOn { get; private set; } = true;
    public bool SfxOn { get; private set; } = true;

    // Multiplier for gameplay sounds that run their own AudioSource (e.g. the flamethrower loop).
    public static float SfxScale => Instance == null ? 1f : (Instance.SfxOn ? Instance.SfxVolume : 0f);

    AudioSource[] music = new AudioSource[2];
    int activeMusic;
    float[] musicFade = { 0f, 0f };
    float[] musicFadeTarget = { 0f, 0f };
    float duck = 1f, duckTarget = 1f;

    readonly List<AudioSource> sfxPool = new List<AudioSource>();
    AudioSource uiSource;
    int nextSfx;
    readonly Dictionary<AudioClip, float> lastPlayed = new Dictionary<AudioClip, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[AudioManager]");
        DontDestroyOnLoad(go);
        go.AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Library = Resources.Load<AudioLibrary>("DrillexAudioLibrary");

        for (int i = 0; i < 2; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.ignoreListenerPause = true;
            s.spatialBlend = 0f;
            s.priority = 0;
            music[i] = s;
        }
        for (int i = 0; i < 12; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            sfxPool.Add(s);
        }
        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.ignoreListenerPause = true;

        LoadSettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // The first scene can already be loaded before this exists (always the case in the editor),
    // so start its music here too; PlayMusic ignores a repeat request for the same clip.
    void Start()
    {
        if (Instance == this) OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        SetPaused(false);
        if (Library == null) return;
        PlayMusic(scene.name == "MainMenu" ? Library.menuMusic : Library.gameplayMusic);
    }

    // ---------------- settings ----------------

    void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
        MusicVolume = PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        SfxVolume = PlayerPrefs.GetFloat(KeySfx, 0.9f);
        MusicOn = PlayerPrefs.GetInt(KeyMusicOn, 1) == 1;
        SfxOn = PlayerPrefs.GetInt(KeySfxOn, 1) == 1;
        ApplyListener();
    }

    // Writes the current in-memory settings. Also used to restore them after code that clears all PlayerPrefs.
    public static void SaveSettings()
    {
        if (Instance == null) return;
        PlayerPrefs.SetFloat(KeyMaster, Instance.MasterVolume);
        PlayerPrefs.SetFloat(KeyMusic, Instance.MusicVolume);
        PlayerPrefs.SetFloat(KeySfx, Instance.SfxVolume);
        PlayerPrefs.SetInt(KeyMusicOn, Instance.MusicOn ? 1 : 0);
        PlayerPrefs.SetInt(KeySfxOn, Instance.SfxOn ? 1 : 0);
        PlayerPrefs.Save();
    }

    public void SetMaster(float v) { MasterVolume = Mathf.Clamp01(v); ApplyListener(); Changed(); }
    public void SetMusic(float v) { MusicVolume = Mathf.Clamp01(v); Changed(); }
    public void SetSfx(float v) { SfxVolume = Mathf.Clamp01(v); Changed(); }
    public void SetMusicOn(bool on) { MusicOn = on; Changed(); }
    public void SetSfxOn(bool on) { SfxOn = on; Changed(); }

    void ApplyListener() { AudioListener.volume = MasterVolume; }

    void Changed()
    {
        SaveSettings();
        SettingsChanged?.Invoke();
    }

    // ---------------- pause ----------------

    // Pauses gameplay SFX (via AudioListener.pause) and ducks the music; UI sounds keep working.
    public void SetPaused(bool paused)
    {
        AudioListener.pause = paused;
        duckTarget = paused ? 0.4f : 1f;
    }

    // ---------------- music ----------------

    public void PlayMusic(AudioClip clip, float fade = 1.5f)
    {
        if (clip == null) return;
        if (music[activeMusic].clip == clip && music[activeMusic].isPlaying) return; // already playing: no duplicate
        int next = 1 - activeMusic;
        musicFadeTarget[activeMusic] = 0f;
        var s = music[next];
        s.clip = clip;
        s.time = 0f;
        s.Play();
        musicFade[next] = 0f;
        musicFadeTarget[next] = 1f;
        activeMusic = next;
        fadeSpeed = 1f / Mathf.Max(0.05f, fade);
    }

    float fadeSpeed = 1f;

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        duck = Mathf.MoveTowards(duck, duckTarget, dt * 2f);
        float baseVol = (Library ? Library.musicBaseVolume : 0.55f) * (MusicOn ? MusicVolume : 0f) * duck;
        for (int i = 0; i < 2; i++)
        {
            musicFade[i] = Mathf.MoveTowards(musicFade[i], musicFadeTarget[i], dt * fadeSpeed);
            music[i].volume = baseVol * musicFade[i];
            if (musicFade[i] <= 0f && musicFadeTarget[i] <= 0f && music[i].isPlaying) music[i].Stop();
        }
    }

    // ---------------- sfx ----------------

    // Plays a gameplay sound. Position (optional) gives simple distance falloff from the camera.
    // minInterval throttles very fast repeats (machine gun) so they don't stack into noise.
    public void PlaySfx(AudioClip clip, float volume = 1f, float pitchJitter = 0.05f, Vector3? position = null, float minInterval = 0f)
    {
        if (clip == null || !SfxOn || SfxVolume <= 0f) return;
        float now = Time.unscaledTime;
        if (minInterval > 0f && lastPlayed.TryGetValue(clip, out float last) && now - last < minInterval) return;
        lastPlayed[clip] = now;

        if (position.HasValue)
        {
            var cam = Camera.main;
            if (cam != null)
            {
                float d = Vector3.Distance(cam.transform.position, position.Value);
                volume *= Mathf.Clamp01(1f - (d - 18f) / 70f);
                if (volume <= 0.01f) return;
            }
        }

        var s = sfxPool[nextSfx];
        nextSfx = (nextSfx + 1) % sfxPool.Count;
        s.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        s.volume = 1f;
        s.PlayOneShot(clip, volume * SfxVolume);
    }

    public void PlayUI(AudioClip clip, float volume = 1f)
    {
        if (clip == null || !SfxOn || SfxVolume <= 0f) return;
        uiSource.PlayOneShot(clip, volume * SfxVolume);
    }

    // Convenience wrappers so gameplay scripts stay one-liners and safe when audio is missing.
    public static void Sfx(System.Func<AudioLibrary, AudioClip> pick, float volume = 1f, Vector3? position = null, float minInterval = 0f, float pitchJitter = 0.05f)
    {
        if (Instance == null || Instance.Library == null) return;
        Instance.PlaySfx(pick(Instance.Library), volume, pitchJitter, position, minInterval);
    }

    public static void Ui(System.Func<AudioLibrary, AudioClip> pick, float volume = 1f)
    {
        if (Instance == null || Instance.Library == null) return;
        Instance.PlayUI(pick(Instance.Library), volume);
    }

    public static void PlayUiClick() => Ui(l => l.uiClick, 0.7f);
}
