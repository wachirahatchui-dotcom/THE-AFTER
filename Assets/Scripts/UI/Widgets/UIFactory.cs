using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Builders for every styled widget the menu uses.
//
// The whole UI is constructed in code (the project has no UI prefabs or
// sprites), so this is the single place that decides what a button, a slider
// or a card looks like. Every colour and size below resolves to
// MenuTheme.Current, i.e. to Assets/Resources/MenuTheme.asset - nothing is
// hard-coded here any more.
public static class UIFactory
{
    // ---------------------------------------------------------------- palette
    public static Color Accent      { get { return MenuTheme.Current.accent; } }
    public static Color AccentSoft  { get { return MenuTheme.Current.accentSoft; } }
    public static Color Muted       { get { return MenuTheme.Current.inkSoft; } }
    public static Color Shadow      { get { return MenuTheme.Current.shadow; } }
    public static Color NightTop    { get { return MenuTheme.Current.backdropTop; } }
    public static Color NightBottom { get { return MenuTheme.Current.backdropBottom; } }
    public static Color DustTint    { get { return MenuTheme.Current.dustTint; } }

    public static Font Font { get { return GameFont.Get(); } }

    // Corner radius cannot be a property here: it is used as an optional
    // parameter default, which C# requires to be a compile-time constant.
    // Passing UseThemeRadius (the default) resolves to the theme value instead.
    public const int UseThemeRadius = -1;

    public static int CornerRadius { get { return MenuTheme.Current.cornerRadius; } }

    static int Radius(int requested)
    {
        return requested < 0 ? MenuTheme.Current.cornerRadius : requested;
    }

    // ------------------------------------------------------------- primitives
    public static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    public static Image NewRounded(string name, Transform parent, Color color, int radius = UseThemeRadius)
    {
        var img = NewImage(name, parent, color);
        img.sprite = UIGfx.RoundedRect(Radius(radius), 0);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        return img;
    }

    public static Text NewText(string name, Transform parent, string content, int size,
                               FontStyle style, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = Font;
        t.text = content;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    // Small text in the plain body face. Use this for anything under about
    // 22pt - descriptions, value readouts, hints - because the display serif
    // is unreadable once the canvas scale shrinks it.
    public static Text NewBodyText(string name, Transform parent, string content, int size,
                                   FontStyle style, TextAnchor anchor, Color color)
    {
        var t = NewText(name, parent, content, size, style, anchor, color);
        t.font = GameFont.GetBody();
        return t;
    }

    public static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static LayoutElement SetHeight(GameObject go, float h)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.preferredHeight = h;
        le.minHeight = h;
        le.flexibleHeight = 0f;
        return le;
    }

