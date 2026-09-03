using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The objective tracker in the top-left corner of the screen.
//
// Built in code and reached through a lazy singleton, the same way ScreenFader
// and TutorialPrompt are, so nothing has to be wired in the Inspector and it
// survives whatever the scene does around it.
//
// Two rows and nothing else:
//
//     * Main Objectives
//     [ ] Go to the campfire and talk with Logan
//
// A marked heading over a ticked list, on a plain dark bar. Deliberately plain -
// this is read at a glance in the corner of the eye while the player is walking,
// and every rule, border and flourish added to it is one more thing between them
// and the sentence that tells them what to do.
//
// The tick box is drawn rather than typed. A box-drawing character is the
// obvious way to write one, but the display face is a decorative serif with no
// such glyph, so it would come out as a missing-character rectangle on some
// machines and as nothing at all on others. Images look the same everywhere.
//
// Colours and sizes come from MenuTheme, so the tracker belongs to the same game
// as the menus rather than being styled here.
//
// Wording is English on purpose: it is text the player reads inside the game,
// and the rest of the game's on-screen text is English too.
public class QuestUI : MonoBehaviour
{
    static QuestUI instance;

    CanvasGroup group;
    RectTransform panel;
    Image slab;
    Image bullet;
    Image box;
    RectTransform tick;
    Image tickShort, tickLong;
    Text heading;
    Text objective;

    float target;
    Coroutine clearing;

    [Tooltip("How quickly the panel fades in and out.")]
    public float fadeSpeed = 3.5f;

    [Tooltip("How far it slides in from, in pixels. Movement is what makes a new objective catch the eye.")]
    public float slideFrom = 22f;

    const float Left = 48f;
    const float Top = -42f;
    const float PanelWidth = 560f;
    const float PanelHeight = 92f;

    // Left edge of the text column, past the bullet and the tick box.
    const float TextX = 34f;

