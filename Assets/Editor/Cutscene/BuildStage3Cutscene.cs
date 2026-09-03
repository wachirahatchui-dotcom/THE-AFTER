using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using System.Text;
using System.Collections.Generic;

// Builds the garage conversation: the shots, the marks people walk to, and the
// script that ties a camera and a movement to every line.
//
// Everything is worked out from where the characters actually stand rather than
// typed in, so moving somebody in the Scene view and running this again gives
// framing that still points at them.
//
// Menu: THE AFTER > Cutscene > Build Stage 3 Cutscene
public static class BuildStage3Cutscene
{
    const string Root = "Cutscene_Stage3";
    const string VoiceDir = "Assets/Audio/Voice/Ch1_Scene3/";

    // Head height is where a camera wants to be for a face, and these rigs put
    // heads between 5.32 and 5.50.
    const float Eye = 5.42f;

    [MenuItem("THE AFTER/Cutscene/Build Stage 3 Cutscene")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new StringBuilder();
        log.AppendFormat("=== สร้างคัตซีน Stage 3 ใน {0} ===\n", scene.name);

        var ethan  = Root3(scene, "Ethan");
        var baena  = Root3(scene, "Baena");
        var sydney = Root3(scene, "Sydney");
        var alex   = Root3(scene, "Alex");
        var asher  = Root3(scene, "Asher");

        if (ethan == null || baena == null || sydney == null || alex == null || asher == null)
        {
            Debug.LogError("[Stage3] หาตัวละครไม่ครบ - ต้องมี Ethan/Baena/Sydney/Alex/Asher");
            return;
        }

        // ---- the group the whole scene hangs off ----
        var root = GameObject.Find(Root);
        if (root == null)
        {
            root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Build Stage 3 Cutscene");
        }
        var cams = Child(root.transform, "Cameras");
        var marks = Child(root.transform, "Marks");

        // ---- where people end up ----
        //
        // Baena starts at his desk, nine metres from the bench. He walks in on
        // his own line: from that distance the argument plays as two men
        // shouting across a room, and Asher stepping up to him would be a
        // seven-metre stroll in the middle of it.
        Vector3 ethanAt = ethan.position;
        Vector3 facing = ethan.forward;                  // Ethan faces his bench
        Vector3 intoRoom = -facing;                      // where Asher stands

        Vector3 asherAt = ethanAt + intoRoom * 1.9f;
        Vector3 baenaAt = ethanAt + intoRoom * 1.5f + Right(ethan) * 2.1f;
        Vector3 asherStepAt = Vector3.Lerp(asherAt, baenaAt, 0.45f);
        Vector3 ethanBlockAt = Vector3.Lerp(asherStepAt, baenaAt, 0.5f) + intoRoom * 0.25f;

        var mkBaena = Mark(marks, "Baena Mark", baenaAt, Look(baenaAt, asherAt));
        var mkStep  = Mark(marks, "Asher Step Mark", asherStepAt, Look(asherStepAt, baenaAt));
        var mkBlock = Mark(marks, "Ethan Block Mark", ethanBlockAt, Look(ethanBlockAt, baenaAt));
        var mkAsher = Mark(marks, "Asher Talk Mark", asherAt, Look(asherAt, ethanAt));

        log.AppendFormat("จุดยืน: Asher {0}  Baena {1}\n", asherAt.ToString("F1"), baenaAt.ToString("F1"));

        // ---- the shots ----
        var made = new List<CinemachineCamera>();

        // Opening wide, from behind and above where Asher walks in.
        made.Add(Shot(cams, "S_Wide", ethanAt + intoRoom * 6.5f + Vector3.up * 1.4f,
                      ethanAt + Vector3.up * 0.9f, 42f));

        // Ethan over Asher's shoulder, and the reverse.
        made.Add(Shot(cams, "S_Ethan_OTS", Behind(asherAt, ethanAt, 0.75f, 0.35f), Head(ethan), 34f));
        made.Add(Shot(cams, "S_Ethan", Toward(ethanAt, asherAt, 1.5f), Head(ethan), 30f));
        made.Add(Shot(cams, "S_Ethan_Tight", Toward(ethanAt, asherAt, 1.15f), Head(ethan), 26f));
        made.Add(Shot(cams, "S_Asher", Toward(asherAt, ethanAt, 1.5f), HeadAt(asherAt), 30f));
        made.Add(Shot(cams, "S_Asher_Tight", Toward(asherAt, ethanAt, 1.1f), HeadAt(asherAt), 26f));

        // Baena, framed slightly from below so he reads as looming.
        made.Add(Shot(cams, "S_Baena", Toward(baenaAt, asherAt, 1.7f, -0.25f), HeadAt(baenaAt), 32f));
        made.Add(Shot(cams, "S_Baena_Tight", Toward(baenaAt, asherAt, 1.2f, -0.2f), HeadAt(baenaAt), 26f));

        // The three of them, for the shove. The geography has to read.
        Vector3 triCentre = (asherStepAt + baenaAt + ethanBlockAt) / 3f;
        made.Add(Shot(cams, "S_Three", triCentre + intoRoom * 4.2f + Vector3.up * 0.8f,
                      triCentre + Vector3.up * 0.75f, 40f));

        made.Add(Shot(cams, "S_Ethan_Block", Toward(ethanBlockAt, baenaAt, 1.9f), HeadAt(ethanBlockAt), 32f));
        made.Add(Shot(cams, "S_Ethan_Two", Toward(ethanBlockAt, asherStepAt, 2.4f) + Right(ethan) * 0.5f,
                      Vector3.Lerp(HeadAt(ethanBlockAt), HeadAt(asherStepAt), 0.5f), 36f));

        // Sydney and Baena at her desk, and Alex across the room.
        made.Add(Shot(cams, "S_Sydney", Toward(sydney.position, baena.position, 2.0f),
                      Vector3.Lerp(Head(sydney), HeadAt(baenaAt), 0.4f), 34f));
        made.Add(Shot(cams, "S_Alex", Toward(alex.position, alex.position - alex.forward, 4.5f) + Vector3.up * 0.6f,
                      Head(alex), 38f));

        log.AppendFormat("สร้างกล้อง {0} ตัว\n", made.Count);

        // ---- the brain ----
        var brain = Object.FindAnyObjectByType<CinemachineBrain>();
        if (brain != null)
        {
            // Cuts, not blends. A blend between two shots eight metres apart
            // flies the camera across the garage in the middle of a line.
            brain.DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Styles.Cut, 0f);
            EditorUtility.SetDirty(brain);
            log.AppendLine("ตั้ง CinemachineBrain ให้ตัดภาพ (ไม่เบลนด์)");
        }

