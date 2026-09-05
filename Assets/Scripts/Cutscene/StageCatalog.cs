using UnityEngine;

// Where each stage of the game begins, so the menu can start at one.
//
// The menu scene cannot see into Chapter 1 - it is a different scene, and the
// spots these entries describe are the positions of marks inside it. So the
// answers are worked out once, in the editor, against the real scene, and
// written here for the menu to read.
//
// Regenerated rather than typed: THE AFTER > Dev > Rebuild Stage Catalog reads
// the arrival marks and the walk start out of Chapter 1 and fills this in. Move
// a door and rebuild, and the stage select moves with it. A hand-typed
// coordinate would be wrong the first time anything was nudged, and wrong
// silently - the menu would still open and still start the game, just somewhere
// slightly inside a wall.
//
// One entry per stage, in playing order. Adding a stage is a row here and a
// case in the rebuild tool, not a new page in the menu.
[CreateAssetMenu(fileName = "StageCatalog", menuName = "THE AFTER/Stage Catalog")]
public class StageCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("What the button says.")]
        public string label = "Chapter 1  -  Stage 1";

        [Tooltip("A line under the label. What happens at this point in the story.")]
        public string hint = "";

        public string sceneName = "Chapter1";
        public string chapterName = "Chapter 1";

        [Tooltip("How far through the chapter this is: the value CutsceneChapter1 stores. 0 plays the chapter from the top.")]
        public int reached;

        [Tooltip("Where the player stands. Ignored when reached is 0, because the opening cutscene puts him somewhere itself.")]
        public Vector3 playerPosition;
        public float playerYaw;
    }

    public Entry[] stages;

    /// The one in Resources. Null when nobody has built it yet, which the menu
    /// treats as "no stage select", not as an error.
    public static StageCatalog Load()
    {
        return Resources.Load<StageCatalog>("StageCatalog");
    }

    /// A save that starts the game at this entry, without touching a real slot.
    ///
    /// Going in through the save system rather than round it means stage select
    /// uses the same path a player's load does, and gets the same fixes when
    /// that path is wrong.
    public static SaveSlotData ToSave(Entry e)
    {
        var data = new SaveSlotData
        {
            sceneName = string.IsNullOrEmpty(e.sceneName) ? "Chapter1" : e.sceneName,
            chapterName = string.IsNullOrEmpty(e.chapterName) ? "Chapter 1" : e.chapterName,
            playTimeSeconds = 0f,
            savedAtIso = System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            playerPosition = e.playerPosition,
            playerYaw = e.playerYaw,
            seenDialogues = new System.Collections.Generic.List<string>(),
            modules = new System.Collections.Generic.List<SaveEntry>()
        };

        // The single value that decides whether the chapter replays its opening
        // or picks up where this stage starts.
        data.modules.Add(new SaveEntry
        {
            id = "chapter1",
            json = JsonUtility.ToJson(new Chapter1Progress { reached = e.reached })
        });

        return data;
    }

    // Mirrors the private state inside CutsceneChapter1. JsonUtility matches on
    // the field name, so the two only have to agree about "reached".
    [System.Serializable]
    class Chapter1Progress { public int reached; }
}
