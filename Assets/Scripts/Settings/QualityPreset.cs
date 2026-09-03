using System.Collections.Generic;
using UnityEngine;

// The Low / Medium / High / Ultra buttons.
//
// A preset is just a bundle of the graphics fields. Applying one writes them
// all; changing any of them by hand afterwards no longer matches a bundle, so
// Detect() reports Custom and the UI flips the label - which is how the player
// can tell a preset is still intact.
public static class QualityPreset
{
    public const string CustomName = "Custom";

    public static readonly string[] Names = { "Low", "Medium", "High", "Ultra", CustomName };

    // Everything a preset controls. Nothing outside this list is touched, so
    // applying a preset never resets audio, controls or resolution.
    public class Bundle
    {
        public int qualityLevel;
        public int antiAliasing;
        public float renderScale;
        public int shadowQuality;
        public float shadowDistance;
        public int textureQuality;
        public int anisotropicFiltering;
        public bool softParticles;
        public bool postProcessing;
    }

    static readonly Dictionary<string, Bundle> bundles = new Dictionary<string, Bundle>
    {
        { "Low", new Bundle {
            qualityLevel = 0, antiAliasing = 0, renderScale = 0.7f,
            shadowQuality = 0, shadowDistance = 20f, textureQuality = 2,
            anisotropicFiltering = (int)AnisotropicFiltering.Disable,
            softParticles = false, postProcessing = false } },

        { "Medium", new Bundle {
            qualityLevel = 1, antiAliasing = 2, renderScale = 0.85f,
            shadowQuality = 1, shadowDistance = 35f, textureQuality = 1,
            anisotropicFiltering = (int)AnisotropicFiltering.Enable,
            softParticles = false, postProcessing = true } },

        { "High", new Bundle {
            qualityLevel = 2, antiAliasing = 4, renderScale = 1f,
            shadowQuality = 2, shadowDistance = 60f, textureQuality = 0,
            anisotropicFiltering = (int)AnisotropicFiltering.Enable,
            softParticles = true, postProcessing = true } },

        { "Ultra", new Bundle {
            qualityLevel = 3, antiAliasing = 8, renderScale = 1.25f,
            shadowQuality = 3, shadowDistance = 110f, textureQuality = 0,
            anisotropicFiltering = (int)AnisotropicFiltering.ForceEnable,
            softParticles = true, postProcessing = true } },
    };

    public static bool TryGet(string name, out Bundle bundle)
    {
        return bundles.TryGetValue(name ?? "", out bundle);
    }

    // Writes a preset into a settings bag. Clamped to the quality levels this
    // project actually has, which may be fewer than four.
    public static void Apply(string name, GameSettingsData data)
    {
        Bundle b;
        if (data == null || !TryGet(name, out b)) return;

        int maxLevel = Mathf.Max(0, QualitySettings.names.Length - 1);

        data.qualityLevel = Mathf.Clamp(b.qualityLevel, 0, maxLevel);
        data.antiAliasing = b.antiAliasing;
        data.renderScale = b.renderScale;
        data.shadowQuality = b.shadowQuality;
        data.shadowDistance = b.shadowDistance;
        data.textureQuality = b.textureQuality;
        data.anisotropicFiltering = b.anisotropicFiltering;
        data.softParticles = b.softParticles;
        data.postProcessing = b.postProcessing;
        data.qualityPreset = name;
    }

    // Which preset, if any, the current values still correspond to.
    public static string Detect(GameSettingsData data)
    {
        if (data == null) return CustomName;

        int maxLevel = Mathf.Max(0, QualitySettings.names.Length - 1);

        foreach (var pair in bundles)
        {
            var b = pair.Value;
            if (data.qualityLevel != Mathf.Clamp(b.qualityLevel, 0, maxLevel)) continue;
            if (data.antiAliasing != b.antiAliasing) continue;
            if (!Mathf.Approximately(data.renderScale, b.renderScale)) continue;
            if (data.shadowQuality != b.shadowQuality) continue;
            if (!Mathf.Approximately(data.shadowDistance, b.shadowDistance)) continue;
            if (data.textureQuality != b.textureQuality) continue;
            if (data.anisotropicFiltering != b.anisotropicFiltering) continue;
            if (data.softParticles != b.softParticles) continue;
            if (data.postProcessing != b.postProcessing) continue;

            return pair.Key;
        }

        return CustomName;
    }

    public static int IndexOf(string name)
    {
        for (int i = 0; i < Names.Length; i++)
            if (Names[i] == name) return i;
        return Names.Length - 1;   // Custom
    }
}
