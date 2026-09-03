using System.Collections.Generic;
using UnityEngine;

// Every setting the game has, declared once.
//
// The options screen is generated from this list, so adding a row is adding an
// entry here - nothing in the UI needs to change. Descriptions live next to
// the setting they describe rather than in a separate strings file, so they
// cannot drift out of sync with what the setting actually does.
public static class SettingsCatalog
{
    static List<SettingEntry> all;
    static Resolution[] resolutions;

    public static List<SettingEntry> All
    {
        get { if (all == null) Build(); return all; }
    }

    public static List<SettingEntry> For(SettingCategory category)
    {
        var list = new List<SettingEntry>();
        foreach (var e in All)
            if (e.category == category) list.Add(e);
        return list;
    }

    public static SettingEntry Find(string id)
    {
        foreach (var e in All)
            if (e.id == id) return e;
        return null;
    }

    // Screen.resolutions is queried once: it is not cheap and it does not
    // change while the game runs.
    public static Resolution[] Resolutions
    {
        get
        {
            if (resolutions == null || resolutions.Length == 0)
            {
                resolutions = Screen.resolutions;
                if (resolutions.Length == 0)
                    resolutions = new[] { Screen.currentResolution };
            }
            return resolutions;
        }
    }

    public static int ResolutionIndexOf(GameSettingsData d)
    {
        var list = Resolutions;
        for (int i = 0; i < list.Length; i++)
            if (list[i].width == d.resolutionWidth && list[i].height == d.resolutionHeight)
                return i;

        // Nothing matched (monitor changed): fall back to the largest.
        return list.Length - 1;
    }

    static List<string> ResolutionLabels()
    {
        var names = new List<string>();
        foreach (var r in Resolutions)
            names.Add(r.width + " x " + r.height + "  " + Mathf.RoundToInt((float)r.refreshRateRatio.value) + "Hz");
        return names;
    }

    public static readonly FullScreenMode[] WindowModes =
    {
        FullScreenMode.Windowed,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.ExclusiveFullScreen
    };

    static List<string> WindowModeLabels()
    {
        return new List<string> { "Windowed", "Borderless", "Fullscreen" };
    }

    public static readonly int[] FrameRateOptions = { 0, 30, 60, 90, 120, 144, 165, 240 };
    public static readonly int[] AntiAliasOptions = { 0, 2, 4, 8 };

    static List<string> FrameRateLabels()
    {
        var l = new List<string>();
        foreach (var f in FrameRateOptions) l.Add(f <= 0 ? "Unlimited" : f + " FPS");
        return l;
    }

    static List<string> AntiAliasLabels()
    {
        var l = new List<string>();
        foreach (var a in AntiAliasOptions) l.Add(a <= 0 ? "Off" : a + "x MSAA");
        return l;
    }

    static int IndexIn(int[] options, int value)
    {
        for (int i = 0; i < options.Length; i++)
            if (options[i] == value) return i;
        return 0;
    }

    // ================================================================== build
    static void Build()
    {
        all = new List<SettingEntry>();

        BuildDisplay();
        BuildGraphics();
        BuildAudio();
        BuildGameplay();
        BuildInterface();
    }

    static void Add(SettingEntry e) { all.Add(e); }

    // ---------------------------------------------------------------- display
    static void BuildDisplay()
    {
        Add(new SettingEntry
        {
            id = "resolution", label = "Resolution", category = SettingCategory.Display,
            kind = SettingKind.Choice, apply = ApplyMode.Confirm,
            description = "Size of the game image. Lower resolutions run faster but look softer.",
            choices = ResolutionLabels,
            getIndex = ResolutionIndexOf,
            setIndex = (d, i) =>
            {
                var r = Resolutions[Mathf.Clamp(i, 0, Resolutions.Length - 1)];
                d.resolutionWidth = r.width;
                d.resolutionHeight = r.height;
                d.refreshRateHz = Mathf.RoundToInt((float)r.refreshRateRatio.value);
            }
        });

        Add(new SettingEntry
        {
            id = "windowMode", label = "Window Mode", category = SettingCategory.Display,
            kind = SettingKind.Choice, apply = ApplyMode.Confirm,
            description = "Borderless matches your desktop and alt-tabs instantly. Fullscreen can be slightly faster.",
            choices = WindowModeLabels,
            getIndex = d => Mathf.Max(0, System.Array.IndexOf(WindowModes, (FullScreenMode)d.windowMode)),
            setIndex = (d, i) => d.windowMode = (int)WindowModes[Mathf.Clamp(i, 0, WindowModes.Length - 1)]
        });

        Add(new SettingEntry
        {
            id = "vsync", label = "V-Sync", category = SettingCategory.Display,
            kind = SettingKind.Toggle,
            description = "Locks the frame rate to your monitor to remove tearing, at the cost of a little input lag.",
            getBool = d => d.vSync, setBool = (d, v) => d.vSync = v
        });

        Add(new SettingEntry
        {
            id = "frameCap", label = "Frame Rate Limit", category = SettingCategory.Display,
            kind = SettingKind.Choice,
            description = "Caps how many frames are drawn. Ignored while V-Sync is on.",
            isEnabled = d => !d.vSync,
            choices = FrameRateLabels,
            getIndex = d => IndexIn(FrameRateOptions, d.frameRateCap),
            setIndex = (d, i) => d.frameRateCap = FrameRateOptions[Mathf.Clamp(i, 0, FrameRateOptions.Length - 1)]
        });

        Add(new SettingEntry
        {
            id = "fov", label = "Field of View", category = SettingCategory.Display,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "How much of the world fits on screen. Wider shows more but distorts the edges.",
            min = 40f, max = 100f,
            getFloat = d => d.fieldOfView, setFloat = (d, v) => d.fieldOfView = v,
            formatFloat = v => Mathf.RoundToInt(v) + " deg"
        });
    }

