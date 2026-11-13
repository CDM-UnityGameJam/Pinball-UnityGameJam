using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Pinball.Editor
{
    // Bakes the flat, colour-block VFX sprites (flames, shards, bolts, rings, blobs, rocks) into
    // Art/Textures/VFX as PNGs. Shapes are hand-placed polygons in a unit square (y up); each layer is a solid
    // colour, a vertical gradient or a hard two-tone split, with an optional outline. Re-run after editing a
    // shape: Pinball > VFX > Bake Shape Textures. White shapes are meant to be tinted by the particle colour.
    public static class VfxShapeBaker
    {
        const string Folder = "Assets/_Project/Art/Textures/VFX";
        const int Size = 256, Samples = 4;

        enum Fill { Solid, Gradient, Split }

        sealed class Layer
        {
            public Vector2[] poly;
            public Color top, bottom;
            public Fill fill;
            public Color outline;
            public float outlineWidth;
            public Layer(Vector2[] poly, Color color) { this.poly = poly; top = bottom = color; }
        }

        static readonly Color White = Color.white;

        // Palettes taken from the card art: Fire (orange / pink / mint), Thunder (cyan / violet / deep blue),
        // Quake (yellow / orange / pink).
        static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }

        [MenuItem("Pinball/VFX/Bake Shape Textures")]
        public static void BakeAll()
        {
            Directory.CreateDirectory(Folder);
            Bake("Vfx_Flame", FlameArt());
            Bake("Vfx_FlameTongue", new[] { new Layer(FlameOutline, White) });
            Bake("Vfx_Shard", new[] { new Layer(P(0.10f, 0.12f, 0.90f, 0.38f, 0.32f, 0.92f), White) });
            Bake("Vfx_Bolt", BoltArt());
            Bake("Vfx_BoltWhite", new[] { new Layer(BoltOutline, White) });
            Bake("Vfx_Star", new[] { new Layer(P(0.5f, 1f, 0.6f, 0.6f, 1f, 0.5f, 0.6f, 0.4f, 0.5f, 0f, 0.4f, 0.4f, 0f, 0.5f, 0.4f, 0.6f), White) });
            Bake("Vfx_Ring", RingArt());
            Bake("Vfx_Blob", BlobArt());
            Bake("Vfx_Rock", RockArt());
            BakeGlow("Vfx_Glow");
            AssetDatabase.Refresh();
            foreach (var name in new[] { "Vfx_Flame", "Vfx_FlameTongue", "Vfx_Shard", "Vfx_Bolt", "Vfx_BoltWhite", "Vfx_Star", "Vfx_Ring", "Vfx_Blob", "Vfx_Rock", "Vfx_Glow" })
                ConfigureImporter($"{Folder}/{name}.png");
            AssetDatabase.Refresh();
            Debug.Log("Baked VFX shape textures into " + Folder);
        }

        static readonly Vector2[] FlameOutline = P(
            0.48f, 0.02f, 0.74f, 0.10f, 0.85f, 0.30f, 0.80f, 0.50f, 0.72f, 0.62f, 0.60f, 0.72f, 0.66f, 0.98f,
            0.46f, 0.80f, 0.33f, 0.70f, 0.40f, 0.60f, 0.25f, 0.52f, 0.17f, 0.40f, 0.15f, 0.24f, 0.26f, 0.09f);

        static Layer[] FlameArt()
        {
            var body = new Layer(FlameOutline, Hex("FFA36C")) { bottom = Hex("F27AD6"), fill = Fill.Gradient };
            var core = new Layer(P(0.50f, 0.10f, 0.66f, 0.18f, 0.70f, 0.36f, 0.62f, 0.52f, 0.56f, 0.45f, 0.50f, 0.56f, 0.44f, 0.44f, 0.36f, 0.40f, 0.32f, 0.26f, 0.38f, 0.16f), Hex("8AF7B5"));
            var ember1 = new Layer(P(0.18f, 0.62f, 0.26f, 0.72f, 0.28f, 0.58f), Hex("FF9A5C"));
            var ember2 = new Layer(P(0.80f, 0.70f, 0.88f, 0.78f, 0.78f, 0.80f), Hex("FFF26B"));
            var ember3 = new Layer(P(0.86f, 0.12f, 0.92f, 0.22f, 0.84f, 0.24f), Hex("F45BD0"));
            return new[] { body, core, ember1, ember2, ember3 };
        }

        static readonly Vector2[] BoltOutline = P(0.60f, 1.00f, 0.28f, 0.56f, 0.48f, 0.56f, 0.32f, 0.00f, 0.76f, 0.50f, 0.56f, 0.50f, 0.80f, 1.00f);

        static Layer[] BoltArt()
        {
            var body = new Layer(BoltOutline, Hex("B45CFF")) { outline = Hex("2B3BFF"), outlineWidth = 0.035f };
            var core = new Layer(P(0.63f, 0.92f, 0.40f, 0.60f, 0.55f, 0.60f, 0.42f, 0.14f, 0.66f, 0.47f, 0.52f, 0.47f, 0.71f, 0.92f), Hex("7DF9FF"));
            return new[] { body, core };
        }

        static Layer[] RingArt()
        {
            var layers = new List<Layer>();
            const int chunks = 6;
            for (int i = 0; i < chunks; i++)
            {
                float a0 = i * Mathf.PI * 2f / chunks + 0.06f, a1 = (i + 1) * Mathf.PI * 2f / chunks - 0.06f;
                var pts = new List<Vector2>();
                for (int k = 0; k <= 8; k++) pts.Add(Polar(Mathf.Lerp(a0, a1, k / 8f), 0.48f));
                for (int k = 8; k >= 0; k--) pts.Add(Polar(Mathf.Lerp(a0, a1, k / 8f), i % 2 == 0 ? 0.34f : 0.38f));
                layers.Add(new Layer(pts.ToArray(), White));
            }
            return layers.ToArray();
        }

        static Layer[] BlobArt()
        {
            var shell = new Layer(Circle(0.5f, 0.5f, 0.42f), Hex("FF5E8A")) { outline = Hex("3B4BFF"), outlineWidth = 0.06f };
            var core = new Layer(Circle(0.58f, 0.58f, 0.20f), Hex("FFE45C"));
            return new[] { shell, core };
        }

        static Layer[] RockArt()
        {
            var rock = new Layer(P(0.12f, 0.30f, 0.40f, 0.08f, 0.82f, 0.18f, 0.92f, 0.56f, 0.66f, 0.90f, 0.28f, 0.80f), Hex("FFD24A"))
            { bottom = Hex("FF8A3C"), fill = Fill.Split, outline = Hex("7A2E12"), outlineWidth = 0.045f };
            var chip = new Layer(P(0.56f, 0.62f, 0.72f, 0.56f, 0.64f, 0.74f), Hex("FF6FB5"));
            return new[] { rock, chip };
        }

        static Vector2 Polar(float angle, float r) => new Vector2(0.5f + Mathf.Cos(angle) * r, 0.5f + Mathf.Sin(angle) * r);

        static Vector2[] Circle(float cx, float cy, float r)
        {
            var pts = new Vector2[40];
            for (int i = 0; i < pts.Length; i++)
            {
                float a = i * Mathf.PI * 2f / pts.Length;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            return pts;
        }

        static Vector2[] P(params float[] xy)
        {
            var pts = new Vector2[xy.Length / 2];
            for (int i = 0; i < pts.Length; i++) pts[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
            return pts;
        }

        static void Bake(string name, Layer[] layers)
        {
            var pixels = new Color[Size * Size];
            float step = 1f / (Size * Samples);
            for (int py = 0; py < Size; py++)
            for (int px = 0; px < Size; px++)
            {
                Color sum = Color.clear;
                for (int sy = 0; sy < Samples; sy++)
                for (int sx = 0; sx < Samples; sx++)
                {
                    var p = new Vector2((px * Samples + sx + 0.5f) * step, (py * Samples + sy + 0.5f) * step);
                    var c = Color.clear;
                    foreach (var layer in layers) c = Over(Sample(layer, p), c);
                    // Accumulate premultiplied so edges blend cleanly.
                    sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                }
                sum /= Samples * Samples;
                pixels[py * Size + px] = sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            }
            Write(name, pixels);
        }

        static void BakeGlow(string name)
        {
            var pixels = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Vector2.Distance(new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size), new Vector2(0.5f, 0.5f)) / 0.5f;
                float a = Mathf.Clamp01(1f - d);
                pixels[y * Size + x] = new Color(1f, 1f, 1f, a * a);
            }
            Write(name, pixels);
        }

        static void Write(string name, Color[] pixels)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes($"{Folder}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static Color Sample(Layer layer, Vector2 p)
        {
            if (!Inside(layer.poly, p)) return Color.clear;
            if (layer.outlineWidth > 0f && EdgeDistance(layer.poly, p) < layer.outlineWidth) return layer.outline;
            switch (layer.fill)
            {
                case Fill.Gradient: return Color.Lerp(layer.bottom, layer.top, p.y);
                case Fill.Split: return p.y >= 0.5f ? layer.top : layer.bottom;
                default: return layer.top;
            }
        }

        static Color Over(Color src, Color dst)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0f) return Color.clear;
            var rgb = (new Vector3(src.r, src.g, src.b) * src.a + new Vector3(dst.r, dst.g, dst.b) * dst.a * (1f - src.a)) / a;
            return new Color(rgb.x, rgb.y, rgb.z, a);
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }

        static float EdgeDistance(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                var a = poly[j];
                var ab = poly[i] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        static void ConfigureImporter(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();
        }
    }
}
