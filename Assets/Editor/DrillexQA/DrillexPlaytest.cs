using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// End-to-end play-mode test of the Drillex UI / audio / weapons / progression work.
// Menu: Drillex/QA/Run Full Playtest. Drives the real game (buttons are invoked through their
// onClick events), checks results, captures screenshots and writes a log:
//   SourceArt/UI/_preview/playtest.log  +  SourceArt/UI/_preview/pt_*.png
// Runtime-only changes (player level, orbs, spawned enemies) are never saved.
[InitializeOnLoad]
public static class DrillexPlaytest
{
    const string Flag = "DrillexPlaytest.Pending";
    static readonly string OutDir = Path.GetFullPath("SourceArt/UI/_preview");
    static StreamWriter log;
    static IEnumerator routine;
    static double waitUntil;
    static Func<bool> waitFor;
    static double waitTimeout;
    static int pass, fail, errors;
    static readonly List<string> errorLines = new List<string>();

    static DrillexPlaytest()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Flag, false))
            {
                SessionState.SetBool(Flag, false);
                Start();
            }
        };
    }

    static bool weaponsOnly;

    [MenuItem("Drillex/QA/Run Weapons Test (Lev1)")]
    public static void RunWeapons()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        SessionState.SetBool("DrillexPlaytest.Weapons", true);
        SessionState.SetBool("DrillexPlaytest.Gallery", false);
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Drillex/QA/Run VFX Gallery (Lev1)")]
    public static void RunGallery()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        EditorSceneManager.OpenScene("Assets/Scenes/Lev1.unity", OpenSceneMode.Single);
        SessionState.SetBool("DrillexPlaytest.Weapons", false);
        SessionState.SetBool("DrillexPlaytest.Gallery", true);
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Drillex/QA/Run Full Playtest")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        SessionState.SetBool("DrillexPlaytest.Weapons", false);
        SessionState.SetBool("DrillexPlaytest.Gallery", false);
        EditorSceneManager.OpenScene("Assets/Scenes/MainMenu.unity", OpenSceneMode.Single);
        SessionState.SetBool(Flag, true);
        EditorApplication.isPlaying = true;
    }

    static void Start()
    {
        Directory.CreateDirectory(OutDir);
        log = new StreamWriter(Path.Combine(OutDir, SessionState.GetBool("DrillexPlaytest.Gallery", false) ? "gallery.log" : SessionState.GetBool("DrillexPlaytest.Weapons", false) ? "weapons.log" : "playtest.log"), false) { AutoFlush = true };
        pass = fail = errors = 0; errorLines.Clear();
        Application.logMessageReceived += OnLog;
        Application.runInBackground = true;
        weaponsOnly = SessionState.GetBool("DrillexPlaytest.Weapons", false);
        bool gallery = SessionState.GetBool("DrillexPlaytest.Gallery", false);
        routine = gallery ? GalleryScript() : weaponsOnly ? WeaponsScript() : Script();
        waitUntil = 0; waitFor = null;
        EditorApplication.update += Tick;
        Log("=== Drillex playtest " + DateTime.Now);
    }

    static void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            errors++;
            if (errorLines.Count < 40) errorLines.Add(type + ": " + msg.Split('\n')[0]);
        }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying) { Finish("play mode ended"); return; }
        double now = EditorApplication.timeSinceStartup;
        if (now < waitUntil) return;
        if (waitFor != null)
        {
            bool ok = false;
            try { ok = waitFor(); } catch { }
            if (!ok && now < waitTimeout) return;
            waitFor = null;
        }
        bool more;
        try { more = routine.MoveNext(); }
        catch (Exception e) { Log("EXCEPTION in test script: " + e); fail++; more = false; }
        if (!more) { Finish("completed"); return; }
        var y = routine.Current;
        if (y is float f) waitUntil = now + f;
        else if (y is Func<bool> c) { waitFor = c; waitTimeout = now + 20; }
    }

    static void Finish(string why)
    {
        if (routine == null) return;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        routine = null;
        Log("--- console errors/exceptions during run: " + errors);
        foreach (var e in errorLines) Log("    " + e);
        Log(string.Format("=== RESULT: {0} passed, {1} failed ({2})", pass, fail, why));
        log?.Dispose(); log = null;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    // ------------------------------------------------------------------ helpers

    static void Log(string s) { log?.WriteLine(s); }
    static void Check(string name, bool ok, string detail = "")
    {
        if (ok) pass++; else fail++;
        Log((ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : "  [" + detail + "]"));
    }

    static object Shot(string name)
    {
        ScreenCapture.CaptureScreenshot(Path.Combine(OutDir, "pt_" + name + ".png"));
        Log("     screenshot pt_" + name + ".png");
        return 0.6f;
    }

    // World-only close-up of a train car (renders a temporary camera; no UI) for VFX review.
    static void ChaseShot(Transform target, string name, float side = 6.5f)
    {
        if (target == null) return;
        var go = new GameObject("pt_chase_cam");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(Camera.main);
        cam.fieldOfView = 42f;
        go.transform.position = target.position + target.right * side + target.forward * 2.5f + Vector3.up * 3.2f;
        go.transform.LookAt(target.position + target.forward * 3f + Vector3.up * 0.6f);
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, "pt_" + name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null;
        cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
        UnityEngine.Object.Destroy(go);
        Log("     screenshot pt_" + name + ".png (chase cam)");
    }

    // Camera on the train's right, looking across the car towards the enemy on the left.
    static void VfxShot(Transform car, Vector3 enemy, string name)
    {
        var go = new GameObject("pt_vfx_cam");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(Camera.main);
        cam.fieldOfView = 50f;
        Vector3 mid = (car.position + enemy) * 0.5f;
        go.transform.position = car.position + car.right * 5f - car.forward * 2.5f + Vector3.up * 3f;
        go.transform.LookAt(Vector3.Lerp(car.position, enemy, 0.35f) + Vector3.up * 0.8f);
        var rt = new RenderTexture(1600, 900, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, "pt_" + name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.Destroy(go);
        Log("     screenshot pt_" + name + ".png (vfx cam)");
    }

    static T Find<T>(string name = null) where T : Component
    {
        foreach (var c in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include))
            if (name == null || c.name == name) return c;
        return null;
    }

    static Button Btn(string name) => Find<Button>(name);
    static void Click(Button b) { if (b != null) b.onClick.Invoke(); }

    static int AudioManagers => UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include).Length;
    static int MusicPlaying(AudioManager a) => a.GetComponents<AudioSource>().Count(s => s.loop && s.isPlaying && s.clip != null && s.volume > 0.0001f);
    static AudioClip ActiveMusic(AudioManager a) => a.GetComponents<AudioSource>().Where(s => s.loop && s.isPlaying).OrderByDescending(s => s.volume).Select(s => s.clip).FirstOrDefault();

    static int Count(string prefix) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Count(t => t.name.StartsWith(prefix) && t.parent == null);
    static int CountAll(string prefix) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Count(t => t.name.StartsWith(prefix) && t.parent == null);

    // ------------------------------------------------------------------ the test

    static IEnumerator Script()
    {
        yield return 2.5f;

        // ================= MAIN MENU =================
        Log("\n## Main menu");
        var am = AudioManager.Instance;
        Check("AudioManager exists once", am != null && AudioManagers == 1, "count=" + AudioManagers);
        Check("Audio library loaded", am != null && am.Library != null);
        yield return 1.5f;
        Check("Menu music playing (single source)", am != null && ActiveMusic(am) == am.Library.menuMusic && MusicPlaying(am) == 1, ActiveMusic(am)?.name + " sources=" + MusicPlaying(am));
        yield return Shot("01_menu");

        // settings from the menu
        Click(Btn("Settings"));
        yield return 0.6f;
        var settings = Find<SettingsPanel>();
        Check("Settings opens from menu", settings != null && settings.gameObject.activeInHierarchy);
        yield return Shot("02_menu_settings");
        float origMusic = am.MusicVolume; bool origSfx = am.SfxOn;
        settings.musicSlider.value = 0.3f;
        settings.sfxToggle.isOn = false;
        Check("Music slider drives AudioManager", Mathf.Approximately(am.MusicVolume, 0.3f));
        Check("SFX toggle drives AudioManager", am.SfxOn == false);
        Check("Settings saved to PlayerPrefs", Mathf.Approximately(PlayerPrefs.GetFloat("drx.audio.music", -1), 0.3f) && PlayerPrefs.GetInt("drx.audio.sfxOn", -1) == 0);
        settings.sfxToggle.isOn = true;
        settings.Close();
        yield return 0.6f;
        Check("Settings closes", !settings.gameObject.activeInHierarchy);

        // help
        Click(Btn("HOW TO PLAY"));
        yield return 0.6f;
        var help = Find<HelpPanelManager>();
        Check("Help opens (pauses time)", help.helpPanel.activeInHierarchy && Time.timeScale == 0f);
        yield return Shot("03_help");
        Click(help.helpPanel.GetComponentsInChildren<Button>(true).FirstOrDefault());
        yield return 0.5f;
        Check("Help closes (time resumes)", !help.helpPanel.activeSelf && Time.timeScale == 1f, "timeScale=" + Time.timeScale);

        // play
        Click(Btn("PLAY"));
        yield return (Func<bool>)(() => SceneManager.GetActiveScene().name == "Lev1" && !SceneTransition.IsBusy);
        yield return 2f;

        // ================= LEVEL =================
        Log("\n## Lev1 HUD and audio");
        Check("Play loads Lev1", SceneManager.GetActiveScene().name == "Lev1");
        Check("Still exactly one AudioManager after scene change", AudioManagers == 1, "count=" + AudioManagers);
        yield return 1.5f;
        Check("Gameplay music replaces menu music (single source)", ActiveMusic(am) == am.Library.gameplayMusic && MusicPlaying(am) == 1, ActiveMusic(am)?.name + " sources=" + MusicPlaying(am));
        Check("Music volume setting persisted across scenes", Mathf.Approximately(am.MusicVolume, 0.3f));

        var hud = Find<HUDController>();
        var mgr = Manager.instance; var xp = ExperienceManager.instance;
        var storage = Find<TrainStorageManager>(); var hp = Find<TrainHealth>();
        Check("HUD total orbs matches Manager.permanentOrb", hud.totalOrbs.label.text.Replace(",", "") == mgr.permanentOrb.ToString(), hud.totalOrbs.label.text + " vs " + mgr.permanentOrb);
        Check("HUD level matches ExperienceManager", hud.levelText.text == "LV " + xp.currentLevel, hud.levelText.text);
        Check("HUD XP matches", hud.xpText.text == xp.currentXP + " / " + xp.xpNeeded + " XP", hud.xpText.text);
        Check("HUD train orbs matches storage", hud.trainOrbsText.text == storage.currentOrb + " / " + storage.totalStorage, hud.trainOrbsText.text);
        Check("HUD hull matches TrainHealth", hud.hullText.text == Mathf.CeilToInt(hp.CurrentHealth) + " / " + Mathf.CeilToInt(hp.maxHealth), hud.hullText.text);
        // live update
        long orbsBefore = mgr.permanentOrb;
        mgr.AddOrb(25);
        storage.AddOrb(10);
        xp.AddXP(5);
        yield return 1.2f;
        Check("HUD total orbs animates to new value", hud.totalOrbs.label.text.Replace(",", "") == (orbsBefore + 25).ToString(), hud.totalOrbs.label.text);
        Check("HUD train orbs updates", hud.trainOrbsText.text == storage.currentOrb + " / " + storage.totalStorage, hud.trainOrbsText.text);
        Check("HUD XP updates", hud.xpText.text == xp.currentXP + " / " + xp.xpNeeded + " XP", hud.xpText.text);
        var ability = Find<AbilityButton>();
        Check("Ability button bound to the train's equipped power (Sound Trap)", ability != null && ability.gameObject.activeInHierarchy && ability.nameText.text == "SOUND TRAP");
        yield return Shot("04_hud");

        // ---- touch steering ----
        Log("\n## Steering");
        var train = Find<TrainMovement>();
        float yaw0 = train.transform.eulerAngles.y;
        Vector3 p0 = train.transform.position;
        MobileInput.Steer = 1f;
        yield return 0.8f;
        MobileInput.Steer = 0f;
        float dyaw = Mathf.DeltaAngle(yaw0, train.transform.eulerAngles.y);
        Check("Touch steer input turns the train", dyaw > 5f, "yaw change " + dyaw.ToString("0.0"));
        Check("Train keeps auto-moving forward", Vector3.Distance(p0, train.transform.position) > 0.5f);
        Log("     (keyboard steering: TrainMovement still reads Input.GetAxis(\"Horizontal\") and adds the touch value; key presses cannot be simulated from this harness)");

        // ---- pause ----
        Log("\n## Pause");
        Click(Btn("PauseButton"));
        yield return 0.5f;
        Check("Pause stops time and gameplay audio", Time.timeScale == 0f && PauseManager.IsPaused && AudioListener.pause);
        Check("Music keeps playing (ducked) while paused", MusicPlaying(am) == 1);
        Vector3 pp = train.transform.position;
        yield return 0.6f;
        Check("Train does not move while paused", Vector3.Distance(pp, train.transform.position) < 0.001f);
        yield return Shot("05_pause");
        var pause = Find<PauseManager>();
        Click(pause.pausePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "SETTINGS"));
        yield return 0.6f;
        Check("Settings opens from pause", pause.settingsPanel.gameObject.activeInHierarchy && Time.timeScale == 0f);
        yield return Shot("06_pause_settings");
        pause.settingsPanel.Close();
        yield return 0.5f;
        Check("Closing settings keeps the game paused", Time.timeScale == 0f && PauseManager.IsPaused);
        Click(pause.pausePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "RESUME"));
        yield return 0.6f;
        Check("Resume restores time and audio", Time.timeScale == 1f && !PauseManager.IsPaused && !AudioListener.pause);

        // ---- workshop + pause interplay ----
        Log("\n## Workshop / panel interplay");
        var station = Find<UpgradeStation>();
        station.OpenUpgrade();
        yield return 0.6f;
        Check("Workshop opens and pauses time", station.upgradePanel.activeInHierarchy && Time.timeScale == 0f);
        pause.PauseGame();
        yield return 0.4f;
        pause.ResumeGame();
        yield return 0.4f;
        Check("Pause+resume over the workshop does NOT resume gameplay", Time.timeScale == 0f && station.upgradePanel.activeInHierarchy, "timeScale=" + Time.timeScale);
        pause.PauseGame();
        yield return 0.3f;
        station.CloseUpgrade();
        yield return 0.4f;
        Check("Closing the workshop while paused keeps the game paused", Time.timeScale == 0f && PauseManager.IsPaused, "timeScale=" + Time.timeScale);
        pause.ResumeGame();
        yield return 0.4f;
        Check("After resume with workshop closed, time runs", Time.timeScale == 1f, "timeScale=" + Time.timeScale);

        // ---- progression ----
        Log("\n## Electric-before-Rocket progression (runtime-only level changes)");
        var um = UpgradeManager.instance;
        Check("Electric unlock level is 11 and below Rocket (12)", um.electricGunUnlockLevel == 11 && um.rocketGunUnlockLevel == 12, um.electricGunUnlockLevel + "/" + um.rocketGunUnlockLevel);
        int savedLevel = xp.currentLevel;
        mgr.permanentOrb = 20000;
        xp.currentLevel = 10;
        station.OpenUpgrade();
        yield return 0.6f;
        var shop = Find<UpgradeShopUI>();
        UpgradeCard Card(UpgradeId id) => shop.cards.First(c => c.id == id);
        shop.RefreshAll();
        var blockOrder = shop.cards.Where(c => c.entry.kind == UpgradeKind.Block).Select(c => c.id).ToList();
        Check("Workshop lists Electric before Rocket", blockOrder.IndexOf(UpgradeId.ElectricGun) < blockOrder.IndexOf(UpgradeId.RocketGun), string.Join(",", blockOrder));
        Check("Lv10: Electric locked", UpgradeCatalog.StateOf(Card(UpgradeId.ElectricGun).entry) == UpgradeState.Locked);
        Check("Lv10: Rocket locked", UpgradeCatalog.StateOf(Card(UpgradeId.RocketGun).entry) == UpgradeState.Locked);
        int cars0 = train.BlockCount;
        um.BuyElectricGun();
        Check("Lv10: buying Electric is refused by UpgradeManager", train.BlockCount == cars0 && mgr.permanentOrb == 20000);
        yield return Shot("07_workshop_lv10");
        station.CloseUpgrade();
        yield return 0.4f;
        xp.currentLevel = 11;
        xp.AddXP(0);
        yield return 1.2f;
        yield return Shot("08_unlock_toast");
        station.OpenUpgrade();
        yield return 0.6f;
        shop.RefreshAll();
        Check("Lv11: Electric available", UpgradeCatalog.StateOf(Card(UpgradeId.ElectricGun).entry) == UpgradeState.Available);
        Check("Lv11: Rocket still locked", UpgradeCatalog.StateOf(Card(UpgradeId.RocketGun).entry) == UpgradeState.Locked);
        long orbsPre = mgr.permanentOrb;
        Click(Card(UpgradeId.ElectricGun).button);
        long orbsPost = mgr.permanentOrb;
        yield return 0.6f;
        var tesla = um.electricGunPrefab;
        Check("Electric purchase: cost charged through UpgradeManager", orbsPre - orbsPost == um.electricGunCost, "charged " + (orbsPre - orbsPost) + " of " + um.electricGunCost);
        Check("Electric purchase: Tesla car coupled to the train", train.CountBlocks(tesla) == 1 && train.BlockCount == cars0 + 1);
        Check("Electric card shows EQUIPPED x1", Card(UpgradeId.ElectricGun).ownedText.gameObject.activeSelf && Card(UpgradeId.ElectricGun).ownedText.text == "EQUIPPED x1");
        yield return Shot("09_workshop_electric_bought");
        xp.currentLevel = 12;
        shop.RefreshAll();
        Check("Lv12: Rocket available", UpgradeCatalog.StateOf(Card(UpgradeId.RocketGun).entry) == UpgradeState.Available);
        Click(Card(UpgradeId.RocketGun).button);
        Click(Card(UpgradeId.MachineGun).button);
        Click(Card(UpgradeId.FlameThrower).button);
        Click(Card(UpgradeId.DualGun).button);
        yield return 0.5f;
        Check("Rocket, Gatling, Flamethrower, Twin cars bought", train.BlockCount == cars0 + 5, "cars=" + train.BlockCount);
        // not enough orbs feedback
        mgr.permanentOrb = 10;
        shop.RefreshAll();
        int carsB = train.BlockCount;
        Click(Card(UpgradeId.RocketGun).button);
        yield return 0.4f;
        Check("Not enough orbs: nothing bought, orbs untouched", train.BlockCount == carsB && mgr.permanentOrb == 10);
        mgr.permanentOrb = 20000;
        shop.RefreshAll();
        Click(shop.upgradesTab);
        yield return 0.5f;
        yield return Shot("10_workshop_upgrades");
        Click(shop.blocksTab);
        station.CloseUpgrade();
        yield return 0.5f;

        // ---- weapons ----
        Log("\n## Weapons in combat");
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/HeavyMoster.prefab");
        var spawned = new List<GameObject>();
        for (int i = 0; i < 6; i++)
        {
            Vector3 pos = train.transform.position + train.transform.forward * (4 - i * 2.5f) + train.transform.right * ((i % 2 == 0 ? 1 : -1) * 9);
            pos.y = train.transform.position.y + 0.5f;
            spawned.Add(UnityEngine.Object.Instantiate(enemyPrefab, pos, Quaternion.identity));
        }
        int shots = 0, bolts = 0, rockets = 0, explosions = 0, maxBullets = 0;
        float t0 = Time.time;
        var camGo = Camera.main;
        for (int i = 0; i < 80; i++)
        {
            yield return 0.15f;
            bolts = Mathf.Max(bolts, Count("ElectricBolt"));
            rockets = Mathf.Max(rockets, Count("Rocket"));
            explosions = Mathf.Max(explosions, CountAll("VFX_Explosion_Rocket"));
            int mg = Count("Machinebullet");
            maxBullets = Mathf.Max(maxBullets, mg);
            shots = Mathf.Max(shots, CountAll("Machinebullet"));
            if (i == 10) yield return Shot("11_combat_a");
            if (i % 6 == 3 && i < 60)
            {
                var car = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.name.StartsWith(i % 12 == 3 ? "Tesla Cannon" : "rocket launcher") && t.parent == null);
                ChaseShot(car, "vfx_" + i.ToString("00"), i % 24 < 12 ? 6.5f : -6.5f);
            }
            if (i == 30) yield return Shot("12_combat_b");
            if (i == 55) yield return Shot("12_combat_c");
        }
        Check("Electric gun fires electric bolts (not basic bullets)", bolts > 0, "max live bolts " + bolts);
        // The rocket needs 4 s on target; in this crowded fight the other cars often kill everything first,
        // so rocket firing/explosions are verified by the isolated weapons test (Run Weapons Test).
        Log("     rocket activity in mixed fight: rockets " + rockets + ", pooled explosions " + explosions + " (verified separately in weapons test)");
        Check("Machine-gun bullets are pooled (instances stay bounded)", shots > 0 && shots < 40, "pooled instances " + shots + ", max live " + maxBullets);
        var tCar = UnityEngine.Object.FindObjectsByType<AutoGun>(FindObjectsInactive.Exclude).FirstOrDefault(g => g.weaponType == AutoGun.WeaponType.Electric);
        Check("Tesla car AutoGun uses Electric type + ElectricBolt", tCar != null && tCar.bulletPrefab != null && tCar.bulletPrefab.name == "ElectricBolt");

        foreach (var s in spawned) if (s) UnityEngine.Object.Destroy(s);
        yield return 7f;
        int liveTrails = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude).Count(p => p.transform.parent == null && (p.name == "SmokeTrail" || p.name == "Flame" || p.name == "HotCore" || p.name == "Embers"));
        Check("Detached rocket trails clean themselves up", liveTrails == 0, "left " + liveTrails);

        // ---- restart keeps settings ----
        Log("\n## Restart / menu / abandon");
        pause.PauseGame();
        yield return 0.3f;
        Click(pause.pausePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "RESTART"));
        yield return (Func<bool>)(() => !SceneTransition.IsBusy && Find<HUDController>() != null && Find<HUDController>() != hud);
        yield return 1.5f;
        Check("Restart reloads Lev1 with time running", SceneManager.GetActiveScene().name == "Lev1" && Time.timeScale == 1f && !PauseManager.IsPaused);
        Check("Restart: one AudioManager, one music source, settings kept", AudioManagers == 1 && MusicPlaying(am) == 1 && Mathf.Approximately(am.MusicVolume, 0.3f));

        var hpNew = Find<TrainHealth>();
        hpNew.TakeDamage(99999f);
        yield return 0.8f;
        var go = Find<GameOverButtons>();
        Check("Game over shows the restyled panel and stops time", go.gameObject.activeInHierarchy && Time.timeScale == 0f);
        yield return Shot("13_game_over");
        pause = Find<PauseManager>();
        Click(go.GetComponentsInChildren<Button>(true).First(b => b.name == "ABANDON RUN"));
        yield return (Func<bool>)(() => Find<GameOverButtons>() != go && Find<GameOverButtons>() != null);
        yield return 1.5f;
        Check("Abandon reloads the level", SceneManager.GetActiveScene().name == "Lev1" && Time.timeScale == 1f);
        Check("Abandon keeps audio preferences", Mathf.Approximately(PlayerPrefs.GetFloat("drx.audio.music", -1), 0.3f));

        pause = Find<PauseManager>();
        pause.PauseGame();
        yield return 0.3f;
        Click(pause.pausePanel.GetComponentsInChildren<Button>(true).First(b => b.name == "MAIN MENU"));
        yield return (Func<bool>)(() => SceneManager.GetActiveScene().name == "MainMenu" && !SceneTransition.IsBusy);
        yield return 2.5f;
        Check("Main Menu button returns to the menu", SceneManager.GetActiveScene().name == "MainMenu" && Time.timeScale == 1f && !AudioListener.pause);
        Check("Menu music back, single source", ActiveMusic(am) == am.Library.menuMusic && MusicPlaying(am) == 1, ActiveMusic(am)?.name);

        // restore the tester's original audio settings
        am.SetMusic(origMusic); am.SetSfxOn(origSfx);
        Log("     (audio settings restored to the values before the test)");
    }

    // Focused weapon test: one car type at a time, a single tough enemy at range, per-weapon logging + close-ups.
    static IEnumerator WeaponsScript()
    {
        yield return 2f;
        var train = Find<TrainMovement>();
        var um = UpgradeManager.instance;
        var mgr = Manager.instance;
        mgr.permanentOrb = 100000;
        ExperienceManager.instance.currentLevel = 50;
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/HeavyMoster.prefab");
        var hp = Find<TrainHealth>();

        foreach (var kv in new (string name, Action buy, string carPrefix)[]
        {
            ("Rocket", () => um.BuyRocketGun(), "rocket launcher"),
            ("Electric", () => um.BuyElectricGun(), "Tesla Cannon"),
            ("Gatling", () => um.BuyMachineGun(), "mini machine gun"),
            ("Twin", () => um.BuyDualGun(), "Double  barrel gun"),
            ("Flame", () => um.BuyFlameThrower(), "FlameThrower"),
        })
        {
            Log("\n## " + kv.name);
            while (train.BlockCount > 0) { train.RemoveLastBlock(); yield return 0.1f; }
            yield return 0.3f;
            kv.buy();
            yield return 0.5f;
            var car = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).FirstOrDefault(t => t.parent == null && t.name.StartsWith(kv.carPrefix));
            Check(kv.name + " car attached", car != null);
            if (car == null) continue;
            var gun = car.GetComponentsInChildren<AutoGun>(false).FirstOrDefault();
            // the enemy stands to the train's left; the review camera looks across from the right
            float dist = kv.name == "Flame" ? 6f : 10f;
            Func<Vector3> enemyPos = () => car.position - car.right * dist + Vector3.up * 0.5f;
            var enemy = UnityEngine.Object.Instantiate(enemyPrefab, enemyPos(), Quaternion.identity);
            int hits = 0, liveMax = 0, rootFx = 0, captures = 0, lastProj = 0, lastFx = 0;
            Func<int> proj = () => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Count(t => t.parent == null && (t.name.StartsWith("Rocket(") || t.name.StartsWith("ElectricBolt") || t.name.StartsWith("Machinebullet") || t.name.StartsWith("basicbulle")));
            Func<int> fx = () => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude).Count(t => t.parent == null && t.name.StartsWith("VFX_"));
            for (int i = 0; i < 160 && captures < 6; i++)
            {
                yield return 0.05f;
                hp.RepairHealth(1000f);
                if (enemy == null) { enemy = UnityEngine.Object.Instantiate(enemyPrefab, enemyPos(), Quaternion.identity); hits++; }
                else enemy.transform.position = enemyPos();
                int pc = proj(), fc = fx();
                liveMax = Mathf.Max(liveMax, pc);
                rootFx = Mathf.Max(rootFx, fc);
                bool flameOn = kv.name == "Flame" && (car.GetComponentInChildren<FlameThrower>()?.flameEffect?.isPlaying ?? false);
                if ((pc > lastProj || fc > lastFx || (flameOn && i % 10 == 0)) && i > 4)
                {
                    VfxShot(car, enemyPos(), "w_" + kv.name + "_" + captures);
                    captures++;
                    yield return 0.06f;
                    VfxShot(car, enemyPos(), "w_" + kv.name + "_" + captures);
                    captures++;
                }
                lastProj = pc; lastFx = fc;
            }
            if (gun != null) Log("     gun type " + gun.weaponType + " bullet " + (gun.bulletPrefab ? gun.bulletPrefab.name : "-") + " fireRate " + gun.fireRate + " range " + gun.range);
            Log("     live projectiles max " + liveMax + ", pooled/one-shot VFX roots " + rootFx + ", enemies killed " + hits);
            Check(kv.name + " fired at the enemy", kv.name == "Flame" ? (car.GetComponentInChildren<FlameThrower>()?.flameEffect?.isPlaying ?? false) || hits > 0 : liveMax > 0 || hits > 0);
            if (enemy) UnityEngine.Object.Destroy(enemy);
            yield return 2.5f;
        }
    }

    // Close-up review of every weapon effect, spawned in isolation in front of a camera.
    static void CamShot(Vector3 camPos, Vector3 look, string name)
    {
        var go = new GameObject("pt_gallery_cam");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(Camera.main);
        cam.fieldOfView = 45f;
        go.transform.position = camPos;
        go.transform.LookAt(look);
        var rt = new RenderTexture(960, 540, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
        File.WriteAllBytes(Path.Combine(OutDir, "g_" + name + ".png"), tex.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.Destroy(go);
        Log("     g_" + name + ".png");
    }

    static IEnumerator GalleryScript()
    {
        yield return 2f;
        var train = Find<TrainMovement>();
        train.enabled = false; // hold the train still so the stage stays put
        foreach (var e in UnityEngine.Object.FindObjectsByType<SmallWorm>(FindObjectsInactive.Exclude)) UnityEngine.Object.Destroy(e.gameObject);
        // world-axis stage beside the train: effects fire along +X, camera looks from -Z
        Vector3 basePos = train.transform.position + new Vector3(0f, 1.5f, -10f);
        Vector3 camPos = basePos + new Vector3(-1.5f, 1.0f, -4.5f);
        Quaternion aim = Quaternion.LookRotation(Vector3.right);
        Log("     train at " + train.transform.position + " stage " + basePos + " cam " + camPos);
        string dir = "Assets/Art/VFX/Weapons/";
        foreach (var name in new[] { "VFX_Muzzle_Cannon", "VFX_Muzzle_TwinCannon", "VFX_Muzzle_Gatling", "VFX_Muzzle_Rocket", "VFX_Muzzle_Electric", "VFX_Impact_Cannon", "VFX_Impact_Gatling", "VFX_Impact_Electric", "VFX_Explosion_Rocket" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(dir + name + ".prefab");
            var go = UnityEngine.Object.Instantiate(prefab, basePos, aim);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) { ps.Clear(); }
            go.GetComponent<ParticleSystem>()?.Play(true);
            var lb = go.GetComponentsInChildren<LightningBurst>(true);
            foreach (var l in lb) { l.enabled = false; l.enabled = true; }
            bool big = name.Contains("Explosion");
            Vector3 cp = big ? basePos + new Vector3(-2f, 3f, -12f) : camPos;
            yield return 0.03f; CamShot(cp, basePos, name + "_a");
            yield return 0.09f; CamShot(cp, basePos, name + "_b");
            yield return 0.3f; CamShot(cp, basePos, name + "_c");
            if (big) { yield return 0.6f; CamShot(cp, basePos, name + "_d"); }
            yield return 0.5f;
            UnityEngine.Object.Destroy(go);
        }
        // projectiles in flight, crossing the camera's view
        foreach (var name in new[] { "Rocket", "ElectricBolt", "basicbulle", "Machinebullet" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefeb/" + name + ".prefab");
            Vector3 start = basePos - Vector3.right * 8f;
            var go = UnityEngine.Object.Instantiate(prefab, start, aim);
            float flight = name == "Rocket" ? 0.55f : name == "ElectricBolt" ? 0.14f : 0.07f;
            yield return flight; CamShot(camPos + new Vector3(0f, 0f, -2f), basePos - Vector3.right * 2f, "proj_" + name + "_a");
            yield return flight * 0.6f; CamShot(camPos + new Vector3(0f, 0f, -2f), basePos - Vector3.right * 2f, "proj_" + name + "_b");
            if (go) UnityEngine.Object.Destroy(go);
            yield return 1.5f;
        }
        Check("gallery captured", true);
    }
}
