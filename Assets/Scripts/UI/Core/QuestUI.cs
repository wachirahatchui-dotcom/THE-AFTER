using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The objective tracker in the top-left corner of the screen.
//
// Built in code and reached through a lazy singleton, the same way ScreenFader
// and TutorialPrompt are, so nothing has to be wired in the Inspector and it
// survives whatever the scene does around it.
//
// Colours and sizes come from MenuTheme rather than being picked here, so the
// tracker belongs to the same game as the menus instead of looking like a debug
// overlay bolted onto the corner.
//
// The tick box is drawn rather than typed. A box-drawing character is the
// obvious way to write one, but the display face is a decorative serif with no
// such glyph, so the box would come out as a missing-character rectangle on some
// machines and as nothing at all on others. Two Images and a pair of rotated
// bars look the same everywhere.
//
// Wording is English on purpose: it is text the player reads inside the game,
// and the rest of the game's on-screen text is English too.
public class QuestUI : MonoBehaviour
{
    static QuestUI instance;

    CanvasGroup group;
    RectTransform panel;
    Image slab;
    Image rule;
    Image box;
    Image boxFill;
    RectTransform tick;
    Image tickShort, tickLong;
    Text heading;
    Text objective;

    float target;
    Coroutine clearing;

    [Tooltip("How quickly the panel fades in and out.")]
    public float fadeSpeed = 3.5f;

    [Tooltip("How far it slides in from, in pixels. Movement is what makes a new objective catch the eye.")]
    public float slideFrom = 26f;

    const float Left = 52f;
    const float Top = -46f;
    const float PanelWidth = 600f;
    const float PanelHeight = 132f;

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
    Color Body   => T != null ? T.parchmentLight : new Color(0.871f, 0.792f, 0.616f);
    Color Done   => T != null ? T.positiveFill : new Color(0.639f, 0.757f, 0.573f);
    Color Fill   => T != null ? T.questPanelFill : new Color(0.071f, 0.086f, 0.118f, 0.74f);
    Color Border => T != null ? T.questPanelBorder : new Color(0.756f, 0.667f, 0.475f, 0.42f);

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

        int radius = T != null ? T.cornerRadius : 14;

        // An ink slab, the same language the dialogue box uses to sit over live
        // gameplay: dark enough to read against a fire, open enough to see the
        // camp through.
        slab = Plate("Slab", panel, UIGfx.RoundedRect(radius, 0), Fill);
        Stretch(slab.rectTransform, new Vector4(-10f, -8f, 10f, 8f));

        var edge = Plate("Edge", panel, UIGfx.RoundedRect(radius, 2), Border);
        Stretch(edge.rectTransform, new Vector4(-10f, -8f, 10f, 8f));

        // A rule down the left edge. It carries the state - warm while the
        // objective is open, green once it is done - which is legible at a glance
        // from the corner of the eye in a way that a word never is.
        rule = Plate("Rule", panel, UIGfx.Solid(), Accent);
        var rr = rule.rectTransform;
        rr.anchorMin = new Vector2(0f, 0f);
        rr.anchorMax = new Vector2(0f, 1f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.sizeDelta = new Vector2(3f, -22f);
        rr.anchoredPosition = new Vector2(-4f, 0f);

        heading = Line("Heading", T != null ? T.questHeadingFontSize : 22,
                       new Vector2(20f, -8f), 32f);

        BuildBox();

        float boxSide = T != null ? T.questBoxSize : 26f;
        objective = Line("Objective", T != null ? T.questObjectiveFontSize : 28,
                         new Vector2(20f + boxSide + 14f, -44f), 84f);
    }