    public static QuestUI I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~QuestUI");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<QuestUI>();
                instance.Build();
            }
            return instance;
        }
    }

    static MenuThemeAsset T => MenuTheme.Current;

    Color Accent => T != null ? T.accentSoft : new Color(0.729f, 0.435f, 0.271f);
    Color Head   => T != null ? T.parchmentLight : new Color(0.871f, 0.792f, 0.616f);
    Color Body   => new Color(0.90f, 0.89f, 0.86f, 0.92f);
    Color Done   => T != null ? T.positiveFill : new Color(0.639f, 0.757f, 0.573f);
    Color Fill   => T != null ? T.questPanelFill : new Color(0.055f, 0.063f, 0.075f, 0.80f);

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Below the fader, so a fade to black covers the tracker too - an
        // objective left burning over a black screen reads as a bug.
        canvas.sortingOrder = 30900;

        // Snapped to whole pixels, the same fix that keeps the captions sharp.
        canvas.pixelPerfect = true;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = T != null ? T.referenceResolution : new Vector2(1920f, 1080f);

        // Sized against height alone. A tracker pinned to the corner should be
        // the same size whatever shape the window is; matching width as well
        // shrinks it on an ultrawide for no reason.
        scaler.matchWidthOrHeight = 1f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(Left, Top);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        // A plain dark bar, barely rounded. No border: an outlined panel reads as
        // a window that wants attention, and this is a note in the corner.
        slab = Plate("Slab", panel, UIGfx.RoundedRect(4, 0), Fill, Image.Type.Sliced);
        var sr = slab.rectTransform;
        sr.anchorMin = Vector2.zero;
        sr.anchorMax = Vector2.one;
        sr.offsetMin = new Vector2(-14f, -8f);
        sr.offsetMax = new Vector2(14f, 8f);

        BuildHeadingRow();
        BuildTaskRow();
    }

    void BuildHeadingRow()
    {
        // The marker beside the heading. A filled disc rather than a star: the
        // display face has no star to type and a hand-built one at twelve pixels
        // is a smudge, while a disc is unmistakably deliberate at any size.
        bullet = Plate("Bullet", panel, UIGfx.RoundedRect(24, 0), Accent, Image.Type.Simple);
        var br = bullet.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 1f);
        br.sizeDelta = new Vector2(13f, 13f);
        br.anchoredPosition = new Vector2(8f, -9f);

        heading = Line("Heading", T != null ? T.questHeadingFontSize : 22,
                       new Vector2(TextX, -2f), 30f, Head);
    }

    void BuildTaskRow()
    {
        float side = T != null ? T.questBoxSize : 20f;

        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(panel, false);
        box = boxGo.GetComponent<Image>();
        box.sprite = UIGfx.RoundedRect(3, 2);
        box.type = Image.Type.Sliced;
        box.color = Body;
        box.raycastTarget = false;

        var br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 1f);
        br.sizeDelta = new Vector2(side, side);
        br.anchoredPosition = new Vector2(6f, -42f);

        // The tick: a short bar down-right and a long bar up-right, drawn rather
        // than typed so it cannot come out as a missing glyph.
        var tickGo = new GameObject("Tick", typeof(RectTransform));
        tickGo.transform.SetParent(box.transform, false);
        tick = tickGo.GetComponent<RectTransform>();
        tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 0.5f);
        tick.pivot = new Vector2(0.5f, 0.5f);
        tick.sizeDelta = new Vector2(side, side);
        tick.anchoredPosition = Vector2.zero;
        tick.localScale = Vector3.zero;

        tickShort = Plate("Short", tick, UIGfx.Solid(), Done, Image.Type.Simple);
        var shr = tickShort.rectTransform;
        shr.anchorMin = shr.anchorMax = new Vector2(0.5f, 0.5f);
        shr.pivot = new Vector2(0.5f, 0f);
        shr.sizeDelta = new Vector2(2.5f, side * 0.34f);
        shr.anchoredPosition = new Vector2(-side * 0.15f, -side * 0.20f);
        shr.localRotation = Quaternion.Euler(0f, 0f, 42f);

        tickLong = Plate("Long", tick, UIGfx.Solid(), Done, Image.Type.Simple);
        var lgr = tickLong.rectTransform;
        lgr.anchorMin = lgr.anchorMax = new Vector2(0.5f, 0.5f);
        lgr.pivot = new Vector2(0.5f, 0f);
        lgr.sizeDelta = new Vector2(2.5f, side * 0.60f);
        lgr.anchoredPosition = new Vector2(-side * 0.15f, -side * 0.20f);
        lgr.localRotation = Quaternion.Euler(0f, 0f, -36f);

        objective = Line("Objective", T != null ? T.questObjectiveFontSize : 26,
                         new Vector2(TextX, -38f), 60f, Body);
    }

    static Image Plate(string name, Transform parent, Sprite sprite, Color colour, Image.Type type)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = type;
        img.color = colour;
        img.raycastTarget = false;
        return img;
    }

    Text Line(string name, int size, Vector2 offset, float height, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Shadow), typeof(Text));
        go.transform.SetParent(panel, false);

        var text = go.GetComponent<Text>();

        // The game's own display face, the one the menus, captions and hints are
        // set in. Body text elsewhere uses a plain system sans because it is set
        // small enough that a decorative serif turns to mush; the tracker is set
        // above that size, so that reason does not apply here.
        text.font = GameFont.Get();
        text.fontSize = size;
        text.alignment = TextAnchor.UpperLeft;
        text.color = colour;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.05f;

        // A soft drop shadow rather than a full outline. The bar behind the text
        // is already doing the work of separating it from the scene; an outline
        // on top of that thickens every letter for no gain.
        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
        shadow.useGraphicAlpha = true;

        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(offset.x, -height);
        rt.offsetMax = new Vector2(-10f, 0f);
        rt.anchoredPosition = offset;
        return text;
    }

    /// Puts an objective up. `headingText` is the small line above it.
    public void Show(string objectiveText, string headingText = "Main Objectives")
    {
        if (clearing != null) { StopCoroutine(clearing); clearing = null; }

        heading.text = headingText;
        heading.color = Head;
        objective.text = objectiveText;
        objective.color = Body;

        bullet.color = Accent;

        // Back to an empty box, in case this objective follows a completed one.
        box.color = Body;
        tick.localScale = Vector3.zero;

        // Slide in from the edge it lives on.
        panel.anchoredPosition = new Vector2(Left - slideFrom, Top);
        target = 1f;
    }

    /// Marks it done and takes it away after a beat, so the player sees that it
    /// was the thing they just did rather than the panel simply vanishing.
    public void Complete(float holdSeconds = 1.8f)
    {
        if (!gameObject.activeInHierarchy) return;
        if (clearing != null) StopCoroutine(clearing);
        clearing = StartCoroutine(CompleteRoutine(holdSeconds));
    }

    IEnumerator CompleteRoutine(float hold)
    {
        bullet.color = Done;
        box.color = Done;
        tickShort.color = Done;
        tickLong.color = Done;
        objective.color = new Color(Body.r, Body.g, Body.b, 0.55f);

        // The tick springs in past full size and settles back. It is the one
        // piece of movement in the panel, and it is what reads as "closed"
        // rather than merely "faded".
        float t = 0f;
        const float pop = 0.3f;
        while (t < pop)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / pop);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            float overshoot = 1f + 0.25f * Mathf.Sin(k * Mathf.PI) * (1f - k);
            tick.localScale = Vector3.one * (eased * overshoot);
            yield return null;
        }
        tick.localScale = Vector3.one;

        float held = 0f;
        while (held < hold) { held += Time.unscaledDeltaTime; yield return null; }

        target = 0f;
        clearing = null;
    }

    public void Clear()
    {
        if (clearing != null) { StopCoroutine(clearing); clearing = null; }
        target = 0f;
    }

    void Update()
    {
        if (group == null) return;

        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * dt);

        // Settles into place as it fades up, and does not slide back out on the
        // way down - a panel that retreats as it fades draws the eye to itself
        // just as it stops mattering.
        if (target > 0f)
        {
            Vector2 want = new Vector2(Left, Top);
            panel.anchoredPosition = Vector2.Lerp(panel.anchoredPosition, want,
                                                  1f - Mathf.Exp(-12f * dt));
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
