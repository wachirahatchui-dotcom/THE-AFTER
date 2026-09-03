using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// One row on the options screen, generated from a SettingEntry.
//
// Layout, top to bottom:
//
//   [dot] Label .................... [ control ]  value
//         was: <previous value>                        <- only when dirty
//         <description>                                <- when tooltips are on
//
// The two sub-lines are separate children toggled on and off, and the row uses
// a ContentSizeFitter, so the row grows and shrinks by itself instead of
// needing height arithmetic here.
//
// The view never writes to Saved: edits go into SettingsStore.Draft and the
// store decides when they become real.
public class SettingRowView
{
    public SettingEntry Entry { get; private set; }
    public RectTransform Root { get; private set; }

    Image dot;
    Text labelText;
    Text valueText;
    Text wasText;
    Text descriptionText;
    RectTransform controlArea;

    Slider slider;
    Toggle toggle;
    ArrowSelector selector;

    CanvasGroup group;

    const float MainHeight = 48f;
    const float SubHeight = 26f;

    public SettingRowView(Transform parent, SettingEntry entry)
    {
        Entry = entry;
        Build(parent);
        Refresh();
    }

    // ================================================================== build
    void Build(Transform parent)
    {
        Root = UIFactory.NewRect("Row_" + Entry.id, parent);
        group = Root.gameObject.AddComponent<CanvasGroup>();

        var v = Root.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = 1f;
        v.padding = new RectOffset(0, 0, 4, 6);
        v.childControlWidth = true;
        v.childControlHeight = true;
        v.childForceExpandWidth = true;
        v.childForceExpandHeight = false;
        v.childAlignment = TextAnchor.UpperLeft;

        var fitter = Root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        BuildMainLine();
        BuildSubLines();
    }

