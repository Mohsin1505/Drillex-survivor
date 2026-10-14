using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
// Contact sheet of the generated UI kit over a desert-ish background, for review.
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;
        var files = Directory.GetFiles("Assets/UI/Sprites", "*.png", SearchOption.AllDirectories).Concat(Directory.GetFiles("Assets/Art/VFX/Textures", "*.png")).OrderBy(f => f).ToArray();
        int cell = 140, cols = 10, rows = (files.Length + cols - 1) / cols;
        var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
        var bg = new Color[sheet.width * sheet.height];
        for (int y = 0; y < sheet.height; y++) for (int x = 0; x < sheet.width; x++) bg[y * sheet.width + x] = ((x / cell + y / cell) % 2 == 0) ? new Color(0.62f, 0.33f, 0.18f) : new Color(0.12f, 0.12f, 0.13f);
        sheet.SetPixels(bg);
        for (int i = 0; i < files.Length; i++)
        {
            var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(files[i]));
            int cx = (i % cols) * cell, cy = (rows - 1 - i / cols) * cell;
            float s = Mathf.Min(1f, 128f / Mathf.Max(t.width, t.height));
            int w = Mathf.RoundToInt(t.width * s), h = Mathf.RoundToInt(t.height * s);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                var c = t.GetPixelBilinear((x + 0.5f) / w, (y + 0.5f) / h);
                int px = cx + 6 + x, py = cy + 6 + y;
                var d = sheet.GetPixel(px, py);
                sheet.SetPixel(px, py, Color.Lerp(d, new Color(c.r, c.g, c.b, 1), c.a));
            }
            Object.DestroyImmediate(t);
        }
        sheet.Apply();
        File.WriteAllBytes(@"D:\Drillex\SourceArt\UI\_preview\contact.png", sheet.EncodeToPNG());
        result.Log(string.Join(",", files.Select(Path.GetFileNameWithoutExtension)));
    }
}
