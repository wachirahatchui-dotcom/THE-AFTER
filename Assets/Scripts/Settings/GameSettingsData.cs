using System;
using UnityEngine;

// Every player-facing setting, as one serialisable bag of values.
//
// This is pure data: no logic, no engine calls. SettingsStore keeps two of
// them - the saved one and the draft the options screen is editing - and
// dirty-tracking is just a field-by-field comparison between the two.
//
// Resolution is stored as width/height/refresh rather than as an index into
// Screen.resolutions. Indices shift when the player changes monitor or driver,
// which would silently move them to a different resolution on next launch.
[Serializable]
public class GameSettingsData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;

    // ================================================================ display
    public int resolutionWidth;
    public int resolutionHeight;
    public int refreshRateHz;
    public int windowMode = (int)FullScreenMode.FullScreenWindow;
    public bool vSync = true;
    public int frameRateCap;                 // 0 = unlimited
    public float fieldOfView = 60f;

    // =============================================================== graphics
    public string qualityPreset = QualityPreset.CustomName;
    public int qualityLevel;
    public int antiAliasing = 2;             // 0 / 2 / 4 / 8
    public float renderScale = 1f;           // 0.5 .. 1.5
    public int shadowQuality = 2;            // 0 off .. 3 very high
    public float shadowDistance = 50f;
    public int textureQuality;               // 0 = full res, 1 = half, ...
    public int anisotropicFiltering = 1;     // AnisotropicFiltering enum
    public bool softParticles = true;
    public bool postProcessing = true;

    // ================================================================== audio
    public float masterVolume = 1f;
    public float musicVolume = 0.55f;
    public float sfxVolume = 0.8f;
    public float voiceVolume = 0.9f;
    public float ambienceVolume = 0.7f;
    public bool muteWhenUnfocused = true;

    // =============================================================== gameplay
    public float mouseSensitivity = 0.28f;
    public bool invertY;
    public bool invertZoom;
    public float zoomSpeed = 1f;
    public float dialogueTextSpeed = 1f;     // multiplier on typing rate
    public bool dialogueAutoAdvance;
    public float dialogueAutoDelay = 2f;
    public bool showInteractHints = true;
    public bool subtitles = true;
    public bool cameraShake = true;

    // ============================================================== interface
    public string menuAnimation = "RiseFade";
    public bool showFps;
    public bool showTooltips = true;

    // Screen.currentResolution is only meaningful once a window exists, so the
    // resolution fields start empty and are filled on first load.
    public void FillMissingResolution()
    {
        if (resolutionWidth > 0 && resolutionHeight > 0) return;

        resolutionWidth = Screen.width;
        resolutionHeight = Screen.height;
        refreshRateHz = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
    }

    // Brings a file written by an older build up to the current shape.
    public void Migrate()
    {
        // v1 is the first versioned format; nothing to migrate yet. New fields
        // added later get their declared defaults automatically, because
        // JsonUtility leaves absent fields untouched.
        version = CurrentVersion;
    }

    public GameSettingsData Clone()
    {
        // Round-tripping through JSON is the cheapest deep copy that stays
        // correct when fields are added, with no reflection and nothing to
        // remember to update.
        return JsonUtility.FromJson<GameSettingsData>(JsonUtility.ToJson(this));
    }
}
