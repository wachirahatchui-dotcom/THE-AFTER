using System;
using System.Collections.Generic;
using UnityEngine;

// Procedural item icons.
//
// The project ships with no item art, and UIGfx only knows about panels and
// glows, so each ItemShape is painted here as a white glyph on transparency
// and tinted by the slot that draws it. One texture per shape, cached for the
// session.
//
// Every glyph is a function from a point in -1..1 to "is this inside the
// shape", sampled 3x3 per pixel for a soft edge. Adding a shape is one entry
// in the switch and one function - no texture, no import settings, no atlas.
public static class ItemIcon
{
    const int Size = 96;
    const int Samples = 3;          // per axis, so 9 coverage samples per pixel

    static readonly Dictionary<ItemShape, Sprite> cache = new Dictionary<ItemShape, Sprite>();

    public static Sprite Get(ItemShape shape)
    {
        if (cache.TryGetValue(shape, out var cached) && cached != null) return cached;

        var sprite = Paint(shape);
        cache[shape] = sprite;
        return sprite;
    }

    public static void ClearCache() { cache.Clear(); }

    // ------------------------------------------------------------------ paint
    static Sprite Paint(ItemShape shape)
    {
        Func<float, float, bool> inside = Glyph(shape);

        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "icon_" + shape
        };

        var pixels = new Color32[Size * Size];

        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                int hits = 0;
                for (int sy = 0; sy < Samples; sy++)
                {
                    for (int sx = 0; sx < Samples; sx++)
                    {
                        float u = (px + (sx + 0.5f) / Samples) / Size;
                        float v = (py + (sy + 0.5f) / Samples) / Size;

                        // -1..1, y up
                        if (inside(u * 2f - 1f, v * 2f - 1f)) hits++;
                    }
                }

                byte a = (byte)(255f * hits / (Samples * Samples));
                pixels[py * Size + px] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Func<float, float, bool> Glyph(ItemShape shape)
    {
        switch (shape)
        {
            case ItemShape.Can:
                // Body with a lid band sitting proud of it.
                return (x, y) => Box(x, y, 0.42f, 0.60f)
                              || Box(x, y - 0.66f, 0.50f, 0.09f);

            case ItemShape.Bottle:
                return (x, y) => Box(x, y + 0.22f, 0.36f, 0.48f)
                              || Box(x, y - 0.42f, 0.14f, 0.22f)
                              || Box(x, y - 0.70f, 0.19f, 0.09f);

            case ItemShape.Bandage:
                // A strip on the diagonal with a thicker pad across its middle.
                return (x, y) =>
                {
                    float a = (x + y) * 0.7071f;
                    float b = (x - y) * 0.7071f;
                    return (Mathf.Abs(a) < 0.20f && Mathf.Abs(b) < 0.78f)
                        || (Mathf.Abs(a) < 0.34f && Mathf.Abs(b) < 0.26f);
                };

            case ItemShape.Wrench:
                // Shaft plus an open ring - the gap at the top is what makes it
                // read as a spanner rather than a lollipop.
                return (x, y) =>
                {
                    bool shaft = Box(x, y + 0.28f, 0.11f, 0.50f);
                    bool ring = Ring(x, y - 0.42f, 0.30f, 0.11f);
                    bool jaw = Box(x, y - 0.66f, 0.13f, 0.16f);
                    return shaft || (ring && !jaw);
                };

            case ItemShape.Battery:
                return (x, y) => Box(x, y + 0.04f, 0.32f, 0.60f)
                              || Box(x, y - 0.70f, 0.12f, 0.10f);

            case ItemShape.Scrap:
                // Deliberately irregular: a torn plate, not a shape.
                return (x, y) => Polygon(x, y, ScrapOutline);

            case ItemShape.Cloth:
                // Straight top, rag-cut bottom.
                return (x, y) => Mathf.Abs(x) < 0.58f
                              && y < 0.52f
                              && y > -0.44f + 0.14f * Mathf.Sin(x * 9f);

            case ItemShape.Key:
                return (x, y) =>
                {
                    bool bow = Ring(x, y - 0.46f, 0.26f, 0.10f);
                    bool shaft = Box(x, y + 0.24f, 0.08f, 0.50f);
                    bool tooth1 = Box(x - 0.19f, y + 0.50f, 0.13f, 0.07f);
                    bool tooth2 = Box(x - 0.16f, y + 0.70f, 0.10f, 0.07f);
                    return bow || shaft || tooth1 || tooth2;
                };

            case ItemShape.Note:
                // Paper with ruled lines cut out of it.
                return (x, y) =>
                {
                    if (!Box(x, y, 0.46f, 0.60f)) return false;

                    for (int i = -1; i <= 1; i++)
                        if (Box(x + 0.06f, y - i * 0.26f, 0.28f, 0.035f)) return false;

                    return true;
                };

            default:
                return (x, y) => Box(x, y, 0.45f, 0.45f);
        }
    }

    // ------------------------------------------------------------------ maths
    static bool Box(float x, float y, float halfW, float halfH)
    {
        return Mathf.Abs(x) <= halfW && Mathf.Abs(y) <= halfH;
    }

    static bool Ring(float x, float y, float radius, float thickness)
    {
        float d = Mathf.Sqrt(x * x + y * y);
        return d <= radius && d >= radius - thickness;
    }

    static readonly Vector2[] ScrapOutline =
    {
        new Vector2(-0.62f,  0.18f),
        new Vector2(-0.30f,  0.66f),
        new Vector2( 0.22f,  0.52f),
        new Vector2( 0.66f,  0.10f),
        new Vector2( 0.38f, -0.58f),
        new Vector2(-0.18f, -0.34f),
        new Vector2(-0.48f, -0.62f),
    };

    // Standard crossing-number test.
    static bool Polygon(float x, float y, Vector2[] points)
    {
        bool inside = false;

        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            if ((points[i].y > y) == (points[j].y > y)) continue;

            float t = (y - points[i].y) / (points[j].y - points[i].y);
            if (x < points[i].x + t * (points[j].x - points[i].x)) inside = !inside;
        }
        return inside;
    }
}
