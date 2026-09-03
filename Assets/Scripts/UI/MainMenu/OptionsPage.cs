using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The OPTIONS screen.
//
// Every row is generated from SettingsCatalog, so this file contains no
// knowledge of any individual setting - adding one is an entry in the catalog.
//
// Edits go into SettingsStore.Draft and are not in force until they are saved.
// A SAVE SETTINGS button appears in the bottom bar the moment anything is
// pending, and pressing it asks for a confirmation that lists what will
// change. Leaving with unsaved rows asks whether to discard them. Display
// changes that could leave the screen unreadable then get their own countdown.
public class OptionsPage
{
    public UIPanel Panel { get; private set; }
    public int ActiveTab { get; private set; }

    readonly IMenuHost menu;

    static readonly SettingCategory[] Tabs =
    {
        SettingCategory.Display,
        SettingCategory.Graphics,
        SettingCategory.Audio,
        SettingCategory.Gameplay,
        SettingCategory.Interface
    };

    static readonly string[] TabLabels = { "DISPLAY", "GRAPHICS", "AUDIO", "GAMEPLAY", "INTERFACE" };

    readonly UIPanel[] tabPanels = new UIPanel[Tabs.Length];
    readonly Button[] tabButtons = new Button[Tabs.Length];
    readonly List<SettingRowView> rows = new List<SettingRowView>();

    Text dirtyLabel;
    Button saveButton;

    DisplayConfirmDialog displayConfirm;
    GameSettingsData beforeApply;

    public OptionsPage(IMenuHost menu)
    {
        this.menu = menu;
        SettingsStore.EnsureLoaded();
        Build();

        SettingsStore.DraftChanged += RefreshAll;
    }

    public void Dispose()
    {
        SettingsStore.DraftChanged -= RefreshAll;
    }

    // ================================================================== build
    void Build()
    {
        var theme = MenuTheme.Current;

        var card = UIFactory.Card("OptionsPanel", menu.CanvasRoot,
                                  new Vector2(1080f, 820f), Vector2.zero);
        Panel = UIFactory.MakePanel(card.gameObject, menu.PanelIn, menu.PanelOut, theme.panelDuration);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(40f, 34f);
        body.offsetMax = new Vector2(-40f, -30f);

        BuildHeader(body);
        BuildTabStrip(body);
        BuildContent(body);
        BuildActionBar(body);

        displayConfirm = new DisplayConfirmDialog(menu);
        displayConfirm.Panel.HideInstant();

        Panel.firstSelected = tabButtons[0].gameObject;
        Panel.onBack = RequestClose;

        RefreshAll();
    }

    void BuildHeader(Transform body)
    {
        var header = UIFactory.NewText("Header", body, "OPTIONS", 42, FontStyle.Bold,
                                       TextAnchor.UpperCenter, GameUITheme.Ink);
        var rt = header.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(0f, 54f);
        rt.anchoredPosition = Vector2.zero;
    }

    void BuildTabStrip(Transform body)
    {
        var strip = UIFactory.NewRect("Tabs", body);
        strip.anchorMin = new Vector2(0f, 1f);
        strip.anchorMax = new Vector2(1f, 1f);
        strip.pivot = new Vector2(0.5f, 1f);
        strip.sizeDelta = new Vector2(0f, 50f);
        strip.anchoredPosition = new Vector2(0f, -62f);
        UIFactory.HStack(strip, 10f);

        for (int i = 0; i < Tabs.Length; i++)
        {
            int captured = i;
            tabButtons[i] = UIFactory.SmallButton(strip, TabLabels[i],
                () => SelectTab(captured, true), new Vector2(0f, 46f));

            var label = tabButtons[i].transform.Find("Label");
            if (label != null) label.GetComponent<Text>().fontSize = 20;
        }
    }

    void BuildContent(Transform body)
    {
        var content = UIFactory.NewRect("Content", body);
        content.anchorMin = new Vector2(0f, 0f);
        content.anchorMax = new Vector2(1f, 1f);
        content.offsetMin = new Vector2(0f, 118f);
        content.offsetMax = new Vector2(0f, -122f);

        for (int i = 0; i < Tabs.Length; i++)
        {
            var host = UIFactory.NewRect("Tab_" + Tabs[i], content);
            UIFactory.Stretch(host);
            tabPanels[i] = UIFactory.MakePanel(host.gameObject, "SlideLeft", "Fade", 0.2f);

            ScrollRect scroll;
            var list = UIFactory.ScrollArea(host, out scroll);
            UIFactory.Stretch((RectTransform)scroll.transform);

            foreach (var entry in SettingsCatalog.For(Tabs[i]))
                rows.Add(new SettingRowView(list, entry));

            tabPanels[i].HideInstant();
        }
    }

