// Every PlayerPrefs key the game uses, in one place.
//
// MouseSensitivity is load-bearing: Gameplay/CameraFollow.cs has read that
// exact string since before GameSettings existed, so it must not be renamed
// without updating that script too.
public static class SettingsKeys
{
    // ---- display
    public const string Quality        = "Quality";
    public const string ResolutionIdx  = "ResolutionIndex";
    public const string WindowMode     = "WindowMode";
    public const string Fullscreen     = "Fullscreen";      // legacy bool, migrated
    public const string VSync          = "VSync";
    public const string FrameRateCap   = "FrameRateCap";
    public const string FieldOfView    = "FieldOfView";
    public const string AntiAliasing   = "AntiAliasing";
    public const string RenderScale    = "RenderScale";

    // ---- audio  (the three volume keys live on UIAudio, which owns mixing)
    // UIAudio.K_Master / K_Music / K_Sfx

    // ---- gameplay
    public const string MouseSens      = "MouseSensitivity";

    // ---- interface
    public const string MenuAnim       = "MenuAnimStyle";

    // ---- bookkeeping
    public const string LastChapter    = "LastChapter";
    public const string HasLaunched    = "HasLaunchedBefore";

    // Pre-split builds stored one combined volume here; folded into the master
    // volume on first run and then ignored.
    public const string LegacyVolume   = "Volume";

    // Cleared by OPTIONS > INTERFACE > RESET ALL SETTINGS.
    // Save files and LastChapter are deliberately absent: resetting
    // preferences must never touch progress.
    public static readonly string[] AllSettings =
    {
        Quality, ResolutionIdx, WindowMode, Fullscreen, VSync, FrameRateCap,
        FieldOfView, AntiAliasing, RenderScale,
        MouseSens, MenuAnim, LegacyVolume,
        UIAudio.K_Master, UIAudio.K_Music, UIAudio.K_Sfx
    };
}
