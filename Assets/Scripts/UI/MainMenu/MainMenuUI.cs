using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

// The main menu orchestrator.
//
// This file owns only the shell: the canvas, the title block, the button
// column, the page stack and the scene transition. Each page lives in its own
// file next to this one:
//
//   SaveLoadPage.cs    SAVE / LOAD slots
//   OptionsPage.cs     DISPLAY / AUDIO / INTERFACE tabs
//   CreditsPage.cs     scrolling credits
//   ConfirmDialog.cs   shared yes/no dialog
//   MainMenuIntro.cs   the opening animation
//
// Look and feel is not configured here - it comes from MenuTheme.Current,
// i.e. Assets/Resources/MenuTheme.asset. See UI/Theme/MenuThemeAsset.cs.
// Player settings live in Settings/GameSettings.cs, saves in SaveData/.
//
// Nothing needs wiring in the scene: drop this on an empty GameObject.
public class MainMenuUI : MonoBehaviour, IMenuHost
{
    [Header("Theme")]
    [Tooltip("Optional. Leave empty to use Assets/Resources/MenuTheme.asset, " +
             "or the built-in defaults if that does not exist either.")]
    public MenuThemeAsset themeAsset;

    [Header("Scenes")]
    [Tooltip("Scene loaded by PLAY and CONTINUE.")]
    public string gameSceneName = "Sandbox";

    [Header("Branding")]
    public string titleText = "THE AFTER";
    public string subtitleText = "A world after the collapse";

    [Header("Splash (shown once per launch)")]
    public bool showSplash = true;
    public string studioName = "The Lazy Group";
    public string splashTagline = "presents";
    public float splashHoldSeconds = 1.8f;

    [Tooltip("Shown on the CREDITS page. Lines starting with # render as headings.")]
    public string[] creditsLines =
    {
        "#THE AFTER",
        "",
        "#Design and Programming",
        "The Lazy Group",
        "",
        "#Art and Environments",
        "The Lazy Group",
        "",
        "#Typeface",
        "New Tegomin  ~  SIL Open Font License",
        "",
        "#Built With",
        "Unity 6  ~  Universal Render Pipeline",
        "",
        "#Thank You",
        "For playing.",
    };

    // --------------------------------------------------------------- exposed
    // Pages and the intro read these; nothing outside UI/MainMenu should.
    public Transform CanvasRoot { get; private set; }
    public UIPanel MainPanel { get; private set; }
    public Text TitleLabel { get; private set; }

    // Set only when a logo file exists; the typed title is hidden in that case.
    public Image TitleLogo { get; private set; }

    // Whichever of the two is actually on screen, so the intro and the exit
    // animate the title without caring which one it is.
    public Graphic TitleGraphic
    {
        get { return TitleLogo != null ? (Graphic)TitleLogo : TitleLabel; }
    }

    public Image TitleRule { get; private set; }
    public CanvasGroup SubtitleGroup { get; private set; }
    public List<CanvasGroup> MainButtonGroups { get; private set; }

    public string PanelIn { get; private set; }
    public string PanelOut { get; private set; }

    public SaveLoadPage SavePage { get; private set; }
    public OptionsPage OptionsPage { get; private set; }
    public CreditsPage CreditsPage { get; private set; }
    public StageSelectPage StagePage { get; private set; }
    public ConfirmDialog Confirm { get; private set; }

    // ---------------------------------------------------------------- private
    CanvasGroup dimGroup;
    readonly List<UIPanel> stack = new List<UIPanel>();
    Button continueButton;

    // ================================================================== setup
    void Awake()
    {
        MenuTheme.Use(themeAsset);

        ApplySavedSettings();
        EnsureEventSystem();
        BuildUI();
    }

    void Start()
    {
        // The splash covers the menu while it builds itself underneath, so the
        // title screen is already warm when it clears. Returns false when it
        // has already run this session (coming back from the game).
        bool splashed = showSplash && SplashScreen.Show(studioName, splashTagline);
        float delay = splashed ? splashHoldSeconds + 1.4f : 0.02f;

        // Deferred at least one frame: MenuButtonFX caches each button's
        // resting position in its own Start, and script Start order is
        // undefined, so the intro must not move anything until they have run.
        UITween.Delay(this, delay, () => MainMenuIntro.Play(this));
    }

