using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// The studio card shown before the main menu appears.
//
// Runs once per launch, on top of the menu scene rather than in a scene of its
// own, so it needs no build-settings entry and no extra load. The menu builds
// itself underneath while this is up, which is why it is already warm when the
// splash clears.
//
// Skippable with any key from the first frame - a splash that cannot be
// skipped is the single most complained-about thing in a menu.
public class SplashScreen : MonoBehaviour
{
    // Cleared on domain reload, so it is once per play session, not per scene.
    static bool shownThisSession;

    public static bool IsShowing { get; private set; }

    CanvasGroup group;
    Text studioLabel;
    Text taglineLabel;
    Image rule;

    string studioName;
    string tagline;
    float holdSeconds;

    // Call from the menu. Returns false if it has already run this session, so
    // the caller can skip straight to its own intro.
    public static bool Show(string studio, string tagline, float holdSeconds = 1.8f)
    {
        if (shownThisSession) return false;
        shownThisSession = true;

        var go = new GameObject("~SplashScreen");
        var splash = go.AddComponent<SplashScreen>();
        splash.studioName = string.IsNullOrEmpty(studio) ? Application.companyName : studio;
        splash.tagline = tagline;
        splash.holdSeconds = holdSeconds;
        splash.Build();
        splash.StartCoroutine(splash.Run());
        return true;
    }

    void Build()
    {
        IsShowing = true;

        var theme = MenuTheme.Current;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 29000;          // under the loading screen

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.referenceResolution;
        scaler.matchWidthOrHeight = theme.scalerMatch;

        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 1f;                     // opaque immediately: it is the
        group.blocksRaycasts = true;          // very first thing on screen

        var bg = UIFactory.NewImage("BG", transform, new Color(0.03f, 0.028f, 0.026f, 1f));
        UIFactory.Stretch(bg.rectTransform);

        var stack = UIFactory.NewRect("Stack", transform);
        stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 0.5f);
        stack.pivot = new Vector2(0.5f, 0.5f);
        stack.sizeDelta = new Vector2(1200f, 260f);
        stack.anchoredPosition = Vector2.zero;

        studioLabel = UIFactory.NewText("Studio", stack, studioName, 64, FontStyle.Bold,
                                        TextAnchor.MiddleCenter, GameUITheme.Parchment);
        studioLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var sRt = studioLabel.rectTransform;
        sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.5f);
        sRt.pivot = new Vector2(0.5f, 0.5f);
        sRt.anchoredPosition = new Vector2(0f, 24f);
        sRt.sizeDelta = new Vector2(1200f, 90f);

        rule = UIFactory.NewImage("Rule", stack, UIFactory.AccentSoft);
        rule.raycastTarget = false;
        var rRt = rule.rectTransform;
        rRt.anchorMin = rRt.anchorMax = new Vector2(0.5f, 0.5f);
        rRt.pivot = new Vector2(0.5f, 0.5f);
        rRt.anchoredPosition = new Vector2(0f, -24f);
        rRt.sizeDelta = new Vector2(0f, 2f);

        taglineLabel = UIFactory.NewText("Tagline", stack, tagline, 24, FontStyle.Italic,
                                         TextAnchor.MiddleCenter, theme.subtitleColor);
        taglineLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var tRt = taglineLabel.rectTransform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0.5f, 0.5f);
        tRt.pivot = new Vector2(0.5f, 0.5f);
        tRt.anchoredPosition = new Vector2(0f, -60f);
        tRt.sizeDelta = new Vector2(1200f, 40f);

        // Start from nothing; Run() brings each piece in.
        studioLabel.color = Fade(studioLabel.color, 0f);
        taglineLabel.color = Fade(taglineLabel.color, 0f);
    }

    static Color Fade(Color c, float a) { c.a = a; return c; }

    IEnumerator Run()
    {
        UITween.FadeGraphic(studioLabel, 1f, 0.8f, Ease.OutCubic);
        UITween.ScaleFrom(studioLabel.transform, Vector3.one * 0.94f, 1.2f, Ease.OutCubic);

        UITween.To(rule, 0.9f, Ease.OutQuint, k =>
        {
            if (rule != null) rule.rectTransform.sizeDelta = new Vector2(Mathf.Lerp(0f, 420f, k), 2f);
        }, null, 0.45f);

        UITween.FadeGraphic(taglineLabel, 1f, 0.7f, Ease.OutCubic, null, 0.8f);

        float elapsed = 0f;
        float total = holdSeconds + 1.2f;

        while (elapsed < total)
        {
            if (AnyInput()) break;         // skippable from frame one
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        UITween.Fade(group, 0f, 0.6f, Ease.InQuad, () =>
        {
            IsShowing = false;
            Destroy(gameObject);
        });
    }

    static bool AnyInput()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.anyKey.wasPressedThisFrame) return true;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;

        var pad = Gamepad.current;
        if (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            return true;

        return false;
    }

    void OnDestroy()
    {
        IsShowing = false;
    }
}
