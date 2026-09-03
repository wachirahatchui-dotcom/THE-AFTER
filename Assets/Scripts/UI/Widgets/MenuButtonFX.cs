using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Which reactions a button plays when it gains focus. Combinable, so a style
// is expressed as a set rather than a single mode:
//
//     fx.style = MenuFxStyle.Slide | MenuFxStyle.Caret | MenuFxStyle.Glow;
//
// Adding a new reaction means adding a flag and one branch in Apply().
[Flags]
public enum MenuFxStyle
{
    None       = 0,
    Slide      = 1 << 0,   // nudges sideways
    Scale      = 1 << 1,   // grows slightly
    Glow       = 1 << 2,   // soft halo fades in behind
    Underline  = 1 << 3,   // rule wipes in under the label
    Caret      = 1 << 4,   // marker slides in on the left
    InkFill    = 1 << 5,   // background darkens toward the accent
    Tilt       = 1 << 6,   // small rotation
    Breathe    = 1 << 7,   // continuous idle pulse while focused

    Classic    = Slide | Underline | Caret | InkFill,
    Soft       = Scale | Glow | InkFill,
    Loud       = Slide | Scale | Glow | Caret | InkFill | Tilt
}

// Hover / keyboard-focus / press feedback for one menu button.
//
// Owns its own decoration children (glow, underline, caret) so callers only
// have to build a plain Image + Button + Text and attach this. Pointer and
// EventSystem selection are treated as the same "focused" state, which is what
// makes mouse and gamepad navigation feel identical.
[RequireComponent(typeof(RectTransform))]
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    ISelectHandler, IDeselectHandler,
    IPointerDownHandler, IPointerClickHandler, ISubmitHandler
{
    public MenuFxStyle style = MenuFxStyle.Classic;

    public float slideDistance = 14f;
    public float scaleAmount = 1.045f;
    public float tiltDegrees = -1.2f;
    public float duration = 0.18f;

    public Color idleFill = Color.white;
    public Color focusFill = Color.white;
    public Color idleText = Color.black;
    public Color focusText = Color.black;
    public Color accent = Color.black;

    public bool playSounds = true;
    public UISound hoverSound = UISound.Hover;
    public UISound clickSound = UISound.Click;

    [NonSerialized] public bool interactableOverride = true;

    RectTransform rt;
    Image fill;
    Text label;
    Image glow;
    Image underline;
    Text caret;
    Button button;

    Vector2 home;
    bool focused;
    bool built;
    float breathePhase;

    void Awake()
    {
        rt = (RectTransform)transform;
        fill = GetComponent<Image>();
        button = GetComponent<Button>();
        label = GetComponentInChildren<Text>(true);

        // Tuning comes from the theme asset. Callers wanting something bespoke
        // (the stepper arrows, the save-slot rows) assign these fields right
        // after AddComponent - which runs after this - so they still win.
        var theme = MenuTheme.Current;
        duration = theme.buttonFxDuration;
        slideDistance = theme.buttonSlideDistance;
        scaleAmount = theme.buttonScaleAmount;
        tiltDegrees = theme.buttonTiltDegrees;

        // We drive every visual ourselves; Unity's own tint would fight it.
        if (button != null) button.transition = Selectable.Transition.None;
    }

    void Start()
    {
        // Captured here, not in Awake: the menu is built inside another
        // component's Awake, so the button is still at (0,0) at that point and
        // caching it then would make the slide animation yank it to the corner.
        home = rt.anchoredPosition;
        BuildDecorations();
        Apply(false, true);
    }

    void OnEnable()
    {
        if (built) { focused = false; Apply(false, true); }
    }

    // ------------------------------------------------------------ decorations
    void BuildDecorations()
    {
        if (built) return;
        built = true;

        if ((style & MenuFxStyle.Glow) != 0)
        {
            var go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            glow = go.GetComponent<Image>();
            glow.sprite = UIGfx.SoftDot(64, 1.6f);
            glow.type = Image.Type.Simple;
            glow.raycastTarget = false;
            glow.color = new Color(accent.r, accent.g, accent.b, 0f);
            var g = glow.rectTransform;
            g.anchorMin = Vector2.zero; g.anchorMax = Vector2.one;
            g.offsetMin = new Vector2(-26f, -20f);
            g.offsetMax = new Vector2(26f, 20f);
        }

        if ((style & MenuFxStyle.Underline) != 0)
        {
            var go = new GameObject("Underline", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            underline = go.GetComponent<Image>();
            underline.color = accent;
            underline.raycastTarget = false;
            var u = underline.rectTransform;
            u.anchorMin = new Vector2(0f, 0f);
            u.anchorMax = new Vector2(0f, 0f);
            u.pivot = new Vector2(0f, 0f);
            u.anchoredPosition = new Vector2(26f, 14f);
            u.sizeDelta = new Vector2(0f, 2f);
        }

        if ((style & MenuFxStyle.Caret) != 0)
        {
            var go = new GameObject("Caret", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(transform, false);
            caret = go.GetComponent<Text>();
            caret.font = GameFont.Get();
            caret.text = "›";           // single right angle quote
            caret.fontSize = 34;
            caret.alignment = TextAnchor.MiddleCenter;
            caret.color = new Color(accent.r, accent.g, accent.b, 0f);
            caret.raycastTarget = false;
            caret.horizontalOverflow = HorizontalWrapMode.Overflow;
            caret.verticalOverflow = VerticalWrapMode.Overflow;
            var c = caret.rectTransform;
            c.anchorMin = new Vector2(0f, 0.5f);
            c.anchorMax = new Vector2(0f, 0.5f);
            c.pivot = new Vector2(0.5f, 0.5f);
            c.anchoredPosition = new Vector2(8f, 0f);
            c.sizeDelta = new Vector2(30f, 40f);
        }
    }

    // ----------------------------------------------------------------- events
    bool Usable
    {
        get { return interactableOverride && (button == null || button.interactable); }
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (!Usable) return;
        // Route through the EventSystem so hover and keyboard focus stay in sync.
        if (button != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
        else
            SetFocused(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (!Usable) return;
        if (button != null && EventSystem.current != null &&
            EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
        else
            SetFocused(false);
    }

    public void OnSelect(BaseEventData e) { if (Usable) SetFocused(true); }
    public void OnDeselect(BaseEventData e) { SetFocused(false); }

    public void OnPointerDown(PointerEventData e)
    {
        if (!Usable) return;
        UITween.ScaleTo(transform, Vector3.one * 0.965f, 0.06f, Ease.OutQuad);
    }

    public void OnPointerClick(PointerEventData e) { Fire(); }
    public void OnSubmit(BaseEventData e) { Fire(); }

    void Fire()
    {
        if (!Usable)
        {
            if (playSounds) UIAudio.Play(UISound.Error, 0.7f);
            UITween.Shake(rt, 5f, 0.22f);
            return;
        }
        if (playSounds) UIAudio.PlayVaried(clickSound);
        UITween.Punch(transform, 0.06f, 0.24f, Vector3.one * (focused && (style & MenuFxStyle.Scale) != 0 ? scaleAmount : 1f));
    }

    void SetFocused(bool value)
    {
        if (focused == value) return;
        focused = value;
        if (value && playSounds) UIAudio.PlayVaried(hoverSound);
        Apply(value, false);
    }

    // ------------------------------------------------------------ the reaction
    void Apply(bool on, bool instant)
    {
        float d = instant ? 0f : duration;
        float targetScale = on && (style & MenuFxStyle.Scale) != 0 ? scaleAmount : 1f;

        if ((style & MenuFxStyle.Slide) != 0)
            UITween.MoveAnchored(rt, home + new Vector2(on ? slideDistance : 0f, 0f), d, Ease.OutCubic);

        if ((style & MenuFxStyle.Scale) != 0)
            UITween.ScaleTo(transform, Vector3.one * targetScale, d, Ease.OutBack);
        else if (!instant)
            UITween.ScaleTo(transform, Vector3.one, d, Ease.OutQuad);

        if ((style & MenuFxStyle.Tilt) != 0)
            UITween.RotateZ(transform, on ? tiltDegrees : 0f, d, Ease.OutCubic);

        if ((style & MenuFxStyle.InkFill) != 0 && fill != null)
            UITween.ColorTo(fill, on ? focusFill : idleFill, d, Ease.OutQuad);

        if (label != null)
            UITween.ColorTo(label, on ? focusText : idleText, d, Ease.OutQuad);

        if (glow != null)
            UITween.FadeGraphic(glow, on ? 0.42f : 0f, d * 1.4f, Ease.OutQuad);

        if (underline != null)
        {
            float w = on ? Mathf.Max(0f, rt.rect.width - 52f) : 0f;
            float from = underline.rectTransform.sizeDelta.x;
            UITween.To(underline, d, Ease.OutQuint, k =>
            {
                if (underline == null) return;
                var s = underline.rectTransform.sizeDelta;
                s.x = Mathf.LerpUnclamped(from, w, k);
                underline.rectTransform.sizeDelta = s;
            });
        }

        if (caret != null)
        {
            UITween.FadeGraphic(caret, on ? 1f : 0f, d, Ease.OutQuad);
            UITween.MoveAnchored(caret.rectTransform, new Vector2(on ? 18f : 4f, 0f), d, Ease.OutBack);
        }
    }

    void Update()
    {
        // Idle pulse, driven directly rather than as a tween so it can run
        // indefinitely without queueing coroutines.
        if (!focused || (style & MenuFxStyle.Breathe) == 0) return;
        breathePhase += Time.unscaledDeltaTime;
        float s = 1f + Mathf.Sin(breathePhase * 3.2f) * 0.012f;
        transform.localScale = Vector3.one * (((style & MenuFxStyle.Scale) != 0 ? scaleAmount : 1f) * s);
    }

    // Re-reads the resting position after a layout pass has moved the button.
    public void RefreshHome()
    {
        if (rt == null) rt = (RectTransform)transform;
        home = rt.anchoredPosition;
    }

    void OnDestroy() { UITween.Kill(rt); }
}
