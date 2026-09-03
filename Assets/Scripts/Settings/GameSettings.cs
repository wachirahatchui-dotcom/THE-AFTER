using UnityEngine;

// What gameplay code reads.
//
// A thin facade over SettingsStore.Saved so that CameraFollow, DialogueManager
// and friends never have to know about drafts, catalogs or JSON - they just
// ask for the value in force right now.
//
// Nothing here writes: settings are edited through the options screen, which
// goes via SettingsStore so the APPLY / REVERT flow stays honest. The one
// exception is the volume properties, which UIAudio needs in order to expose
// its existing API.
public static class GameSettings
{
    static GameSettingsData D
    {
        get { SettingsStore.EnsureLoaded(); return SettingsStore.Saved; }
    }

    // For live-previewed rows the player is dragging right now, gameplay should
    // follow the draft, otherwise the preview would not be visible in-game.
    static GameSettingsData Live
    {
        get { SettingsStore.EnsureLoaded(); return SettingsStore.Draft; }
    }

    // ================================================================ display
    public static float FieldOfView { get { return Live.fieldOfView; } }
    public static bool VSync { get { return D.vSync; } }
    public static int FrameRateCap { get { return D.frameRateCap; } }

    // =============================================================== graphics
    public static int Quality { get { return D.qualityLevel; } }
    public static string QualityPresetName { get { return D.qualityPreset; } }
    public static bool PostProcessing { get { return D.postProcessing; } }

    // ================================================================== audio
    public static float MasterVolume { get { return Live.masterVolume; } }
    public static float MusicVolume { get { return Live.musicVolume; } }
    public static float SfxVolume { get { return Live.sfxVolume; } }
    public static float VoiceVolume { get { return Live.voiceVolume; } }
    public static float AmbienceVolume { get { return Live.ambienceVolume; } }
    public static bool MuteWhenUnfocused { get { return D.muteWhenUnfocused; } }

    // =============================================================== gameplay
    public static float MouseSensitivity { get { return Live.mouseSensitivity; } }
    public static bool InvertY { get { return D.invertY; } }
    public static bool InvertZoom { get { return D.invertZoom; } }
    public static float ZoomSpeed { get { return Live.zoomSpeed; } }
    public static float DialogueTextSpeed { get { return Live.dialogueTextSpeed; } }
    public static bool DialogueInstant { get { return Live.dialogueTextSpeed >= 3.99f; } }
    public static bool DialogueAutoAdvance { get { return D.dialogueAutoAdvance; } }
    public static float DialogueAutoDelay { get { return D.dialogueAutoDelay; } }
    public static bool Subtitles { get { return D.subtitles; } }
    public static bool ShowInteractHints { get { return D.showInteractHints; } }
    public static bool CameraShake { get { return D.cameraShake; } }

    // ============================================================== interface
    public static string MenuAnimation { get { return D.menuAnimation; } }
    public static bool ShowFps { get { return Live.showFps; } }
    public static bool ShowTooltips { get { return Live.showTooltips; } }

    // The menu writes this one directly, because the animation picker applies
    // as a preview and the menu has to follow it immediately.
    public static void SetMenuAnimation(string name)
    {
        SettingsStore.EnsureLoaded();
        SettingsStore.Draft.menuAnimation = name;
        SettingsStore.Saved.menuAnimation = name;
    }
}