    void BuildBox()
    {
        float side = T != null ? T.questBoxSize : 26f;

        var boxGo = new GameObject("Box", typeof(RectTransform), typeof(Image));
        boxGo.transform.SetParent(panel, false);
        box = boxGo.GetComponent<Image>();
        box.sprite = UIGfx.RoundedRect(5, 2);
        box.type = Image.Type.Sliced;
        box.color = Body;
        box.raycastTarget = false;

        var br = box.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
        br.pivot = new Vector2(0f, 1f);
        br.sizeDelta = new Vector2(side, side);
        br.anchoredPosition = new Vector2(20f, -48f);

        // Sits inside the box and tints on completion, so the tick lands in
        // something rather than appearing over nothing.
        boxFill = Plate("Fill", box.transform, UIGfx.RoundedRect(4, 0), new Color(0f, 0f, 0f, 0f));
        Stretch(boxFill.rectTransform, new Vector4(3f, 3f, -3f, -3f));

        // The tick: a short bar down-right and a long bar up-right. Drawn, not
        // typed, so it cannot come out as a missing glyph on somebody's machine.
        var tickGo = new GameObject("Tick", typeof(RectTransform));
        tickGo.transform.SetParent(box.transform, false);
        tick = tickGo.GetComponent<RectTransform>();
        tick.anchorMin = tick.anchorMax = new Vector2(0.5f, 0.5f);
        tick.pivot = new Vector2(0.5f, 0.5f);
        tick.sizeDelta = new Vector2(side, side);
        tick.anchoredPosition = Vector2.zero;
        tick.localScale = Vector3.zero;

        tickShort = Plate("Short", tick, UIGfx.Solid(), Done);
        var sr = tickShort.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.sizeDelta = new Vector2(3f, side * 0.34f);
        sr.anchoredPosition = new Vector2(-side * 0.15f, -side * 0.20f);
        sr.localRotation = Quaternion.Euler(0f, 0f, 42f);

        tickLong = Plate("Long", tick, UIGfx.Solid(), Done);
        var lr = tickLong.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
        lr.pivot = new Vector2(0.5f, 0f);
        lr.sizeDelta = new Vector2(3f, side * 0.60f);
        lr.anchoredPosition = new Vector2(-side * 0.15f, -side * 0.20f);
        lr.localRotation = Quaternion.Euler(0f, 0f, -36f);
    }

    static Image Plate(string name, Transform parent, Sprite sprite, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.color = colour;
        img.raycastTarget = false;
        return img;
    }

    static void Stretch(RectTransform rt, Vector4 pad)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad.x, pad.y);
        rt.offsetMax = new Vector2(pad.z, pad.w);
    }

    Text Line(string name, int size, Vector2 offset, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Outline), typeof(Text));
        go.transform.SetParent(panel, false);

        var text = go.GetComponent<Text>();

        // The game's own display face, the one the menus, captions and hints are
        // set in. Body text elsewhere uses a plain system sans because it is set
        // small enough that a decorative serif turns to mush; the tracker is set
        // well above that size, so that reason does not apply here.
        text.font = GameFont.Get();
        text.fontSize = size;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.lineSpacing = 1.1f;

        // An outline rather than a drop shadow: the tracker lands on whatever the
        // camp happens to be doing, bright on one side of a letter and dark on
        // the other.
        var edge = go.GetComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.8f);
        float ring = Mathf.Max(1.2f, size * 0.055f);
        edge.effectDistance = new Vector2(ring, -ring);
        edge.useGraphicAlpha = true;

        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(offset.x, -height);
        rt.offsetMax = new Vector2(-14f, 0f);
        rt.anchoredPosition = offset;
        return text;
    }

    /// Letter-spaced, because a short all-caps label reads as a heading rather
    /// than a word when it is opened out. Unity's legacy Text has no tracking.
    static string Spaced(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length * 2);
        for (int i = 0; i < s.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// Puts an objective up. `headingText` is the small line above it.
    public void Show(string objectiveText, string headingText = "OBJECTIVE")
    {
        if (clearing != null) { StopCoroutine(clearing); clearing = null; }

        heading.text = Spaced(headingText);
        heading.color = Accent;
        objective.text = objectiveText;
        objective.color = Body;
        rule.color = Accent;

        // Back to an empty box, in case this objective follows a completed one.
        box.color = Body;
        boxFill.color = new Color(0f, 0f, 0f, 0f);
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
        heading.text = Spaced("COMPLETE");
        heading.color = Done;
        rule.color = Done;
        objective.color = new Color(Body.r, Body.g, Body.b, 0.6f);

        box.color = Done;
        tickShort.color = Done;
        tickLong.color = Done;
        boxFill.color = new Color(Done.r, Done.g, Done.b, 0.20f);

        // The tick springs in past full size and settles back. It is the one
        // piece of movement in the panel, and it is what reads as "closed"
        // rather than merely "faded".
        float t = 0f;
        const float pop = 0.34f;
        while (t < pop)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / pop);
            float eased = 1f - Mathf.Pow(1f - k, 3f);
            float overshoot = 1f + 0.28f * Mathf.Sin(k * Mathf.PI) * (1f - k);
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
