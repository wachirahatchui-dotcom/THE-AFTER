using System.Collections.Generic;
using UnityEngine;

// Procedural sprite factory for the runtime-built UI.
//
// The project ships with no UI art at all, so every panel, border, glow and
// dust mote in the menu is a Texture2D generated here on first use and cached
// for the rest of the session. Keeping generation in one place means the whole
// menu can change its corner radius or grain density from a single call site.
public static class UIGfx
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    static Sprite Cached(string key, System.Func<Sprite> make)
    {
        if (cache.TryGetValue(key, out var s) && s != null) return s;
        s = make();
        cache[key] = s;
        return s;
    }

    static Sprite Wrap(Texture2D tex, float pixelsPerUnit, Vector4 border)
    {
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect, border);
    }

    // ------------------------------------------------------------------ solid
    public static Sprite Solid()
    {
        return Cached("solid", () =>
        {
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            t.SetPixels(px);
            return Wrap(t, 100f, Vector4.zero);
        });
    }

    // ------------------------------------------------------- rounded rectangle
    // 9-sliced so one texture stretches to any panel size without distorting the
    // corners. When outline > 0 the fill is punched out, leaving just the ring.
    public static Sprite RoundedRect(int radius, int outline)
    {
        string key = "round_" + radius + "_" + outline;
        return Cached(key, () =>
        {
            int size = radius * 2 + 8;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = EdgeDistance(x, y, size, radius);   // <0 inside, >0 outside
                float a = Mathf.Clamp01(0.5f - d);
                if (outline > 0) a *= Mathf.Clamp01(d + outline + 0.5f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            t.SetPixels(px);
            float b = radius + 2;
            return Wrap(t, 100f, new Vector4(b, b, b, b));
        });
    }

    // Signed distance from the rounded-rect edge for pixel (x,y).
    static float EdgeDistance(int x, int y, int size, int radius)
    {
        float cx = Mathf.Min(x + 0.5f, size - x - 0.5f);
        float cy = Mathf.Min(y + 0.5f, size - y - 0.5f);
        if (cx >= radius || cy >= radius) return -Mathf.Min(cx, cy);
        float dx = radius - cx, dy = radius - cy;
        return Mathf.Sqrt(dx * dx + dy * dy) - radius;
    }

    // --------------------------------------------------------------- gradients
    public static Sprite VerticalGradient(Color top, Color bottom, int height)
    {
        string key = "vgrad_" + top + bottom + height;
        return Cached(key, () =>
        {
            var t = new Texture2D(1, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
                t.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(height - 1)));
            return Wrap(t, 100f, Vector4.zero);
        });
    }

    // Transparent in the middle, `edge` at the corners. This is the vignette.
    public static Sprite RadialFalloff(Color center, Color edge, float power, int size)
    {
        string key = "radial_" + center + edge + power + size;
        return Cached(key, () =>
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f);
                px[y * size + x] = Color.Lerp(center, edge, Mathf.Pow(d, power));
            }
            t.SetPixels(px);
            return Wrap(t, 100f, Vector4.zero);
        });
    }

    // Soft round dot with a smooth alpha falloff - dust motes and button glow.
    public static Sprite SoftDot(int size, float power)
    {
        string key = "dot_" + size + "_" + power;
        return Cached(key, () =>
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size * 0.5f;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(1f - d, power));
            }
            t.SetPixels(px);
            return Wrap(t, 100f, Vector4.zero);
        });
    }

    // ------------------------------------------------------------------- grain
    // Tileable monochrome noise. MenuBackgroundFX cycles a few of these so the
    // grain shimmers like projected film instead of sitting as a static crust.
    public static Sprite Grain(int seed, int size, float strength)
    {
        string key = "grain_" + seed + "_" + size;
        return Cached(key, () =>
        {
            var rnd = new System.Random(seed);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color(1f, 1f, 1f, (float)rnd.NextDouble() * strength);
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Point;
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        });
    }

    // -------------------------------------------------------------- paper grit
    // Faint mottling so large parchment fills do not read as flat plastic.
    public static Sprite PaperFibre(int size)
    {
        return Cached("fibre_" + size, () =>
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.55f
                        + Mathf.PerlinNoise(x * 0.31f, y * 0.31f) * 0.30f
                        + Mathf.PerlinNoise(x * 0.90f, y * 0.90f) * 0.15f;
                px[y * size + x] = new Color(0f, 0f, 0f, Mathf.Clamp01(n - 0.45f) * 0.30f);
            }
            t.SetPixels(px);
            t.wrapMode = TextureWrapMode.Repeat;
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        });
    }

    public static void ClearCache() { cache.Clear(); }
}