    void ApplySavedSettings()
    {
        // Older builds stored a single "Volume"; fold it into the new master.
        if (!PlayerPrefs.HasKey(UIAudio.K_Master) && PlayerPrefs.HasKey(SettingsKeys.LegacyVolume))
            PlayerPrefs.SetFloat(UIAudio.K_Master, PlayerPrefs.GetFloat(SettingsKeys.LegacyVolume, 1f));

        // SettingsRuntime already pushed everything at the engine before the
        // first scene loaded; this is a no-op safety net for entering the menu
        // scene directly from the editor.
        SettingsStore.EnsureLoaded();

        PanelIn = GameSettings.MenuAnimation;
        PanelOut = PairedExit(PanelIn);
    }

    void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // Pairs each entrance with an exit that reads as the same motion reversed.
    public static string PairedExit(string enter)
    {
        switch ((enter ?? "").ToLowerInvariant())
        {
            case "slideleft":  return "SlideRight";
            case "slideright": return "SlideLeft";
            case "sinkfade":   return "RiseFade";
            case "risefade":   return "SinkFade";
            case "scalepop":
            case "elastic":
            case "inkstamp":   return "ScaleShrink";
            case "scaledrop":  return "ScaleBurst";
            case "unfold":
            case "unfoldwide":
            case "flipx":      return "Fold";
            case "instant":    return "Instant";
            default:           return "Fade";
        }
    }

