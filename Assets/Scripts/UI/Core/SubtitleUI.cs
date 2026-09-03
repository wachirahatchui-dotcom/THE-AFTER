using UnityEngine;
using UnityEngine.UI;

// The caption line along the bottom of the screen during a cutscene.
//
// Built in code and reached through a lazy singleton, like ScreenFader and the
// rest, so nothing has to be wired in the Inspector.
//
// It sits under the screen fader on purpose: a scene that fades to black should
// take its captions with it. It sits above the tutorial prompt because a line of
// dialogue matters more than a reminder of which key walks.
//
// Honours the Subtitles setting the game already has - somebody who turned
// captions off meant it, including here.
public class SubtitleUI : MonoBehaviour
{
    static SubtitleUI instance;

    CanvasGroup group;
    Text label;
    Outline edge;
    float target;

    [Tooltip("How quickly a line fades in and out. Fast, because a caption that eases in is a caption you start reading late.")]
    public float fadeSpeed = 8f;

    public static SubtitleUI I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~SubtitleUI");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SubtitleUI>();
                instance.Build();
            }
            return instance;
        }
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above the cutscene stills (31700) and below the fader (32000). Captions
        // over the pictures they are captioning, and still taken away by a fade to
        // black. sortingOrder is a signed 16-bit value, so everything here has to
        // stay under 32767.
        canvas.sortingOrder = 31900;

        var theme = MenuTheme.Current;

        // Snapped to whole pixels. A dynamic font rasterised across half a pixel
        // is what makes text look soft, and it is the one setting that fixes it
        // for a screen-space overlay.
        canvas.pixelPerfect = true;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme != null ? theme.referenceResolution : new Vector2(1920f, 1080f);

        // Sized against the screen's height rather than a blend of both axes.
        //
        // Captions have to stay the same size relative to the picture whatever the
        // window shape is. Matching width as well shrinks them on an ultrawide and
        // blows them up on a narrow window - the reading size should not depend on
        // how wide somebody's monitor happens to be.
        scaler.matchWidthOrHeight = 1f;

        // Below this the font is rasterised too small to stay sharp on a small
        // window; above it, nothing is gained.
        scaler.referencePixelsPerUnit = 100f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var go = new GameObject("Line", typeof(RectTransform), typeof(Outline), typeof(Text));
        go.transform.SetParent(transform, false);

        label = go.GetComponent<Text>();

        // The game's own face, the one the menus and headings are set in.
        //
        // Body text elsewhere uses a plain system sans because it is set small -
        // a decorative serif at eight real pixels turns to mush. Captions are not
        // small, so that reason does not apply here, and a caption in a different
        // typeface from everything else looks like it came from another game.
        label.font = GameFont.Get();

        // captionFontSize, not subtitleFontSize - that one is the tagline under the
        // game's title on the main menu, and captions have no business being tied
        // to the size of a piece of menu dressing.
        label.fontSize = theme != null ? theme.captionFontSize : 44;
        // White, not the theme's parchment.
        //
        // The menus can afford a tinted cream because they sit on their own paper
        // background. A caption lands on whatever the shot happens to be - a lamp,
        // a fire, a pale sky in a drawing - and anything short of white starts
        // disappearing into the bright half of those.
        label.color = Color.white;
        label.alignment = TextAnchor.LowerCenter;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.lineSpacing = 1.15f;

        // An outline rather than a drop shadow: captions land on whatever the shot
        // happens to be, and a bedroom lamp behind the text is bright on one side
        // of a letter and dark on the other.
        // Solid black, and thick enough to hold. Outline draws the text four times
        // at the four diagonals of effectDistance, so this is a genuine border
        // around every letter rather than a shadow on one side of it.
        edge = go.GetComponent<Outline>();
        edge.effectColor = Color.black;

        // Proportional to the type, not a fixed number of pixels. Change the size
        // in the theme and the border stays the same weight against the letters
        // instead of turning into a hairline or a blob.
        float ring = Mathf.Max(1.5f, label.fontSize * 0.08f);
        edge.effectDistance = new Vector2(ring, -ring);
        edge.useGraphicAlpha = true;

        // A wide band across the bottom, held clear of the very edge so it is not
        // cut off on a screen that overscans.
        var rt = label.rectTransform;
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        // Room for two lines at the larger size without the second one falling off
        // the bottom of the band.
        rt.sizeDelta = new Vector2(1500f, 260f);
        rt.anchoredPosition = new Vector2(0f, 80f);
    }

    /// Puts a line up. Empty or null takes it away.
    public void Show(string text)
    {
        if (!GameSettings.Subtitles) { Clear(); return; }

        if (string.IsNullOrEmpty(text)) { Clear(); return; }

        // Only touch the text when it actually changes, so a line held across
        // several frames does not restart its fade every frame.
        if (label.text != text) label.text = text;
        target = 1f;
    }

    public void Clear() => target = 0f;

    void Update()
    {
        if (group == null) return;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
