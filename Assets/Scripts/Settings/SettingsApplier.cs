using UnityEngine;
using UnityEngine.Rendering;

// The only place that talks to the engine about settings.
//
// Kept apart from the store so the store stays pure bookkeeping and this stays
// a straight list of "value -> engine call". Everything here is idempotent and
// safe to call every time anything changes.
public static class SettingsApplier
{
    public static void ApplyAll(GameSettingsData d)
    {
        if (d == null) return;

        ApplyQuality(d);
        ApplyResolution(d);
        ApplyFramePacing(d);
        ApplyAudio(d);
        // Field of view needs a live scene object; SettingsRuntime
        // owns those and reacts to SettingsStore.Applied.
    }

    // ---------------------------------------------------------------- quality
    public static void ApplyQuality(GameSettingsData d)
    {
        int level = Mathf.Clamp(d.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));

        // Switching tier resets most of the fields below, so it goes first and
        // everything else is re-pushed on top of it.
        QualitySettings.SetQualityLevel(level, true);

        QualitySettings.antiAliasing = d.antiAliasing;
        QualitySettings.globalTextureMipmapLimit = Mathf.Clamp(d.textureQuality, 0, 3);
        QualitySettings.anisotropicFiltering = (AnisotropicFiltering)Mathf.Clamp(d.anisotropicFiltering, 0, 2);
        QualitySettings.softParticles = d.softParticles;
        QualitySettings.shadowDistance = d.shadowQuality > 0 ? d.shadowDistance : 0f;

        switch (Mathf.Clamp(d.shadowQuality, 0, 3))
        {
            case 0: QualitySettings.shadows = ShadowQuality.Disable; break;
            case 1: QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowResolution = ShadowResolution.Low; break;
            case 2: QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowResolution = ShadowResolution.Medium; break;
            default: QualitySettings.shadows = ShadowQuality.All;
                     QualitySettings.shadowResolution = ShadowResolution.VeryHigh; break;
        }

        var urp = GraphicsSettings.defaultRenderPipeline as
            UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.msaaSampleCount = d.antiAliasing <= 0 ? 1 : d.antiAliasing;
            urp.renderScale = Mathf.Clamp(d.renderScale, 0.1f, 2f);
            urp.shadowDistance = d.shadowQuality > 0 ? d.shadowDistance : 0f;
            urp.supportsHDR = d.postProcessing;
        }
    }

    // ------------------------------------------------------------- resolution
    public static void ApplyResolution(GameSettingsData d)
    {
        if (d.resolutionWidth <= 0 || d.resolutionHeight <= 0) return;

        var mode = (FullScreenMode)d.windowMode;

        bool sameSize = Screen.width == d.resolutionWidth && Screen.height == d.resolutionHeight;
        bool sameMode = Screen.fullScreenMode == mode;
        if (sameSize && sameMode) return;   // avoid a pointless mode flicker

        if (d.refreshRateHz > 0)
        {
            var rate = new RefreshRate { numerator = (uint)d.refreshRateHz, denominator = 1 };
            Screen.SetResolution(d.resolutionWidth, d.resolutionHeight, mode, rate);
        }
        else
        {
            Screen.SetResolution(d.resolutionWidth, d.resolutionHeight, mode);
        }
    }

    // ----------------------------------------------------------- frame pacing
    public static void ApplyFramePacing(GameSettingsData d)
    {
        QualitySettings.vSyncCount = d.vSync ? 1 : 0;

        // Unity ignores targetFrameRate entirely while vsync is on, so the cap
        // is only meaningful with vsync off.
        Application.targetFrameRate = d.vSync ? -1 : (d.frameRateCap <= 0 ? -1 : d.frameRateCap);
    }

    // ------------------------------------------------------------------ audio
    public static void ApplyAudio(GameSettingsData d)
    {
        AudioListener.volume = Mathf.Clamp01(d.masterVolume);
        UIAudio.RefreshVolumes();
    }
}
