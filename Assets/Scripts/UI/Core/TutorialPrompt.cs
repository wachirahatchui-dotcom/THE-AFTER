using UnityEngine;
using UnityEngine.UI;

// The line of text that tells the player what to press.
//
// Built in code and reached through a lazy singleton, the same way ScreenFader
// is, so nothing has to be wired in the Inspector and it survives whatever the
// scene does around it. One line at a time is deliberate: two instructions on
// screen at once and the player reads neither.
public class TutorialPrompt : MonoBehaviour
{
    static TutorialPrompt instance;

    CanvasGroup group;
    Text label;
    Outline edge;
    RectTransform labelRt;
    float target;

    [Tooltip("How quickly the prompt fades in and out.")]
    public float fadeSpeed = 4f;

    [Tooltip("Pixels the line settles down from as it appears.")]
    public float riseDistance = 14f;

    public static TutorialPrompt I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~TutorialPrompt");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<TutorialPrompt>();
                instance.Build();
            }
            return instance;
        }
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Under the fader, so a fade to black covers the prompt too - a hint left
        // burning over a black screen between shots looks like a bug.
        canvas.sortingOrder = 31000;

        // Snapped to whole pixels - the same fix that keeps the captions sharp.
        // A dynamic font rasterised across half a pixel is what makes text look
        // soft, and it is the one setting that fixes that for a screen-space overlay.
        canvas.pixelPerfect = true;

        var theme = MenuTheme.Current;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme != null ? theme.referenceResolution : new Vector2(1920f, 1080f);

        // Height-only, like the captions: a hint has to stay the same size
        // relative to the picture regardless of the window's aspect ratio.
        scaler.matchWidthOrHeight = 1f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var go = new GameObject("Label", typeof(RectTransform), typeof(Outline), typeof(Text));
        go.transform.SetParent(transform, false);

        label = go.GetComponent<Text>();

        // The game's own display face, not the small-text sans. GetBody() exists
        // for text set small enough that NewTegomin's serifs turn to mush - a
        // hint is set well above that size, and in the plain system sans it read
        // like a debug label bolted onto a game set in a different typeface.
        label.font = GameFont.Get();
        label.fontSize = theme != null ? theme.tutorialFontSize : 38;
        label.alignment = TextAnchor.LowerCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        // An outline instead of the old offset-copy trick: one Text component
        // instead of two, and a genuine border on every side of every letter -
        // drawn at the four diagonals of effectDistance - rather than a shadow
        // that only reads against the bright half of whatever is behind it.
        edge = go.GetComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.85f);

        // Proportional to the type rather than a fixed pixel count, so the
        // border keeps the same weight against the letters if the size in the
        // theme ever changes, instead of turning into a hairline or a blob.
        float ring = Mathf.Max(1.5f, label.fontSize * 0.08f);
        edge.effectDistance = new Vector2(ring, -ring);
        edge.useGraphicAlpha = true;

        labelRt = label.rectTransform;
        labelRt.anchorMin = new Vector2(0.5f, 0f);
        labelRt.anchorMax = new Vector2(0.5f, 0f);
        labelRt.pivot = new Vector2(0.5f, 0f);
        labelRt.sizeDelta = new Vector2(1400f, 120f);
    }

    public void Show(string message)
    {
        label.text = message;
        target = 1f;
    }

    public void Hide() => target = 0f;

    void Update()
    {
        if (group == null) return;

        group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.unscaledDeltaTime);

        // A small settle-and-pop rather than a flat fade, eased so the motion
        // arrives slowing down instead of at a constant speed and stopping dead -
        // the one touch that makes the line feel placed rather than switched on.
        float eased = 1f - (1f - group.alpha) * (1f - group.alpha);
        labelRt.anchoredPosition = new Vector2(0f, 140f - (1f - eased) * riseDistance);
        labelRt.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, eased);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
