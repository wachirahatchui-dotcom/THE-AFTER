using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// The contents of one save slot.
//
// Flat and [Serializable] so JsonUtility can round-trip it without custom
// code, and versioned so an older file can be migrated instead of discarded.
//
// Systems beyond the player and camera do not get fields here - they plug in
// through `modules` via ISaveable. See SaveData/ISaveable.cs.
[Serializable]
public class SaveSlotData
{
    public const int CurrentVersion = 2;

    public int version = CurrentVersion;

    // ---- where
    public string sceneName = "";
    public string chapterName = "Chapter 1";

    // ---- when
    public float playTimeSeconds;
    public string savedAtIso = "";

    // ---- player
    public Vector3 playerPosition;
    public float playerYaw;

    // ---- camera (so a load restores the exact framing, not a default orbit)
    public float cameraYaw;
    public float cameraPitch = 32f;
    public float cameraDistance = 8f;

    // ---- story flags
    public List<string> seenDialogues = new List<string>();

    // ---- everything else, contributed by ISaveable implementations
    public List<SaveEntry> modules = new List<SaveEntry>();

    // ------------------------------------------------------------ formatting
    public DateTime SavedAt
    {
        get
        {
            DateTime dt;
            if (DateTime.TryParse(savedAtIso, CultureInfo.InvariantCulture,
                                  DateTimeStyles.RoundtripKind, out dt))
                return dt;
            return DateTime.MinValue;
        }
    }

    public string PlayTimeText
    {
        get
        {
            var ts = TimeSpan.FromSeconds(Mathf.Max(0f, playTimeSeconds));
            return ts.TotalHours >= 1
                ? string.Format("{0:00}:{1:00}:{2:00}", (int)ts.TotalHours, ts.Minutes, ts.Seconds)
                : string.Format("{0:00}:{1:00}", ts.Minutes, ts.Seconds);
        }
    }

    public string SavedAtText
    {
        get
        {
            var dt = SavedAt;
            return dt == DateTime.MinValue ? "-" : dt.ToLocalTime().ToString("dd MMM yyyy  HH:mm");
        }
    }

    // Brings a file written by an older build up to the current shape.
    public void Migrate()
    {
        if (version < 2)
        {
            // v1 had no camera block; the defaults above are already sane.
            if (cameraDistance <= 0f) cameraDistance = 8f;
            if (seenDialogues == null) seenDialogues = new List<string>();
            if (modules == null) modules = new List<SaveEntry>();
        }

        version = CurrentVersion;
    }
}
