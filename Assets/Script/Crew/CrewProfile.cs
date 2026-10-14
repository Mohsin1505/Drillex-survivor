using System;
using UnityEngine;

// The player's crew member: name + look. Persisted in PlayerPrefs, shared by every CrewAppearance
// that follows the profile (menu stage, future cab driver, results screen...).
[Serializable]
public struct CrewLook
{
    public int colourway;
    public int skin;
    public int headgear;

    public CrewLook(int colourway, int skin, int headgear) { this.colourway = colourway; this.skin = skin; this.headgear = headgear; }
}

public static class CrewProfile
{
    public const int HeadgearCount = 3;
    public static readonly string[] HeadgearNames = { "HARD HAT", "WELDER MASK", "GOGGLE BEANIE" };
    public const int MaxNameLength = 14;
    static readonly string[] RandomNames = { "RUSTY", "BOLT", "DUSTY", "GRIT", "SPARKS", "AUGER", "CINDER", "TORQUE", "MAGMA", "KNUCKLES", "RIVET", "GEARS" };

    const string KeyName = "crew.name", KeyColour = "crew.colourway", KeySkin = "crew.skin", KeyHeadgear = "crew.headgear";

    public static event Action Changed;

    public static string Name
    {
        get => PlayerPrefs.GetString(KeyName, "RUSTY");
        set { PlayerPrefs.SetString(KeyName, Clean(value)); Changed?.Invoke(); }
    }

    public static CrewLook Look
    {
        get => new CrewLook(PlayerPrefs.GetInt(KeyColour, 0), PlayerPrefs.GetInt(KeySkin, 1), PlayerPrefs.GetInt(KeyHeadgear, 0));
        set
        {
            PlayerPrefs.SetInt(KeyColour, Wrap(value.colourway, CrewPalette.Colourways.Length));
            PlayerPrefs.SetInt(KeySkin, Wrap(value.skin, CrewPalette.SkinTones.Length));
            PlayerPrefs.SetInt(KeyHeadgear, Wrap(value.headgear, HeadgearCount));
            Changed?.Invoke();
        }
    }

    public static void Save() => PlayerPrefs.Save();

    public static string Clean(string s)
    {
        s = string.IsNullOrWhiteSpace(s) ? "RUSTY" : s.Trim().ToUpperInvariant();
        return s.Length > MaxNameLength ? s.Substring(0, MaxNameLength) : s;
    }

    public static int Wrap(int i, int n) => ((i % n) + n) % n;

    public static CrewLook RandomLook() => new CrewLook(UnityEngine.Random.Range(0, CrewPalette.Colourways.Length), UnityEngine.Random.Range(0, CrewPalette.SkinTones.Length), UnityEngine.Random.Range(0, HeadgearCount));
    public static string RandomName() => RandomNames[UnityEngine.Random.Range(0, RandomNames.Length)];
}
