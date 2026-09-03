using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// File-backed save slots.
//
// Writes JSON to Application.persistentDataPath/saves/slot_N.json through a
// temp file + replace, so a crash mid-write cannot leave a half-written file
// that still looks valid. A thumbnail PNG is written alongside it.
//
// What gets captured:
//   * player position and facing        (this file)
//   * camera orbit                      (this file)
//   * which conversations have happened (DialogueProgress)
//   * anything else                     (ISaveable, via SaveRegistry)
//
// Adding a new saveable system means implementing ISaveable and registering
// it - this file does not change.
public static class SaveSystem
{
    public const int SlotCount = 3;

    // Set by the menu before it loads a scene; PlayTimeTracker applies it once
    // the target scene is up, then clears it.
    public static SaveSlotData PendingLoad;

    // Raised after any successful write or delete, so open UI can refresh.
    public static event Action SlotsChanged;

    public static string Dir
    {
        get { return Path.Combine(Application.persistentDataPath, "saves"); }
    }

    public static string PathFor(int slot)
    {
        return Path.Combine(Dir, "slot_" + slot + ".json");
    }

    public static bool Exists(int slot)
    {
        return File.Exists(PathFor(slot));
    }

    // ================================================================== read
    public static SaveSlotData Read(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return null;

            var data = JsonUtility.FromJson<SaveSlotData>(File.ReadAllText(path));
            if (data == null) return null;

            data.Migrate();
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveSystem] Could not read slot " + slot + ": " + e.Message);
            return null;
        }
    }

    // ================================================================= write
    public static bool Write(int slot, SaveSlotData data)
    {
        try
        {
            Directory.CreateDirectory(Dir);

            string path = PathFor(slot);
            string tmp = path + ".tmp";

            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            Raise();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] Could not write slot " + slot + ": " + e.Message);
            return false;
        }
    }

    // The one call gameplay code should use: snapshot everything, write it,
    // and grab a thumbnail.
    public static bool SaveToSlot(int slot, string chapterName)
    {
        var data = CaptureFromScene(chapterName);
        bool ok = Write(slot, data);

        if (ok) SaveThumbnail.Capture(slot);

        Raise();
        return ok;
    }

    public static bool Delete(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);

            SaveThumbnail.Delete(slot);
            Raise();
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] Could not delete slot " + slot + ": " + e.Message);
            return false;
        }
    }

    public static bool AnyExists()
    {
        for (int i = 0; i < SlotCount; i++)
            if (Exists(i)) return true;
        return false;
    }

    // Slot backing the CONTINUE button: whichever file was written last.
    public static int MostRecentSlot()
    {
        int best = -1;
        DateTime bestTime = DateTime.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            var data = Read(i);
            if (data == null) continue;

            var t = data.SavedAt;
            if (best < 0 || t > bestTime) { best = i; bestTime = t; }
        }
        return best;
    }

    public static int FirstEmptySlot()
    {
        for (int i = 0; i < SlotCount; i++)
            if (!Exists(i)) return i;
        return -1;
    }

    // ======================================================= scene <-> slot
    // Snapshots whatever the live scene can tell us about. Deliberately
    // tolerant: a scene with no player still produces a usable save.
    public static SaveSlotData CaptureFromScene(string chapterName)
    {
        var data = new SaveSlotData
        {
            sceneName = SceneManager.GetActiveScene().name,
            chapterName = string.IsNullOrEmpty(chapterName) ? "Chapter 1" : chapterName,
            playTimeSeconds = PlayTimeTracker.TotalSeconds,
            savedAtIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            seenDialogues = DialogueProgress.Capture(),
            modules = SaveRegistry.CaptureAll()
        };

        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            data.playerPosition = player.transform.position;
            data.playerYaw = player.transform.eulerAngles.y;
        }

        var cam = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        if (cam != null)
        {
            data.cameraYaw = cam.yaw;
            data.cameraPitch = cam.pitch;
            data.cameraDistance = cam.distance;
        }

        return data;
    }

    // Teleporting a CharacterController requires disabling it first, otherwise
    // its internal collision state fights the assignment and the player snaps
    // straight back to where it was.
    public static void ApplyToScene(SaveSlotData data)
    {
        if (data == null) return;

        var player = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            var cc = player.GetComponent<CharacterController>();
            bool wasEnabled = cc != null && cc.enabled;
            if (cc != null) cc.enabled = false;

            player.transform.position = data.playerPosition;
            player.transform.rotation = Quaternion.Euler(0f, data.playerYaw, 0f);

            if (cc != null) cc.enabled = wasEnabled;
        }

        var cam = UnityEngine.Object.FindAnyObjectByType<CameraFollow>();
        if (cam != null)
        {
            cam.yaw = data.cameraYaw;
            cam.pitch = data.cameraPitch;
            cam.SetDistanceImmediate(data.cameraDistance);
        }

        DialogueProgress.Restore(data.seenDialogues);
        SaveRegistry.RestoreAll(data.modules);

        PlayTimeTracker.SetTotal(data.playTimeSeconds);
    }

    // Called by the menu right before it loads a scene.
    public static void QueueLoad(SaveSlotData data)
    {
        PendingLoad = data;
    }

    // Applied once the target scene is up. PlayTimeTracker calls this from its
    // own scene-loaded hook, so game code needs no wiring.
    public static void ConsumePendingLoad()
    {
        if (PendingLoad == null) return;

        var data = PendingLoad;
        PendingLoad = null;
        ApplyToScene(data);
    }

    static void Raise()
    {
        if (SlotsChanged != null) SlotsChanged();
    }
}
