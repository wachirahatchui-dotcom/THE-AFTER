using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Playables;

// The in-game pause menu.
//
// Built on the same UI framework as the main menu and implementing IMenuHost,
// so OptionsPage and ConfirmDialog are the exact same classes shown from the
// exact same code - settings changed here behave identically to settings
// changed from the title screen, because they are the same screen.
//
//   RESUME  /  SAVE  /  OPTIONS  /  MAIN MENU  /  QUIT
//
// Bootstraps itself on every non-menu scene load, so no scene needs to place
// it by hand. Everything runs on unscaled time, because pausing sets
// Time.timeScale to 0.
public class PauseMenuUI : MonoBehaviour, IMenuHost
{
    public static PauseMenuUI Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    [Tooltip("Scene loaded by the MAIN MENU button.")]
    public string mainMenuScene = "MainMenu";

    [Tooltip("Pause on its own when the window loses focus - switching to a browser should not leave the game running behind your back.")]
    public bool pauseWhenUnfocused = true;

    // --------------------------------------------------------------- IMenuHost
    public Transform CanvasRoot { get; private set; }
    public string PanelIn { get; private set; }
    public string PanelOut { get; private set; }

    public PauseSaveLoadPage SavePage { get; private set; }
    public OptionsPage OptionsPage { get; private set; }
    public ConfirmDialog Confirm { get; private set; }

    // ---------------------------------------------------------------- private
    UIPanel rootPanel;
    CanvasGroup dimGroup;
    Canvas canvas;
    readonly List<UIPanel> stack = new List<UIPanel>();
    readonly List<CanvasGroup> buttonGroups = new List<CanvasGroup>();

    // ================================================================ bootstrap
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        // sceneLoaded does NOT fire for the scene the game starts in, so the
        // first scene has to be handled directly. Without this, pressing Play
        // straight into a gameplay scene gives you no pause menu at all.
        EnsureFor(SceneManager.GetActiveScene());

