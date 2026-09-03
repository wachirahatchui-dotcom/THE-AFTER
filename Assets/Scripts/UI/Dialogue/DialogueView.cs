using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Everything the dialogue box is made of - and nothing about what it says.
//
// Visual language: the menu's parchment palette, inverted. The panel is a
// translucent ink slab so the scene still reads behind it, edged with a
// parchment border and topped by a parchment nameplate that straddles the top
// edge. Same materials as the menu, arranged so they can sit over live
// gameplay without blocking it.
//
// Layers, back to front:
//   Box            animated root (CanvasGroup + UIPanel)
//     Shadow       soft drop under the slab
//     Fill         translucent ink, rounded, paper grain, parchment outline
//     Nameplate    parchment card on the top edge
//     LineHost     the spoken line (own CanvasGroup so it can flick per line)
//     SkipButton / KeyHint / NextButton
//   Prompt         the "Press E" pill, outside Box so it survives its exits
//
// Every colour and measurement comes from MenuTheme.Current, so retuning the
// look never means editing this file. See the "Dialogue box" section of
// Assets/Resources/MenuTheme.asset.
public class DialogueView : MonoBehaviour
{
    public Action onAdvance;
    public Action onSkip;

    // 0 = follow MenuTheme.asset. Set from the DialogueManager inspector field
    // so a scene can slow the box down without touching the shared theme.
    public float durationOverride;

    public UIPanel Panel { get; private set; }
    public DialogueTextRevealer Revealer { get; private set; }

    RectTransform boxRect;
    CanvasGroup boxGroup;

    Image nameplate;
    Text nameLabel;

    RectTransform lineHost;
    CanvasGroup lineGroup;
    Text lineLabel;

    Button nextButton;
    Text nextLabel;
    Text nextArrow;
    RectTransform arrowRect;
    Vector2 arrowHome;
    bool arrowPulsing;

    CanvasGroup promptGroup;
    RectTransform promptRect;
    Text promptLabel;
    Vector2 promptHome;
    bool promptVisible;

    string speaker = "";

    // ------------------------------------------------------------------ build
    public void Build()
    {
        DialogueAnimations.EnsureRegistered();

        var theme = MenuTheme.Current;

        var canvasGo = new GameObject("DialogueCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.referenceResolution;
        scaler.matchWidthOrHeight = theme.scalerMatch;

        BuildPrompt(canvasGo.transform, theme);
        BuildBox(canvasGo.transform, theme);
    }

    // The floating "Press E to talk" pill. Parchment side up, because it is an
    // invitation rather than speech.
    void BuildPrompt(Transform parent, MenuThemeAsset theme)
    {
        var pill = UIFactory.NewRounded("Prompt", parent, GameUITheme.Parchment, 10);
        promptRect = pill.rectTransform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);
        promptRect.anchoredPosition = new Vector2(0f, theme.dialoguePromptY);
        promptRect.sizeDelta = new Vector2(460f, 52f);
        promptHome = promptRect.anchoredPosition;

        UIFactory.AddOutline(pill, GameUITheme.Ink, 2, 10);

        promptGroup = pill.gameObject.AddComponent<CanvasGroup>();
        promptGroup.alpha = 0f;
        promptGroup.blocksRaycasts = false;
        promptGroup.interactable = false;

        promptLabel = UIFactory.NewBodyText("Label", pill.transform, "", theme.dialoguePromptFontSize,
            FontStyle.Bold, TextAnchor.MiddleCenter, GameUITheme.Ink);
        UIFactory.Stretch(promptLabel.rectTransform, 8f);

        pill.gameObject.SetActive(false);
    }

    void BuildBox(Transform parent, MenuThemeAsset theme)
    {
        // ---- animated root
        boxRect = UIFactory.NewRect("DialogueBox", parent);
        boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0f);
        boxRect.pivot = new Vector2(0.5f, 0f);
        boxRect.anchoredPosition = new Vector2(0f, theme.dialogueBottomMargin);
        boxRect.sizeDelta = theme.dialoguePanelSize;

        boxGroup = boxRect.gameObject.AddComponent<CanvasGroup>();
        boxGroup.alpha = 0f;

        Panel = boxRect.gameObject.AddComponent<UIPanel>();
        Panel.SetTransition(DialogueAnimations.Unfurl, DialogueAnimations.FoldAway,
                            theme.dialogueOpenDuration);
        // Left null on purpose: the box is driven by DialogueManager reading the
        // keyboard directly, and a selected NEXT button would make Space fire
        // both that button and the manager, advancing two lines at once.
        Panel.firstSelected = null;