        // ---- the script ----
        var npc = ethan.GetComponent<NPCInteractable>();
        if (npc == null) npc = Undo.AddComponent<NPCInteractable>(ethan.gameObject);
        Undo.RecordObject(npc, "Build Stage 3 Cutscene");

        npc.npcName = "Ethan";
        npc.interactRange = 3.2f;
        npc.script = Script();
        EditorUtility.SetDirty(npc);
        log.AppendFormat("ผูกบท {0} บรรทัด\n", npc.script.Length);

        // ---- the runner ----
        var run = root.GetComponent<Stage3Cutscene>();
        if (run == null) run = Undo.AddComponent<Stage3Cutscene>(root);
        Undo.RecordObject(run, "Build Stage 3 Cutscene");

        run.ethan = npc;
        run.player = asher;
        run.baena = baena;
        run.sydney = sydney;
        run.brain = brain;
        run.shots = made.ToArray();
        run.baenaMark = mkBaena;
        run.asherStepMark = mkStep;
        run.ethanBlockMark = mkBlock;

        // Asher's walk, played on whoever crosses the floor. Checked binding
        // by binding: all five rigs share the same bone paths.
        run.walkClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Animations/Asher/Asher_Walk.anim");
        if (run.walkClip == null) log.AppendLine("เตือน: หา Asher_Walk.anim ไม่เจอ - การเดินจะเป็นการไถล");
        else log.AppendLine("ใช้ Asher_Walk.anim เป็นท่าเดินของทุกตัว");

        // ---- the quest ----
        foreach (var g in Object.FindObjectsByType<TeleportGate>(FindObjectsSortMode.None))
        {
            Undo.RecordObject(g, "Build Stage 3 Cutscene");
            if (g.name.Contains("Back to Stage 2"))
            {
                // Arriving in the garage closes the errand Logan gave and opens
                // the one the scene is actually about.
                g.completesObjective = true;
                g.objectiveOnArrival = "Find Ethan and talk to him";
            }
            else g.objectiveOnArrival = "";
            EditorUtility.SetDirty(g);
        }
        log.AppendLine("ต่อเควสเข้ากับประตูเทเลพอร์ตแล้ว");

        var move = asher.GetComponent<PlayerMovement>();
        var interact = asher.GetComponent<PlayerInteractor>();
        var look = Object.FindAnyObjectByType<FirstPersonCamera>();
        var follow = Object.FindAnyObjectByType<CameraFollow>();

        var control = new List<Behaviour>();
        if (move != null) control.Add(move);
        if (interact != null) control.Add(interact);
        if (look != null) control.Add(look);
        if (follow != null) control.Add(follow);
        run.playerControl = control.ToArray();
        run.playerBody = asher.GetComponent<CharacterController>();

