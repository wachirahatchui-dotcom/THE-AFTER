using System;
using System.Collections.Generic;
using UnityEngine;

// Holds two copies of the settings: the one in force, and the one the options
// screen is editing.
//
//   Saved  - written to disk, applied to the engine, what the game runs on
//   Draft  - what the player has typed in but not committed
//
// Every "is this row dirty", "what was it before" and REVERT falls out of
// comparing the two through SettingsCatalog, so none of that logic is
// duplicated per setting.
//
// Rows marked LivePreview are pushed at the engine as they are edited so the
// player can see the change, but they are still only committed by APPLY, and
// REVERT puts the engine back.
public static class SettingsStore
{
    public static GameSettingsData Saved { get; private set; }
    public static GameSettingsData Draft { get; private set; }

    // Raised whenever the draft changes, so the options screen can repaint its
    // dirty markers and its "N unsaved" counter.
    public static event Action DraftChanged;

    // Raised after Apply or Revert, i.e. whenever Saved becomes authoritative
    // again. SettingsRuntime listens for this to re-push the FOV.
    public static event Action Applied;

    static bool loaded;

    // =================================================================== load
    public static void Load()
    {
        if (loaded) return;
        loaded = true;

        Saved = SettingsFile.Load();
        Saved.qualityPreset = QualityPreset.Detect(Saved);
        Draft = Saved.Clone();

        SettingsApplier.ApplyAll(Saved);
        RaiseApplied();
    }

    public static void EnsureLoaded() { if (!loaded) Load(); }

    // ================================================================== dirty
    public static bool IsDirty(SettingEntry entry)
    {
        EnsureLoaded();
        return entry != null && !entry.SameIn(Draft, Saved);
    }

    public static int DirtyCount
    {
        get
        {
            EnsureLoaded();
            int n = 0;
            foreach (var e in SettingsCatalog.All)
                if (!e.SameIn(Draft, Saved)) n++;
            return n;
        }
    }

    public static bool HasUnsavedChanges { get { return DirtyCount > 0; } }

    public static List<SettingEntry> DirtyEntries()
    {
        EnsureLoaded();
        var list = new List<SettingEntry>();
        foreach (var e in SettingsCatalog.All)
            if (!e.SameIn(Draft, Saved)) list.Add(e);
        return list;
    }

    // True when a change needs the player to confirm the screen is still
    // readable afterwards.
    public static bool DirtyNeedsConfirm()
    {
        foreach (var e in DirtyEntries())
            if (e.apply == ApplyMode.Confirm) return true;
        return false;
    }

    // =================================================================== edit
    // Called by the options screen after it has written into Draft.
    public static void NotifyEdited(SettingEntry entry)
    {
        EnsureLoaded();

        // Touching any field a preset owns means the bundle no longer holds.
        if (entry != null && entry.partOfPreset)
            Draft.qualityPreset = QualityPreset.Detect(Draft);

        if (entry != null && entry.apply == ApplyMode.LivePreview)
            PreviewDraft();

        RaiseDraftChanged();
    }

    // Pushes the draft at the engine without committing it. Only the cheap,
    // reversible things: never resolution or window mode.
    static void PreviewDraft()
    {
        SettingsApplier.ApplyFramePacing(Draft);
        SettingsApplier.ApplyAudio(Draft);
        RaiseApplied();   // SettingsRuntime picks the FOV up from Live
    }

    // ================================================================== apply
    public static bool Apply()
    {
        EnsureLoaded();

        Saved = Draft.Clone();
        Saved.qualityPreset = QualityPreset.Detect(Saved);
        Draft = Saved.Clone();

        SettingsApplier.ApplyAll(Saved);
        bool ok = SettingsFile.Save(Saved);

        RaiseApplied();
        RaiseDraftChanged();
        return ok;
    }

    // Throws the draft away and puts the engine back where it was. Undoes any
    // live preview.
    public static void Revert()
    {
        EnsureLoaded();

        Draft = Saved.Clone();
        SettingsApplier.ApplyAll(Saved);

        RaiseApplied();
        RaiseDraftChanged();
    }

    // ================================================================== reset
    // Defaults land in the draft, not on disk: the player still has to press
    // APPLY, and can still change their mind.
    public static void ResetToDefaults()
    {
        EnsureLoaded();

        var fresh = new GameSettingsData();
        fresh.FillMissingResolution();

        // Keep the current display mode: resetting preferences should not
        // suddenly throw the player into a different resolution.
        fresh.resolutionWidth = Saved.resolutionWidth;
        fresh.resolutionHeight = Saved.resolutionHeight;
        fresh.refreshRateHz = Saved.refreshRateHz;
        fresh.windowMode = Saved.windowMode;
        fresh.qualityPreset = QualityPreset.Detect(fresh);

        Draft = fresh;
        PreviewDraft();
        RaiseDraftChanged();
    }

    // Used by the countdown dialog when the player does not confirm in time.
    public static void RollBackDisplay(GameSettingsData previous)
    {
        if (previous == null) return;

        Saved.resolutionWidth = previous.resolutionWidth;
        Saved.resolutionHeight = previous.resolutionHeight;
        Saved.refreshRateHz = previous.refreshRateHz;
        Saved.windowMode = previous.windowMode;

        Draft = Saved.Clone();

        SettingsApplier.ApplyResolution(Saved);
        SettingsFile.Save(Saved);

        RaiseApplied();
        RaiseDraftChanged();
    }

    public static void SaveNow() { if (loaded) SettingsFile.Save(Saved); }

    static void RaiseDraftChanged() { if (DraftChanged != null) DraftChanged(); }
    static void RaiseApplied() { if (Applied != null) Applied(); }
}