    // --------------------------------------------------------------- graphics
    static void BuildGraphics()
    {
        Add(new SettingEntry
        {
            id = "preset", label = "Quality Preset", category = SettingCategory.Graphics,
            kind = SettingKind.Choice,
            description = "Sets everything below at once. Changing any of them by hand switches this to Custom.",
            choices = () => new List<string>(QualityPreset.Names),
            getIndex = d => QualityPreset.IndexOf(d.qualityPreset),
            setIndex = (d, i) =>
            {
                string name = QualityPreset.Names[Mathf.Clamp(i, 0, QualityPreset.Names.Length - 1)];
                if (name == QualityPreset.CustomName) { d.qualityPreset = name; return; }
                QualityPreset.Apply(name, d);
            }
        });

        Add(new SettingEntry
        {
            id = "quality", label = "Detail Level", category = SettingCategory.Graphics,
            kind = SettingKind.Choice, partOfPreset = true,
            description = "Unity's overall quality tier: draw distance, LOD bias and particle budget.",
            choices = () => new List<string>(QualitySettings.names),
            getIndex = d => d.qualityLevel,
            setIndex = (d, i) => d.qualityLevel = i
        });

        Add(new SettingEntry
        {
            id = "aa", label = "Anti-Aliasing", category = SettingCategory.Graphics,
            kind = SettingKind.Choice, partOfPreset = true,
            description = "Smooths jagged edges. Higher costs more, with diminishing returns past 4x.",
            choices = AntiAliasLabels,
            getIndex = d => IndexIn(AntiAliasOptions, d.antiAliasing),
            setIndex = (d, i) => d.antiAliasing = AntiAliasOptions[Mathf.Clamp(i, 0, AntiAliasOptions.Length - 1)]
        });

        Add(new SettingEntry
        {
            id = "renderScale", label = "Render Scale", category = SettingCategory.Graphics,
            kind = SettingKind.Slider, partOfPreset = true,
            description = "Renders the world above or below your resolution, then rescales. The strongest performance dial.",
            min = 0.5f, max = 1.5f,
            getFloat = d => d.renderScale, setFloat = (d, v) => d.renderScale = v,
            formatFloat = v => Mathf.RoundToInt(v * 100f) + "%"
        });

        Add(new SettingEntry
        {
            id = "shadowQuality", label = "Shadows", category = SettingCategory.Graphics,
            kind = SettingKind.Choice, partOfPreset = true,
            description = "Shadow resolution. Turning shadows off is a large gain on weak hardware.",
            choices = () => new List<string> { "Off", "Low", "Medium", "High" },
            getIndex = d => Mathf.Clamp(d.shadowQuality, 0, 3),
            setIndex = (d, i) => d.shadowQuality = Mathf.Clamp(i, 0, 3)
        });

        Add(new SettingEntry
        {
            id = "shadowDistance", label = "Shadow Distance", category = SettingCategory.Graphics,
            kind = SettingKind.Slider, partOfPreset = true,
            description = "How far from the camera shadows are still drawn.",
            isEnabled = d => d.shadowQuality > 0,
            min = 10f, max = 150f,
            getFloat = d => d.shadowDistance, setFloat = (d, v) => d.shadowDistance = v,
            formatFloat = v => Mathf.RoundToInt(v) + " m"
        });

        Add(new SettingEntry
        {
            id = "textureQuality", label = "Texture Quality", category = SettingCategory.Graphics,
            kind = SettingKind.Choice, partOfPreset = true,
            description = "Lowering this frees video memory. Mostly matters on cards with under 4GB.",
            choices = () => new List<string> { "Full", "Half", "Quarter", "Eighth" },
            getIndex = d => Mathf.Clamp(d.textureQuality, 0, 3),
            setIndex = (d, i) => d.textureQuality = Mathf.Clamp(i, 0, 3)
        });

        Add(new SettingEntry
        {
            id = "aniso", label = "Anisotropic Filtering", category = SettingCategory.Graphics,
            kind = SettingKind.Choice, partOfPreset = true,
            description = "Keeps ground textures sharp when seen at a shallow angle. Very cheap.",
            choices = () => new List<string> { "Off", "Per Texture", "Forced On" },
            getIndex = d => Mathf.Clamp(d.anisotropicFiltering, 0, 2),
            setIndex = (d, i) => d.anisotropicFiltering = Mathf.Clamp(i, 0, 2)
        });

        Add(new SettingEntry
        {
            id = "softParticles", label = "Soft Particles", category = SettingCategory.Graphics,
            kind = SettingKind.Toggle, partOfPreset = true,
            description = "Stops smoke and dust cutting a hard line where they meet geometry.",
            getBool = d => d.softParticles, setBool = (d, v) => d.softParticles = v
        });

        Add(new SettingEntry
        {
            id = "postProcessing", label = "Post Processing", category = SettingCategory.Graphics,
            kind = SettingKind.Toggle, partOfPreset = true,
            description = "Colour grading, bloom and vignette. Turning it off changes the look considerably.",
            getBool = d => d.postProcessing, setBool = (d, v) => d.postProcessing = v
        });
    }

