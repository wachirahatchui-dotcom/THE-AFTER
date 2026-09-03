using System.Collections.Generic;

// Which conversations the player has already had.
//
// Small enough to live as a plain static set rather than a MonoBehaviour, and
// serialised straight into SaveSlotData.seenDialogues.
//
// DialogueManager.Begin calls MarkSeen, so NPCs can branch on HasSeen without
// any extra wiring:
//
//     var lines = DialogueProgress.HasSeen(npc.npcName) ? repeatLines : firstLines;
public static class DialogueProgress
{
    static readonly HashSet<string> seen = new HashSet<string>();

    public static void MarkSeen(string id)
    {
        if (!string.IsNullOrEmpty(id)) seen.Add(id);
    }

    public static bool HasSeen(string id)
    {
        return !string.IsNullOrEmpty(id) && seen.Contains(id);
    }

    public static int Count { get { return seen.Count; } }

    public static void Clear()
    {
        seen.Clear();
    }

    public static List<string> Capture()
    {
        return new List<string>(seen);
    }

    public static void Restore(List<string> ids)
    {
        seen.Clear();
        if (ids == null) return;

        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) seen.Add(id);
    }
}