        EditorUtility.SetDirty(run);
        log.AppendFormat("ล็อกการควบคุม {0} ตัว + CharacterController\n", control.Count);

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    // ------------------------------------------------------------------ script
    static NPCInteractable.Line[] Script()
    {
        return new[]
        {
            L("Ethan", "There you are, Asher. Logan told me you'd step up. We're running critically low on hands.",
              "01_Ethan_Welcome", shot: "S_Ethan_OTS"),

            L("Asher", "Glad to help.", "02_Asher_GladToHelp", shot: "S_Asher"),

            L("Ethan", "You certain you can handle yourself out there?",
              "03_Ethan_YouCertain", shot: "S_Ethan_Tight"),

            L("Asher", "I'm ready.", "04_Asher_ImReady", shot: "S_Asher_Tight",
              cue: "baena-walks-over"),

            // Clip 5 is one 23-second take. It runs unbroken under the next five
            // lines: only the first carries the clip, the rest continue it.
            L("Baena", "Is this a damn joke, Ethan?", "05_Baena_Sneer", shot: "S_Baena"),

            L("Baena", "We're so desperate we're dragging along a scrawny runt whose mother vanished to die in a ditch, and who spends his days clinging to an old man?",
              null, shot: "S_Ethan", cont: true),

            L("Baena", "He looks like he rolled out of bed five minutes ago.",
              null, shot: "S_Baena", cont: true),

            L("Baena", "You sure he won't wet himself the second he smells the savages out there?",
              null, shot: "S_Asher_Tight", cont: true),

            L("Baena", "Let me make one thing clear: I ain't hauling dead weight back.",
              null, shot: "S_Baena_Tight", cont: true),

            L("Asher", "Say that to my face again, you bastard!", "06_Asher_SayThatAgain",
              shot: "S_Asher", cue: "asher-steps-up"),

            L("Ethan", "That's enough! Both of you! Baena, shut your mouth.",
              "07_Ethan_Enough", shot: "S_Three", cue: "ethan-blocks"),

            L("Ethan", "We need every body we can get, and Asher volunteered.",
              null, shot: "S_Ethan_Block", cont: true),

            L("Ethan", "If you don't feel like starving to death in this hole, learn to work as a team!",
              null, shot: "S_Ethan_Block", cont: true),

            L("Baena", "...", null, shot: "S_Baena", cue: "baena-scoffs"),

            L("Ethan", "Don't let him get under your skin, Asher. You ready to roll?",
              "08_Ethan_ReadyToRoll", shot: "S_Ethan_Two"),

            // No recording for this one - it plays as a caption, the way
            // "Get moving." does at the end of Stage 2.
            L("Asher", "...Ready.", null, shot: "S_Asher_Tight"),

            L("Sydney", "Come on, boys! Load up before the daylight scorches our skulls!",
              "09_Sydney_LoadUp", shot: "S_Sydney", cue: "sydney-pats-baena"),

            L("Alex", "Gear is locked down. Let's move.", "10_Alex_GearLocked", shot: "S_Alex"),
        };
    }

    static NPCInteractable.Line L(string who, string text, string clip,
                                  string shot = null, string cue = null, bool cont = false)
    {
        return new NPCInteractable.Line
        {
            speaker = who,
            text = text,
            voice = clip == null ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + clip + ".mp3"),
            continuesVoice = cont,
            shot = shot,
            cue = cue,
        };
    }

    // ------------------------------------------------------------------ shots
    static CinemachineCamera Shot(Transform parent, string name, Vector3 at, Vector3 lookAt, float fov)
    {
        var t = parent.Find(name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Stage 3 Cutscene");
        }

        go.transform.position = at;
        go.transform.rotation = Quaternion.LookRotation((lookAt - at).normalized);

        var cam = go.GetComponent<CinemachineCamera>();
        if (cam == null) cam = go.AddComponent<CinemachineCamera>();

        cam.Priority = 0;
        var lens = cam.Lens;
        lens.FieldOfView = fov;
        cam.Lens = lens;

        EditorUtility.SetDirty(cam);
        return cam;
    }

    // A camera set back from `from`, on the line away from `toward`, at eye height.
    static Vector3 Toward(Vector3 from, Vector3 toward, float back, float rise = 0f)
    {
        Vector3 dir = toward - from; dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
        return new Vector3(from.x, 0f, from.z) + dir.normalized * back + Vector3.up * (Eye + rise);
    }

    // Behind one person's shoulder, looking past them at another.
    static Vector3 Behind(Vector3 who, Vector3 at, float back, float side)
    {
        Vector3 dir = at - who; dir.y = 0f; dir.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, dir).normalized;
        return new Vector3(who.x, 0f, who.z) - dir * back + right * side + Vector3.up * (Eye + 0.15f);
    }

    static Vector3 Head(Transform who)
    {
        var h = Bone(who, "Head");
        return h != null ? h.position : who.position + Vector3.up * 1.45f;
    }

    static Vector3 HeadAt(Vector3 stand) { return stand + Vector3.up * 1.45f; }

    static Quaternion Look(Vector3 from, Vector3 at)
    {
        Vector3 d = at - from; d.y = 0f;
        return d.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(d.normalized);
    }

    static Vector3 Right(Transform t) { return t.right; }

    static Transform Mark(Transform parent, string name, Vector3 pos, Quaternion rot)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Stage 3 Cutscene");
            t = go.transform;
        }
        t.SetPositionAndRotation(pos, rot);
        return t;
    }

    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Build Stage 3 Cutscene");
        return go.transform;
    }

    static Transform Root3(UnityEngine.SceneManagement.Scene s, string name)
    {
        foreach (var r in s.GetRootGameObjects())
            if (r.name == name && r.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return r.transform;
        return null;
    }

    static Transform Bone(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var d = Bone(c, name); if (d != null) return d; }
        return null;
    }
}
