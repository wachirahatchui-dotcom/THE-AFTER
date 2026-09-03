using UnityEngine;

// The game's two UI typefaces.
//
//   Get()      the display face - titles, labels, buttons, headings
//   GetBody()  a plain face for small text - descriptions, values, hints
//
// The split exists because NewTegomin is a decorative serif. It carries the
// menu beautifully at 26pt and above, but small text is rasterised at
// fontSize x canvasScaleFactor, so a 16pt description on a 1100px-wide window
// lands at roughly 8 real pixels - and at that size its serifs and thin
// strokes turn to mush. Body text uses Unity's built-in sans instead, which
// stays legible right down to a few pixels.
//
// To change either face, drop a .ttf into Assets/Resources/Fonts/ and point
// the matching constant at it.
public static class GameFont
{
    const string DisplayFontPath = "Fonts/NewTegomin-Regular";

    // Leave empty to use Unity's built-in sans for body text.
    const string BodyFontPath = "";

    static Font display;
    static Font body;

    public static Font Get()
    {
        if (display != null) return display;

        display = Resources.Load<Font>(DisplayFontPath);
        if (display == null) display = BuiltIn();
        return display;
    }

    // OS faces tried, in order, when no body font is supplied. Unity's built-in
    // LegacyRuntime.ttf is Liberation Sans, which carries no Thai glyphs at all
    // - any Thai text in the UI would render as a row of boxes. These all ship
    // with Windows and cover Thai as well as Latin.
    static readonly string[] OSFallbacks = { "Leelawadee UI", "Tahoma", "Arial Unicode MS", "Segoe UI" };

    public static Font GetBody()
    {
        if (body != null) return body;

        if (!string.IsNullOrEmpty(BodyFontPath))
            body = Resources.Load<Font>(BodyFontPath);

        if (body == null) body = Font.CreateDynamicFontFromOSFont(OSFallbacks, 16);
        if (body == null) body = BuiltIn();
        return body;
    }

    static Font BuiltIn()
    {
        var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }
}
