using UnityEngine;
using UnityEngine.UI;

// A small frame rate readout in the top-right corner.
//
// Shows a rolling average rather than the instantaneous 1/deltaTime, which
// jitters far too much to read, plus the worst frame in the last second - the
// number that actually corresponds to visible stutter.
//
// Colour-coded against the current target: green at or above it, amber within
// two thirds, red below.
public class FpsCounter : MonoBehaviour
{
    const float SampleWindow = 0.5f;

    static FpsCounter instance;

    Text label;
    CanvasGroup group;

    float accumulated;
    int frames;
    float timer;
    float worstFrameTime;

    public static void SetVisible(bool visible)
    {
        if (!visible)
        {
            if (instance != null) instance.Show(false);
            return;
        }

        Ensure();
        instance.Show(true);
    }

    static void Ensure()
    {
        if (instance != null) return;

        var go = new GameObject("~FpsCounter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FpsCounter>();
        instance.Build();
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30500;          // over the HUD, under the fader and the pause menu

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = MenuTheme.Current.referenceResolution;
        scaler.matchWidthOrHeight = MenuTheme.Current.scalerMatch;

        group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 0f;

        var plate = UIFactory.NewRounded("Plate", transform, new Color(0f, 0f, 0f, 0.45f), 6);
        plate.raycastTarget = false;
        var pRt = plate.rectTransform;
        pRt.anchorMin = pRt.anchorMax = new Vector2(1f, 1f);
        pRt.pivot = new Vector2(1f, 1f);
        pRt.anchoredPosition = new Vector2(-24f, -24f);
        pRt.sizeDelta = new Vector2(190f, 48f);

        label = UIFactory.NewText("Label", plate.transform, "-- FPS", 22, FontStyle.Bold,
                                  TextAnchor.MiddleCenter, Color.white);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.Stretch(label.rectTransform);
    }

    void Show(bool visible)
    {
        if (group == null) return;

        UITween.Kill(group);
        UITween.Fade(group, visible ? 1f : 0f, 0.2f, Ease.OutQuad);
        enabled = visible;

        if (visible) { accumulated = 0f; frames = 0; timer = 0f; worstFrameTime = 0f; }
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        accumulated += 1f / dt;
        frames++;
        timer += dt;
        if (dt > worstFrameTime) worstFrameTime = dt;

        if (timer < SampleWindow) return;

        float average = accumulated / Mathf.Max(1, frames);
        float worst = worstFrameTime > 0f ? 1f / worstFrameTime : average;

        if (label != null)
        {
            label.text = Mathf.RoundToInt(average) + " FPS   " + Mathf.RoundToInt(worst) + " low";
            label.color = ColourFor(average);
        }

        accumulated = 0f;
        frames = 0;
        timer = 0f;
        worstFrameTime = 0f;
    }

    // Compared against whatever the player asked for: the vsync'd refresh rate,
    // an explicit cap, or 60 as a sane default when uncapped.
    static Color ColourFor(float fps)
    {
        float target = 60f;

        if (GameSettings.VSync)
            target = Mathf.Max(30f, (float)Screen.currentResolution.refreshRateRatio.value);
        else if (GameSettings.FrameRateCap > 0)
            target = GameSettings.FrameRateCap;

        if (fps >= target * 0.95f) return new Color(0.60f, 0.87f, 0.55f, 1f);
        if (fps >= target * 0.66f) return new Color(0.93f, 0.80f, 0.42f, 1f);
        return new Color(0.90f, 0.45f, 0.40f, 1f);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
