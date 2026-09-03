using System;
using System.IO;
using UnityEngine;

// Reading and writing settings.json.
//
// Lives at Application.persistentDataPath/settings.json so the player can open
// it, edit it, back it up or delete it. Written through a temp file + replace
// with a .bak kept behind, so a crash mid-write or a hand-edit that breaks the
// JSON never costs the whole configuration.
//
// On first run it migrates whatever the old PlayerPrefs build left behind, so
// nobody loses the settings they already chose.
public static class SettingsFile
{
    public const string FileName = "settings.json";

    public static string Path
    {
        get { return System.IO.Path.Combine(Application.persistentDataPath, FileName); }
    }

    public static string BackupPath { get { return Path + ".bak"; } }

    public static bool Exists { get { return File.Exists(Path); } }

    // ================================================================== load
    public static GameSettingsData Load()
    {
        var data = ReadFrom(Path);

        if (data == null && File.Exists(BackupPath))
        {
            Debug.LogWarning("[SettingsFile] settings.json unreadable, falling back to the backup.");
            data = ReadFrom(BackupPath);
        }

        if (data == null)
        {
            data = MigrateFromPlayerPrefs();
            if (data != null) Debug.Log("[SettingsFile] Migrated settings from PlayerPrefs.");
        }

        if (data == null) data = new GameSettingsData();

        data.Migrate();
        data.FillMissingResolution();
        return data;
    }

    static GameSettingsData ReadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;

            string json = File.ReadAllText(path);
            if (string.IsNullOrEmpty(json)) return null;

            return JsonUtility.FromJson<GameSettingsData>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SettingsFile] Could not read " + path + ": " + e.Message);
            return null;
        }
    }

    // ================================================================== save
    public static bool Save(GameSettingsData data)
    {
        if (data == null) return false;

        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);

            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));

            // Keep the previous good file as the backup before replacing it.
            if (File.Exists(Path))
            {
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
                File.Move(Path, BackupPath);
            }

            File.Move(tmp, Path);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SettingsFile] Could not write settings: " + e.Message);
            return false;
        }
    }

    public static void Delete()
    {
        try
        {
            if (File.Exists(Path)) File.Delete(Path);
            if (File.Exists(BackupPath)) File.Delete(BackupPath);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SettingsFile] Could not delete settings: " + e.Message);
        }
    }

    // ============================================================ import
    public static bool Export(GameSettingsData data, string destination)
    {
        try
        {
            File.WriteAllText(destination, JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SettingsFile] Export failed: " + e.Message);
            return false;
        }
    }

    public static GameSettingsData Import(string source)
    {
        var data = ReadFrom(source);
        if (data == null) return null;

        data.Migrate();
        data.FillMissingResolution();
        return data;
    }

    // ============================================================= migration
    // Reads the keys the pre-JSON build used. Returns null when there is
    // nothing to migrate, so a fresh install just gets the defaults.
    static GameSettingsData MigrateFromPlayerPrefs()
    {
        bool any =
            PlayerPrefs.HasKey(SettingsKeys.Quality) ||
            PlayerPrefs.HasKey(SettingsKeys.MouseSens) ||
            PlayerPrefs.HasKey(SettingsKeys.FieldOfView) ||
            PlayerPrefs.HasKey(UIAudio.K_Master) ||
            PlayerPrefs.HasKey(SettingsKeys.LegacyVolume);

        if (!any) return null;

        var d = new GameSettingsData();

        d.qualityLevel = PlayerPrefs.GetInt(SettingsKeys.Quality, QualitySettings.GetQualityLevel());
        d.vSync = PlayerPrefs.GetInt(SettingsKeys.VSync, 1) == 1;
        d.frameRateCap = PlayerPrefs.GetInt(SettingsKeys.FrameRateCap, 0);
        d.fieldOfView = PlayerPrefs.GetFloat(SettingsKeys.FieldOfView, 60f);
        d.antiAliasing = PlayerPrefs.GetInt(SettingsKeys.AntiAliasing, 2);
        d.renderScale = PlayerPrefs.GetFloat(SettingsKeys.RenderScale, 1f);
        d.mouseSensitivity = PlayerPrefs.GetFloat(SettingsKeys.MouseSens, 0.28f);
        d.menuAnimation = PlayerPrefs.GetString(SettingsKeys.MenuAnim, "RiseFade");

        if (PlayerPrefs.HasKey(SettingsKeys.WindowMode))
            d.windowMode = PlayerPrefs.GetInt(SettingsKeys.WindowMode);
        else if (PlayerPrefs.HasKey(SettingsKeys.Fullscreen))
            d.windowMode = PlayerPrefs.GetInt(SettingsKeys.Fullscreen) == 1
                ? (int)FullScreenMode.FullScreenWindow
                : (int)FullScreenMode.Windowed;

        // The oldest builds had one combined volume; fold it into the master.
        float legacy = PlayerPrefs.GetFloat(SettingsKeys.LegacyVolume, 1f);
        d.masterVolume = PlayerPrefs.GetFloat(UIAudio.K_Master, legacy);
        d.musicVolume = PlayerPrefs.GetFloat(UIAudio.K_Music, 0.55f);
        d.sfxVolume = PlayerPrefs.GetFloat(UIAudio.K_Sfx, 0.8f);

        Save(d);
        return d;
    }
}