        SceneManager.sceneLoaded += (scene, mode) => EnsureFor(scene);
    }

    static void EnsureFor(Scene scene)
    {
        if (scene.name == PlayTimeTracker.MenuSceneName) return;
        if (Instance != null) return;

        new GameObject("PauseMenu").AddComponent<PauseMenuUI>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        IsPaused = false;
        Time.timeScale = 1f;

        PanelIn = GameSettings.MenuAnimation;
        PanelOut = MainMenuUI.PairedExit(PanelIn);

        EnsureEventSystem();
        BuildUI();
        SetVisible(false, true);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            IsPaused = false;
        }
        UITween.Kill(this);
    }

    void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    // ================================================================== build
    void BuildUI()
    {
        var theme = MenuTheme.Current;

        var canvasGo = new GameObject("PauseCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        CanvasRoot = canvasGo.transform;

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Above everything, the screen fader included.
        //
        // Pausing has to work at any moment, and some of those moments are black:
        // a cutscene ends on a fade, a transition is halfway through. At 100 this
        // menu sat under the fader (32000), the objective tracker and the tutorial
        // line, so pressing Escape on a dark frame paused a game that showed no
        // menu at all - and the fader blocks raycasts while it is opaque, so even
        // the buttons that were there could not be clicked. Being on top is what
        // makes the pause menu the pause menu.
        //
        // 32600 and not a round 33000: sortingOrder is a signed 16-bit value, so it
        // stops at 32767. 33000 does not clamp, it wraps - to -32536, which put this
        // menu underneath every other canvas in the game instead of above them.
        canvas.sortingOrder = 32600;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.referenceResolution;
        scaler.matchWidthOrHeight = theme.scalerMatch;

        // Full-screen darkener sits behind everything this menu draws.
        var dim = UIFactory.NewImage("Dim", CanvasRoot, theme.dimColor);
        UIFactory.Stretch(dim.rectTransform);
        dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        dimGroup.alpha = 0f;

        BuildRootPanel();

        SavePage = new PauseSaveLoadPage(this);
        OptionsPage = new OptionsPage(this);
        Confirm = new ConfirmDialog(this);

        SavePage.Panel.HideInstant();
        OptionsPage.Panel.HideInstant();
        Confirm.Panel.HideInstant();
    }

    void BuildRootPanel()
    {
        var theme = MenuTheme.Current;

        var card = UIFactory.Card("PausePanel", CanvasRoot, new Vector2(470f, 560f), Vector2.zero);
        rootPanel = UIFactory.MakePanel(card.gameObject, "ScalePop", "ScaleShrink", 0.26f);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(34f, 32f);
        body.offsetMax = new Vector2(-34f, -28f);

        var header = UIFactory.NewText("Header", body, "PAUSED", 42, FontStyle.Bold,
                                       TextAnchor.UpperCenter, GameUITheme.Ink);
        var hRt = header.rectTransform;
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.sizeDelta = new Vector2(0f, 58f);

        var column = UIFactory.NewRect("Buttons", body);
        column.anchorMin = new Vector2(0f, 1f);
        column.anchorMax = new Vector2(1f, 1f);
        column.pivot = new Vector2(0.5f, 1f);
        column.anchoredPosition = new Vector2(0f, -74f);
        column.sizeDelta = new Vector2(0f, 400f);

        int row = 0;
        Row(column, "RESUME", row++, Resume);
        Row(column, "SAVE", row++, OpenSave);
        Row(column, "OPTIONS", row++, OpenOptions);
        Row(column, "MAIN MENU", row++, AskMainMenu);
        Row(column, "QUIT", row++, AskQuit, true);

        rootPanel.firstSelected = column.GetChild(0).gameObject;
        rootPanel.onBack = Resume;
    }

    Button Row(Transform parent, string label, int index, Action onClick, bool danger = false)
    {
        var btn = UIFactory.MenuButton(parent, label, () => onClick(),
                                       MenuFxStyle.Soft, 64f, 28, danger);

        var rt = (RectTransform)btn.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 64f);
        rt.anchoredPosition = new Vector2(0f, -index * 74f);

        var cg = btn.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        buttonGroups.Add(cg);

        return btn;
    }

    // ================================================================== pause
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Escape closes the top page first, and only unpauses when the root
        // panel is what is showing.
        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (OptionsPage != null) OptionsPage.Tick();   // display-confirm countdown


            if (LoadingScreen.IsLoading) return;

            if (!IsPaused)
            {
                Pause();
            }
            else if (stack.Count > 0)
            {
                var top = stack[stack.Count - 1];
                if (top.onBack != null) top.onBack();
                else CloseTop();
            }
            else
            {
                Resume();
            }
        }
    }

    public void Toggle() { if (IsPaused) Resume(); else Pause(); }

    /// Pauses itself when the window stops being the one you are looking at.
    ///
    /// Alt-tabbing to a browser does not stop the game: cutscenes keep playing to
    /// their end and hand the controls back to nobody, so coming back lands you
    /// somewhere in the middle of a scene you never watched, already walking
    /// around. Time carrying on while the player cannot see it is the bug; this
    /// stops the clock instead.
    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) PauseForLostFocus();
    }

    /// Minimised, or the OS took the screen away. Same story.
    void OnApplicationPause(bool suspended)
    {
        if (suspended) PauseForLostFocus();
    }

    void PauseForLostFocus()
    {
        // Says out loud what happened, because this path cannot be watched from
        // the editor: losing focus is the very thing being tested, and an unfocused
        // editor is not running to be looked at.
        Debug.Log("[Pause] เสียโฟกัส  ตั้งค่าเปิดใช้=" + pauseWhenUnfocused
                + "  หยุดอยู่แล้ว=" + IsPaused
                + "  กำลังโหลด=" + LoadingScreen.IsLoading);

        if (!pauseWhenUnfocused || IsPaused) return;

        // Mid-load there is nothing coherent to pause into, and the loading screen
        // finishes on its own in a moment.
        if (LoadingScreen.IsLoading) return;

        Pause();
    }

    public void Pause()
    {
        if (IsPaused) return;
        IsPaused = true;

        Time.timeScale = 0f;

        // Silence the world too, not just freeze it.
        //
        // This used to be left running, on the grounds that the menu's own sounds
        // had to stay audible - but UIAudioPlayer already sets ignoreListenerPause
        // on every source it owns, so its clicks carry on regardless. All this line
        // was actually keeping alive was the game: the voice over kept talking
        // behind the pause menu, and by the time the game came back the dialogue
        // had run on well past the picture. That is the "it skipped" - the sound
        // never stopped, so it arrived somewhere the scene had not reached.
        AudioListener.pause = true;

        PauseTimelines();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SetVisible(true, false);
        UIAudio.Play(UISound.PanelOpen, 0.8f);

        Debug.Log("[Pause] เปิดเมนูแล้ว  canvas sortingOrder=" + (canvas != null ? canvas.sortingOrder : -1)
                + "  เปิดอยู่=" + (canvas != null && canvas.enabled)
                + "  timeScale=" + Time.timeScale);
    }

    public void Resume()
    {
        if (!IsPaused) return;

        // Unwind any open page so reopening starts at the root.
        while (stack.Count > 0) CloseTopSilent();

        IsPaused = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        ResumeTimelines();

        SetVisible(false, false);
        UIAudio.Play(UISound.PanelClose, 0.8f);
    }

    // The cutscene directors that were running when the game was paused.
    readonly List<PlayableDirector> held = new List<PlayableDirector>();

    /// Stops the cutscenes properly, rather than trusting timeScale alone.
    ///
    /// A director set to game time does freeze when the clock stops, but its audio
    /// is scheduled against the sound card's own clock and carries on regardless -
    /// so the picture waits and the dialogue does not, and they come back out of
    /// step. Pausing the director is what makes Timeline put both down together
    /// and pick both up in the same place.
    void PauseTimelines()
    {
        held.Clear();
        foreach (var d in FindObjectsByType<PlayableDirector>(FindObjectsSortMode.None))
        {
            if (d == null || d.state != PlayState.Playing) continue;
            d.Pause();
            held.Add(d);
        }
    }

    void ResumeTimelines()
    {
        foreach (var d in held)
            if (d != null) d.Resume();
        held.Clear();
    }

    void SetVisible(bool visible, bool instant)
    {
        if (visible)
        {
            // Enable first, then animate - a disabled canvas draws nothing, so
            // enabling after the tween would skip the whole entrance.
            canvas.enabled = true;
            dimGroup.blocksRaycasts = true;
            UITween.Fade(dimGroup, 1f, instant ? 0f : 0.2f, Ease.OutQuad);
            rootPanel.Show();
            return;
        }

        dimGroup.blocksRaycasts = false;
        UITween.Fade(dimGroup, 0f, instant ? 0f : 0.2f, Ease.OutQuad);

        if (instant)
        {
            rootPanel.HideInstant();
            canvas.enabled = false;
            return;
        }

        // Stay enabled until the exit animation has finished playing, then
        // switch off so the pause canvas costs nothing during gameplay.
        rootPanel.Hide(() =>
        {
            if (canvas != null && !IsPaused) canvas.enabled = false;
        });
    }

    // ============================================================= IMenuHost
    public void OpenPanel(UIPanel panel)
    {
        if (panel == null || stack.Contains(panel)) return;

        if (stack.Count == 0)
            rootPanel.Group.interactable = false;

        stack.Add(panel);
        UIAudio.Play(UISound.PanelOpen, 0.8f);
        panel.Show();
    }

    public void CloseTop()
    {
        if (stack.Count == 0) return;
        UIAudio.Play(UISound.PanelClose, 0.8f);
        CloseTopSilent();
    }

    void CloseTopSilent()
    {
        if (stack.Count == 0) return;

        var panel = stack[stack.Count - 1];
        stack.RemoveAt(stack.Count - 1);
        panel.Hide();

        if (stack.Count == 0)
        {
            rootPanel.Group.interactable = true;
            Focus(rootPanel.firstSelected);
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

    public void SetPanelTransition(string enter)
    {
        PanelIn = enter;
        PanelOut = MainMenuUI.PairedExit(enter);
        GameSettings.SetMenuAnimation(PanelIn);

        SavePage.Panel.SetTransition(PanelIn, PanelOut);
        OptionsPage.Panel.SetTransition(PanelIn, PanelOut);
        Confirm.Panel.SetTransition(PanelIn, PanelOut);
    }

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

    static void Focus(GameObject go)
    {
        if (go != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(go);
    }

    // =============================================================== handlers
    void OpenSave()
    {
        SavePage.Refresh();
        OpenPanel(SavePage.Panel);
    }

    void OpenOptions()
    {
        OpenPanel(OptionsPage.Panel);
        OptionsPage.SelectTab(OptionsPage.ActiveTab, false);
    }

    // The save thumbnail renders the camera, so this menu has to be off screen
    // for one frame first - otherwise every thumbnail is a picture of the pause
    // menu. Hide, wait a frame, capture, show again.
    public void CaptureThumbnailAndSave(int slot, Action onDone)
    {
        StartCoroutine(CaptureRoutine(slot, onDone));
    }

    IEnumerator CaptureRoutine(int slot, Action onDone)
    {
        canvas.enabled = false;
        yield return null;                 // let a clean frame render

        SaveSystem.SaveToSlot(slot, PlayerPrefs.GetString(SettingsKeys.LastChapter, "Chapter 1"));

        canvas.enabled = true;
        if (onDone != null) onDone();
    }

    public void LoadSlot(int index)
    {
        var data = SaveSystem.Read(index);
        if (data == null) { UIAudio.Play(UISound.Error); return; }

        string scene = string.IsNullOrEmpty(data.sceneName)
            ? SceneManager.GetActiveScene().name
            : data.sceneName;

        SaveSystem.QueueLoad(data);
        PlayerPrefs.SetString(SettingsKeys.LastChapter, data.chapterName);
        PlayerPrefs.Save();

        Time.timeScale = 1f;
        IsPaused = false;

        LoadingScreen.Load(scene, data.chapterName, false);
    }

    void AskMainMenu()
    {
        ShowConfirm("RETURN TO MAIN MENU",
            "Any progress since your last save is lost.\n" +
            "Return to the title screen?",
            () =>
            {
                Time.timeScale = 1f;
                IsPaused = false;
                PlayerPrefs.Save();
                LoadingScreen.Load(mainMenuScene, "", false);
            });
    }

    void AskQuit()
    {
        ShowConfirm("QUIT GAME",
            "Any progress since your last save is lost.\n" +
            "Close the game?",
            () =>
            {
                Time.timeScale = 1f;
                PlayerPrefs.Save();
                UIAudio.StopMusic(0.3f);
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
}
