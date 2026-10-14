using UnityEngine;

// Palette textures for the Driller. Every face of the mesh samples the centre of one 8x8 px cell
// in a 64x64 texture (rows 0-3); hazard-stripe faces sample the stripe band (rows 4-7).
// Cell indices must match SourceArt/Characters/Driller/build_driller.py.
// One tiny RGBA texture per visible character is generated at runtime from the chosen
// colourway + skin tone, so every combination costs 16 KB and no extra materials.
public static class CrewPalette
{
    public const int Size = 64;
    public const int Suit = 0, SuitDark = 1, Accent = 2, Helmet = 3, Metal = 4, MetalDark = 5, Rubber = 6, Leather = 7;
    public const int Skin = 8, SkinDark = 9, Hair = 10, EyeWhite = 11, EyeDark = 12, Lens = 13, Visor = 14, Brass = 15;
    public const int Tank = 16, Cloth = 17, Glass = 18;

    public struct Colourway
    {
        public string name;
        public Color suit, suitDark, accent, helmet, tank, cloth;
        public Colourway(string n, string suit, string suitDark, string accent, string helmet, string tank, string cloth)
        {
            name = n; this.suit = Hex(suit); this.suitDark = Hex(suitDark); this.accent = Hex(accent);
            this.helmet = Hex(helmet); this.tank = Hex(tank); this.cloth = Hex(cloth);
        }
    }

    public struct SkinTone
    {
        public string name;
        public Color skin, skinDark, hair;
        public SkinTone(string n, string skin, string skinDark, string hair) { name = n; this.skin = Hex(skin); this.skinDark = Hex(skinDark); this.hair = Hex(hair); }
    }

    // Crimson matches the drill train; the others echo the weapon cars (hazard, gunmetal, tesla blue).
    public static readonly Colourway[] Colourways =
    {
        new Colourway("CRIMSON",  "B3261E", "5E1712", "F2B10F", "F2B10F", "D0CEC4", "3E4A3D"),
        new Colourway("HAZARD",   "E89A12", "6E4708", "232427", "FF7A1E", "3A3D42", "2C2C30"),
        new Colourway("GUNMETAL", "4A5058", "24272C", "FF5229", "C22A1E", "B8BEC6", "5E1712"),
        new Colourway("TESLA",    "1F5C7A", "112F40", "5BCBFF", "E9EEF2", "B8BEC6", "203848"),
        new Colourway("DUNE",     "C8A06A", "6E5232", "2E8C7A", "EDE3CF", "8C3B22", "6B4126"),
    };

    public static readonly SkinTone[] SkinTones =
    {
        new SkinTone("LIGHT",  "E8B894", "C98E6C", "B5632E"),
        new SkinTone("TAN",    "D9A07A", "B87A57", "5A3A22"),
        new SkinTone("BROWN",  "9C6644", "7A4B30", "2A1B12"),
        new SkinTone("DEEP",   "6B4430", "4E3022", "17110D"),
    };

    static readonly Color MetalC = Hex("9AA0A6"), MetalDarkC = Hex("3A3D42"), RubberC = Hex("232427"), LeatherC = Hex("6B4126");
    static readonly Color EyeWhiteC = Hex("F2EEE6"), EyeDarkC = Hex("1A1410"), LensC = Hex("FFE7A0"), VisorC = Hex("FF7A1E"), BrassC = Hex("C9962E"), GlassC = Hex("9FE4FF");
    static readonly Color StripeA = Hex("F2B10F"), StripeB = Hex("1C1C1E");

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    static Color[] Cells(Colourway w, SkinTone s)
    {
        var c = new Color[32];
        for (int i = 0; i < c.Length; i++) c[i] = Color.magenta;
        c[Suit] = w.suit; c[SuitDark] = w.suitDark; c[Accent] = w.accent; c[Helmet] = w.helmet;
        c[Metal] = MetalC; c[MetalDark] = MetalDarkC; c[Rubber] = RubberC; c[Leather] = LeatherC;
        c[Skin] = s.skin; c[SkinDark] = s.skinDark; c[Hair] = s.hair; c[EyeWhite] = EyeWhiteC;
        c[EyeDark] = EyeDarkC; c[Lens] = LensC; c[Visor] = VisorC; c[Brass] = BrassC;
        c[Tank] = w.tank; c[Cloth] = w.cloth; c[Glass] = GlassC;
        return c;
    }

    static Texture2D New(string name, bool linear)
    {
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false, linear) { name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        return t;
    }

    // Base colour (sRGB). Pass an existing texture to refill it in place.
    public static Texture2D BaseColor(Colourway w, SkinTone s, Texture2D into = null)
    {
        var t = into != null ? into : New("T_Driller_BaseColor", false);
        var cells = Cells(w, s);
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
                px[y * Size + x] = y < 32 ? (Color32)cells[(y / 8) * 8 + x / 8] : (Color32)((((x + y) / 8) % 2 == 0) ? StripeA : StripeB);
        t.SetPixels32(px);
        t.Apply(false, false);
        return t;
    }

    // Emission: only the lamp lens, welder visor and goggle glass glow.
    public static Texture2D Emission()
    {
        var t = New("T_Driller_Emission", false);
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color c = Color.black;
                if (y < 32)
                {
                    int i = (y / 8) * 8 + x / 8;
                    if (i == Lens) c = LensC; else if (i == Visor) c = VisorC; else if (i == Glass) c = GlassC * 0.5f;
                }
                px[y * Size + x] = c;
            }
        t.SetPixels32(px);
        t.Apply(false, false);
        return t;
    }

    // URP Lit metallic map: R = metallic, A = smoothness (linear).
    public static Texture2D Mask()
    {
        var t = New("T_Driller_Mask", true);
        var px = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float m = 0f, s = 0.3f;
                if (y < 32)
                {
                    int i = (y / 8) * 8 + x / 8;
                    switch (i)
                    {
                        case Metal: case Brass: m = 0.85f; s = 0.55f; break;
                        case MetalDark: m = 0.7f; s = 0.4f; break;
                        case Helmet: case Tank: s = 0.55f; break;
                        case Rubber: s = 0.2f; break;
                        case Lens: case Visor: case Glass: case EyeDark: case EyeWhite: s = 0.85f; break;
                        case Skin: case SkinDark: s = 0.35f; break;
                        case Hair: case Cloth: s = 0.12f; break;
                        case Leather: s = 0.4f; break;
                    }
                }
                else s = 0.35f;
                px[y * Size + x] = new Color(m, 0, 0, s);
            }
        t.SetPixels32(px);
        t.Apply(false, false);
        return t;
    }
}
