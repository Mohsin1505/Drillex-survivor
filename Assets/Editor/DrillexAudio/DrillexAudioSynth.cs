using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Original procedural music and sound effects for Drillex, synthesised from code (no samples,
// no third-party audio). Re-running regenerates identical files (fixed seeds).
//   Music: industrial / desert (D phrygian-dominant), seamless loops with wrapped reverb tails.
//   SFX:   UI, orbs, weapons, explosions, electricity, flamethrower loop, sound trap.
public static class DrillexAudioSynth
{
    const string MusicDir = "Assets/Audio/Music";
    const string SfxDir = "Assets/Audio/SFX";
    const string LibraryPath = "Assets/Resources/DrillexAudioLibrary.asset";
    const int MusicRate = 32000;
    const int SfxRate = 44100;

    static System.Random rng;
    static float Rand() => (float)rng.NextDouble() * 2f - 1f;
    static float Mtof(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);

    // ================================================================ entry

    [MenuItem("Drillex/Audio/Generate SFX And Build Audio Library")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(MusicDir);
        Directory.CreateDirectory(SfxDir);
        Directory.CreateDirectory("Assets/Resources");

        // Music now comes from licensed CC0 tracks (Assets/Audio/External/OpenGameArt, see LICENSES.md).
        // The procedural tracks below are kept only as a reference/fallback generator.

        // Only sounds without a fitting CC0 recording are synthesised (gunpowder cannons, gatling,
        // flamethrower loop, sound-trap ping, worm siren). Everything else uses Kenney CC0 audio.
        var sfx = new Dictionary<string, float[]>
        {
            { "SFX_Warning_Worm", Warning() },
            { "SFX_Weapon_Cannon", Cannon(1f) }, { "SFX_Weapon_TwinCannon", TwinCannon() }, { "SFX_Weapon_Gatling", Gatling() },
            { "SFX_Flamethrower_Loop", FlameLoop() }, { "SFX_SoundTrap_Deploy", TrapPing() },
        };
        foreach (var kv in sfx) WriteMono(SfxDir + "/" + kv.Key + ".wav", kv.Value, SfxRate);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureImport();
        BuildLibrary();
        Debug.Log("Drillex audio generated");
    }

    // ================================================================ import + library

    static void ConfigureImport()
    {
        foreach (var path in Directory.GetFiles(MusicDir, "*.wav"))
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            var s = imp.defaultSampleSettings;
            s.loadType = AudioClipLoadType.Streaming;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.45f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            imp.defaultSampleSettings = s;
            imp.forceToMono = false;
            imp.loadInBackground = true;
            imp.SaveAndReimport();
        }
        foreach (var path in Directory.GetFiles(SfxDir, "*.wav"))
        {
            var imp = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            var s = imp.defaultSampleSettings;
            bool loop = path.Contains("Loop");
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = 0.6f;
            s.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
            imp.defaultSampleSettings = s;
            imp.forceToMono = true;
            imp.SaveAndReimport();
        }
    }

    const string ExtDir = "Assets/Audio/External";

    static void ConfigureExternal()
    {
        foreach (var path in Directory.GetFiles(ExtDir, "*.*", SearchOption.AllDirectories))
        {
            if (!(path.EndsWith(".ogg") || path.EndsWith(".mp3"))) continue;
            var imp = (AudioImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            if (imp == null) continue;
            bool music = path.Contains("MUS_");
            var s = imp.defaultSampleSettings;
            s.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM;
            s.quality = music ? 0.45f : 0.6f;
            s.sampleRateSetting = music ? AudioSampleRateSetting.PreserveSampleRate : AudioSampleRateSetting.OptimizeSampleRate;
            imp.defaultSampleSettings = s;
            imp.forceToMono = !music;
            imp.loadInBackground = music;
            imp.SaveAndReimport();
        }
    }

    static AudioClip Ext(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(ExtDir + "/" + file);

    static AudioClip Clip(string dir, string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "/" + name + ".wav");

    static void BuildLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
        if (lib == null) { lib = ScriptableObject.CreateInstance<AudioLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
        ConfigureExternal();
        // CC0 music (OpenGameArt) - see Assets/Audio/External/LICENSES.md
        lib.menuMusic = Ext("OpenGameArt/MUS_DesertLoop_iamoneabe.mp3");
        lib.gameplayMusic = Ext("OpenGameArt/MUS_EasternArcticDubstep_VishwaJai.mp3");
        lib.uiClick = Ext("Kenney/UI_click_002.ogg");
        lib.uiOpen = Ext("Kenney/UI_maximize_006.ogg");
        lib.uiClose = Ext("Kenney/UI_minimize_006.ogg");
        lib.purchase = Ext("Kenney/UI_confirmation_002.ogg");
        lib.denied = Ext("Kenney/UI_error_006.ogg");
        lib.unlock = Ext("Kenney/UI_confirmation_004.ogg");
        lib.levelUp = Ext("Kenney/UI_confirmation_003.ogg");
        lib.orbPickup = Ext("Kenney/UI_glass_002.ogg");
        lib.orbDeposit = Ext("Kenney/UI_glass_005.ogg");
        lib.warning = Clip(SfxDir, "SFX_Warning_Worm");
        lib.basicShot = Clip(SfxDir, "SFX_Weapon_Cannon");
        lib.doubleShot = Clip(SfxDir, "SFX_Weapon_TwinCannon");
        lib.machineGunShot = Clip(SfxDir, "SFX_Weapon_Gatling");
        lib.rocketLaunch = Ext("Kenney/SciFi_thrusterFire_001.ogg");
        lib.explosion = Ext("Kenney/SciFi_explosionCrunch_002.ogg");
        lib.electricShot = Ext("Kenney/SciFi_laserRetro_004.ogg");
        lib.electricImpact = Ext("Kenney/SciFi_forceField_002.ogg");
        lib.bulletImpact = Ext("Kenney/Impact_impactMetal_light_002.ogg");
        lib.flameLoop = Clip(SfxDir, "SFX_Flamethrower_Loop");
        lib.trapDeploy = Clip(SfxDir, "SFX_SoundTrap_Deploy");
        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
    }

    // ================================================================ wav io

    static void WriteMono(string path, float[] data, int rate)
    {
        Normalize(data, 0.89f);
        using (var bw = new BinaryWriter(File.Create(path)))
        {
            Header(bw, data.Length, 1, rate);
            foreach (var v in data) bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }

    static void WriteStereo(string path, float[][] lr, int rate)
    {
        float peak = 1e-6f;
        for (int c = 0; c < 2; c++) foreach (var v in lr[c]) peak = Mathf.Max(peak, Mathf.Abs(v));
        float g = 0.89f / peak;
        using (var bw = new BinaryWriter(File.Create(path)))
        {
            Header(bw, lr[0].Length, 2, rate);
            for (int i = 0; i < lr[0].Length; i++)
            {
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(lr[0][i] * g * 32767f), -32768, 32767));
                bw.Write((short)Mathf.Clamp(Mathf.RoundToInt(lr[1][i] * g * 32767f), -32768, 32767));
            }
        }
    }

    static void Header(BinaryWriter bw, int frames, int ch, int rate)
    {
        int bytes = frames * ch * 2;
        bw.Write(new[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + bytes); bw.Write(new[] { 'W', 'A', 'V', 'E' });
        bw.Write(new[] { 'f', 'm', 't', ' ' }); bw.Write(16); bw.Write((short)1); bw.Write((short)ch); bw.Write(rate);
        bw.Write(rate * ch * 2); bw.Write((short)(ch * 2)); bw.Write((short)16);
        bw.Write(new[] { 'd', 'a', 't', 'a' }); bw.Write(bytes);
    }

    static void Normalize(float[] d, float target)
    {
        float peak = 1e-6f; foreach (var v in d) peak = Mathf.Max(peak, Mathf.Abs(v));
        float g = target / peak; for (int i = 0; i < d.Length; i++) d[i] *= g;
    }

    // ================================================================ dsp building blocks

    class OnePole { float z; public float LP(float x, float a) { z += a * (x - z); return z; } public float HP(float x, float a) => x - LP(x, a); }

    // state-variable filter (Chamberlin), returns low/band/high
    class Svf
    {
        float low, band;
        public float Process(float x, float cutoff, float q, int rate, int mode)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, rate * 0.24f) / rate);
            low += f * band;
            float high = x - low - q * band;
            band += f * high;
            return mode == 0 ? low : mode == 1 ? band : high;
        }
    }

    static float Saw(float ph) => 2f * (ph - Mathf.Floor(ph + 0.5f));
    static float Sq(float ph, float pw = 0.5f) => (ph - Mathf.Floor(ph)) < pw ? 1f : -1f;
    static float Sin(float ph) => Mathf.Sin(ph * Mathf.PI * 2f);
    static float Env(float t, float a, float d) => t < 0 ? 0 : t < a ? t / a : Mathf.Exp(-(t - a) / d);

    // simple stereo reverb: 4 combs + 2 allpasses per side (Schroeder), used as a send
    class Reverb
    {
        readonly float[][] comb; readonly int[] ci; readonly float[] cf;
        readonly float[][] ap; readonly int[] ai;
        public Reverb(int rate, int seed)
        {
            int[] cl = { 1557, 1617, 1491, 1422 }; int[] al = { 225, 556 };
            comb = new float[4][]; ci = new int[4]; cf = new float[4];
            for (int i = 0; i < 4; i++) { comb[i] = new float[(int)(cl[i] * rate / 44100f) + seed * 23]; cf[i] = 0f; }
            ap = new float[2][]; ai = new int[2];
            for (int i = 0; i < 2; i++) ap[i] = new float[(int)(al[i] * rate / 44100f) + seed * 7];
        }
        public float Process(float x, float feedback = 0.82f, float damp = 0.3f)
        {
            float o = 0;
            for (int i = 0; i < 4; i++)
            {
                float y = comb[i][ci[i]];
                cf[i] = y * (1 - damp) + cf[i] * damp;
                comb[i][ci[i]] = x + cf[i] * feedback;
                ci[i] = (ci[i] + 1) % comb[i].Length;
                o += y;
            }
            o *= 0.25f;
            for (int i = 0; i < 2; i++)
            {
                float b = ap[i][ai[i]];
                float y = -o + b;
                ap[i][ai[i]] = o + b * 0.5f;
                ai[i] = (ai[i] + 1) % ap[i].Length;
                o = y;
            }
            return o;
        }
    }

    // mixes a mono voice into the (looping) stereo buffer, wrapping tails to the start
    static void Mix(float[][] buf, int start, float[] voice, float gain, float pan)
    {
        int n = buf[0].Length;
        float l = gain * Mathf.Cos((pan + 1) * Mathf.PI / 4), r = gain * Mathf.Sin((pan + 1) * Mathf.PI / 4);
        for (int i = 0; i < voice.Length; i++)
        {
            int j = (start + i) % n; if (j < 0) j += n;
            buf[0][j] += voice[i] * l; buf[1][j] += voice[i] * r;
        }
    }

    // ================================================================ instruments (music)

    static float[] Kick(int rate, float len = 0.42f)
    {
        int n = (int)(len * rate); var o = new float[n]; float ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float f = 44f + 120f * Mathf.Exp(-t * 28f);
            ph += f / rate;
            o[i] = (float)Math.Tanh(Sin(ph) * 2.2f * Env(t, 0.002f, 0.16f)) + Rand() * 0.15f * Env(t, 0.0005f, 0.006f);
        }
        return o;
    }

    static float[] Snare(int rate)
    {
        int n = (int)(0.32f * rate); var o = new float[n]; var bp = new Svf(); float ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate; ph += 185f / rate;
            float noise = bp.Process(Rand(), 2600f, 0.7f, rate, 1) * Env(t, 0.001f, 0.09f);
            o[i] = noise * 1.6f + Sin(ph) * 0.5f * Env(t, 0.001f, 0.05f);
        }
        return o;
    }

    static float[] Hat(int rate, float decay)
    {
        int n = (int)((decay * 6 + 0.01f) * rate); var o = new float[n]; var hp = new Svf();
        // metallic: sum of inharmonic squares + noise, high-passed
        float[] fr = { 205.3f, 304.4f, 369.6f, 522.7f, 540f, 800f }; var ph = new float[fr.Length];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate; float m = 0;
            for (int k = 0; k < fr.Length; k++) { ph[k] += fr[k] * 6.1f / rate; m += Sq(ph[k]); }
            o[i] = hp.Process(m * 0.15f + Rand() * 0.5f, 7000f, 0.5f, rate, 2) * Env(t, 0.0005f, decay);
        }
        return o;
    }

    // industrial "anvil" hit: FM bell with inharmonic ratio
    static float[] Anvil(int rate, float note, float len = 1.2f)
    {
        int n = (int)(len * rate); var o = new float[n]; float f = Mtof(note); float pc = 0, pm = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            pm += f * 3.51f / rate; pc += f / rate;
            float idx = 4f * Mathf.Exp(-t * 6f);
            o[i] = Mathf.Sin(2 * Mathf.PI * pc + idx * Sin(pm)) * Env(t, 0.001f, 0.35f);
        }
        return o;
    }

    static float[] Bass(int rate, float note, float len, float bright)
    {
        int n = (int)(len * rate); var o = new float[n]; var f = new Svf(); float p1 = 0, p2 = 0, fr = Mtof(note);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            p1 += fr / rate; p2 += fr * 1.006f / rate;
            float s = Saw(p1) + Saw(p2) * 0.6f + Sin(p1 * 0.5f) * 0.8f;
            float cut = 180f + bright * 1800f * Mathf.Exp(-t * 14f);
            float a = Env(t, 0.004f, len * 0.6f) * Mathf.Clamp01((len - t) * 60f);
            o[i] = (float)Math.Tanh(f.Process(s, cut, 0.35f, rate, 0) * 1.6f) * a;
        }
        return o;
    }

    static float[] Pad(int rate, float[] notes, float len)
    {
        int n = (int)(len * rate); var o = new float[n]; var f = new Svf();
        int v = notes.Length * 3; var ph = new float[v]; var fr = new float[v];
        for (int k = 0; k < notes.Length; k++) for (int d = 0; d < 3; d++) { fr[k * 3 + d] = Mtof(notes[k]) * (1f + (d - 1) * 0.004f); ph[k * 3 + d] = (float)rng.NextDouble(); }
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate; float s = 0;
            for (int k = 0; k < v; k++) { ph[k] += fr[k] / rate; s += Saw(ph[k]); }
            float a = Mathf.Clamp01(t / (len * 0.25f)) * Mathf.Clamp01((len - t) / (len * 0.3f));
            float cut = 700f + 500f * Mathf.Sin(t * 0.9f);
            o[i] = f.Process(s / v, cut, 0.9f, rate, 0) * a;
        }
        return o;
    }

    static float[] Pluck(int rate, float note, float len, float tone)
    {
        int n = (int)(len * rate); var o = new float[n]; float fr = Mtof(note); float pc = 0, pm = 0; var lp = new OnePole();
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            pc += fr / rate; pm += fr * 2f / rate;
            float idx = tone * 2.5f * Mathf.Exp(-t * 9f);
            float s = Mathf.Sin(2 * Mathf.PI * pc + idx * Sin(pm)) + 0.3f * Sq(pc, 0.3f) * Mathf.Exp(-t * 20f);
            o[i] = lp.LP(s, 0.35f) * Env(t, 0.003f, 0.35f) * Mathf.Clamp01((len - t) * 40f);
        }
        return o;
    }

    static float[] Wind(int rate, float len)
    {
        int n = (int)(len * rate); var o = new float[n]; var f = new Svf();
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float cut = 500f + 350f * Mathf.Sin(t * 0.37f) + 200f * Mathf.Sin(t * 1.13f + 1f);
            o[i] = f.Process(Rand(), cut, 0.25f, rate, 1) * (0.6f + 0.4f * Mathf.Sin(t * 0.23f));
        }
        return o;
    }

    // ================================================================ music: gameplay

    static float[][] GameplayTrack()
    {
        rng = new System.Random(4127);
        int rate = MusicRate; float bpm = 118f; float beat = 60f / bpm; float bar = beat * 4; int bars = 32;
        int n = (int)(bar * bars * rate);
        var buf = new[] { new float[n], new float[n] };
        var send = new[] { new float[n], new float[n] };
        int S(float sec) => (int)(sec * rate);

        // D phrygian dominant: D Eb F# G A Bb C
        int[] bassRoot = { 38, 38, 34, 36, 38, 38, 34, 33 }; // per bar (D D Bb C D D Bb A), cycles every 8 bars
        float[][] chords = { new float[] { 50, 57, 62, 65 }, new float[] { 46, 53, 58, 62 }, new float[] { 48, 55, 60, 64 }, new float[] { 45, 52, 57, 61 } };
        int[] lead = { 74, -1, 75, 74, 72, -1, 70, 69, 74, -1, 78, 79, 81, -1, 79, 78 };
        int[] lead2 = { 81, 79, 78, -1, 79, 78, 75, 74, 72, -1, 74, 75, 74, -1, -1, -1 };

        var kick = Kick(rate); var snare = Snare(rate); var hatC = Hat(rate, 0.025f); var hatO = Hat(rate, 0.09f);
        for (int b = 0; b < bars; b++)
        {
            float t0 = b * bar;
            int sec = b / 8; // 0 groove, 1 groove+lead, 2 breakdown, 3 groove+lead
            bool breakdown = sec == 2;
            // drums
            for (int q = 0; q < 16; q++)
            {
                float t = t0 + q * beat / 4;
                bool kickHit = breakdown ? (q == 0 || q == 10) : (q % 4 == 0 || q == 14 || (q == 7 && b % 2 == 1));
                if (kickHit) Mix(buf, S(t), kick, 0.95f, 0);
                if ((q == 4 || q == 12) && !(breakdown && b % 2 == 0)) { Mix(buf, S(t), snare, 0.55f, 0.05f); Mix(send, S(t), snare, 0.18f, 0); }
                float hv = q % 4 == 2 ? 0.32f : 0.16f;
                if (!breakdown || q % 2 == 0) Mix(buf, S(t), q % 8 == 6 ? hatO : hatC, hv, 0.35f);
            }
            if (b % 4 == 3) { var anv = Anvil(rate, 62 + (b % 8 == 7 ? 3 : 0)); Mix(buf, S(t0 + beat * 3.5f), anv, 0.16f, -0.5f); Mix(send, S(t0 + beat * 3.5f), anv, 0.25f, -0.5f); }

            // bass: chugging 8ths/16ths, the "train" engine
            int root = bassRoot[b % 8];
            for (int q = 0; q < 16; q++)
            {
                bool hit = breakdown ? (q % 4 == 0) : (q % 2 == 0 || q == 3 || q == 11);
                if (!hit) continue;
                int note = root + ((q == 14) ? 7 : (q == 6 && b % 2 == 1) ? 1 : 0);
                Mix(buf, S(t0 + q * beat / 4), Bass(rate, note, beat / 4 * 0.9f, q % 4 == 0 ? 1f : 0.55f), 0.42f, 0);
            }

            // pad: one chord per 2 bars
            if (b % 2 == 0)
            {
                var ch = chords[(b / 2) % 4];
                var pad = Pad(rate, ch, bar * 2 + 0.6f);
                Mix(buf, S(t0), pad, breakdown ? 0.32f : 0.22f, 0);
                Mix(send, S(t0), pad, 0.2f, 0);
            }

            // lead in sections 1 and 3
            if (sec == 1 || sec == 3)
            {
                var line = (b % 2 == 0) ? lead : lead2;
                for (int q = 0; q < 16; q++)
                {
                    if (line[q] < 0 || q % 2 == 1 && rng.NextDouble() < 0.3) continue;
                    var pl = Pluck(rate, line[q] - (sec == 3 ? 0 : 12), beat / 2 * 0.95f, 1f);
                    float pan = (q % 4 < 2) ? -0.25f : 0.25f;
                    Mix(buf, S(t0 + q * beat / 4), pl, 0.2f, pan);
                    Mix(send, S(t0 + q * beat / 4), pl, 0.22f, pan);
                    // dotted-8th echo
                    Mix(buf, S(t0 + q * beat / 4 + beat * 0.75f), pl, 0.07f, -pan);
                }
            }
            if (breakdown)
            {
                // sparse arpeggio over the breakdown
                var ch = chords[(b / 2) % 4];
                for (int k = 0; k < 4; k++)
                {
                    var pl = Pluck(rate, ch[k] + 12, beat * 0.9f, 0.6f);
                    Mix(buf, S(t0 + k * beat), pl, 0.13f, (k - 1.5f) * 0.3f);
                    Mix(send, S(t0 + k * beat), pl, 0.3f, 0);
                }
            }
        }
        var wind = Wind(rate, n / (float)rate);
        Mix(buf, 0, wind, 0.05f, 0);
        return Master(buf, send, rate);
    }

    // ================================================================ music: menu

    static float[][] MenuTrack()
    {
        rng = new System.Random(911);
        int rate = MusicRate; float bpm = 84f; float beat = 60f / bpm; float bar = beat * 4; int bars = 24;
        int n = (int)(bar * bars * rate);
        var buf = new[] { new float[n], new float[n] };
        var send = new[] { new float[n], new float[n] };
        int S(float sec) => (int)(sec * rate);
        float[][] chords = { new float[] { 50, 57, 62, 66 }, new float[] { 46, 53, 58, 62 }, new float[] { 48, 55, 60, 63 }, new float[] { 45, 52, 57, 61 } };
        int[] motif = { 62, 63, 66, 67, 69, 67, 66, 63 };
        var kick = Kick(rate, 0.5f); var hat = Hat(rate, 0.04f);

        for (int b = 0; b < bars; b++)
        {
            float t0 = b * bar;
            var ch = chords[(b / 2) % 4];
            if (b % 2 == 0)
            {
                var pad = Pad(rate, ch, bar * 2 + 1f);
                Mix(buf, S(t0), pad, 0.3f, 0); Mix(send, S(t0), pad, 0.35f, 0);
            }
            // slow heartbeat pulse: an idling engine
            Mix(buf, S(t0), kick, 0.5f, 0);
            Mix(buf, S(t0 + beat * 2.5f), kick, 0.3f, 0);
            Mix(buf, S(t0), Bass(rate, ch[0] - 12, beat * 1.8f, 0.4f), 0.35f, 0);
            Mix(buf, S(t0 + beat * 2), Bass(rate, ch[0] - 12, beat * 1.5f, 0.25f), 0.25f, 0);
            for (int q = 0; q < 8; q++) if (b >= 4) Mix(buf, S(t0 + q * beat / 2), hat, q % 2 == 1 ? 0.12f : 0.06f, 0.4f);
            // plucked desert motif, every other bar, with echoes
            if (b % 2 == 1 || b >= 16)
                for (int k = 0; k < 4; k++)
                {
                    int note = motif[(b * 4 + k) % motif.Length] + (b >= 16 ? 12 : 0);
                    var pl = Pluck(rate, note, beat * 1.2f, 0.8f);
                    float tt = t0 + k * beat + (k % 2 == 1 ? beat * 0.5f : 0);
                    Mix(buf, S(tt), pl, 0.16f, -0.3f); Mix(send, S(tt), pl, 0.35f, 0);
                    Mix(buf, S(tt + beat * 0.75f), pl, 0.06f, 0.4f);
                }
            if (b % 4 == 2) { var anv = Anvil(rate, 74, 2f); Mix(buf, S(t0 + beat * 3), anv, 0.08f, 0.6f); Mix(send, S(t0 + beat * 3), anv, 0.3f, 0.6f); }
        }
        Mix(buf, 0, Wind(rate, n / (float)rate), 0.12f, 0);
        return Master(buf, send, rate);
    }

    static float[][] Master(float[][] buf, float[][] send, int rate)
    {
        int n = buf[0].Length;
        var rl = new Reverb(rate, 0); var rr = new Reverb(rate, 1);
        // run the reverb over two loop passes so its tail at the loop seam is already "warm"
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                float wl = rl.Process(send[0][i]), wr = rr.Process(send[1][i]);
                if (pass == 1) { buf[0][i] += wl * 0.9f; buf[1][i] += wr * 0.9f; }
            }
        var hpL = new OnePole(); var hpR = new OnePole();
        for (int i = 0; i < n; i++)
        {
            buf[0][i] = (float)Math.Tanh(hpL.HP(buf[0][i], 0.004f) * 1.3f);
            buf[1][i] = (float)Math.Tanh(hpR.HP(buf[1][i], 0.004f) * 1.3f);
        }
        return buf;
    }

    // ================================================================ sfx

    static float[] New(float sec) => new float[(int)(sec * SfxRate)];
    const int R = SfxRate;

    static float[] UiClick()
    {
        rng = new System.Random(1); var o = New(0.06f); float ph = 0; var hp = new Svf();
        for (int i = 0; i < o.Length; i++) { float t = i / (float)R; ph += 2300f / R; o[i] = Sin(ph) * Env(t, 0.0005f, 0.012f) + hp.Process(Rand(), 5000, 0.5f, R, 2) * Env(t, 0.0003f, 0.004f) * 0.6f; }
        return o;
    }

    static float[] Whoosh(bool up)
    {
        rng = new System.Random(up ? 2 : 3); var o = New(0.32f); var bp = new Svf(); float ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R; float k = t / 0.32f; float sweep = up ? k : 1 - k;
            float a = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) ;
            o[i] = bp.Process(Rand(), 600f + 3500f * sweep, 0.6f, R, 1) * a * 0.8f;
            float clunkT = up ? t - 0.24f : t - 0.02f; ph += (90f) / R;
            if (clunkT > 0) o[i] += Sin(ph) * Env(clunkT, 0.001f, 0.03f) * 0.5f;
        }
        return o;
    }

    static float[] Purchase()
    {
        rng = new System.Random(4); var o = New(0.8f); var lp = new Svf(); float p1 = 0, p2 = 0, p3 = 0;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            p1 += 95f / R;
            float chunk = Sin(p1) * Env(t, 0.001f, 0.05f) + lp.Process(Rand(), 900, 0.6f, R, 0) * Env(t, 0.001f, 0.04f);
            p2 += 1318.5f / R; p3 += 1975.5f / R;
            float chime = Sin(p2) * Env(t - 0.06f, 0.002f, 0.18f) * 0.5f + Sin(p3) * Env(t - 0.14f, 0.002f, 0.3f) * 0.5f;
            o[i] = chunk * 0.9f + chime * 0.6f;
        }
        return o;
    }

    static float[] Denied()
    {
        var o = New(0.32f); float ph = 0; var lp = new OnePole();
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R; ph += 140f / R;
            float gate = (t < 0.11f || (t > 0.16f && t < 0.28f)) ? 1 : 0;
            o[i] = lp.LP(Sq(ph) * gate, 0.25f) * 0.7f;
        }
        return o;
    }

    static float[] Unlock()
    {
        rng = new System.Random(5); var o = New(1.6f);
        int[] notes = { 74, 78, 81, 86, 90 };
        for (int k = 0; k < notes.Length; k++)
        {
            float f = Mtof(notes[k]); float start = k * 0.07f; float ph = 0, pm = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)R - start; if (t < 0) continue;
                ph += f / R; pm += f * 3f / R;
                o[i] += Mathf.Sin(2 * Mathf.PI * ph + 1.2f * Mathf.Exp(-t * 8) * Sin(pm)) * Env(t, 0.002f, 0.45f) * 0.35f;
            }
        }
        var hp = new Svf();
        for (int i = 0; i < o.Length; i++) { float t = i / (float)R; o[i] += hp.Process(Rand(), 8000, 0.4f, R, 2) * Env(t - 0.2f, 0.3f, 0.4f) * 0.08f; }
        return o;
    }

    static float[] LevelUp()
    {
        var o = New(1.3f); int[] notes = { 62, 66, 69, 74 };
        for (int k = 0; k < notes.Length; k++)
        {
            float f = Mtof(notes[k]); float start = k * 0.1f; float p1 = 0, p2 = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)R - start; if (t < 0) continue;
                p1 += f / R; p2 += f * 2.01f / R;
                float len = k == notes.Length - 1 ? 0.6f : 0.16f;
                o[i] += (Saw(p1) * 0.3f + Sin(p2) * 0.4f) * Env(t, 0.004f, len) * 0.5f;
            }
        }
        var anv = Anvil(R, 86, 1.2f); for (int i = 0; i < anv.Length && i + (int)(0.3f * R) < o.Length; i++) o[i + (int)(0.3f * R)] += anv[i] * 0.25f;
        return o;
    }

    static float[] OrbPickup()
    {
        var o = New(0.18f); float ph = 0;
        for (int i = 0; i < o.Length; i++) { float t = i / (float)R; float f = 1250f + 500f * Mathf.Clamp01(t / 0.05f); ph += f / R; o[i] = (Sin(ph) + 0.3f * Sin(ph * 2.5f)) * Env(t, 0.002f, 0.05f); }
        return o;
    }

    static float[] OrbDeposit()
    {
        var o = New(0.9f);
        for (int k = 0; k < 7; k++)
        {
            float f = Mtof(76 + k * 2 + (k % 2) * 3); float st = k * 0.06f; float ph = 0;
            for (int i = 0; i < o.Length; i++) { float t = i / (float)R - st; if (t < 0) continue; ph += f / R; o[i] += Sin(ph) * Env(t, 0.002f, 0.12f) * 0.3f; }
        }
        return o;
    }

    static float[] Warning()
    {
        var o = New(1.3f); float ph = 0; var lp = new OnePole();
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R; float f = (Mathf.Repeat(t, 0.5f) < 0.25f) ? 330f : 247f; ph += f / R;
            float a = Mathf.Clamp01(t * 20) * Mathf.Clamp01((1.3f - t) * 6);
            o[i] = lp.LP(Saw(ph) * 0.6f + Sq(ph * 0.5f) * 0.3f, 0.2f) * a;
        }
        return o;
    }

    static float[] Cannon(float pitch)
    {
        rng = new System.Random(7); var o = New(0.35f); var lp = new Svf(); float ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R; ph += (70f + 160f * Mathf.Exp(-t * 40)) * pitch / R;
            float body = Sin(ph) * Env(t, 0.001f, 0.06f);
            float blast = lp.Process(Rand(), 1800f * Mathf.Exp(-t * 8) + 300f, 0.5f, R, 0) * Env(t, 0.0005f, 0.07f);
            o[i] = (float)Math.Tanh((body * 1.2f + blast * 1.5f) * 1.5f);
        }
        return o;
    }

    static float[] TwinCannon()
    {
        var a = Cannon(1.05f); var b = Cannon(0.92f); var o = New(0.45f); int off = (int)(0.07f * R);
        for (int i = 0; i < a.Length; i++) { o[i] += a[i] * 0.8f; if (i + off < o.Length) o[i + off] += b[i] * 0.8f; }
        return o;
    }

    static float[] Gatling()
    {
        rng = new System.Random(8); var o = New(0.09f); var hp = new Svf(); float ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R; ph += 160f / R;
            o[i] = (float)Math.Tanh((hp.Process(Rand(), 1500, 0.6f, R, 2) * Env(t, 0.0003f, 0.018f) * 1.4f + Sin(ph) * Env(t, 0.0005f, 0.02f)) * 2f);
        }
        return o;
    }

    static float[] RocketLaunch()
    {
        rng = new System.Random(9); var o = New(0.9f); var bp = new Svf(); var lp = new Svf(); float ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            float hiss = bp.Process(Rand(), 900f + 2400f * Mathf.Clamp01(t / 0.5f), 0.8f, R, 1) * Mathf.Clamp01(t * 30) * Mathf.Exp(-t * 2.8f);
            ph += 60f / R;
            float thump = Sin(ph) * Env(t, 0.001f, 0.06f) + lp.Process(Rand(), 500, 0.5f, R, 0) * Env(t, 0.001f, 0.05f);
            o[i] = hiss * 1.3f + thump;
        }
        return o;
    }

    static float[] Explosion()
    {
        rng = new System.Random(10); var o = New(1.8f); var lp = new Svf(); var lp2 = new Svf(); float ph = 0;
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            ph += (38f + 80f * Mathf.Exp(-t * 12)) / R;
            float boom = Sin(ph) * Env(t, 0.002f, 0.28f);
            float roar = lp.Process(Rand(), 300f + 2500f * Mathf.Exp(-t * 6f), 0.4f, R, 0) * Env(t, 0.002f, 0.45f);
            float crackle = (rng.NextDouble() < 0.004 * Mathf.Exp(-t * 3) ? Rand() * 2f : 0f);
            float debris = lp2.Process(crackle, 3000, 0.3f, R, 1);
            o[i] = (float)Math.Tanh((boom * 1.5f + roar * 1.8f + debris * 0.8f) * 1.4f);
        }
        return o;
    }

    static float[] ElectricZap()
    {
        rng = new System.Random(11); var o = New(0.32f); float pc = 0, pm = 0; var hp = new Svf();
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R; float f = 300f + 1900f * Mathf.Exp(-t * 18f);
            pc += f / R; pm += f * 1.414f / R;
            float tone = Saw(pc + 0.6f * Sin(pm));
            float buzz = Sq(t * 120f) * 0.3f;
            float crack = hp.Process(Rand(), 4000, 0.4f, R, 2) * Env(t, 0.0005f, 0.03f);
            o[i] = (tone * 0.6f + buzz) * Env(t, 0.002f, 0.09f) + crack * 0.9f;
        }
        return o;
    }

    static float[] ElectricImpact()
    {
        rng = new System.Random(12); var o = New(0.4f); var bp = new Svf();
        for (int i = 0; i < o.Length; i++)
        {
            float t = i / (float)R;
            float spike = rng.NextDouble() < 0.02 * Mathf.Exp(-t * 6) ? Rand() * 3f : 0f;
            o[i] = bp.Process(spike + Rand() * 0.2f, 2500, 0.3f, R, 1) * Env(t, 0.001f, 0.12f) + Sq(t * 100f) * 0.15f * Env(t, 0.001f, 0.06f);
        }
        return o;
    }

    static float[] BulletImpact()
    {
        rng = new System.Random(13); var o = New(0.07f); var bp = new Svf();
        for (int i = 0; i < o.Length; i++) { float t = i / (float)R; o[i] = bp.Process(Rand(), 1800, 0.5f, R, 1) * Env(t, 0.0003f, 0.012f); }
        return o;
    }

    static float[] FlameLoop()
    {
        rng = new System.Random(14); float len = 2f; int n = (int)(len * R); int fade = (int)(0.25f * R);
        var raw = new float[n + fade]; var lp = new Svf(); var bp = new Svf();
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)R;
            float roar = lp.Process(Rand(), 700f + 250f * Mathf.Sin(t * 7.3f) + 150f * Mathf.Sin(t * 2.1f), 0.4f, R, 0);
            float hiss = bp.Process(Rand(), 3500f, 0.6f, R, 1) * 0.25f;
            float pops = rng.NextDouble() < 0.0015 ? Rand() * 1.5f : 0;
            raw[i] = roar * 1.4f + hiss + pops;
        }
        // seamless loop: crossfade the overhang into the start
        var o = new float[n];
        for (int i = 0; i < n; i++) o[i] = raw[i];
        for (int i = 0; i < fade; i++) { float k = i / (float)fade; o[i] = raw[i] * k + raw[n + i] * (1 - k); }
        return o;
    }

    static float[] TrapPing()
    {
        var o = New(1.2f);
        for (int e = 0; e < 3; e++)
        {
            float st = e * 0.22f; float g = Mathf.Pow(0.45f, e); float ph = 0;
            for (int i = 0; i < o.Length; i++)
            {
                float t = i / (float)R - st; if (t < 0) continue;
                float f = 880f * (1f + 0.04f * Mathf.Sin(t * 40f)) * (1f - 0.15f * Mathf.Clamp01(t * 3f)); ph += f / R;
                o[i] += Sin(ph) * Env(t, 0.003f, 0.18f) * g;
            }
        }
        return o;
    }
}