    void BuildActionBar(Transform body)
    {
        var bar = UIFactory.NewRect("ActionBar", body);
        bar.anchorMin = new Vector2(0f, 0f);
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(0f, 104f);
        bar.anchoredPosition = Vector2.zero;

        UIFactory.Divider(bar).rectTransform.parent.gameObject.SetActive(true);

        // Unsaved counter, left.
        dirtyLabel = UIFactory.NewBodyText("Dirty", bar, "", 21, FontStyle.Bold,
                                       TextAnchor.MiddleLeft, UIFactory.Accent);
        dirtyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var dRt = dirtyLabel.rectTransform;
        dRt.anchorMin = new Vector2(0f, 0f);
        dRt.anchorMax = new Vector2(0.4f, 1f);
        dRt.offsetMin = new Vector2(6f, 0f);
        dRt.offsetMax = Vector2.zero;

        // Buttons, right.
        var buttons = UIFactory.NewRect("Buttons", bar);
        buttons.anchorMin = new Vector2(0.34f, 0.5f);
        buttons.anchorMax = new Vector2(1f, 0.5f);
        buttons.pivot = new Vector2(0.5f, 0.5f);
        buttons.sizeDelta = new Vector2(0f, 54f);
        buttons.anchoredPosition = Vector2.zero;
        UIFactory.HStack(buttons, 12f);

        UIFactory.SmallButton(buttons, "DEFAULTS", AskResetDefaults,
                              new Vector2(0f, 52f), MenuFxStyle.Soft, true);

        // SAVE SETTINGS is not a permanently greyed-out button: it only exists
        // while there is something to save, so its presence is the signal. The
        // layout group reflows around it as it comes and goes.
        saveButton = UIFactory.SmallButton(buttons, "SAVE SETTINGS", AskSave, new Vector2(0f, 52f));
        saveButton.gameObject.SetActive(false);

        UIFactory.SmallButton(buttons, "BACK", RequestClose, new Vector2(0f, 52f));
    }

    // ================================================================== tabs
    public void SelectTab(int index, bool audible)
    {
        ActiveTab = Mathf.Clamp(index, 0, tabPanels.Length - 1);

        for (int i = 0; i < tabPanels.Length; i++)
        {
            if (i == ActiveTab) tabPanels[i].Show();
            else tabPanels[i].HideInstant();

            var label = tabButtons[i].transform.Find("Label");
            if (label != null)
            {
                var t = label.GetComponent<Text>();
                UITween.ColorTo(t, i == ActiveTab ? UIFactory.Accent : GameUITheme.Ink, 0.18f);
            }
        }

        if (audible) UIAudio.Play(UISound.Tick, 0.9f);
    }

    // =============================================================== refresh
    void RefreshAll()
    {
        foreach (var row in rows)
            if (row != null) row.Refresh();

        int dirty = SettingsStore.DirtyCount;

        if (dirtyLabel != null)
        {
            dirtyLabel.text = dirty == 0
                ? "All changes saved"
                : dirty + (dirty == 1 ? " unsaved change" : " unsaved changes");

            var c = dirty == 0 ? GameUITheme.InkSoft : UIFactory.Accent;
            dirtyLabel.color = c;
        }

        ShowSaveButton(dirty > 0);
    }

    // Appears and disappears rather than greying out, and pops in the first
    // time so the change is noticed without being loud about it.
    void ShowSaveButton(bool show)
    {
        if (saveButton == null) return;

        bool was = saveButton.gameObject.activeSelf;
        if (was == show) return;

        saveButton.gameObject.SetActive(show);

        if (show)
        {
            var fx = saveButton.GetComponent<MenuButtonFX>();
            if (fx != null) fx.RefreshHome();
            UITween.ScaleFrom(saveButton.transform, Vector3.one * 0.86f, 0.22f, Ease.OutBack);
        }
    }