    void BuildMainLine()
    {
        var main = UIFactory.NewRect("Main", Root);
        UIFactory.SetHeight(main.gameObject, MainHeight);

        // Dirty marker. Always present so the label never shifts sideways when
        // a row becomes dirty; it just fades in.
        dot = UIFactory.NewImage("Dot", main, UIFactory.Accent);
        dot.sprite = UIGfx.SoftDot(32, 1.2f);
        dot.raycastTarget = false;
        var dRt = dot.rectTransform;
        dRt.anchorMin = dRt.anchorMax = new Vector2(0f, 0.5f);
        dRt.pivot = new Vector2(0.5f, 0.5f);
        dRt.sizeDelta = new Vector2(11f, 11f);
        dRt.anchoredPosition = new Vector2(8f, 0f);

        labelText = UIFactory.NewText("Label", main, Entry.label, 27, FontStyle.Bold,
                                      TextAnchor.MiddleLeft, GameUITheme.Ink);
        labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var lRt = labelText.rectTransform;
        lRt.anchorMin = new Vector2(0f, 0f);
        lRt.anchorMax = new Vector2(0.46f, 1f);
        lRt.offsetMin = new Vector2(22f, 0f);
        lRt.offsetMax = Vector2.zero;

        // Value readout, right-aligned, always visible.
        valueText = UIFactory.NewBodyText("Value", main, "", 23, FontStyle.Bold,
                                      TextAnchor.MiddleRight, UIFactory.Accent);
        valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
        var vRt = valueText.rectTransform;
        vRt.anchorMin = new Vector2(0.86f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.offsetMin = Vector2.zero;
        vRt.offsetMax = Vector2.zero;

        controlArea = UIFactory.NewRect("Control", main);
        controlArea.anchorMin = new Vector2(0.46f, 0f);
        controlArea.anchorMax = new Vector2(0.84f, 1f);
        controlArea.offsetMin = Vector2.zero;
        controlArea.offsetMax = Vector2.zero;

        BuildControl();
    }

    void BuildControl()
    {
        var draft = SettingsStore.Draft;

        switch (Entry.kind)
        {
            case SettingKind.Slider:
                slider = UIFactory.SliderControl(controlArea, Entry.min, Entry.max,
                    Entry.getFloat(draft), OnSlider);
                break;

            case SettingKind.Toggle:
                // The checkbox reads better hard against the value column.
                controlArea.anchorMin = new Vector2(0.70f, 0f);
                controlArea.anchorMax = new Vector2(0.84f, 1f);
                toggle = UIFactory.ToggleControl(controlArea, Entry.getBool(draft), OnToggle);
                break;

            case SettingKind.Choice:
                // The stepper already prints the value, so the separate
                // readout would just repeat it - and long labels like
                // "1920 x 1080 144Hz" then overlap the right arrow.
                valueText.gameObject.SetActive(false);
                controlArea.anchorMax = new Vector2(0.99f, 1f);

                var options = Entry.choices != null ? Entry.choices() : new List<string>();
                selector = UIFactory.SelectorControl(controlArea, options,
                    Entry.getIndex(draft), OnChoice);
                break;
        }
    }

    // Darker than InkSoft on purpose. The description is the smallest text on
    // the screen, so it needs the most contrast, not the least.
    static Color DescriptionColour()
    {
        var c = GameUITheme.Ink;
        c.a = 0.88f;
        return c;
    }

    void BuildSubLines()
    {
        var wasColour = UIFactory.Accent;
        wasColour.a = 1f;

        wasText = UIFactory.NewBodyText("Was", Root, "", 20, FontStyle.Bold,
                                    TextAnchor.MiddleLeft, wasColour);
        wasText.horizontalOverflow = HorizontalWrapMode.Overflow;
        UIFactory.SetHeight(wasText.gameObject, SubHeight);
        wasText.rectTransform.offsetMin = new Vector2(22f, 0f);
        wasText.gameObject.SetActive(false);

        descriptionText = UIFactory.NewBodyText("Description", Root, Entry.description, 19,
                                                FontStyle.Normal, TextAnchor.UpperLeft,
                                                DescriptionColour());
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        UIFactory.SetHeight(descriptionText.gameObject, SubHeight);
    }

    // ================================================================== edits
    void OnSlider(float v)
    {
        Entry.setFloat(SettingsStore.Draft, v);
        SettingsStore.NotifyEdited(Entry);
        Refresh();
    }

    void OnToggle(bool v)
    {
        Entry.setBool(SettingsStore.Draft, v);
        SettingsStore.NotifyEdited(Entry);
        Refresh();
    }

    void OnChoice(int i)
    {
        Entry.setIndex(SettingsStore.Draft, i);
        SettingsStore.NotifyEdited(Entry);
        Refresh();
    }

    // ================================================================ refresh
    // Called on every draft change, for every row: rows react to each other
    // (a frame cap greys out under V-Sync, a preset change rewrites six rows).
    public void Refresh()
    {
        var draft = SettingsStore.Draft;
        var saved = SettingsStore.Saved;

        PullFromDraft(draft);

        bool dirty = !Entry.SameIn(draft, saved);
        bool enabled = Entry.Enabled(draft);

        // Dirty marker.
        var dotColour = UIFactory.Accent;
        dotColour.a = dirty ? 1f : 0f;
        if (dot != null) dot.color = dotColour;

        // Value readout. Choice rows hide it: the stepper is the readout.
        if (valueText != null && valueText.gameObject.activeSelf)
        {
            valueText.text = Entry.Display(draft);
            var c = dirty ? UIFactory.Accent : GameUITheme.Ink;
            c.a = enabled ? 1f : 0.35f;
            valueText.color = c;
        }

        // "was: X" line.
        if (wasText != null)
        {
            bool show = dirty;
            if (wasText.gameObject.activeSelf != show) wasText.gameObject.SetActive(show);
            if (show) wasText.text = "was:  " + Entry.Display(saved);
        }

        // Description line.
        if (descriptionText != null)
        {
            bool show = GameSettings.ShowTooltips && !string.IsNullOrEmpty(Entry.description);
            if (descriptionText.gameObject.activeSelf != show)
                descriptionText.gameObject.SetActive(show);
        }

        // Label and whole-row dimming when the setting is not applicable.
        if (labelText != null)
        {
            var c = GameUITheme.Ink;
            c.a = enabled ? 1f : 0.35f;
            labelText.color = c;
        }

        if (group != null)
        {
            group.alpha = enabled ? 1f : 0.55f;
            group.interactable = enabled;
            group.blocksRaycasts = enabled;
        }
    }

    // Pushes the draft value back into the widget without firing its callback.
    // Needed because one row can change another: picking a preset rewrites the
    // graphics rows, and REVERT rewrites everything.
    void PullFromDraft(GameSettingsData draft)
    {
        switch (Entry.kind)
        {
            case SettingKind.Slider:
                if (slider != null)
                {
                    float v = Entry.getFloat(draft);
                    if (!Mathf.Approximately(slider.value, v)) slider.SetValueWithoutNotify(v);
                }
                break;

            case SettingKind.Toggle:
                if (toggle != null)
                {
                    bool b = Entry.getBool(draft);
                    if (toggle.isOn != b)
                    {
                        toggle.SetIsOnWithoutNotify(b);
                        var check = toggle.transform.Find("Box/Check");
                        if (check != null) check.gameObject.SetActive(b);
                    }
                }
                break;

            case SettingKind.Choice:
                if (selector != null)
                {
                    int i = Entry.getIndex(draft);
                    if (selector.Index != i) selector.SetIndex(i, false);
                }
                break;
        }
    }
}
