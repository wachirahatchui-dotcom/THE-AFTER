using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The objective tracker in the top-left corner of the screen.
//
// Built in code and reached through a lazy singleton, the same way ScreenFader
// and TutorialPrompt are, so nothing has to be wired in the Inspector and it
// survives whatever the scene does around it.
//
// Colours come from MenuTheme rather than being picked here, so the tracker
// belongs to the same game as the menus instead of looking like a debug overlay
// bolted onto the corner.
//
// Wording is English on purpose: it is text the player reads inside the game,
// and the rest of the game's on-screen text is English too.
public class QuestUI : MonoBehaviour
{
    static QuestUI instance;

    CanvasGroup group;
    RectTransform panel;
    Image rule;
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

    Color Accent => MenuTheme.Current != null ? MenuTheme.Current.accentSoft : new Color(0.729f, 0.435f, 0.271f);
    Color Body => MenuTheme.Current != null ? MenuTheme.Current.parchmentLight : new Color(0.871f, 0.792f, 0.616f);
    Color Done => MenuTheme.Current != null ? MenuTheme.Current.positiveFill : new Color(0.639f, 0.757f, 0.573f);

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Below the fader, so a fade to black covers the tracker too - an
        // objective left burning over a black screen reads as a bug.
        canvas.sortingOrder = 30900;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = MenuTheme.Current != null
                                   ? MenuTheme.Current.referenceResolution
                                   : new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

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
        panel.sizeDelta = new Vector2(560f, 116f);

        // A rule down the left edge. It carries the state - warm while the
        // objective is open, green once it is done - which is legible at a glance
        // from the corner of the eye in a way that a word never is.
        var ruleGo = new GameObject("Rule", typeof(RectTransform), typeof(Image));
        ruleGo.transform.SetParent(panel, false);
        rule = ruleGo.GetComponent<Image>();
        rule.raycastTarget = false;
        var rr = rule.rectTransform;
        rr.anchorMin = new Vector2(0f, 0f);
        rr.anchorMax = new Vector2(0f, 1f);
        rr.pivot = new Vector2(0f, 0.5f);
        rr.sizeDelta = new Vector2(3f, -10f);
        rr.anchoredPosition = Vector2.zero;

        heading = Line("Heading", 20, TextAnchor.UpperLeft, new Vector2(20f, 0f), 26f);
        objective = Line("Objective", 30, TextAnchor.UpperLeft, new Vector2(20f, -30f), 76f);
    }

    Text Line(string name, int size, TextAnchor anchor, Vector2 offset, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Shadow), typeof(Text));
        go.transform.SetParent(panel, false);

        var text = go.GetComponent<Text>();
        text.font = GameFont.GetBody();
        text.fontSize = size;
        text.alignment = anchor;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        // The camp is bright in places and dark in others; plain text loses its
        // edges against both.
        var shadow = go.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(2f, -2f);

        var rt = text.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.offsetMin = new Vector2(offset.x, -height);
        rt.offsetMax = new Vector2(0f, 0f);
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

        // Slide in from the edge it lives on.
        panel.anchoredPosition = new Vector2(Left - slideFrom, Top);
        target = 1f;
    }

    /// Marks it done and takes it away after a beat, so the player sees that it
    /// was the thing they just did rather than the panel simply vanishing.
    public void Complete(float holdSeconds = 1.6f)
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
        objective.color = new Color(Body.r, Body.g, Body.b, 0.55f);

        float t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

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
