using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;

// Writes a save file that starts the game where you want to work, instead of at
// the beginning of the chapter.
//
// Testing anything in the garage meant sitting through the flashback, the waking
// scene, the walk to the fire and the whole conversation with Logan every single
// time. Several minutes to reach the thing being changed, on every run.
//
// It writes a real save into a real slot rather than adding a debug shortcut, so
// it goes in through Load Game like any other save and exercises the same code
// the player's saves do. Which is worth having anyway: loading a save into
// Chapter 1 used to replay the chapter from its first frame, and that was only
// ever going to be noticed by loading one.
//
// Menu: THE AFTER > Dev
public static class MakeTestSave
{
    // Kept high so they stay out of the way of real saves, and apart from each
    // other so writing one does not wipe the other.
    const int BunkerSlot = 3;
    const int GarageSlot = 4;

    [MenuItem("THE AFTER/Dev/Save: end of Stage 2 (bunker)")]
    public static void EndOfStage2() { Write(BunkerSlot, 1, "Stage 2 - จบบทสนทนากับ Logan", false); }

    [MenuItem("THE AFTER/Dev/Save: in the garage (Stage 3)")]
    public static void InGarage() { Write(GarageSlot, 2, "Stage 3 - ในโรงรถ", true); }

    static void Write(int slot, int reached, string label, bool garage)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "Chapter1")
        {
            Debug.LogError("[TestSave] ต้องเปิดซีน Chapter1 ก่อน (ตอนนี้เปิด " + scene.name + ")");
            return;
        }

        var log = new StringBuilder();
        log.AppendFormat("=== เขียนเซฟทดสอบ: {0} ===\n", label);

        // Where the player is put down. Taken from the scene rather than typed
        // in, so moving the door or the arrival mark moves the save with it.
        Vector3 spot; float yaw;
        if (!Spot(scene, garage, out spot, out yaw, log)) return;

        var data = new SaveSlotData
        {
            sceneName = "Chapter1",
            chapterName = "Chapter 1",
            playTimeSeconds = 0f,
            savedAtIso = System.DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
            playerPosition = spot,
            playerYaw = yaw,
            seenDialogues = new System.Collections.Generic.List<string>(),
            modules = new System.Collections.Generic.List<SaveEntry>()
        };

        // The one piece of state that decides whether the chapter replays itself.
        data.modules.Add(new SaveEntry
        {
            id = "chapter1",
            json = JsonUtility.ToJson(new Chapter1State { reached = reached })
        });

        if (!SaveSystem.Write(slot, data))
        {
            Debug.LogError("[TestSave] เขียนไม่สำเร็จ");
            return;
        }

        log.AppendFormat("ผู้เล่นจะเริ่มที่ {0}  หัน {1:F0}°\n", spot.ToString("F2"), yaw);
        log.AppendFormat("stage ที่บันทึก = {0}\n", reached);
        log.AppendFormat("เขียนลงสล็อต {0}: {1}\n", slot, SaveSystem.PathFor(slot));
        log.AppendLine("\nเปิดเกม > Load Game > สล็อตนี้ จะเข้าไปตรงจุดเลย ไม่ต้องดูคัตซีนซ้ำ");

        Debug.Log(log.ToString());
    }

    // Mirrors the private State inside CutsceneChapter1. Kept in step by the
    // field name, which is what JsonUtility matches on.
    [System.Serializable]
    class Chapter1State { public int reached; }

    /// A spot on the floor, found from the scene's own landmarks.
    static bool Spot(UnityEngine.SceneManagement.Scene scene, bool garage,
                     out Vector3 pos, out float yaw, StringBuilder log)
    {
        pos = Vector3.zero; yaw = 0f;

        // Everything is switched off in the saved scene, and a collider on a
        // switched-off object does not exist, so the floor cannot be found until
        // the sets are up. Put back exactly as they were.
        var was = new System.Collections.Generic.Dictionary<GameObject, bool>();
        foreach (var r in scene.GetRootGameObjects())
        {
            if (!(r.name.Contains("Stage") && r.name.Contains("Set"))) continue;
            foreach (Transform t in r.transform)
            {
                was[t.gameObject] = t.gameObject.activeSelf;
                t.gameObject.SetActive(true);
            }
        }
        Physics.SyncTransforms();

        try
        {
            // In the bunker: standing at the black door, which is where the
            // errand Logan gives you points. In the garage: on the arrival mark,
            // which is where the door puts you.
            string want = garage ? "Arrival - Garage Side" : "Arrival - Bunker Side";

            Transform mark = null;
            foreach (var r in scene.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == want) mark = t;

            if (mark == null)
            {
                Debug.LogError("[TestSave] ไม่เจอจุด '" + want + "' ในซีน");
                return false;
            }

            pos = mark.position;
            yaw = mark.eulerAngles.y;

            // Back off the doorway. The arrival marks sit about a metre and a
            // half from their own gate, which is inside the range that opens it,
            // so a save taken standing on one loads and immediately teleports.
            // The gate refuses to fire until somebody has stood clear of it, but
            // starting the player three metres out is also just better staging:
            // they walk into the door instead of appearing in it.
            //
            // The mark faces away from its gate - that is what "arriving" means -
            // so its own forward is the way further into the room.
            pos += mark.forward * 3.0f;
            log.AppendFormat("ถอยจากจุด '{0}' ออกมา 3.0 m ให้พ้นระยะเกต\n", want);

            // Standing on the floor rather than in it, whatever the mark's own
            // height happens to be.
            RaycastHit hit;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out hit, 6f))
            {
                log.AppendFormat("พื้นใต้จุด '{0}' อยู่ y={1:F3}\n", want, hit.point.y);
                pos.y = hit.point.y + 0.05f;
            }
            else log.AppendFormat("!! ไม่เจอพื้นใต้ '{0}' - ใช้ความสูงของจุดเอง\n", want);

            return true;
        }
        finally
        {
            foreach (var kv in was) kv.Key.SetActive(kv.Value);
            Physics.SyncTransforms();
        }
    }
}