    // ------------------------------------------------------------------ audio
    static void BuildAudio()
    {
        Add(new SettingEntry
        {
            id = "master", label = "Master Volume", category = SettingCategory.Audio,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "Scales everything below it.",
            min = 0f, max = 1f,
            getFloat = d => d.masterVolume, setFloat = (d, v) => d.masterVolume = v,
            formatFloat = Percent
        });

        Add(new SettingEntry
        {
            id = "music", label = "Music", category = SettingCategory.Audio,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "The menu theme and any scored moments.",
            min = 0f, max = 1f,
            getFloat = d => d.musicVolume, setFloat = (d, v) => d.musicVolume = v,
            formatFloat = Percent
        });

        Add(new SettingEntry
        {
            id = "sfx", label = "Sound Effects", category = SettingCategory.Audio,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "Interface clicks, footsteps and world sounds.",
            min = 0f, max = 1f,
            getFloat = d => d.sfxVolume, setFloat = (d, v) => d.sfxVolume = v,
            formatFloat = Percent
        });

        Add(new SettingEntry
        {
            id = "voice", label = "Voice", category = SettingCategory.Audio,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "Spoken dialogue only. Lower it if you prefer to read at your own pace.",
            min = 0f, max = 1f,
            getFloat = d => d.voiceVolume, setFloat = (d, v) => d.voiceVolume = v,
            formatFloat = Percent
        });

        Add(new SettingEntry
        {
            id = "ambience", label = "Ambience", category = SettingCategory.Audio,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "Wind and background atmosphere.",
            min = 0f, max = 1f,
            getFloat = d => d.ambienceVolume, setFloat = (d, v) => d.ambienceVolume = v,
            formatFloat = Percent
        });

        Add(new SettingEntry
        {
            id = "muteUnfocused", label = "Mute When Minimised", category = SettingCategory.Audio,
            kind = SettingKind.Toggle,
            description = "Silences the game while another window has focus.",
            getBool = d => d.muteWhenUnfocused, setBool = (d, v) => d.muteWhenUnfocused = v
        });
    }