    // Sliced ring drawn just inside the edge of a rounded rect.
    public static Image AddOutline(Image target, Color color, int thickness = 3, int radius = UseThemeRadius)
    {
        var go = new GameObject("Outline", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(target.transform, false);
        var img = go.GetComponent<Image>();
        img.sprite = UIGfx.RoundedRect(Radius(radius), thickness);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = color;
        img.raycastTarget = false;
        Stretch(img.rectTransform);
        return img;
    }

    public static Image AddDropShadow(Image target, Vector2 offset, int radius = UseThemeRadius)
    {
        var go = new GameObject("Shadow", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(target.transform.parent, false);
        go.transform.SetSiblingIndex(target.transform.GetSiblingIndex());

        var img = go.GetComponent<Image>();
        img.sprite = UIGfx.RoundedRect(Radius(radius), 0);
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f;
        img.color = Shadow;
        img.raycastTarget = false;

        var rt = img.rectTransform;
        var src = target.rectTransform;
        rt.anchorMin = src.anchorMin;
        rt.anchorMax = src.anchorMax;
        rt.pivot = src.pivot;
        rt.sizeDelta = src.sizeDelta + new Vector2(6f, 6f);
        rt.anchoredPosition = src.anchoredPosition + offset;
        return img;
    }

    // ------------------------------------------------------------------ cards
    // A parchment page: shadow, fill, grain and an ink outline.
    //
    // Returns an invisible root that owns all four layers, so hiding the card
    // hides its shadow too. An earlier version parented the shadow to the
    // card's parent, which left every closed panel's shadow stranded on screen.
    public static Image Card(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var root = NewImage(name, parent, new Color(0f, 0f, 0f, 0f));
        var rt = root.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = position;

        var shadow = NewImage("Shadow", root.transform, Shadow);
        shadow.sprite = UIGfx.RoundedRect(CornerRadius, 0);
        shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = 1f;
        shadow.raycastTarget = false;
        Stretch(shadow.rectTransform, -7f);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -9f);

        var fill = NewRounded("Fill", root.transform, GameUITheme.Parchment);
        Stretch(fill.rectTransform);

        var fibre = NewImage("Grit", fill.transform, new Color(1f, 1f, 1f, 0.5f));
        fibre.sprite = UIGfx.PaperFibre(256);
        fibre.type = Image.Type.Tiled;
        fibre.raycastTarget = false;
        Stretch(fibre.rectTransform, 4f);

        AddOutline(fill, GameUITheme.Ink, 3);
        return root;
    }

    public static Text Header(Transform parent, string text, int size = 44)
    {
        var t = NewText("Header", parent, text, size, FontStyle.Bold, TextAnchor.MiddleCenter, GameUITheme.Ink);
        SetHeight(t.gameObject, size + 22);
        return t;
    }

    // A hairline rule with breathing room. The line lives inside a taller host
    // so the layout group reserves the padding without fattening the stroke.
    public static Image Divider(Transform parent, float height = 2f)
    {
        var host = NewRect("Divider", parent);
        SetHeight(host.gameObject, height + 20f);

        var img = NewImage("Line", host, new Color(GameUITheme.Ink.r, GameUITheme.Ink.g, GameUITheme.Ink.b, 0.26f));
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, height);
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    public static RectTransform Spacer(Transform parent, float height)
    {
        var rt = NewRect("Spacer", parent);
        SetHeight(rt.gameObject, height);
        return rt;
    }

    // ================================================= bare controls
    // The *Row builders below own a whole labelled row. These three build only
    // the control itself, stretched to fill whatever rect they are given, so a
    // caller composing its own row (SettingRowView) can place them freely.

    // A slider filling `parent`, with no label and no readout of its own.
    public static Slider SliderControl(Transform parent, float min, float max, float value,
                                       UnityAction<float> onChange)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);

        var bg = NewRounded("Background", go.transform, GameUITheme.ParchmentDeep, 6);
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(0f, 9f);
        bgRt.anchoredPosition = Vector2.zero;
        AddOutline(bg, new Color(GameUITheme.Ink.r, GameUITheme.Ink.g, GameUITheme.Ink.b, 0.55f), 2, 6);

        var fillArea = NewRect("Fill Area", go.transform);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.pivot = new Vector2(0.5f, 0.5f);
        fillArea.sizeDelta = new Vector2(-20f, 9f);
        fillArea.anchoredPosition = new Vector2(-5f, 0f);

        var fill = NewRounded("Fill", fillArea, Accent, 6);
        var fillRt = fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = new Vector2(10f, 0f);

