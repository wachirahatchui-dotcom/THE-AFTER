using UnityEngine;
using UnityEngine.UI;

// Shared "worn parchment" visual language for every runtime-built UI in the
// game (main menu, dialogue box, pause menu).
//
// The colours are thin accessors over MenuTheme.Current, so the palette lives
// in one editable asset (Assets/Resources/MenuTheme.asset) rather than being
// duplicated here. Call sites are unchanged: GameUITheme.Ink still works.
public static class GameUITheme
{
    public static Color Parchment      { get { return MenuTheme.Current.parchment; } }
    public static Color ParchmentLight { get { return MenuTheme.Current.parchmentLight; } }
    public static Color ParchmentDeep  { get { return MenuTheme.Current.parchmentDeep; } }
    public static Color Ink            { get { return MenuTheme.Current.ink; } }
    public static Color InkSoft        { get { return MenuTheme.Current.inkSoft; } }
    public static Color Danger         { get { return MenuTheme.Current.danger; } }
    public static Color DangerFill     { get { return MenuTheme.Current.dangerFill; } }
    public static Color Positive       { get { return MenuTheme.Current.positive; } }
    public static Color PositiveFill   { get { return MenuTheme.Current.positiveFill; } }
    public static Color CloseBadge     { get { return MenuTheme.Current.closeBadge; } }
    public static Color CloseBadgeText { get { return MenuTheme.Current.closeBadgeText; } }
    public static Color Dim            { get { return MenuTheme.Current.dimColor; } }
    public static Color NightBG        { get { return MenuTheme.Current.nightBG; } }

    public static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static Text NewText(Font font, string name, Transform parent, string content, int size,
                                FontStyle style, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // Thin inset frame on each side of an Image — a sketchy ink outline without
    // needing a sliced sprite asset.
    public static void AddBorder(Image target, Color color, float thickness)
    {
        AddEdge(target.transform, color, thickness, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -thickness), Vector2.zero);
        AddEdge(target.transform, color, thickness, new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, thickness));
        AddEdge(target.transform, color, thickness, new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(thickness, 0));
        AddEdge(target.transform, color, thickness, new Vector2(1, 0), new Vector2(1, 1), new Vector2(-thickness, 0), Vector2.zero);
    }

    static void AddEdge(Transform parent, Color color, float thickness,
                         Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
    }

    // Standard parchment button: filled Image + Button + ink border + label.
    // Returns the Button so callers can still tweak its RectTransform/colors.
    public static Button NewButton(Font font, string label, Transform parent,
                                    Color fill, Color border, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage("Btn_" + label, parent, fill);
        AddBorder(img, border, 3);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white; // Image already carries the fill color
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var txt = NewText(font, "Label", img.transform, label, 32, FontStyle.Normal,
            TextAnchor.MiddleCenter, border);
        Stretch(txt.rectTransform);
        return btn;
    }
}
