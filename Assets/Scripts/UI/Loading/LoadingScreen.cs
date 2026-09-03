using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Async scene loading with a real progress bar.
//
// SceneManager.LoadScene blocks until the scene is ready, which freezes the
// last menu frame on screen for however long that takes. LoadSceneAsync lets
// the bar move instead.
//
// Two details that matter:
//   * Unity stops async progress at 0.9 and holds there until allowSceneActivation
//     is set, so the raw value is remapped to 0..1 for display.
//   * The bar is eased toward the real value rather than snapped to it, so a
//     scene that loads instantly still reads as a load rather than a flicker.
//
// Lives on its own DontDestroyOnLoad canvas above everything except the fader.
public class LoadingScreen : MonoBehaviour
{
    static LoadingScreen instance;

    public static bool IsLoading { get; private set; }

    CanvasGroup group;
    Text chapterLabel;
    Text hintLabel;
    Text percentLabel;
    RectTransform barFill;
    RectTransform barTrack;

    float displayed;
    bool waitingForInput;

    public static LoadingScreen I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~LoadingScreen");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<LoadingScreen>();
                instance.Build();
            }
            return instance;
        }
    }

    // ================================================================== build
    void Build()
    {
        var theme = MenuTheme.Current;

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;             // under the screen fader

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.referenceResolution;
        scaler.matchWidthOrHeight = theme.scalerMatch;

        gameObject.AddComponent<GraphicRaycaster>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // Opaque backdrop so the half-torn-down previous scene never shows.
        var bg = UIFactory.NewImage("BG", transform, Color.white);
        bg.sprite = UIGfx.VerticalGradient(theme.backdropTop, theme.backdropBottom, 256);
        UIFactory.Stretch(bg.rectTransform);

        var vignette = UIFactory.NewImage("Vignette", transform,
            new Color(1f, 1f, 1f, theme.vignetteOpacity));
        vignette.sprite = UIGfx.RadialFalloff(new Color(0f, 0f, 0f, 0f), theme.vignetteColor, 2.4f, 256);
        vignette.raycastTarget = false;
        UIFactory.Stretch(vignette.rectTransform);

        // --- chapter title, bottom-left, like a novel's running head
        chapterLabel = UIFactory.NewText("Chapter", transform, "", 54, FontStyle.Bold,
                                         TextAnchor.LowerLeft, GameUITheme.Parchment);
        chapterLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var cRt = chapterLabel.rectTransform;
        cRt.anchorMin = cRt.anchorMax = new Vector2(0f, 0f);
        cRt.pivot = new Vector2(0f, 0f);
        cRt.anchoredPosition = new Vector2(190f, 210f);
        cRt.sizeDelta = new Vector2(1400f, 70f);

        // --- progress track
        barTrack = UIFactory.NewRect("BarTrack", transform);
        barTrack.anchorMin = barTrack.anchorMax = new Vector2(0f, 0f);
        barTrack.pivot = new Vector2(0f, 0f);
        barTrack.anchoredPosition = new Vector2(190f, 160f);
        barTrack.sizeDelta = new Vector2(900f, 6f);

        var track = UIFactory.NewImage("Track", barTrack, GameUITheme.InkSoft);
        track.raycastTarget = false;
        UIFactory.Stretch(track.rectTransform);

        var fill = UIFactory.NewImage("Fill", barTrack, UIFactory.AccentSoft);
        fill.raycastTarget = false;
        barFill = fill.rectTransform;
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.pivot = new Vector2(0f, 0.5f);
        barFill.anchoredPosition = Vector2.zero;
        barFill.sizeDelta = new Vector2(0f, 0f);

        percentLabel = UIFactory.NewText("Percent", transform, "0%", 22, FontStyle.Normal,
                                         TextAnchor.LowerRight, UIFactory.AccentSoft);
        percentLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var pRt = percentLabel.rectTransform;
        pRt.anchorMin = pRt.anchorMax = new Vector2(0f, 0f);
        pRt.pivot = new Vector2(1f, 0f);
        pRt.anchoredPosition = new Vector2(1090f, 174f);
        pRt.sizeDelta = new Vector2(200f, 30f);

        // --- "press any key", hidden until the scene is actually ready
        var subtitle = MenuTheme.Current.subtitleColor;
        hintLabel = UIFactory.NewText("Hint", transform, "", 24, FontStyle.Italic,
                                      TextAnchor.LowerLeft, subtitle);
        hintLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var hRt = hintLabel.rectTransform;
        hRt.anchorMin = hRt.anchorMax = new Vector2(0f, 0f);
        hRt.pivot = new Vector2(0f, 0f);
        hRt.anchoredPosition = new Vector2(190f, 110f);
        hRt.sizeDelta = new Vector2(1000f, 34f);
    }

    // ================================================================== load
    // waitForInput holds on 100% until a key is pressed. It defaults to OFF:
    // as a default it reads as a hang - the screen sits at 100% and nothing
    // happens - so it is opt-in for the places that actually want a beat.
    public static void Load(string sceneName, string chapterName, bool waitForInput = false,
                            Action onLoaded = null)
    {
        I.StartCoroutine(I.Run(sceneName, chapterName, waitForInput, onLoaded));
    }

    IEnumerator Run(string sceneName, string chapterName, bool waitForInput, Action onLoaded)
    {
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError("[LoadingScreen] Scene not in Build Settings: " + sceneName);
            yield break;
        }

        IsLoading = true;
        waitingForInput = false;
        displayed = 0f;

        chapterLabel.text = string.IsNullOrEmpty(chapterName) ? "" : chapterName;
        hintLabel.text = "";
        SetProgress(0f);

        group.blocksRaycasts = true;
        UITween.Kill(group);
        UITween.Fade(group, 1f, 0.35f, Ease.OutQuad);
        yield return WaitUnscaled(0.35f);

        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;

        // Unity clamps progress at 0.9 while activation is blocked.
        while (op.progress < 0.9f)
        {
            Ease01(op.progress / 0.9f);
            yield return null;
        }

        // Let the bar finish visually even though the work is already done.
        while (displayed < 0.999f)
        {
            Ease01(1f);
            yield return null;
        }
        SetProgress(1f);

        if (waitForInput)
        {
            waitingForInput = true;
            hintLabel.text = "Press any key to continue";
            UIAudio.Play(UISound.Confirm, 0.6f);

            while (waitingForInput)
                yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        if (onLoaded != null) onLoaded();

        // Give the fresh scene one frame to draw before revealing it.
        yield return null;

        UITween.Fade(group, 0f, 0.6f, Ease.InQuad, () =>
        {
            if (group != null) group.blocksRaycasts = false;
            IsLoading = false;
        });
    }

    void Ease01(float target)
    {
        displayed = Mathf.MoveTowards(displayed, target, Time.unscaledDeltaTime * 0.85f);
        SetProgress(displayed);
    }

    void SetProgress(float value)
    {
        displayed = Mathf.Clamp01(value);
        if (barFill != null && barTrack != null)
            barFill.sizeDelta = new Vector2(barTrack.sizeDelta.x * displayed, 0f);
        if (percentLabel != null)
            percentLabel.text = Mathf.RoundToInt(displayed * 100f) + "%";
    }

    void Update()
    {
        if (!waitingForInput) return;

        // Pulse the hint so it reads as waiting rather than stuck.
        if (hintLabel != null)
        {
            var c = hintLabel.color;
            c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 1.6f));
            hintLabel.color = c;
        }

        if (AnyInputThisFrame())
        {
            waitingForInput = false;
            UIAudio.Play(UISound.Click);
        }
    }

    static bool AnyInputThisFrame()
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

    static IEnumerator WaitUnscaled(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