        var handleArea = NewRect("Handle Slide Area", go.transform);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        var handle = NewRounded("Handle", handleArea, GameUITheme.ParchmentLight, 10);
        var hRt = handle.rectTransform;
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.sizeDelta = new Vector2(20f, 20f);
        AddOutline(handle, GameUITheme.Ink, 3, 10);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = hRt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.transition = Selectable.Transition.None;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChange);

        return slider;
    }

    // A checkbox pinned to the right edge of `parent`.
    public static Toggle ToggleControl(Transform parent, bool value, UnityAction<bool> onChange)
    {
        var go = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);
        Stretch((RectTransform)go.transform);

        var box = NewRounded("Box", go.transform, GameUITheme.ParchmentDeep, 6);
        var bRt = box.rectTransform;
        bRt.anchorMin = bRt.anchorMax = new Vector2(1f, 0.5f);
        bRt.pivot = new Vector2(1f, 0.5f);
        bRt.sizeDelta = new Vector2(32f, 32f);
        bRt.anchoredPosition = Vector2.zero;
        AddOutline(box, GameUITheme.Ink, 3, 6);

        var check = NewRounded("Check", box.transform, Accent, 4);
        Stretch(check.rectTransform, 7f);

        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.transition = Selectable.Transition.None;
        toggle.SetIsOnWithoutNotify(value);
        check.gameObject.SetActive(value);

        toggle.onValueChanged.AddListener(v =>
        {
            UIAudio.Play(UISound.Toggle, 0.8f, v ? 1.08f : 0.92f);
            UITween.Punch(box.transform, 0.18f, 0.20f, Vector3.one);
            if (v)
            {
                check.gameObject.SetActive(true);
                UITween.ScaleFrom(check.transform, Vector3.one * 0.3f, 0.20f, Ease.OutBack);
            }
            else
            {
                check.gameObject.SetActive(false);
            }
            onChange(v);
        });

        return toggle;
    }

    // A framed "< value >" stepper filling `parent`.
    public static ArrowSelector SelectorControl(Transform parent, List<string> options,
                                                int index, Action<int> onChange)
    {
        var frame = NewRounded("Frame", parent, GameUITheme.ParchmentDeep, 8);
        var fRt = frame.rectTransform;
        fRt.anchorMin = new Vector2(0f, 0.5f);
        fRt.anchorMax = new Vector2(1f, 0.5f);
        fRt.pivot = new Vector2(0.5f, 0.5f);
        fRt.sizeDelta = new Vector2(0f, 40f);
        fRt.anchoredPosition = Vector2.zero;
        AddOutline(frame, GameUITheme.Ink, 2, 8);

        var value = NewBodyText("Value", frame.transform, "", 23, FontStyle.Bold,
                            TextAnchor.MiddleCenter, GameUITheme.Ink);
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        var vRt = value.rectTransform;
        vRt.anchorMin = Vector2.zero;
        vRt.anchorMax = Vector2.one;
        vRt.offsetMin = new Vector2(42f, 0f);
        vRt.offsetMax = new Vector2(-42f, 0f);

        var left = Arrow(frame.transform, "‹", true);
        var right = Arrow(frame.transform, "›", false);

        return new ArrowSelector(frame.gameObject, left, right, value, options, index, onChange);
    }

    // ------------------------------------------------------------ button tone
    // What a button means, expressed as colour: neutral, destructive, or the
    // safe/affirmative one. Applied after construction so a reusable dialog can
    // re-colour its buttons per question instead of building new ones.
    public enum MenuTone { Neutral, Danger, Positive }

    public static void ApplyButtonTone(Button btn, MenuTone tone)
    {
        if (btn == null) return;

        Color fill, border, focusFill, focusText;

        switch (tone)
        {
            case MenuTone.Danger:
                fill = GameUITheme.DangerFill;
                border = GameUITheme.Danger;
                focusFill = new Color(border.r, border.g, border.b, 0.9f);
                focusText = new Color(0.97f, 0.93f, 0.88f, 1f);
                break;

            case MenuTone.Positive:
                fill = GameUITheme.PositiveFill;
                border = GameUITheme.Positive;
                focusFill = new Color(border.r, border.g, border.b, 0.9f);
                focusText = new Color(0.94f, 0.97f, 0.91f, 1f);
                break;

            default:
                fill = GameUITheme.ParchmentLight;
                border = GameUITheme.Ink;
                focusFill = GameUITheme.Ink;
                focusText = GameUITheme.ParchmentLight;
                break;
        }

        var img = btn.GetComponent<Image>();
        if (img != null) img.color = fill;

        var outline = btn.transform.Find("Outline");
        if (outline != null) outline.GetComponent<Image>().color = border;

        var label = btn.transform.Find("Label");
        if (label != null) label.GetComponent<Text>().color = border;

        var fx = btn.GetComponent<MenuButtonFX>();
        if (fx != null)
        {
            fx.idleFill = fill;
            fx.focusFill = focusFill;
            fx.idleText = border;
            fx.focusText = focusText;
            fx.accent = border;
            fx.clickSound = tone == MenuTone.Danger ? UISound.Cancel : UISound.Click;
        }
    }

    // ---------------------------------------------------------------- buttons
    public static Button MenuButton(Transform parent, string label, UnityAction onClick,
                                    MenuFxStyle style = MenuFxStyle.Classic,
                                    float height = 74f, int fontSize = 32,
                                    bool danger = false)
    {
        // Danger buttons are a dusty rose rather than a translucent red: the
        // menu column sits straight on the dark backdrop, where a low-alpha red
        // fill went almost black and swallowed its own dark-red label.
        Color fill = danger
            ? new Color(0.784f, 0.616f, 0.549f, 1f)
            : GameUITheme.ParchmentLight;
        Color border = danger ? GameUITheme.Danger : GameUITheme.Ink;
        Color focusFill = danger
            ? new Color(GameUITheme.Danger.r, GameUITheme.Danger.g, GameUITheme.Danger.b, 0.85f)
            : GameUITheme.Ink;
        Color focusText = danger ? new Color(0.97f, 0.93f, 0.88f, 1f) : GameUITheme.ParchmentLight;

        var img = NewRounded("Btn_" + label, parent, fill, 10);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);
        SetHeight(img.gameObject, height);

        AddOutline(img, border, 2, 10);

        var text = NewText("Label", img.transform, label, fontSize, FontStyle.Normal,
                           TextAnchor.MiddleCenter, border);
        Stretch(text.rectTransform);

        var fx = img.gameObject.AddComponent<MenuButtonFX>();
        fx.style = style;
        fx.idleFill = fill;
        fx.focusFill = focusFill;
        fx.idleText = border;
        fx.focusText = focusText;
        fx.accent = danger ? GameUITheme.Danger : Accent;
        fx.clickSound = danger ? UISound.Cancel : UISound.Click;

        return btn;
    }

    public static Button SmallButton(Transform parent, string label, UnityAction onClick,
                                     Vector2 size, MenuFxStyle style = MenuFxStyle.Soft,
                                     bool danger = false)
    {
        var btn = MenuButton(parent, label, onClick, style, size.y, 24, danger);
        var le = btn.GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth = size.x;
            le.minWidth = size.x;
            le.flexibleWidth = 0f;
        }
        var rt = (RectTransform)btn.transform;
        rt.sizeDelta = size;
        return btn;
    }

    // ---------------------------------------------------------------- sliders
    // Label on the left, live percentage on the right, track underneath.
    public static Slider SliderRow(Transform parent, string label, float min, float max, float value,
                                   UnityAction<float> onChange, Func<float, string> format = null)
    {
        var row = NewRect("Row_" + label, parent);
        SetHeight(row.gameObject, 74f);

        var caption = NewText("Caption", row, label, 26, FontStyle.Bold, TextAnchor.UpperLeft, GameUITheme.Ink);
        caption.rectTransform.anchorMin = new Vector2(0f, 1f);
        caption.rectTransform.anchorMax = new Vector2(0.7f, 1f);
        caption.rectTransform.pivot = new Vector2(0f, 1f);
        caption.rectTransform.offsetMin = new Vector2(0f, -32f);
        caption.rectTransform.offsetMax = Vector2.zero;

        var readout = NewText("Value", row, "", 24, FontStyle.Normal, TextAnchor.UpperRight, Accent);
        readout.rectTransform.anchorMin = new Vector2(0.7f, 1f);
        readout.rectTransform.anchorMax = new Vector2(1f, 1f);
        readout.rectTransform.pivot = new Vector2(1f, 1f);
        readout.rectTransform.offsetMin = new Vector2(0f, -32f);
        readout.rectTransform.offsetMax = Vector2.zero;

        // --- the slider itself, mirroring Unity's default hierarchy so the
        //     Slider component behaves exactly as it expects to.
        var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(row, false);
        var srt = (RectTransform)sliderGo.transform;
        srt.anchorMin = new Vector2(0f, 0f);
        srt.anchorMax = new Vector2(1f, 0f);
        srt.pivot = new Vector2(0.5f, 0f);
        srt.offsetMin = new Vector2(0f, 6f);
        srt.offsetMax = new Vector2(0f, 28f);

        var bg = NewRounded("Background", sliderGo.transform, GameUITheme.ParchmentDeep, 6);
        var bgRt = bg.rectTransform;
        bgRt.anchorMin = new Vector2(0f, 0.5f);
        bgRt.anchorMax = new Vector2(1f, 0.5f);
        bgRt.pivot = new Vector2(0.5f, 0.5f);
        bgRt.sizeDelta = new Vector2(0f, 10f);
        bgRt.anchoredPosition = Vector2.zero;
        AddOutline(bg, new Color(GameUITheme.Ink.r, GameUITheme.Ink.g, GameUITheme.Ink.b, 0.6f), 2, 6);

        var fillArea = NewRect("Fill Area", sliderGo.transform);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.pivot = new Vector2(0.5f, 0.5f);
        fillArea.sizeDelta = new Vector2(-22f, 10f);
        fillArea.anchoredPosition = new Vector2(-5f, 0f);

        var fill = NewRounded("Fill", fillArea, Accent, 6);
        var fillRt = fill.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(1f, 1f);
        fillRt.sizeDelta = new Vector2(12f, 0f);

        var handleArea = NewRect("Handle Slide Area", sliderGo.transform);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(11f, 0f);
        handleArea.offsetMax = new Vector2(-11f, 0f);

        var handle = NewRounded("Handle", handleArea, GameUITheme.ParchmentLight, 10);
        var hRt = handle.rectTransform;
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(0f, 0.5f);
        hRt.pivot = new Vector2(0.5f, 0.5f);
        hRt.sizeDelta = new Vector2(22f, 22f);
        AddOutline(handle, GameUITheme.Ink, 3, 10);

        var slider = sliderGo.GetComponent<Slider>();
        slider.fillRect = fillRt;
        slider.handleRect = hRt;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.transition = Selectable.Transition.None;
        slider.SetValueWithoutNotify(value);

        Func<float, string> fmt = format ?? (v => Mathf.RoundToInt(Mathf.InverseLerp(min, max, v) * 100f) + "%");
        readout.text = fmt(value);

        // Ticking on every value change would be a buzzsaw, so the step sound
        // only fires when the rounded readout actually changes.
        string last = readout.text;
        slider.onValueChanged.AddListener(v =>
        {
            string s = fmt(v);
            if (s != last)
            {
                last = s;
                readout.text = s;
                UIAudio.Play(UISound.SliderStep, 0.5f, 1f + UnityEngine.Random.Range(-0.05f, 0.05f));
                UITween.Punch(handle.transform, 0.12f, 0.14f, Vector3.one);
            }
            onChange?.Invoke(v);
        });

        return slider;
    }

    // ---------------------------------------------------------------- toggles
    public static Toggle ToggleRow(Transform parent, string label, bool value, UnityAction<bool> onChange)
    {
        var go = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(Toggle));
        go.transform.SetParent(parent, false);
        SetHeight(go, 52f);

        var box = NewRounded("Box", go.transform, GameUITheme.ParchmentDeep, 6);
        var bRt = box.rectTransform;
        bRt.anchorMin = new Vector2(0f, 0.5f);
        bRt.anchorMax = new Vector2(0f, 0.5f);
        bRt.pivot = new Vector2(0f, 0.5f);
        bRt.sizeDelta = new Vector2(34f, 34f);
        bRt.anchoredPosition = new Vector2(2f, 0f);
        AddOutline(box, GameUITheme.Ink, 3, 6);

        var check = NewRounded("Check", box.transform, Accent, 4);
        var cRt = check.rectTransform;
        Stretch(cRt, 8f);

        var caption = NewText("Caption", go.transform, label, 26, FontStyle.Normal,
                              TextAnchor.MiddleLeft, GameUITheme.Ink);
        var capRt = caption.rectTransform;
        capRt.anchorMin = new Vector2(0f, 0f);
        capRt.anchorMax = new Vector2(1f, 1f);
        capRt.offsetMin = new Vector2(50f, 0f);
        capRt.offsetMax = Vector2.zero;

        var toggle = go.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.transition = Selectable.Transition.None;
        toggle.SetIsOnWithoutNotify(value);
        check.gameObject.SetActive(value);

        toggle.onValueChanged.AddListener(v =>
        {
            UIAudio.Play(UISound.Toggle, 0.8f, v ? 1.08f : 0.92f);
            UITween.Punch(box.transform, 0.18f, 0.20f, Vector3.one);
            if (v)
            {
                check.gameObject.SetActive(true);
                UITween.ScaleFrom(check.transform, Vector3.one * 0.3f, 0.20f, Ease.OutBack);
            }
            onChange?.Invoke(v);
        });

        return toggle;
    }

    // --------------------------------------------------------- arrow selector
    public static ArrowSelector SelectorRow(Transform parent, string label, List<string> options,
                                            int index, Action<int> onChange)
    {
        var row = NewRect("Sel_" + label, parent);
        SetHeight(row.gameObject, 62f);

        var caption = NewText("Caption", row, label, 26, FontStyle.Bold, TextAnchor.MiddleLeft, GameUITheme.Ink);
        var capRt = caption.rectTransform;
        capRt.anchorMin = new Vector2(0f, 0f);
        capRt.anchorMax = new Vector2(0.44f, 1f);
        capRt.offsetMin = Vector2.zero;
        capRt.offsetMax = Vector2.zero;

        var frame = NewRounded("Frame", row, GameUITheme.ParchmentDeep, 8);
        var fRt = frame.rectTransform;
        fRt.anchorMin = new Vector2(0.44f, 0.5f);
        fRt.anchorMax = new Vector2(1f, 0.5f);
        fRt.pivot = new Vector2(0.5f, 0.5f);
        fRt.sizeDelta = new Vector2(0f, 46f);
        fRt.anchoredPosition = Vector2.zero;
        AddOutline(frame, GameUITheme.Ink, 2, 8);

        var value = NewText("Value", frame.transform, "", 24, FontStyle.Normal,
                            TextAnchor.MiddleCenter, GameUITheme.Ink);
        value.horizontalOverflow = HorizontalWrapMode.Overflow;
        var vRt = value.rectTransform;
        vRt.anchorMin = new Vector2(0f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.offsetMin = new Vector2(46f, 0f);
        vRt.offsetMax = new Vector2(-46f, 0f);

        var left = Arrow(frame.transform, "‹", true);
        var right = Arrow(frame.transform, "›", false);

        return new ArrowSelector(row.gameObject, left, right, value, options, index, onChange);
    }

    static Button Arrow(Transform parent, string glyph, bool onLeft)
    {
        var img = NewImage(onLeft ? "Left" : "Right", parent, new Color(0f, 0f, 0f, 0f));
        var rt = img.rectTransform;
        rt.anchorMin = new Vector2(onLeft ? 0f : 1f, 0.5f);
        rt.anchorMax = new Vector2(onLeft ? 0f : 1f, 0.5f);
        rt.pivot = new Vector2(onLeft ? 0f : 1f, 0.5f);
        rt.sizeDelta = new Vector2(46f, 46f);
        rt.anchoredPosition = Vector2.zero;

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.transition = Selectable.Transition.None;

        var t = NewText("Glyph", img.transform, glyph, 30, FontStyle.Bold, TextAnchor.MiddleCenter, GameUITheme.Ink);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        Stretch(t.rectTransform);

        var fx = img.gameObject.AddComponent<MenuButtonFX>();
        fx.style = MenuFxStyle.Scale;
        fx.scaleAmount = 1.22f;
        fx.idleFill = new Color(0f, 0f, 0f, 0f);
        fx.focusFill = new Color(0f, 0f, 0f, 0f);
        fx.idleText = GameUITheme.Ink;
        fx.focusText = Accent;
        fx.accent = Accent;
        fx.hoverSound = UISound.Tick;
        fx.clickSound = UISound.Tick;
        fx.duration = 0.12f;

        return btn;
    }

    // ------------------------------------------------------------ scroll area
    // Returns the content RectTransform; callers parent their rows to it.
    public static RectTransform ScrollArea(Transform parent, out ScrollRect scrollRect)
    {
        var viewportGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect),
                                        typeof(RectMask2D), typeof(Image));
        viewportGo.transform.SetParent(parent, false);

        var viewportImg = viewportGo.GetComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0f);   // invisible but catches drags

        scrollRect = viewportGo.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.08f;
        scrollRect.scrollSensitivity = 34f;
        scrollRect.viewport = (RectTransform)viewportGo.transform;

        var content = NewRect("Content", viewportGo.transform);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(8, 8, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.UpperCenter;

        scrollRect.content = content;
        return content;
    }

    // ------------------------------------------------------------------ misc
    public static VerticalLayoutGroup VStack(Transform parent, float spacing,
                                             RectOffset padding = null,
                                             TextAnchor align = TextAnchor.UpperCenter)
    {
        var vlg = parent.gameObject.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = parent.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = spacing;
        vlg.padding = padding ?? new RectOffset(0, 0, 0, 0);
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childAlignment = align;
        return vlg;
    }

    public static HorizontalLayoutGroup HStack(Transform parent, float spacing,
                                               TextAnchor align = TextAnchor.MiddleCenter)
    {
        var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = true;
        h.childForceExpandHeight = false;
        h.childAlignment = align;
        return h;
    }

    // Wraps a GameObject in a UIPanel + CanvasGroup so it can be animated.
    public static UIPanel MakePanel(GameObject go, string inAnim, string outAnim, float duration = 0.3f)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();

        var panel = go.GetComponent<UIPanel>();
        if (panel == null) panel = go.AddComponent<UIPanel>();

        panel.inAnim = inAnim;
        panel.outAnim = outAnim;
        panel.duration = duration;
        return panel;
    }
}