    // ================================================================== build
    void BuildUI()
    {
        var theme = MenuTheme.Current;

        var canvasGo = new GameObject("MenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        CanvasRoot = canvasGo.transform;

        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.referenceResolution;
        scaler.matchWidthOrHeight = theme.scalerMatch;

        MenuBackgroundFX.Create(CanvasRoot);

        BuildTitle();
        BuildMainColumn();
        BuildDim();

        // Built after the dim layer, so each page darkens what is behind it
        // without darkening itself.
        SavePage = new SaveLoadPage(this);
        OptionsPage = new OptionsPage(this);
        CreditsPage = new CreditsPage(this);
        StagePage = new StageSelectPage(this);
        Confirm = new ConfirmDialog(this);

        BuildVersionLabel();

        SavePage.Panel.HideInstant();
        OptionsPage.Panel.HideInstant();
        CreditsPage.Panel.HideInstant();
        StagePage.Panel.HideInstant();
        Confirm.Panel.HideInstant();
    }

    // Bottom edge of the title block (title/logo + rule + subtitle), as a
    // negative offset from the top of the canvas. Set by BuildTitle, read by
    // BuildMainPanel.
    float titleBottom;

    void BuildTitle()
    {
        var theme = MenuTheme.Current;

        var group = UIFactory.NewRect("TitleGroup", CanvasRoot);
        group.anchorMin = group.anchorMax = new Vector2(0f, 1f);
        group.pivot = new Vector2(0f, 1f);
        group.anchoredPosition = new Vector2(theme.titleX, theme.titleY);
        group.sizeDelta = new Vector2(1100f, 260f);

        TitleLabel = UIFactory.NewText("Title", group, "", theme.titleFontSize, FontStyle.Bold,
                                       TextAnchor.UpperLeft, GameUITheme.Parchment);
        TitleLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var tRt = TitleLabel.rectTransform;
        tRt.anchorMin = tRt.anchorMax = new Vector2(0f, 1f);
        tRt.pivot = new Vector2(0f, 1f);
        tRt.anchoredPosition = Vector2.zero;
        tRt.sizeDelta = new Vector2(1100f, theme.titleFontSize + 32f);

        // A logo file, if one has been dropped in, replaces the typed title
        // entirely - see MenuLogo. Everything below is positioned against
        // whichever of the two ends up on screen.
        float blockHeight = theme.titleFontSize;

        if (MenuLogo.Exists)
        {
            var logoSprite = MenuLogo.Get();

            TitleLogo = UIFactory.NewImage("TitleLogo", group, theme.titleLogoTint);
            TitleLogo.sprite = logoSprite;
            TitleLogo.preserveAspect = true;
            TitleLogo.raycastTarget = false;

            float aspect = logoSprite.rect.height > 0f
                ? logoSprite.rect.width / logoSprite.rect.height
                : 2f;

            var lRt = TitleLogo.rectTransform;
            lRt.anchorMin = lRt.anchorMax = new Vector2(0f, 1f);
            lRt.pivot = new Vector2(0f, 1f);
            lRt.anchoredPosition = theme.titleLogoOffset;
            lRt.sizeDelta = new Vector2(theme.titleLogoHeight * aspect, theme.titleLogoHeight);

            // Faded in by MainMenuIntro, like the typed title it replaces.
            var faded = TitleLogo.color;
            faded.a = 0f;
            TitleLogo.color = faded;

            TitleLabel.gameObject.SetActive(false);

            // The logo rect hangs from the group's top edge, so a positive
            // offset lifts it and its bottom rises with it. Adding the offset
            // here (rather than subtracting) left a gap the size of the offset
            // between the logo and the rule.
            blockHeight = theme.titleLogoHeight - theme.titleLogoOffset.y;
        }

        // Thin rule that draws itself in under the title during the intro.
        TitleRule = UIFactory.NewImage("Rule", group, UIFactory.AccentSoft);
        TitleRule.raycastTarget = false;
        var rRt = TitleRule.rectTransform;
        rRt.anchorMin = rRt.anchorMax = new Vector2(0f, 1f);
        rRt.pivot = new Vector2(0f, 1f);
        rRt.anchoredPosition = new Vector2(4f, -(blockHeight + 28f));
        rRt.sizeDelta = new Vector2(0f, 3f);

        var subRect = UIFactory.NewRect("Subtitle", group);
        subRect.anchorMin = subRect.anchorMax = new Vector2(0f, 1f);
        subRect.pivot = new Vector2(0f, 1f);
        subRect.anchoredPosition = new Vector2(6f, -(blockHeight + 44f));
        subRect.sizeDelta = new Vector2(900f, 50f);

        // Where the whole title block ends, measured from the top of the screen.
        // The button column is placed under this rather than at a fixed offset:
        // a logo is far taller than the line of type it replaced, and the old
        // fixed columnTop put the first button straight through it.
        titleBottom = theme.titleY - (blockHeight + 44f + subRect.sizeDelta.y);

        SubtitleGroup = subRect.gameObject.AddComponent<CanvasGroup>();
        SubtitleGroup.alpha = 0f;

        var sub = UIFactory.NewText("Text", subRect, subtitleText, theme.subtitleFontSize,
                                    FontStyle.Italic, TextAnchor.UpperLeft, theme.subtitleColor);
        sub.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.Stretch(sub.rectTransform);
    }

    void BuildMainColumn()
    {
        var theme = MenuTheme.Current;
        MainButtonGroups = new List<CanvasGroup>();

        var root = UIFactory.NewRect("MainPanel", CanvasRoot);
        // Anchored to the TOP edge, a fixed distance under the title. Anchoring
        // it to the middle collided with the subtitle on short viewports,
        // because the gap between title and centre shrinks with height.
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        // columnTop is a minimum, not a position: whichever sits lower, the
        // configured offset or the bottom of the title block, wins. Both are
        // negative, so "lower" is the smaller number.
        float top = Mathf.Min(theme.columnTop, titleBottom - theme.columnGapUnderTitle);

        root.anchoredPosition = new Vector2(theme.columnX, top);
        root.sizeDelta = new Vector2(theme.buttonWidth, theme.buttonSpacing * 6f);

        MainPanel = UIFactory.MakePanel(root.gameObject, "Instant", "Fade", 0.25f);
        MainPanel.ShowInstant();

        int row = 0;

        // CONTINUE only appears when there is something to continue.
        int recent = SaveSystem.MostRecentSlot();
        if (recent >= 0)
        {
            continueButton = ColumnButton(root, "CONTINUE", row++, () => LoadSlot(recent));
            var data = SaveSystem.Read(recent);
            if (data != null)
                AddButtonHint(continueButton, data.chapterName + "  ~  " + data.PlayTimeText);
        }

        ColumnButton(root, "PLAY", row++, OnPlay);

        // Only when there is a catalog to show. An empty stage select is worse
        // than no stage select: it looks like the feature is broken rather than
        // like it has not been set up.
        //
        // Asked of the catalog rather than of StagePage, which does not exist
        // yet - the pages are built after this column so that each of them
        // darkens what is behind it without darkening itself.
        var stages = StageCatalog.Load();
        if (stages != null && stages.stages != null && stages.stages.Length > 0)
            ColumnButton(root, "STAGE SELECT", row++, OnOpenStages);

        ColumnButton(root, "LOAD GAME", row++, OnOpenSave);
        ColumnButton(root, "OPTIONS", row++, OnOpenOptions);
        ColumnButton(root, "CREDITS", row++, OnOpenCredits);
        ColumnButton(root, "EXIT", row++, OnExit, true);

        MainPanel.firstSelected = continueButton != null
            ? continueButton.gameObject
            : root.GetChild(0).gameObject;
    }

    // A left-aligned menu row. Positioned by hand rather than by a layout
    // group, because the Slide reaction animates anchoredPosition and a layout
    // group would fight it every frame.
    Button ColumnButton(Transform parent, string label, int index, Action onClick, bool danger = false)
    {
        var theme = MenuTheme.Current;

        var btn = UIFactory.MenuButton(parent, label, () => onClick(),
                                       MenuFxStyle.Classic, theme.buttonHeight,
                                       theme.buttonFontSize, danger);

        var rt = (RectTransform)btn.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(theme.buttonWidth, theme.buttonHeight);
        rt.anchoredPosition = new Vector2(0f, -index * theme.buttonSpacing);

        var labelT = btn.transform.Find("Label");
        if (labelT != null)
        {
            var t = labelT.GetComponent<Text>();
            t.alignment = TextAnchor.MiddleLeft;
            t.rectTransform.offsetMin = new Vector2(58f, 0f);
            t.rectTransform.offsetMax = new Vector2(-18f, 0f);
        }

        // Hidden until the intro brings it in.
        var cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        MainButtonGroups.Add(cg);

        return btn;
    }

    // Small secondary line under a button label (used by CONTINUE).
    void AddButtonHint(Button btn, string hint)
    {
        var label = btn.transform.Find("Label");
        if (label == null) return;

        var t = label.GetComponent<Text>();
        t.alignment = TextAnchor.LowerLeft;
        t.rectTransform.offsetMin = new Vector2(58f, 22f);

        var sub = UIFactory.NewText("Hint", btn.transform, hint, 18, FontStyle.Italic,
                                    TextAnchor.UpperLeft, GameUITheme.InkSoft);
        sub.horizontalOverflow = HorizontalWrapMode.Overflow;
        var rt = sub.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(58f, 8f);
        rt.offsetMax = new Vector2(-18f, -44f);
    }

    void BuildDim()
    {
        var img = UIFactory.NewImage("Dim", CanvasRoot, MenuTheme.Current.dimColor);
        UIFactory.Stretch(img.rectTransform);
        dimGroup = img.gameObject.AddComponent<CanvasGroup>();
        dimGroup.alpha = 0f;
        dimGroup.blocksRaycasts = false;
    }

    void BuildVersionLabel()
    {
        var c = MenuTheme.Current.subtitleColor;
        c.a = 0.45f;

        var t = UIFactory.NewText("Version", CanvasRoot,
            "v" + Application.version + "   ~   Unity " + Application.unityVersion,
            18, FontStyle.Normal, TextAnchor.LowerRight, c);
        t.horizontalOverflow = HorizontalWrapMode.Overflow;

        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-32f, 24f);
        rt.sizeDelta = new Vector2(700f, 30f);
    }

