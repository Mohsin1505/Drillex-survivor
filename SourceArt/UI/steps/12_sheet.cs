using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        var files = Directory.GetFiles(@"D:\Drillex\SourceArt\UI\_preview", "g_*.png").OrderBy(f => f).ToArray();
        int per = 16, cw = 400, ch = 225, cols = 4;
        for (int s = 0; s * per < files.Length; s++)
        {
            var part = files.Skip(s * per).Take(per).ToArray();
            int rows = (part.Length + cols - 1) / cols;
            var sheet = new Texture2D(cols * cw, rows * ch, TextureFormat.RGB24, false);
            for (int i = 0; i < part.Length; i++)
            {
                var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(part[i]));
                int cx = (i % cols) * cw, cy = (rows - 1 - i / cols) * ch;
                for (int y = 0; y < ch; y++) for (int x = 0; x < cw; x++) sheet.SetPixel(cx + x, cy + y, t.GetPixelBilinear((x + 0.5f) / cw, (y + 0.5f) / ch));
                Object.DestroyImmediate(t);
            }
            sheet.Apply();
            File.WriteAllBytes(@"D:\Drillex\SourceArt\UI\_preview\sheet_" + s + ".png", sheet.EncodeToPNG());
            result.Log("sheet {0}: {1}", s, string.Join(" | ", part.Select(Path.GetFileNameWithoutExtension)));
        }
    }
}
