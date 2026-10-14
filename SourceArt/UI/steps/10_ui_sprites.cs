using UnityEngine;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine.U2D;
using System;
using System.IO;
using System.Collections.Generic;

// Generates the Drillex industrial UI kit (panels, buttons, bars, rings, icons) and VFX textures
// procedurally with signed-distance shapes, imports them as sprites and packs one SpriteAtlas.
internal class CommandScript : IRunCommand
{
    const string UiDir = "Assets/UI/Sprites";
    const string IconDir = "Assets/UI/Sprites/Icons";
    const string VfxDir = "Assets/Art/VFX/Textures";

    delegate float Sdf(Vector2 p);

    // ---------------- SDF helpers (128-unit space, centre origin, y up) ----------------
    static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
    static float Box(Vector2 p, Vector2 c, Vector2 h)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y);
        return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0);
    }
    static float RBox(Vector2 p, Vector2 c, Vector2 h, float r) => Box(p, c, h - Vector2.one * r) - r;
    static float Seg(Vector2 p, Vector2 a, Vector2 b, float r)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude - r;
    }
    static float Poly(Vector2 p, Vector2[] v)
    {
        float d = Vector2.Dot(p - v[0], p - v[0]); float s = 1f;
        for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
        {
            Vector2 e = v[j] - v[i], w = p - v[i];
            Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
            d = Mathf.Min(d, Vector2.Dot(b, b));
            bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
            if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s *= -1f;
        }
        return s * Mathf.Sqrt(d);
    }
    static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w;
    // ring limited to angles [a0,a1] degrees (0 = +x, ccw)
    static float Arc(Vector2 p, Vector2 c, float r, float w, float a0, float a1)
    {
        Vector2 q = p - c; float ang = Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg; if (ang < 0) ang += 360;
        bool inside = a0 <= a1 ? (ang >= a0 && ang <= a1) : (ang >= a0 || ang <= a1);
        if (inside) return Mathf.Abs(q.magnitude - r) - w;
        Vector2 e0 = c + new Vector2(Mathf.Cos(a0 * Mathf.Deg2Rad), Mathf.Sin(a0 * Mathf.Deg2Rad)) * r;
        Vector2 e1 = c + new Vector2(Mathf.Cos(a1 * Mathf.Deg2Rad), Mathf.Sin(a1 * Mathf.Deg2Rad)) * r;
        return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - w;
    }
    static float U(params float[] d) { float m = d[0]; for (int i = 1; i < d.Length; i++) m = Mathf.Min(m, d[i]); return m; }
    static float Sub(float a, float b) => Mathf.Max(a, -b);
    static Vector2 Rot(Vector2 p, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(c * p.x + s * p.y, -s * p.x + c * p.y); }
    static Vector2 V(float x, float y) => new Vector2(x, y);
    // chamfered box: corners top-right and bottom-left cut by `cut`
    static float Chamfer(Vector2 p, Vector2 h, float cut)
    {
        float b = Box(p, Vector2.zero, h);
        float k = (h.x + h.y - cut);
        float c1 = (p.x + p.y - k) * 0.70710678f;
        float c2 = (-p.x - p.y - k) * 0.70710678f;
        return Mathf.Max(b, Mathf.Max(c1, c2));
    }

    static float Hash(float x) { float s = Mathf.Sin(x * 127.1f) * 43758.5453f; return s - Mathf.Floor(s); }
    static float Noise2(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float xf = x - xi, yf = y - yi;
        float h(int a, int b) => Hash(a * 57.0f + b * 113.0f);
        float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
        return Mathf.Lerp(Mathf.Lerp(h(xi, yi), h(xi + 1, yi), u), Mathf.Lerp(h(xi, yi + 1), h(xi + 1, yi + 1), u), v);
    }
    static float Fbm(float x, float y) { float a = 0, amp = 0.5f; for (int i = 0; i < 5; i++) { a += Noise2(x, y) * amp; x *= 2.03f; y *= 2.03f; amp *= 0.5f; } return a; }

    // ---------------- rasterisers ----------------
    static Texture2D NewTex(int w, int h) => new Texture2D(w, h, TextureFormat.RGBA32, false, false);

    static Texture2D Icon(Sdf f, int size = 128)
    {
        var t = NewTex(size, size); float s = 128f / size;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) * s - 64f, (y + 0.5f) * s - 64f);
                float a = Mathf.Clamp01(0.5f - f(p) / s);
                px[y * size + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

    // Pre-coloured gunmetal panel / button with steel bevel.
    static Texture2D MetalPlate(int w, int h, float cut, Color top, Color bottom, Color bevel, Color edgeDark, float alpha, bool brushed, bool topHighlight)
    {
        var t = NewTex(w, h); var px = new Color[w * h];
        Vector2 half = new Vector2(w / 2f - 1f, h / 2f - 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - w / 2f, y + 0.5f - h / 2f);
                float d = Chamfer(p, half, cut);
                float cover = Mathf.Clamp01(0.5f - d);
                if (cover <= 0) { px[y * w + x] = Color.clear; continue; }
                float gy = (float)y / (h - 1);
                Color c = Color.Lerp(bottom, top, gy);
                if (brushed) { float n = (Hash(y * 0.71f) - 0.5f) * 0.025f + (Fbm(x * 0.05f, y * 0.9f) - 0.5f) * 0.03f; c += new Color(n, n, n, 0); }
                float inner = -d;
                // outer bevel: lighter on top/left edges, darker on bottom/right
                float lightDir = Mathf.Clamp01(0.5f + (p.y / half.y) * 0.35f - (p.x / half.x) * 0.15f);
                Color bev = Color.Lerp(bevel * 0.55f, bevel * 1.15f, lightDir); bev.a = 1;
                if (inner < 2.2f) c = Color.Lerp(c, bev, Mathf.Clamp01(2.2f - inner + 0.3f));
                else if (inner < 3.6f) c = Color.Lerp(c, edgeDark, 0.85f * Mathf.Clamp01(3.6f - inner));
                if (topHighlight && inner > 3.6f && inner < 5.2f && p.y > 0) c = Color.Lerp(c, Color.white, 0.10f * Mathf.Clamp01(p.y / half.y));
                c.a = alpha * cover;
                px[y * w + x] = c;
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    // White chamfered plate for tinting (yellow buttons, toggle tracks, badges).
    static Texture2D WhitePlate(int w, int h, float cut, bool bevel)
    {
        var t = NewTex(w, h); var px = new Color[w * h];
        Vector2 half = new Vector2(w / 2f - 1f, h / 2f - 1f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - w / 2f, y + 0.5f - h / 2f);
                float d = Chamfer(p, half, cut);
                float cover = Mathf.Clamp01(0.5f - d);
                float gy = (float)y / (h - 1);
                float v = bevel ? Mathf.Lerp(0.78f, 1f, gy) : 1f;
                float inner = -d;
                if (bevel)
                {
                    if (inner < 2.5f && p.y < 0) v *= 0.7f;           // dark lower lip
                    else if (inner < 2.5f) v = Mathf.Min(1f, v * 1.1f);
                    if (inner > 2.5f && inner < 4f && p.y > 0) v = 1f; // top highlight
                }
                px[y * w + x] = new Color(v, v, v, cover);
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Outline(int w, int h, float cut, float thick, float glow)
    {
        var t = NewTex(w, h); var px = new Color[w * h];
        float pad = glow > 0 ? glow : 1f;
        Vector2 half = new Vector2(w / 2f - pad - thick, h / 2f - pad - thick);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - w / 2f, y + 0.5f - h / 2f);
                float d = Mathf.Abs(Chamfer(p, half, cut)) - thick;
                float a = glow > 0 ? Mathf.Exp(-Mathf.Max(0, d) * Mathf.Max(0, d) / (glow * glow * 0.18f)) : Mathf.Clamp01(0.5f - d);
                px[y * w + x] = new Color(1, 1, 1, a);
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Radial(int size, Func<float, float> fn) // fn(r 0..1) -> alpha
    {
        var t = NewTex(size, size); var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(fn(r)));
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D MetalRing(int size)
    {
        var t = NewTex(size, size); var px = new Color[size * size]; float R = size / 2f;
        Color steelL = Hex("#a9afb6"), steelD = Hex("#2b2f34"), inner = Hex("#0d0e10");
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - R, y + 0.5f - R);
                float r = p.magnitude; float ang = Mathf.Atan2(p.y, p.x);
                float outerD = r - (R - 2f), innerD = (R - 22f) - r;
                float cover = Mathf.Clamp01(0.5f - outerD) * Mathf.Clamp01(0.5f - innerD);
                if (cover <= 0) { px[y * size + x] = Color.clear; continue; }
                float light = 0.5f + 0.5f * Mathf.Cos(ang - Mathf.PI * 0.75f);
                float band = Mathf.InverseLerp(R - 22f, R - 2f, r);
                Color c = Color.Lerp(steelD, steelL, light * 0.8f + 0.1f);
                if (band < 0.18f || band > 0.86f) c = Color.Lerp(c, inner, 0.6f);
                if (Mathf.Abs(band - 0.5f) < 0.06f) c = Color.Lerp(c, inner, 0.35f);
                // notches every 30 degrees
                float notch = Mathf.Abs(Mathf.Repeat(ang * Mathf.Rad2Deg + 15f, 30f) - 15f);
                if (notch < 1.3f && band > 0.25f && band < 0.8f) c = Color.Lerp(c, inner, 0.7f);
                c.a = cover; px[y * size + x] = c;
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Hazard(int size)
    {
        var t = NewTex(size, size); var px = new Color[size * size];
        Color yel = Hex("#f2b10f"), blk = Hex("#141416");
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float v = Mathf.Repeat((x + y) / (float)size * 2f, 1f);
                float a = Mathf.Clamp01((Mathf.Min(v, 1f - v) * size - 0.25f * size + 0.5f * 1.4f) / 1.4f);
                Color c = Color.Lerp(blk, yel, Mathf.SmoothStep(0, 1, Mathf.Clamp01((v < 0.5f ? 1 : 0) == 1 ? 1 : 0)));
                c = (v < 0.5f) ? yel : blk;
                float edge = Mathf.Min(Mathf.Abs(v - 0.5f), Mathf.Min(v, 1f - v)) * size;
                if (edge < 1f) c = Color.Lerp(yel, blk, 0.5f);
                float n = (Fbm(x * 0.15f, y * 0.15f) - 0.5f) * 0.12f; c += new Color(n, n, n, 0);
                c.a = 1; px[y * size + x] = c;
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Orb(int size)
    {
        var t = NewTex(size, size); var px = new Color[size * size]; float R = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - R, y + 0.5f - R) / R;
                float r = p.magnitude;
                float body = Mathf.Clamp01((0.62f - r) * R);
                float glow = Mathf.Exp(-Mathf.Pow(Mathf.Max(0, r - 0.55f) / 0.18f, 2f)) * 0.6f;
                Color core = Color.Lerp(Hex("#fff4c2"), Hex("#ff9d14"), Mathf.Clamp01(r / 0.62f));
                core = Color.Lerp(core, Hex("#e0560a"), Mathf.Clamp01((r - 0.4f) / 0.22f));
                float spec = Mathf.Exp(-((p - V(-0.2f, 0.22f)).sqrMagnitude) / 0.012f);
                core = Color.Lerp(core, Color.white, spec);
                Color g = Hex("#ff8a1a"); g.a = glow;
                Color c = body > 0 ? new Color(core.r, core.g, core.b, Mathf.Max(body, glow)) : g;
                px[y * size + x] = c;
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Arcs(int w, int h, int seed, int count)
    {
        var rnd = new System.Random(seed);
        var segs = new List<Vector4>();
        for (int a = 0; a < count; a++)
        {
            Vector2 p = new Vector2((float)rnd.NextDouble() * w * 0.2f, h * (0.2f + 0.6f * (float)rnd.NextDouble()));
            int n = 14;
            for (int i = 0; i < n; i++)
            {
                Vector2 q = p + new Vector2(w / (float)n * (0.8f + 0.4f * (float)rnd.NextDouble()), ((float)rnd.NextDouble() - 0.5f) * h * 0.22f);
                q.y = Mathf.Clamp(q.y, 6, h - 6);
                segs.Add(new Vector4(p.x, p.y, q.x, q.y));
                if (rnd.NextDouble() < 0.18)
                {
                    Vector2 f = q + new Vector2(w * 0.06f, ((float)rnd.NextDouble() - 0.5f) * h * 0.4f);
                    segs.Add(new Vector4(q.x, q.y, f.x, Mathf.Clamp(f.y, 4, h - 4)));
                }
                p = q;
            }
        }
        var t = NewTex(w, h); var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 pp = new Vector2(x + 0.5f, y + 0.5f); float d = 1e9f;
                foreach (var s in segs) d = Mathf.Min(d, Seg(pp, V(s.x, s.y), V(s.z, s.w), 0));
                float core = Mathf.Clamp01(1.6f - d);
                float glow = Mathf.Exp(-d * d / 30f) * 0.55f;
                px[y * w + x] = new Color(1, 1, 1, Mathf.Clamp01(core + glow));
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Burst(int size)
    {
        var t = NewTex(size, size); var px = new Color[size * size]; float R = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - R, y + 0.5f - R) / R; float r = p.magnitude; float ang = Mathf.Atan2(p.y, p.x);
                float rays = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f)), 18f) * Mathf.Clamp01(1f - r) * 0.9f;
                float rays2 = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f + 0.26f)), 30f) * Mathf.Clamp01(0.8f - r) * 0.6f;
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.55f) / 0.06f, 2f)) * 0.5f;
                float core = Mathf.Exp(-r * r / 0.03f);
                px[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(rays + rays2 + ring + core));
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Gradient(int w, int h, bool horizontal)
    {
        var t = NewTex(w, h); var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float k = horizontal ? (x + 0.5f) / w : (y + 0.5f) / h;
                px[y * w + x] = new Color(1, 1, 1, Mathf.SmoothStep(1f, 0f, k));
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D BarFill(int w, int h)
    {
        var t = NewTex(w, h); var px = new Color[w * h];
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float gy = (y + 0.5f) / h;
                float v = Mathf.Lerp(0.72f, 1f, gy);
                if (gy > 0.62f && gy < 0.8f) v = Mathf.Min(1f, v + 0.08f);
                // fine diagonal grooves for an industrial gauge feel
                float groove = Mathf.Repeat((x + y) / 10f, 1f) < 0.12f ? 0.9f : 1f;
                float a = (y == 0 || y == h - 1) ? 0.6f : 1f;
                px[y * w + x] = new Color(v * groove, v * groove, v * groove, a);
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    static Texture2D Noise(int size, float scale, float contrast, Func<float, float> radial)
    {
        var t = NewTex(size, size); var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float r = new Vector2(x + 0.5f - size / 2f, y + 0.5f - size / 2f).magnitude / (size / 2f);
                float n = Fbm(x * scale + 13.1f, y * scale + 7.7f);
                float a = Mathf.Clamp01((n - 0.5f) * contrast + 0.5f) * radial(r);
                float v = Mathf.Lerp(0.75f, 1f, n);
                px[y * size + x] = new Color(v, v, v, Mathf.Clamp01(a));
            }
        t.SetPixels(px); t.Apply(); return t;
    }

    // ---------------- icons ----------------
    static Dictionary<string, Sdf> Icons()
    {
        var d = new Dictionary<string, Sdf>();
        d["pause"] = p => U(RBox(p, V(-17, 0), V(9, 30), 3), RBox(p, V(17, 0), V(9, 30), 3));
        d["play"] = p => Poly(p, new[] { V(-22, -34), V(38, 0), V(-22, 34) }) - 3;
        d["close"] = p => U(Seg(p, V(-28, -28), V(28, 28), 7), Seg(p, V(-28, 28), V(28, -28), 7));
        d["check"] = p => U(Seg(p, V(-34, 0), V(-10, -24), 8), Seg(p, V(-10, -24), V(36, 26), 8));
        d["gear"] = p =>
        {
            float body = Circle(p, Vector2.zero, 30);
            for (int i = 0; i < 8; i++) { Vector2 q = Rot(p, i * 45f); body = Mathf.Min(body, RBox(q, V(0, 34), V(8, 10), 2)); }
            return Sub(body, Circle(p, Vector2.zero, 13));
        };
        d["home"] = p => Sub(U(Poly(p, new[] { V(-44, 4), V(0, 42), V(44, 4) }), Box(p, V(0, -18), V(30, 24))), Box(p, V(0, -26), V(9, 16)));
        d["restart"] = p => U(Arc(p, Vector2.zero, 30, 6.5f, 70, 20), Poly(p, new[] { V(12, 46), V(36, 26), V(8, 14) }) - 1);
        d["exit"] = p => U(Sub(Box(p, V(-16, 0), V(24, 40)), Box(p, V(-14, 0), V(16, 32))), Seg(p, V(0, 0), V(36, 0), 6), Poly(p, new[] { V(30, 16), V(48, 0), V(30, -16) }));
        d["music"] = p => U(Circle(p, V(-22, -26), 12), Circle(p, V(22, -18), 12), Seg(p, V(-12, -26), V(-12, 30), 4.5f), Seg(p, V(32, -18), V(32, 38), 4.5f), Seg(p, V(-12, 30), V(32, 38), 7));
        Sdf speaker = p => U(Box(p, V(-26, 0), V(12, 14)), Poly(p, new[] { V(-16, 14), V(10, 36), V(10, -36), V(-16, -14) }));
        d["speaker"] = speaker;
        d["sfx"] = p => U(speaker(p), Arc(p, V(10, 0), 18, 4.5f, 300, 60), Arc(p, V(10, 0), 32, 4.5f, 305, 55));
        d["upgrade"] = p => U(Poly(p, new[] { V(0, 50), V(-36, 12), V(-14, 12), V(-14, -6), V(14, -6), V(14, 12), V(36, 12) }),
                              Seg(p, V(-30, -22), V(0, -10), 6.5f), Seg(p, V(0, -10), V(30, -22), 6.5f),
                              Seg(p, V(-30, -42), V(0, -30), 6.5f), Seg(p, V(0, -30), V(30, -42), 6.5f));
        d["cannon"] = p => U(RBox(p, V(-20, -8), V(24, 22), 6), Box(p, V(20, 2), V(28, 7)), Box(p, V(46, 2), V(4, 10)), Box(p, V(-20, -34), V(30, 5)));
        d["twin"] = p => U(RBox(p, V(-20, -8), V(24, 24), 6), Box(p, V(20, 8), V(28, 5)), Box(p, V(20, -10), V(28, 5)), Box(p, V(46, -1), V(4, 17)), Box(p, V(-20, -36), V(30, 5)));
        d["gatling"] = p => U(RBox(p, V(-28, -6), V(18, 22), 5), Box(p, V(16, 10), V(32, 3.5f)), Box(p, V(16, -2), V(32, 3.5f)), Box(p, V(16, -14), V(32, 3.5f)), Box(p, V(8, -2), V(4, 18)), Box(p, V(38, -2), V(3, 16)), Box(p, V(-28, -34), V(26, 5)));
        d["missile"] = p =>
        {
            Vector2 q = Rot(p, -45f);
            return U(RBox(q, V(-2, 0), V(26, 9), 4), Poly(q, new[] { V(23, 9), V(46, 0), V(23, -9) }),
                     Poly(q, new[] { V(-12, 8), V(-28, 8), V(-40, 24), V(-26, 24) }), Poly(q, new[] { V(-12, -8), V(-28, -8), V(-40, -24), V(-26, -24) }),
                     Poly(q, new[] { V(-30, 6), V(-50, 0), V(-30, -6) }));
        };
        d["bolt"] = p => Poly(p, new[] { V(10, 52), V(-30, -4), V(-4, -4), V(-14, -52), V(30, 10), V(4, 10) }) - 1.5f;
        d["flame"] = p =>
        {
            float outer = Mathf.Min(Circle(p, V(0, -20), 28), Poly(p, new[] { V(-28, -16), V(-24, 14), V(-10, 2), V(-2, 54), V(14, 20), V(24, 34), V(28, -16) }) - 2);
            float inner = Mathf.Min(Circle(p, V(0, -26), 12), Poly(p, new[] { V(-12, -24), V(-2, 12), V(4, -2), V(12, -24) }));
            return Sub(outer, inner + 1);
        };
        d["storage"] = p => U(Sub(RBox(p, V(0, 0), V(40, 34), 4), RBox(p, V(0, 0), V(32, 26), 2)), Box(p, V(0, 8), V(36, 3)), Box(p, V(0, -8), V(36, 3)), Circle(p, V(0, 0), 7));
        d["speed"] = p => U(Seg(p, V(-34, 30), V(-6, 0), 7), Seg(p, V(-6, 0), V(-34, -30), 7), Seg(p, V(4, 30), V(32, 0), 7), Seg(p, V(32, 0), V(4, -30), 7));
        d["shield"] = p => Sub(Poly(p, new[] { V(-38, 34), V(0, 46), V(38, 34), V(34, -6), V(0, -46), V(-34, -6) }), U(Box(p, V(0, 2), V(5, 22)), Box(p, V(0, 2), V(20, 5))));
        d["repair"] = p =>
        {
            Vector2 q = Rot(p, 45f);
            float head = Sub(Circle(q, V(0, 28), 20), U(Box(q, V(0, 42), V(8, 16)), Circle(q, V(0, 28), 7)));
            return U(head, RBox(q, V(0, -14), V(8, 30), 6));
        };
        d["detach"] = p => U(Sub(RBox(p, V(-24, 8), V(22, 13), 12), RBox(p, V(-24, 8), V(13, 5), 5)), Sub(RBox(p, V(24, -8), V(22, 13), 12), RBox(p, V(24, -8), V(13, 5), 5)), Seg(p, V(-6, -24), V(-14, -40), 3.5f), Seg(p, V(6, 24), V(14, 40), 3.5f));
        d["soundtrap"] = p => U(Circle(p, Vector2.zero, 10), Arc(p, Vector2.zero, 24, 5, 315, 45), Arc(p, Vector2.zero, 24, 5, 135, 225), Arc(p, Vector2.zero, 40, 5, 320, 40), Arc(p, Vector2.zero, 40, 5, 140, 220));
        d["star"] = p =>
        {
            var v = new Vector2[10];
            for (int i = 0; i < 10; i++) { float a = (90 + i * 36) * Mathf.Deg2Rad; float r = i % 2 == 0 ? 48 : 20; v[i] = V(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
            return Poly(p, v) - 1;
        };
        d["warning"] = p => U(Sub(Poly(p, new[] { V(0, 48), V(48, -38), V(-48, -38) }) - 3, Poly(p, new[] { V(0, 30), V(32, -28), V(-32, -28) })), RBox(p, V(0, 6), V(5, 15), 4), Circle(p, V(0, -18), 6));
        d["lock"] = p => U(Sub(RBox(p, V(0, -14), V(32, 26), 5), U(Circle(p, V(0, -8), 7), Box(p, V(0, -20), V(3, 10)))), Arc(p, V(0, 14), 20, 6, 0, 180), Box(p, V(-20, 10), V(6, 6)), Box(p, V(20, 10), V(6, 6)));
        d["orb"] = p => Sub(Circle(p, Vector2.zero, 38), Arc(p, Vector2.zero, 26, 3.5f, 100, 170));
        d["train"] = p => U(RBox(p, V(-6, 4), V(40, 22), 4), Box(p, V(26, 30), V(8, 6)), Circle(p, V(-30, -26), 10), Circle(p, V(0, -26), 10), Circle(p, V(30, -26), 10), Poly(p, new[] { V(34, 26), V(52, 4), V(34, -18) }));
        return d;
    }

    // ---------------- io ----------------
    static readonly List<string> written = new List<string>();
    static readonly Dictionary<string, Vector4> borders = new Dictionary<string, Vector4>();
    static readonly HashSet<string> vfx = new HashSet<string>();

    static void Save(Texture2D t, string dir, string name, Vector4 border = default)
    {
        Directory.CreateDirectory(dir);
        string path = dir + "/" + name + ".png";
        File.WriteAllBytes(path, t.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(t);
        written.Add(path);
        if (border != default) borders[path] = border;
        if (dir == VfxDir) vfx.Add(path);
    }

    public void Execute(ExecutionResult result)
    {
        written.Clear(); borders.Clear(); vfx.Clear();
        Color gunTop = Hex("#2c3035"), gunBot = Hex("#16181b"), steel = Hex("#8a9097"), dark = Hex("#08090a");

        // panels & buttons (9-slice)
        Save(MetalPlate(128, 128, 22, gunTop, gunBot, steel, dark, 0.95f, true, true), UiDir, "ui_panel", new Vector4(30, 30, 30, 30));
        Save(MetalPlate(128, 128, 22, Hex("#1d2023"), Hex("#0f1012"), Hex("#5c6268"), dark, 0.9f, true, false), UiDir, "ui_panel_dark", new Vector4(30, 30, 30, 30));
        Save(MetalPlate(128, 96, 16, Hex("#454a51"), Hex("#1f2226"), Hex("#9aa1a8"), dark, 1f, true, true), UiDir, "ui_button", new Vector4(24, 24, 24, 24));
        Save(WhitePlate(128, 96, 16, true), UiDir, "ui_button_fill", new Vector4(24, 24, 24, 24));
        Save(WhitePlate(64, 64, 10, false), UiDir, "ui_plate", new Vector4(16, 16, 16, 16));
        Save(Outline(128, 128, 22, 1.6f, 0), UiDir, "ui_frame", new Vector4(30, 30, 30, 30));
        Save(Outline(160, 160, 22, 1.5f, 16), UiDir, "ui_frame_glow", new Vector4(46, 46, 46, 46));
        Save(MetalPlate(64, 32, 8, Hex("#0b0c0e"), Hex("#121417"), Hex("#4a4f55"), dark, 0.92f, false, false), UiDir, "ui_bar_bg", new Vector4(12, 12, 12, 12));
        Save(BarFill(256, 32), UiDir, "ui_bar_fill");
        Save(Hazard(64), UiDir, "ui_hazard");
        Save(Gradient(128, 8, true), UiDir, "ui_grad_h");
        Save(Gradient(8, 128, false), UiDir, "ui_grad_v");

        // round shapes
        Save(Radial(256, r => (1f - r) * 128f), UiDir, "ui_circle");
        Save(Radial(256, r => Mathf.Clamp01((0.07f - Mathf.Abs(r - 0.92f)) * 128f)), UiDir, "ui_ring");
        Save(Radial(256, r => Mathf.Clamp01((0.022f - Mathf.Abs(r - 0.96f)) * 128f)), UiDir, "ui_ring_thin");
        Save(Radial(256, r => Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.12f, 2f))), UiDir, "ui_ring_glow");
        Save(Radial(128, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f)), UiDir, "ui_glow");
        Save(MetalRing(256), UiDir, "ui_ring_metal");
        {
            var t = NewTex(256, 256); var px = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
                {
                    Vector2 p = V(x + 0.5f - 128, y + 0.5f - 128); float r = p.magnitude / 128f; float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg + 180f;
                    float dash = Mathf.Repeat(ang, 15f) < 9f ? 1f : 0f;
                    px[y * 256 + x] = new Color(1, 1, 1, dash * Mathf.Clamp01((0.03f - Mathf.Abs(r - 0.95f)) * 128f));
                }
            t.SetPixels(px); t.Apply(); Save(t, UiDir, "ui_ring_dashed");
        }
        Save(Orb(128), UiDir, "ui_orb");
        Save(Arcs(256, 96, 7, 3), UiDir, "ui_arcs");
        Save(Burst(256), UiDir, "ui_burst");

        // icons
        foreach (var kv in Icons()) Save(Icon(kv.Value), IconDir, "icon_" + kv.Key);

        // vfx textures
        Save(Radial(64, r => Mathf.Pow(Mathf.Clamp01(1f - r), 2f)), VfxDir, "T_VFX_SoftDot");
        Save(Radial(128, r => Mathf.Exp(-r * r / 0.02f) + Mathf.Exp(-r * r / 0.2f) * 0.35f), VfxDir, "T_VFX_Glow");
        {
            var t = NewTex(128, 128); var px = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                {
                    Vector2 p = V(x + 0.5f - 64, y + 0.5f - 64) / 64f; float r = p.magnitude;
                    float spikes = Mathf.Exp(-Mathf.Abs(p.x) * 26f) * Mathf.Clamp01(1f - Mathf.Abs(p.y)) + Mathf.Exp(-Mathf.Abs(p.y) * 26f) * Mathf.Clamp01(1f - Mathf.Abs(p.x));
                    float core = Mathf.Exp(-r * r / 0.03f) + Mathf.Exp(-r * r / 0.18f) * 0.35f;
                    px[y * 128 + x] = new Color(1, 1, 1, Mathf.Clamp01(core + spikes * 0.7f));
                }
            t.SetPixels(px); t.Apply(); Save(t, VfxDir, "T_VFX_Flash");
        }
        Save(Noise(128, 0.06f, 2.2f, r => Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(r))), VfxDir, "T_VFX_Smoke");
        Save(Noise(128, 0.08f, 2.8f, r => Mathf.Pow(Mathf.Clamp01(1f - r), 1.2f)), VfxDir, "T_VFX_Fire");
        Save(Radial(128, r => Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.08f, 2f))), VfxDir, "T_VFX_Ring");
        {
            var t = NewTex(64, 16); var px = new Color[64 * 16];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 64; x++)
                {
                    float v = Mathf.Abs((y + 0.5f) / 16f - 0.5f) * 2f;
                    px[y * 64 + x] = new Color(1, 1, 1, Mathf.Exp(-v * v * 6f) * 0.7f + Mathf.Exp(-v * v * 60f) * 0.3f);
                }
            t.SetPixels(px); t.Apply(); Save(t, VfxDir, "T_VFX_Beam");
        }
        {   // streak for sparks / tracers (stretched billboards): bright head, soft tail along x
            var t = NewTex(64, 16); var px = new Color[64 * 16];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 64; x++)
                {
                    float v = Mathf.Abs((y + 0.5f) / 16f - 0.5f) * 2f; float u = (x + 0.5f) / 64f;
                    px[y * 64 + x] = new Color(1, 1, 1, Mathf.Exp(-v * v * 8f) * Mathf.SmoothStep(0f, 1f, Mathf.Min(u * 3f, (1f - u) * 6f)));
                }
            t.SetPixels(px); t.Apply(); Save(t, VfxDir, "T_VFX_Streak");
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var path in written)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            bool isVfx = vfx.Contains(path);
            imp.textureType = isVfx ? TextureImporterType.Default : TextureImporterType.Sprite;
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = isVfx;
            imp.sRGBTexture = true;
            imp.isReadable = false;
            imp.wrapMode = isVfx ? TextureWrapMode.Clamp : TextureWrapMode.Clamp;
            if (!isVfx)
            {
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spritePixelsPerUnit = 100;
                var s = new TextureImporterSettings(); imp.ReadTextureSettings(s);
                s.spriteMeshType = SpriteMeshType.FullRect;
                s.spriteGenerateFallbackPhysicsShape = false;
                imp.SetTextureSettings(s);
                imp.spriteBorder = borders.TryGetValue(path, out var b) ? b : Vector4.zero;
            }
            else
            {
                imp.maxTextureSize = 128;
                foreach (var plat in new[] { "Android", "iPhone" })
                {
                    var ps = imp.GetPlatformTextureSettings(plat);
                    ps.overridden = true; ps.maxTextureSize = 128; ps.format = TextureImporterFormat.ASTC_6x6;
                    imp.SetPlatformTextureSettings(ps);
                }
            }
            imp.SaveAndReimport();
        }

        // ---------------- sprite atlas ----------------
        string mode = EditorSettings.spritePackerMode.ToString();
        var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(UiDir);
        string atlasPath;
        if (EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2 || EditorSettings.spritePackerMode == SpritePackerMode.SpriteAtlasV2Build)
        {
            atlasPath = "Assets/UI/DrillexUI.spriteatlasv2";
            var a = new SpriteAtlasAsset();
            a.Add(new[] { folder });
            SpriteAtlasAsset.Save(a, atlasPath);
            AssetDatabase.ImportAsset(atlasPath, ImportAssetOptions.ForceSynchronousImport);
            var imp = (SpriteAtlasImporter)AssetImporter.GetAtPath(atlasPath);
            imp.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4, enableAlphaDilation = true };
            imp.textureSettings = new SpriteAtlasTextureSettings { generateMipMaps = false, filterMode = FilterMode.Bilinear, sRGB = true, readable = false };
            foreach (var plat in new[] { "Android", "iPhone" })
            {
                var ps = imp.GetPlatformSettings(plat);
                ps.overridden = true; ps.maxTextureSize = 2048; ps.format = TextureImporterFormat.ASTC_5x5;
                imp.SetPlatformSettings(ps);
            }
            imp.SaveAndReimport();
        }
        else
        {
            atlasPath = "Assets/UI/DrillexUI.spriteatlas";
            var atlas = new SpriteAtlas();
            atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4, enableAlphaDilation = true });
            atlas.SetTextureSettings(new SpriteAtlasTextureSettings { generateMipMaps = false, filterMode = FilterMode.Bilinear, sRGB = true, readable = false });
            foreach (var plat in new[] { "Android", "iPhone" })
            {
                var ps = atlas.GetPlatformSettings(plat);
                ps.overridden = true; ps.maxTextureSize = 2048; ps.format = TextureImporterFormat.ASTC_5x5;
                atlas.SetPlatformSettings(ps);
            }
            atlas.Add(new[] { folder });

            AssetDatabase.CreateAsset(atlas, atlasPath);
        }
        AssetDatabase.SaveAssets();
        result.Log("wrote {0} textures; packer mode {1}; atlas {2}", written.Count, mode, atlasPath);
    }
}