    // ============================================================ navigation
    public void OpenPanel(UIPanel panel)
    {
        if (panel == null || stack.Contains(panel)) return;

        if (stack.Count == 0)
        {
            MainPanel.Group.interactable = false;
            UITween.Fade(dimGroup, 1f, MenuTheme.Current.dimFadeDuration, Ease.OutQuad);
            dimGroup.blocksRaycasts = true;
        }

        stack.Add(panel);
        UIAudio.Play(UISound.PanelOpen, 0.8f);
        panel.Show();
    }

    public void CloseTop()
    {
        if (stack.Count == 0) return;

        var panel = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);

        UIAudio.Play(UISound.PanelClose, 0.8f);
        panel.Hide();

        if (stack.Count == 0)
        {
            UITween.Fade(dimGroup, 0f, MenuTheme.Current.dimFadeDuration, Ease.OutQuad);
            dimGroup.blocksRaycasts = false;
            MainPanel.Group.interactable = true;
            Focus(MainPanel.firstSelected);
        }
        else
        {
            Focus(stack[stack.Count - 1].firstSelected);
        }
    }

    public void ShowConfirm(string title, string message, Action onAccept)
    {
        Confirm.Show(title, message, onAccept);
    }

    static void Focus(GameObject go)
    {
        if (go != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    // Applied by OPTIONS > INTERFACE. Retargets every page at once.
    public void SetPanelTransition(string enter)
    {
        PanelIn = enter;
        PanelOut = PairedExit(enter);
        GameSettings.SetMenuAnimation(PanelIn);

        SavePage.Panel.SetTransition(PanelIn, PanelOut);
        OptionsPage.Panel.SetTransition(PanelIn, PanelOut);
        CreditsPage.Panel.SetTransition(PanelIn, PanelOut);
        StagePage.Panel.SetTransition(PanelIn, PanelOut);
        Confirm.Panel.SetTransition(PanelIn, PanelOut);
    }

    // Rebuilds the options page from scratch. Its rows are constructed from the
    // saved values, so a reset has to recreate them rather than poke each one.
    public void RebuildOptionsPage()
    {
        bool wasOpen = stack.Contains(OptionsPage.Panel);
        int tab = OptionsPage.ActiveTab;
        int sibling = OptionsPage.Panel.transform.GetSiblingIndex();

        if (wasOpen) stack.Remove(OptionsPage.Panel);
        OptionsPage.Dispose();   // stop listening to SettingsStore
        Destroy(OptionsPage.Panel.gameObject);

        OptionsPage = new OptionsPage(this);
        OptionsPage.Panel.HideInstant();
        OptionsPage.Panel.transform.SetSiblingIndex(sibling);

        if (wasOpen)
        {
            stack.Add(OptionsPage.Panel);
            OptionsPage.Panel.Show();
            OptionsPage.SelectTab(tab, false);
        }
    }

    // =============================================================== handlers
    void OnPlay()
    {
        if (SaveSystem.AnyExists())
        {
            ShowConfirm("NEW GAME",
                "Starting a new game does not erase your saves,\n" +
                "but unsaved progress in this session is lost.",
                () => { PlayTimeTracker.Reset(); StartGame(gameSceneName); });
            return;
        }

        PlayTimeTracker.Reset();
        StartGame(gameSceneName);
    }

    public void LoadSlot(int index)
    {
        var data = SaveSystem.Read(index);
        if (data == null) { UIAudio.Play(UISound.Error); return; }

        SaveSystem.QueueLoad(data);
        PlayerPrefs.SetString(SettingsKeys.LastChapter, data.chapterName);
        StartGame(string.IsNullOrEmpty(data.sceneName) ? gameSceneName : data.sceneName,
                  data.chapterName);
    }

    public void StartGame(string sceneName)
    {
        StartGame(sceneName, PlayerPrefs.GetString(SettingsKeys.LastChapter, "Chapter 1"));
    }

    public void StartGame(string sceneName, string chapterName)
    {
        PlayerPrefs.Save();
        UIAudio.Play(UISound.GameStart, 1f);
        UIAudio.StopMusic(0.8f);

        // Title and buttons leave before the loading screen arrives, so the
        // exit reads as deliberate rather than as the screen going dark.
        UITween.Fade(MainPanel.Group, 0f, 0.45f, Ease.InQuad);
        UITween.Fade(SubtitleGroup, 0f, 0.4f, Ease.InQuad);
        UITween.ScaleTo(TitleGraphic.transform, Vector3.one * 1.06f, 0.9f, Ease.InOutQuad);
        UITween.FadeGraphic(TitleGraphic, 0f, 0.8f, Ease.InQuad);

        // Async, with a progress bar. The old synchronous LoadScene froze the
        // last menu frame on screen for the whole load.
        UITween.Delay(this, 0.5f, () => LoadingScreen.Load(sceneName, chapterName));
    }

    void OnOpenSave()
    {
        SavePage.Refresh();
        OpenPanel(SavePage.Panel);
    }

    void OnOpenOptions()
    {
        OpenPanel(OptionsPage.Panel);
        OptionsPage.SelectTab(OptionsPage.ActiveTab, false);
    }

    void OnOpenCredits()
    {
        OpenPanel(CreditsPage.Panel);
        CreditsPage.RestartScroll();
    }

    void OnOpenStages()
    {
        OpenPanel(StagePage.Panel);
    }

    void OnExit()
    {
        ShowConfirm("EXIT GAME", "Leave " + titleText + " and return to the desktop?", () =>
        {
            PlayerPrefs.Save();
            UIAudio.StopMusic(0.4f);
            ScreenFader.I.FadeOut(0.5f, () =>
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
        });
    }

    // =================================================================== loop
    void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame && stack.Count > 0)
        {
            var top = stack[stack.Count - 1];
            if (top.onBack != null) top.onBack();
            else CloseTop();
        }

        if (CreditsPage != null) CreditsPage.Tick();
        if (OptionsPage != null) OptionsPage.Tick();   // display-confirm countdown
    }

    void OnDestroy()
    {
        UITween.Kill(this);
    }
}
