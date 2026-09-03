using UnityEngine;

// Loads the main-menu logo, if there is one.
//
// Drop an image at Assets/Resources/Logo/TheAfterLogo.png and the menu uses it
// instead of the typed "THE AFTER" title. With no file there, nothing changes -
// the menu falls back to the text, so the game never depends on the art
// existing.
//
// The awkward part is that logo art is normally drawn as black lines on white,
// and the menu backdrop is nearly black. Rather than making that the artist's
// problem, a white-background image is converted here into a mask: how dark a
// pixel was becomes how opaque it is, and the colour comes from the theme. So
// the same file works whether it was exported flat on white or with
// transparency already cut.
public static class MenuLogo
{
    public const string ResourcePath = "Logo/TheAfterLogo";

    static Sprite sprite;
    static bool resolved;
    static bool isMask;      // true when the art was converted from a flat background

    // Whether the loaded sprite is a white mask that should be tinted by the
    // theme, rather than finished art that should be drawn as-is.
    public static bool IsTintable { get { Resolve(); return isMask; } }

    public static bool Exists { get { Resolve(); return sprite != null; } }

    public static Sprite Get()
    {
        Resolve();
        return sprite;
    }

    // Forces the next Get to load again. Useful straight after dropping the
    // file in, without restarting play mode.
    public static void Reset()
    {
        resolved = false;
        sprite = null;
        isMask = false;
    }

    static void Resolve()
    {
        if (resolved) return;
        resolved = true;

        var texture = Resources.Load<Texture2D>(ResourcePath);
        if (texture == null)
        {
            // Imported as a Sprite rather than a plain texture: use it as it is.
            sprite = Resources.Load<Sprite>(ResourcePath);
            isMask = false;
            return;
        }

        var converted = TryBuildMask(texture);
        if (converted != null)
        {
            sprite = converted;
            isMask = true;
            return;
        }

        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                               new Vector2(0.5f, 0.5f), 100f);
        isMask = false;
    }

    // Returns a white sprite whose alpha is the darkness of the source, or null
    // when the source already has transparency (in which case it is finished
    // art and must not be touched) or cannot be read.
    static Sprite TryBuildMask(Texture2D source)
    {
        Color[] pixels;
        try
        {
            pixels = source.GetPixels();
        }
        catch
        {
            // Read/Write is off on the importer. LogoImportSettings turns it on
            // for anything under Resources/Logo, so this only happens for a file
            // that was placed somewhere unexpected.
            Debug.LogWarning("[MenuLogo] " + ResourcePath + " is not readable; using it as-is. " +
                             "Tick Read/Write on the texture importer to enable theme tinting.");
            return null;
        }

        if (pixels.Length == 0) return null;

        // A transparent corner means the artist already cut the background out.
        int w = source.width, h = source.height;
        if (pixels[0].a < 0.9f || pixels[w - 1].a < 0.9f ||
            pixels[(h - 1) * w].a < 0.9f || pixels[h * w - 1].a < 0.9f)
            return null;

        // A dark corner means the art is light-on-dark already.
        float cornerLuma = Luma(pixels[0]);
        if (cornerLuma < 0.5f) return null;

        var masked = new Color32[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
        {
            float dark = 1f - Luma(pixels[i]);
            masked[i] = new Color32(255, 255, 255, (byte)Mathf.Clamp(dark * 255f, 0f, 255f));
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "TheAfterLogo (mask)"
        };
        tex.SetPixels32(masked);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    static float Luma(Color c)
    {
        return 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
    }
}
