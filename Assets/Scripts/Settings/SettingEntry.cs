using System;
using System.Collections.Generic;
using UnityEngine;

public enum SettingKind { Slider, Toggle, Choice }

public enum SettingCategory { Display, Graphics, Audio, Gameplay, Interface }

public enum ApplyMode
{
    // Written to the draft, pushed at the engine on APPLY.
    Deferred,

    // Same, but the engine is also updated as the player drags so they can see
    // what they are choosing. REVERT undoes the preview.
    LivePreview,

    // Deferred, and after APPLY the player has to confirm it looks right
    // before it sticks. For anything that can leave the game unviewable.
    Confirm
}

// The description of one row on the options screen.
//
// Every row - its label, its help text, its range, how it is applied and how
// to read and write it - is declared once here, and the UI is generated from
// that. Dirty tracking, "was: X" text and REVERT all fall out of it for free,
// because they only need to compare the draft and saved bags through the same
// accessors.
//
// Adding a setting means adding one entry in SettingsCatalog. No UI code
// changes, no dirty-tracking code changes.
public class SettingEntry
{
    public string id;
    public string label;
    public string description;

    public SettingCategory category;
    public SettingKind kind;
    public ApplyMode apply = ApplyMode.Deferred;

    // True for the graphics fields a preset owns: touching one flips the
    // preset label to Custom.
    public bool partOfPreset;

    // Rows can grey themselves out when another setting makes them pointless
    // (a frame cap under V-Sync, auto-advance delay with auto-advance off).
    public Func<GameSettingsData, bool> isEnabled;

    // Some changes only take hold on the next launch; the row says so.
    public bool needsRestart;

    // ---- Slider
    public float min, max;
    public Func<GameSettingsData, float> getFloat;
    public Action<GameSettingsData, float> setFloat;
    public Func<float, string> formatFloat;

    // ---- Toggle
    public Func<GameSettingsData, bool> getBool;
    public Action<GameSettingsData, bool> setBool;

    // ---- Choice
    public Func<List<string>> choices;
    public Func<GameSettingsData, int> getIndex;
    public Action<GameSettingsData, int> setIndex;

    // ------------------------------------------------------------- comparison
    public bool SameIn(GameSettingsData a, GameSettingsData b)
    {
        if (a == null || b == null) return true;

        switch (kind)
        {
            case SettingKind.Slider:
                return Mathf.Approximately(getFloat(a), getFloat(b));
            case SettingKind.Toggle:
                return getBool(a) == getBool(b);
            case SettingKind.Choice:
                return getIndex(a) == getIndex(b);
        }
        return true;
    }

    // The text shown as the row's current value, and as "was: X" on a dirty row.
    public string Display(GameSettingsData data)
    {
        if (data == null) return "";

        switch (kind)
        {
            case SettingKind.Slider:
            {
                float v = getFloat(data);
                return formatFloat != null ? formatFloat(v) : v.ToString("0.00");
            }
            case SettingKind.Toggle:
                return getBool(data) ? "On" : "Off";

            case SettingKind.Choice:
            {
                var list = choices != null ? choices() : null;
                if (list == null || list.Count == 0) return "-";
                int i = Mathf.Clamp(getIndex(data), 0, list.Count - 1);
                return list[i];
            }
        }
        return "";
    }

    public bool Enabled(GameSettingsData data)
    {
        return isEnabled == null || isEnabled(data);
    }
}