        // ---- shadow
        var shadow = UIFactory.NewImage("Shadow", boxRect, UIFactory.Shadow);
        shadow.sprite = UIGfx.RoundedRect(theme.cornerRadius, 0);
        shadow.type = Image.Type.Sliced;
        shadow.pixelsPerUnitMultiplier = 1f;
        shadow.raycastTarget = false;
        UIFactory.Stretch(shadow.rectTransform, -8f);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -10f);

        // ---- translucent ink slab
        var fill = UIFactory.NewRounded("Fill", boxRect, theme.dialogueFill);
        UIFactory.Stretch(fill.rectTransform);

        var grain = UIFactory.NewImage("Grain", fill.transform,
            new Color(1f, 1f, 1f, theme.dialogueGrainOpacity));
        grain.sprite = UIGfx.PaperFibre(256);
        grain.type = Image.Type.Tiled;
        grain.raycastTarget = false;
        UIFactory.Stretch(grain.rectTransform, 4f);

        UIFactory.AddOutline(fill, theme.dialogueBorder, 3);

        // ---- nameplate, straddling the top edge
        nameplate = UIFactory.NewRounded("Nameplate", boxRect, theme.dialogueNameFill, 8);
        var npRect = nameplate.rectTransform;
        npRect.anchorMin = npRect.anchorMax = new Vector2(0f, 1f);
        npRect.pivot = new Vector2(0f, 0.5f);
        npRect.anchoredPosition = new Vector2(46f, 0f);
        npRect.sizeDelta = new Vector2(240f, theme.dialogueNameFontSize + 20f);

        UIFactory.AddOutline(nameplate, theme.dialogueBorder, 2, 8);

        nameLabel = UIFactory.NewText("Label", nameplate.transform, "", theme.dialogueNameFontSize,
            FontStyle.Bold, TextAnchor.MiddleCenter, theme.dialogueNameText);
        nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.Stretch(nameLabel.rectTransform);

        // ---- the line itself
        lineHost = UIFactory.NewRect("LineHost", boxRect);
        lineHost.anchorMin = Vector2.zero;
        lineHost.anchorMax = Vector2.one;
        lineHost.offsetMin = new Vector2(52f, 76f);
        lineHost.offsetMax = new Vector2(-52f, -46f);
        lineGroup = lineHost.gameObject.AddComponent<CanvasGroup>();

        lineLabel = UIFactory.NewText("Line", lineHost, "", theme.dialogueLineFontSize,
            FontStyle.Normal, TextAnchor.UpperLeft, theme.dialogueText);
        lineLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
        lineLabel.verticalOverflow = VerticalWrapMode.Truncate;
        lineLabel.lineSpacing = 1.15f;
        UIFactory.Stretch(lineLabel.rectTransform);

        lineLabel.gameObject.AddComponent<TypewriterFade>();
        Revealer = lineLabel.gameObject.AddComponent<DialogueTextRevealer>();

        // ---- bottom row: skip on the left, next on the right.
        // No key-hint line: the box is driven entirely by these two buttons.
        var skip = UIFactory.SmallButton(boxRect, "SKIP", InvokeSkip, new Vector2(112f, 42f));
        var skipRect = (RectTransform)skip.transform;
        skipRect.anchorMin = skipRect.anchorMax = Vector2.zero;
        skipRect.pivot = Vector2.zero;
        skipRect.anchoredPosition = new Vector2(46f, 20f);

        nextButton = UIFactory.MenuButton(boxRect, "NEXT", InvokeAdvance, MenuFxStyle.Soft, 56f, 26);
        var nextRect = (RectTransform)nextButton.transform;
        nextRect.anchorMin = nextRect.anchorMax = new Vector2(1f, 0f);
        nextRect.pivot = new Vector2(1f, 0f);
        nextRect.anchoredPosition = new Vector2(-42f, 20f);
        nextRect.sizeDelta = new Vector2(216f, 56f);

        nextLabel = nextButton.transform.Find("Label").GetComponent<Text>();
        nextLabel.rectTransform.offsetMax = new Vector2(-34f, 0f);

        nextArrow = UIFactory.NewText("Arrow", nextButton.transform, "▶", 22,
            FontStyle.Normal, TextAnchor.MiddleCenter, nextLabel.color);
        arrowRect = nextArrow.rectTransform;
        arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(1f, 0.5f);
        arrowRect.pivot = new Vector2(1f, 0.5f);
        arrowRect.anchoredPosition = new Vector2(-22f, 0f);
        arrowRect.sizeDelta = new Vector2(28f, 28f);
        arrowHome = arrowRect.anchoredPosition;

        boxRect.gameObject.SetActive(false);
    }

    // ------------------------------------------------------------------ open
    public void Open(bool firstMeeting)
    {
        var theme = MenuTheme.Current;
        Panel.duration = durationOverride > 0f ? durationOverride : theme.dialogueOpenDuration;
        Panel.inAnim = DialogueAnimations.PickOpen(firstMeeting);
        Panel.Show();
    }

    public void Close(DialogueAnimations.Exit reason, Action onClosed = null)
    {
        SetArrowPulsing(false);

        var theme = MenuTheme.Current;
        Panel.duration = durationOverride > 0f ? durationOverride : theme.dialogueCloseDuration;
        Panel.outAnim = DialogueAnimations.PickClose(reason);
        Panel.Hide(() =>
        {
            if (Revealer != null) Revealer.Clear();
            onClosed?.Invoke();
        });
    }

    public void CloseInstant()
    {
        SetArrowPulsing(false);
        if (Revealer != null) Revealer.Clear();
        if (Panel != null) Panel.HideInstant();
    }

    // ------------------------------------------------------------------ line
    // speakerChanged is returned rather than taken, because the view is the
    // thing that knows who was on the plate a moment ago.
    public bool SetSpeaker(string name)
    {
        bool changed = speaker != name;
        speaker = name;
        nameLabel.text = name;

        // The plate is sized to the name, not the other way round.
        float width = Mathf.Max(160f, nameLabel.preferredWidth + 52f);
        nameplate.rectTransform.sizeDelta =
            new Vector2(width, MenuTheme.Current.dialogueNameFontSize + 20f);

        if (changed)
            UITween.Punch(nameplate.transform, 0.08f, 0.3f, Vector3.one);

        return changed;
    }

    public void ShowLine(string line, int lineIndex, bool speakerChanged, bool isLast)
    {
        var theme = MenuTheme.Current;

        SetArrowPulsing(false);
        SetNextLabel(isLast);

        DialogueAnimations.PlayLineChange(lineHost, lineGroup, lineIndex, speakerChanged,
                                          theme.dialogueLineChangeDuration);

        Revealer.charactersPerSecond = CharactersPerSecond;
        Revealer.Play(line, GameSettings.DialogueInstant, GameSettings.Subtitles);
    }

    public float CharactersPerSecond { get; set; } = 35f;

    public void CompleteLine()
    {
        Revealer.Complete();
    }

    void SetNextLabel(bool isLast)
    {
        nextLabel.text = isLast ? "END" : "NEXT";
        nextArrow.gameObject.SetActive(!isLast);
        UIFactory.ApplyButtonTone(nextButton,
            isLast ? UIFactory.MenuTone.Positive : UIFactory.MenuTone.Neutral);

        // ApplyButtonTone recolours the Label; the arrow has to follow it.
        nextArrow.color = nextLabel.color;
    }

    // The arrow only breathes once the line has finished typing - that is the
    // whole point of it, so the player can tell "still talking" from "your move".
    public void SetArrowPulsing(bool on)
    {
        arrowPulsing = on;
        if (arrowRect == null) return;

        if (!on)
        {
            arrowRect.anchoredPosition = arrowHome;
            var c = nextArrow.color;
            c.a = 1f;
            nextArrow.color = c;
        }
    }

    // --------------------------------------------------------------- prompt
    public void ShowPrompt(string text)
    {
        if (promptRect == null) return;

        promptLabel.text = text;
        promptRect.sizeDelta = new Vector2(Mathf.Max(300f, promptLabel.preferredWidth + 64f), 52f);

        if (promptVisible) return;
        promptVisible = true;

        promptRect.gameObject.SetActive(true);
        UITween.Kill(promptGroup);
        UITween.Fade(promptGroup, 1f, 0.2f, Ease.OutCubic);
        UITween.ScaleFrom(promptRect, Vector3.one * 0.88f, 0.24f, Ease.OutBack);
    }

    public void HidePrompt()
    {
        if (promptRect == null || !promptVisible) return;
        promptVisible = false;

        UITween.Kill(promptGroup);
        UITween.Fade(promptGroup, 0f, 0.15f, Ease.InQuad, () =>
        {
            if (promptRect != null && !promptVisible) promptRect.gameObject.SetActive(false);
        });
    }

    // ---------------------------------------------------------------- update
    void Update()
    {
        float t = Time.unscaledTime;
        var theme = MenuTheme.Current;

        if (arrowPulsing && arrowRect != null)
        {
            float w = Mathf.Sin(t * theme.dialogueArrowPulseSpeed);
            arrowRect.anchoredPosition = arrowHome + new Vector2(w * 5f, 0f);

            var c = nextArrow.color;
            c.a = 0.55f + 0.45f * (0.5f + 0.5f * w);
            nextArrow.color = c;
        }

        if (promptVisible && promptRect != null)
            promptRect.anchoredPosition = promptHome
                + new Vector2(0f, Mathf.Sin(t * 2.2f) * theme.dialoguePromptBob);
    }

    // ---------------------------------------------------------------- clicks
    // Clicking a button also selects it, and a selected button treats the next
    // Space as its own Submit - which would advance twice. Dropping the
    // selection immediately keeps the keyboard path single-fire.
    void InvokeAdvance()
    {
        Deselect();
        onAdvance?.Invoke();
    }

    void InvokeSkip()
    {
        Deselect();
        onSkip?.Invoke();
    }

    static void Deselect()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void OnDestroy()
    {
        DialogueAnimations.Forget(boxRect);
        DialogueAnimations.Forget(lineHost);
    }
}
