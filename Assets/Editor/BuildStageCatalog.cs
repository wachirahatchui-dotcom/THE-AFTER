using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;
using System.Collections.Generic;

// Fills in StageCatalog from Chapter 1, so the menu's stage select knows where
// each stage begins.
//
// Read out of the scene rather than typed in. A hand-written coordinate is
// correct until the first time somebody nudges the door it was measured from,
// and then it is wrong quietly: the menu still opens, the game still starts,
// the player just arrives a little way inside a wall. Reading the marks means
// the answer is re-derived every time this is run, and running it is one menu
// item.
//
// Menu: THE AFTER > Dev > Rebuild Stage Catalog
public static class BuildStageCatalog
{
    const string AssetPath = "Assets/Resources/StageCatalog.asset";

    [MenuItem("THE AFTER/Dev/Rebuild Stage Catalog")]
    public static void Rebuild()
    {
        var log = new StringBuilder();
        log.AppendLine("=== สร้าง Stage Catalog จากซีนจริง ===");

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                Debug.LogError("[StageCatalog] มีซีนที่ยังไม่ได้เซฟ - เซฟก่อนแล้วสั่งใหม่");
                return;
            }

        string was = EditorSceneManager.GetActiveScene().path;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Chapter1.unity", OpenSceneMode.Single);

        // Colliders on switched-off objects do not exist, and the floor under
        // each mark is found with a ray. Everything comes up for the reading and
        // goes back exactly as it was.
        var state = new Dictionary<GameObject, bool>();
        foreach (var r in scene.GetRootGameObjects())
        {
            if (!(r.name.Contains("Stage") && r.name.Contains("Set"))) continue;
            foreach (Transform t in r.transform)
            {
                state[t.gameObject] = t.gameObject.activeSelf;
                t.gameObject.SetActive(true);
            }
        }
        Physics.SyncTransforms();

        var stages = new List<StageCatalog.Entry>();

        // Stage 1 is the beginning, so it has no spot to stand on: the opening
        // cutscene puts him where it wants him.
        stages.Add(new StageCatalog.Entry
        {
            label = "CHAPTER 1  -  STAGE 1",
            hint = "ห้องนอน  ~  ความทรงจำถึงแม่ (เล่นตั้งแต่ต้น)",
            reached = 0
        });

        stages.Add(Spot(scene, "CHAPTER 1  -  STAGE 2",
                        "บังเกอร์  ~  หลังคุยกับ Logan จบ", 1,
                        "Arrival - Bunker Side", log));

        stages.Add(Spot(scene, "CHAPTER 1  -  STAGE 3",
                        "โรงรถ  ~  ไปหา Ethan", 2,
                        "Arrival - Garage Side", log));

        foreach (var kv in state) kv.Key.SetActive(kv.Value);
        Physics.SyncTransforms();

        // Written before the scene is put back, because writing the asset is the
        // only part that must not be skipped if anything below throws.
        System.IO.Directory.CreateDirectory("Assets/Resources");
        var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<StageCatalog>();
            AssetDatabase.CreateAsset(catalog, AssetPath);
            log.AppendLine("สร้างไฟล์ใหม่ที่ " + AssetPath);
        }

        catalog.stages = stages.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        if (!string.IsNullOrEmpty(was) && was != scene.path)
            EditorSceneManager.OpenScene(was, OpenSceneMode.Single);

        log.AppendFormat("\nเขียน {0} ด่าน:\n", stages.Count);
        foreach (var s in stages)
            log.AppendFormat("   {0,-24} reached={1}  {2}\n",
                s.label, s.reached,
                s.reached == 0 ? "(เล่นตั้งแต่ต้น)" : s.playerPosition.ToString("F2"));

        Debug.Log(log.ToString());
    }

    /// One entry, standing clear of the doorway its mark belongs to.
    static StageCatalog.Entry Spot(UnityEngine.SceneManagement.Scene scene,
                                   string label, string hint, int reached,
                                   string markName, StringBuilder log)
    {
        var e = new StageCatalog.Entry { label = label, hint = hint, reached = reached };

        Transform mark = null;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == markName) mark = t;

        if (mark == null)
        {
            log.AppendFormat("  !! ไม่เจอจุด '{0}' - ด่านนี้จะเริ่มที่ (0,0,0)\n", markName);
            return e;
        }

        // Three metres further into the room than the mark itself. An arrival
        // mark sits about a metre and a half from its own gate, which is inside
        // the range that opens it, so starting exactly on one would teleport the
        // player somewhere else on their first frame. The mark faces away from
        // its gate - that is what arriving means - so its forward is inward.
        Vector3 pos = mark.position + mark.forward * 3.0f;

        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out hit, 6f))
            pos.y = hit.point.y + 0.05f;
        else
            log.AppendFormat("  !! ไม่เจอพื้นใต้ '{0}' - ใช้ความสูงของจุดเอง\n", markName);

        e.playerPosition = pos;
        e.playerYaw = mark.eulerAngles.y;

        log.AppendFormat("  {0}: จาก '{1}' ถอย 3 m -> {2}\n", label, markName, pos.ToString("F2"));
        return e;
    }
}