    // ================================================================ actions
    // Both outcomes are two-step, and neither touches anything until its second
    // CONFIRM:
    //
    //   SAVE SETTINGS        CONFIRM (green) -> CONFIRM SAVE    -> writes
    //                        REVERT  (red)   -> REVERT CHANGES  -> discards
    //
    // Every step replaces the previous question in the same panel, BACK steps
    // one level up, and Escape abandons the whole thing with nothing changed.
    void AskSave()
    {
        if (!SettingsStore.HasUnsavedChanges) { UIAudio.Play(UISound.Error, 0.6f); return; }

        int n = SettingsStore.DirtyCount;

        menu.Confirm.Show("SAVE SETTINGS",
            n + (n == 1 ? " change is" : " changes are") + " ready to be saved:\n\n"
                + SummariseChanges(),
            AskSaveConfirm,
            "CONFIRM", "REVERT",
            UIFactory.MenuTone.Positive, UIFactory.MenuTone.Danger,
            AskRevert,
            false);          // leads to another question, so stay open
    }

    // Second step of the save. Nothing has been written at this point.
    void AskSaveConfirm()
    {
        int n = SettingsStore.DirtyCount;

        menu.Confirm.Show("CONFIRM SAVE",
            "Apply " + n + (n == 1 ? " change" : " changes") + " and overwrite your\n" +
            "saved settings?\n\n" + SummariseChanges(),
            CommitSave,
            "CONFIRM", "BACK",
            UIFactory.MenuTone.Positive, UIFactory.MenuTone.Neutral,
            AskSave);        // BACK returns to the first question
    }

    // Second step of the revert. Nothing has been discarded at this point.
    void AskRevert()
    {
        int n = SettingsStore.DirtyCount;

        menu.Confirm.Show("REVERT CHANGES",
            "Discard " + n + (n == 1 ? " change" : " changes") + " and go back to\n" +
            "the saved values?\n\n" + SummariseChanges(),
            CommitRevert,
            "CONFIRM", "BACK",
            UIFactory.MenuTone.Danger, UIFactory.MenuTone.Neutral,
            AskSave);        // BACK returns to the first question
    }

    void CommitRevert()
    {
        SettingsStore.Revert();
        UIAudio.Play(UISound.Cancel);
        RefreshAll();
    }

    // Names the rows about to change, so CONFIRM is an informed press rather
    // than a blind one.
    string SummariseChanges()
    {
        var dirty = SettingsStore.DirtyEntries();
        var sb = new System.Text.StringBuilder();

        const int MaxListed = 5;
        for (int i = 0; i < dirty.Count && i < MaxListed; i++)
        {
            var e = dirty[i];
            sb.Append(e.label).Append(":   ")
              .Append(e.Display(SettingsStore.Saved)).Append("   ->   ")
              .Append(e.Display(SettingsStore.Draft)).Append('\n');
        }

        if (dirty.Count > MaxListed)
            sb.Append("and ").Append(dirty.Count - MaxListed).Append(" more");

        return sb.ToString().TrimEnd('\n');
    }

    void CommitSave()
    {
        bool needsConfirm = SettingsStore.DirtyNeedsConfirm();
        beforeApply = needsConfirm ? SettingsStore.Saved.Clone() : null;

        bool written = SettingsStore.Apply();
        UIAudio.Play(written ? UISound.Confirm : UISound.Error);

        // A resolution or window-mode change still gets its own countdown on
        // top of this, because the screen may now be unreadable.
        if (needsConfirm) displayConfirm.Show(beforeApply);
        RefreshAll();
    }

    void AskResetDefaults()
    {
        menu.ShowConfirm("RESTORE DEFAULTS",
            "Every setting goes back to its default value.\n" +
            "Your resolution and window mode are kept, and nothing is\n" +
            "saved until you press SAVE SETTINGS.",
            () =>
            {
                SettingsStore.ResetToDefaults();
                UIAudio.Play(UISound.Confirm);
                RefreshAll();
            });
    }

    // Leaving with pending edits asks rather than silently discarding them.
    void RequestClose()
    {
        if (!SettingsStore.HasUnsavedChanges) { menu.CloseTop(); return; }

        int n = SettingsStore.DirtyCount;
        menu.ShowConfirm("UNSAVED CHANGES",
            n + (n == 1 ? " change is" : " changes are") + " not saved yet.\n" +
            "Leave and discard them?",
            () =>
            {
                SettingsStore.Revert();
                RefreshAll();
                menu.CloseTop();
            });
    }

    // Driven from the host's Update, for the countdown dialog.
    public void Tick()
    {
        if (displayConfirm != null) displayConfirm.Tick();
    }
}