    // --------------------------------------------------------------- gameplay
    static void BuildGameplay()
    {
        Add(new SettingEntry
        {
            id = "mouseSens", label = "Mouse Sensitivity", category = SettingCategory.Gameplay,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "How fast the camera orbits while you hold the right mouse button.",
            min = 0.05f, max = 1f,
            getFloat = d => d.mouseSensitivity, setFloat = (d, v) => d.mouseSensitivity = v,
            formatFloat = v => v.ToString("0.00")
        });

        Add(new SettingEntry
        {
            id = "invertY", label = "Invert Vertical Look", category = SettingCategory.Gameplay,
            kind = SettingKind.Toggle,
            description = "Push the mouse forward to look down instead of up.",
            getBool = d => d.invertY, setBool = (d, v) => d.invertY = v
        });

        Add(new SettingEntry
        {
            id = "invertZoom", label = "Invert Zoom", category = SettingCategory.Gameplay,
            kind = SettingKind.Toggle,
            description = "Swap which way the scroll wheel pushes the camera.",
            getBool = d => d.invertZoom, setBool = (d, v) => d.invertZoom = v
        });

        Add(new SettingEntry
        {
            id = "zoomSpeed", label = "Zoom Speed", category = SettingCategory.Gameplay,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "How far one notch of the scroll wheel moves the camera.",
            min = 0.25f, max = 3f,
            getFloat = d => d.zoomSpeed, setFloat = (d, v) => d.zoomSpeed = v,
            formatFloat = v => v.ToString("0.00") + "x"
        });

        Add(new SettingEntry
        {
            id = "textSpeed", label = "Dialogue Speed", category = SettingCategory.Gameplay,
            kind = SettingKind.Slider, apply = ApplyMode.LivePreview,
            description = "How fast dialogue types itself out. All the way right prints instantly.",
            min = 0.25f, max = 4f,
            getFloat = d => d.dialogueTextSpeed, setFloat = (d, v) => d.dialogueTextSpeed = v,
            formatFloat = v => v >= 3.99f ? "Instant" : v.ToString("0.00") + "x"
        });

        Add(new SettingEntry
        {
            id = "autoAdvance", label = "Auto-Advance Dialogue", category = SettingCategory.Gameplay,
            kind = SettingKind.Toggle,
            description = "Moves to the next line on its own once the current one has finished.",
            getBool = d => d.dialogueAutoAdvance, setBool = (d, v) => d.dialogueAutoAdvance = v
        });

        Add(new SettingEntry
        {
            id = "autoDelay", label = "Auto-Advance Delay", category = SettingCategory.Gameplay,
            kind = SettingKind.Slider,
            description = "How long a finished line stays up before the next one.",
            isEnabled = d => d.dialogueAutoAdvance,
            min = 0.5f, max = 8f,
            getFloat = d => d.dialogueAutoDelay, setFloat = (d, v) => d.dialogueAutoDelay = v,
            formatFloat = v => v.ToString("0.0") + " s"
        });

        Add(new SettingEntry
        {
            id = "subtitles", label = "Subtitles", category = SettingCategory.Gameplay,
            kind = SettingKind.Toggle,
            description = "Show the dialogue text alongside spoken lines.",
            getBool = d => d.subtitles, setBool = (d, v) => d.subtitles = v
        });

        Add(new SettingEntry
        {
            id = "hints", label = "Interaction Hints", category = SettingCategory.Gameplay,
            kind = SettingKind.Toggle,
            description = "Show the \"Press E\" prompt when you are near someone you can talk to.",
            getBool = d => d.showInteractHints, setBool = (d, v) => d.showInteractHints = v
        });

        Add(new SettingEntry
        {
            id = "cameraShake", label = "Camera Shake", category = SettingCategory.Gameplay,
            kind = SettingKind.Toggle,
            description = "Turn off if screen movement makes you uncomfortable.",
            getBool = d => d.cameraShake, setBool = (d, v) => d.cameraShake = v
        });
    }

    // -------------------------------------------------------------- interface
    static void BuildInterface()
    {
        Add(new SettingEntry
        {
            id = "menuAnim", label = "Menu Animation", category = SettingCategory.Interface,
            kind = SettingKind.Choice, apply = ApplyMode.LivePreview,
            description = "How menu pages enter and leave. Instant removes the motion entirely.",
            choices = MenuAnimationNames,
            getIndex = d =>
            {
                var list = MenuAnimationNames();
                int i = list.IndexOf(d.menuAnimation);
                return i < 0 ? 0 : i;
            },
            setIndex = (d, i) =>
            {
                var list = MenuAnimationNames();
                d.menuAnimation = list[Mathf.Clamp(i, 0, list.Count - 1)];
            }
        });

        Add(new SettingEntry
        {
            id = "showFps", label = "Show FPS Counter", category = SettingCategory.Interface,
            kind = SettingKind.Toggle, apply = ApplyMode.LivePreview,
            description = "A small frame rate readout in the corner. Useful while tuning the graphics settings.",
            getBool = d => d.showFps, setBool = (d, v) => d.showFps = v
        });

        Add(new SettingEntry
        {
            id = "tooltips", label = "Setting Descriptions", category = SettingCategory.Interface,
            kind = SettingKind.Toggle, apply = ApplyMode.LivePreview,
            description = "Show the explanatory line under every setting, like this one.",
            getBool = d => d.showTooltips, setBool = (d, v) => d.showTooltips = v
        });
    }

    static List<string> cachedAnimNames;

    static List<string> MenuAnimationNames()
    {
        if (cachedAnimNames == null)
        {
            cachedAnimNames = new List<string>(UIAnimLibrary.InNames);
            cachedAnimNames.Sort(System.StringComparer.OrdinalIgnoreCase);
        }
        return cachedAnimNames;
    }

    static string Percent(float v) { return Mathf.RoundToInt(v * 100f) + "%"; }
}
