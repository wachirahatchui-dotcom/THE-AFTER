using System;
using UnityEngine;
using UnityEngine.UI;

// The "keep these display settings?" countdown.
//
// A resolution or window mode the monitor cannot show leaves the player
// staring at a black screen with no way to undo it. So after applying one, the
// game asks - and if no answer comes back within the countdown, it puts the
// old settings back on its own. Every game does this, and this is why.
//
// The countdown runs on unscaled time so it still works from the pause menu.
public class DisplayConfirmDialog
{
    public UIPanel Panel { get; private set; }

    readonly IMenuHost menu;
    Text bodyLabel;
    Image barFill;
    RectTransform barTrack;

    GameSettingsData rollbackTo;
    float remaining;
    float total;
    bool running;

    public DisplayConfirmDialog(IMenuHost menu)
    {
        this.menu = menu;
        Build();
    }

    void Build()
    {
        var theme = MenuTheme.Current;

        var card = UIFactory.Card("DisplayConfirmPanel", menu.CanvasRoot,
                                  new Vector2(760f, 380f), Vector2.zero);
        Panel = UIFactory.MakePanel(card.gameObject, "ScalePop", "ScaleShrink", 0.24f);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(36f, 30f);
        body.offsetMax = new Vector2(-36f, -28f);

        var title = UIFactory.NewText("Title", body, "KEEP THESE DISPLAY SETTINGS?", 30,
                                      FontStyle.Bold, TextAnchor.UpperCenter, GameUITheme.Ink);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        var tRt = title.rectTransform;
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.sizeDelta = new Vector2(0f, 42f);

        bodyLabel = UIFactory.NewText("Message", body, "", 21, FontStyle.Normal,
                                      TextAnchor.UpperCenter, GameUITheme.InkSoft);
        var bRt = bodyLabel.rectTransform;
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 1f);
        bRt.offsetMin = new Vector2(0f, 118f);
        bRt.offsetMax = new Vector2(0f, -54f);

        // Countdown bar, so the deadline is visible rather than just a number.
        barTrack = UIFactory.NewRect("BarTrack", body);
        barTrack.anchorMin = new Vector2(0f, 0f);
        barTrack.anchorMax = new Vector2(1f, 0f);
        barTrack.pivot = new Vector2(0.5f, 0f);
        barTrack.anchoredPosition = new Vector2(0f, 82f);
        barTrack.sizeDelta = new Vector2(0f, 6f);

        var track = UIFactory.NewImage("Track", barTrack, GameUITheme.ParchmentDeep);
        track.raycastTarget = false;
        UIFactory.Stretch(track.rectTransform);

        barFill = UIFactory.NewImage("Fill", barTrack, UIFactory.Accent);
        barFill.raycastTarget = false;
        barFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        barFill.rectTransform.anchorMax = new Vector2(0f, 1f);
        barFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        barFill.rectTransform.anchoredPosition = Vector2.zero;

        var actions = UIFactory.NewRect("Actions", body);
        actions.anchorMin = new Vector2(0f, 0f);
        actions.anchorMax = new Vector2(1f, 0f);
        actions.pivot = new Vector2(0.5f, 0f);
        actions.sizeDelta = new Vector2(0f, 58f);
        UIFactory.HStack(actions, 18f);

        // REVERT first and focused: if the screen is unreadable, blindly
        // hitting Enter must be the safe answer.
        var revert = UIFactory.SmallButton(actions, "REVERT", RevertNow,
                                           new Vector2(0f, 58f), MenuFxStyle.Soft, true);
        UIFactory.SmallButton(actions, "KEEP", KeepNow, new Vector2(0f, 58f));

        Panel.firstSelected = revert.gameObject;
        Panel.onBack = RevertNow;
    }

    // `previous` is the settings to go back to if the player says nothing.
    public void Show(GameSettingsData previous, float seconds = 15f)
    {
        rollbackTo = previous;
        total = seconds;
        remaining = seconds;
        running = true;

        UpdateText();
        menu.OpenPanel(Panel);
    }

    // Driven from the host's Update so this needs no MonoBehaviour of its own.
    public void Tick()
    {
        if (!running || Panel == null || !Panel.IsOpen) return;

        remaining -= Time.unscaledDeltaTime;
        UpdateText();

        if (remaining <= 0f) RevertNow();
    }

    void UpdateText()
    {
        int seconds = Mathf.Max(0, Mathf.CeilToInt(remaining));

        bodyLabel.text =
            Screen.width + " x " + Screen.height + "  ~  " +
            SettingsCatalog.Find("windowMode").Display(SettingsStore.Saved) + "\n\n" +
            "Reverting automatically in " + seconds + " second" + (seconds == 1 ? "" : "s") + ".";

        if (barFill != null && barTrack != null)
        {
            float k = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            barFill.rectTransform.sizeDelta = new Vector2(barTrack.rect.width * k, 0f);

            // Runs from accent to danger as the deadline approaches.
            barFill.color = Color.Lerp(GameUITheme.Danger, UIFactory.Accent, k);
        }
    }

    void KeepNow()
    {
        running = false;
        rollbackTo = null;
        UIAudio.Play(UISound.Confirm);
        menu.CloseTop();
    }

    void RevertNow()
    {
        if (!running) { menu.CloseTop(); return; }

        running = false;
        UIAudio.Play(UISound.Cancel);

        SettingsStore.RollBackDisplay(rollbackTo);
        rollbackTo = null;
        menu.CloseTop();
    }
}
