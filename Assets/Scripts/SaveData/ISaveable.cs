using System;
using System.Collections.Generic;
using UnityEngine;

// One saved blob belonging to one ISaveable.
[Serializable]
public class SaveEntry
{
    public string id;
    public string json;
}

// Implement this on any system whose state belongs in a save file, then
// register it. The save system does not need to know what the system is:
//
//     public class Inventory : MonoBehaviour, ISaveable
//     {
//         public string SaveId { get { return "inventory"; } }
//         public string CaptureState()            { return JsonUtility.ToJson(data); }
//         public void   RestoreState(string json) { JsonUtility.FromJsonOverwrite(json, data); }
//
//         void OnEnable()  { SaveRegistry.Register(this); }
//         void OnDisable() { SaveRegistry.Unregister(this); }
//     }
//
// This is the extension point: inventory, quests, world flags and door states
// all plug in without SaveSystem.cs ever changing.
public interface ISaveable
{
    // Stable across builds - it is the key the blob is stored under. Renaming
    // it orphans existing saves.
    string SaveId { get; }

    string CaptureState();
    void RestoreState(string json);
}

public static class SaveRegistry
{
    static readonly List<ISaveable> items = new List<ISaveable>();

    public static void Register(ISaveable item)
    {
        if (item == null || items.Contains(item)) return;

        if (string.IsNullOrEmpty(item.SaveId))
        {
            Debug.LogError("[SaveRegistry] " + item.GetType().Name + " has an empty SaveId; not registered.");
            return;
        }
        items.Add(item);
    }

    public static void Unregister(ISaveable item)
    {
        items.Remove(item);
    }

    public static int Count { get { return items.Count; } }

    public static List<SaveEntry> CaptureAll()
    {
        var result = new List<SaveEntry>();

        foreach (var item in items)
        {
            if (item == null) continue;
            try
            {
                result.Add(new SaveEntry { id = item.SaveId, json = item.CaptureState() });
            }
            catch (Exception e)
            {
                // One broken system must not cost the player the whole save.
                Debug.LogError("[SaveRegistry] " + item.SaveId + " failed to capture: " + e.Message);
            }
        }
        return result;
    }

    public static void RestoreAll(List<SaveEntry> entries)
    {
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id)) continue;

            var target = items.Find(i => i != null && i.SaveId == entry.id);
            if (target == null)
            {
                // Normal when loading an old save into a build that dropped a
                // system, or before the object has spawned. Not an error.
                continue;
            }

            try
            {
                target.RestoreState(entry.json);
            }
            catch (Exception e)
            {
                Debug.LogError("[SaveRegistry] " + entry.id + " failed to restore: " + e.Message);
            }
        }
    }
}
