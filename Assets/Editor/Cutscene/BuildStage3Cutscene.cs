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

        // Whose head each shot holds on. Handing the camera a bone rather than a
        // point is what lets somebody miss their mark by half a metre - which
        // they all do - without ending up off the side of the frame.
        Transform hEthan = Bone(ethan, "Head");
        Transform hBaena = Bone(baena, "Head");
        Transform hAsher = Bone(asher, "Head");

        // Ethan over Asher's shoulder, and the reverse.
        made.Add(Tracking(cams, "S_Ethan_OTS", Behind(asherAt, ethanAt, 0.75f, 0.35f), hEthan, 34f));
        made.Add(Tracking(cams, "S_Ethan", Toward(ethanAt, asherAt, 1.5f), hEthan, 30f));
        made.Add(Tracking(cams, "S_Ethan_Tight", Toward(ethanAt, asherAt, 1.15f), hEthan, 26f));
        made.Add(Tracking(cams, "S_Asher", Toward(asherAt, ethanAt, 1.5f), hAsher, 30f));
        made.Add(Tracking(cams, "S_Asher_Tight", Toward(asherAt, ethanAt, 1.1f), hAsher, 26f));

        // Asher again once he has stepped up to Baena.
        //
        // He moves a metre and a quarter across the floor on his own line and
        // turns to face a different person, and the two shots above were built
        // for where he was standing before that. Reusing them after the step put
        // the camera behind his shoulder for both of his remaining lines.
        made.Add(Tracking(cams, "S_Asher_Step", Toward(asherStepAt, baenaAt, 1.25f), hAsher, 28f));

        // Baena, framed slightly from below so he reads as looming.
        made.Add(Tracking(cams, "S_Baena", Toward(baenaAt, asherAt, 1.7f, -0.25f), hBaena, 32f));
        made.Add(Tracking(cams, "S_Baena_Tight", Toward(baenaAt, asherAt, 1.2f, -0.2f), hBaena, 26f));

        // The three of them, for the shove. The geography has to read.
        Vector3 triCentre = (asherStepAt + baenaAt + ethanBlockAt) / 3f;
        made.Add(Shot(cams, "S_Three", triCentre + intoRoom * 4.2f + Vector3.up * 0.8f,
                      triCentre + Vector3.up * 0.75f, 40f));

        made.Add(Tracking(cams, "S_Ethan_Block", Toward(ethanBlockAt, baenaAt, 1.9f), hEthan, 32f));
        // The two of them together, from Baena's side of the argument.
        //
        // It used to sit between Ethan and Asher, which is the one place in the
        // room where both of them have their backs to you: by this point they
        // are squared up at Baena, and a camera anywhere on the line between
        // them is standing behind whichever one it is nearer. Past Baena's
        // shoulder is where both faces are pointed, and it keeps the argument on
        // the same side of the line as the rest of the scene.
        Vector3 pairCentre = Vector3.Lerp(ethanBlockAt, asherStepAt, 0.5f);
        made.Add(Shot(cams, "S_Ethan_Two", Behind(baenaAt, pairCentre, 1.75f, 0.75f),
                      Vector3.Lerp(HeadAt(ethanBlockAt), HeadAt(asherStepAt), 0.5f), 44f));

        // Sydney and Baena at her desk, and Alex across the room.
        //
        // These two are framed off the character's own facing rather than off
        // somebody else's position. Aiming Sydney's shot at where Baena keeps
        // his desk put the camera at her shoulder, because that is simply the
        // direction her desk happens to lie in; and Alex's was set back along
        // the reverse of his forward, which is the definition of standing
        // behind him. What a shot of somebody wants is the space they are
        // facing into, swung off the centre line far enough to be a face rather
        // than a passport photograph.
        made.Add(Tracking(cams, "S_Sydney", Framed(sydney, 2.0f), Bone(sydney, "Head"), 34f));
        made.Add(Tracking(cams, "S_Alex", Framed(alex, 3.0f), Bone(alex, "Head"), 38f));

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

        // The spot every shot in the scene was composed against, handed to the
        // runner so it can stand the player on it before the first cut.
        run.playerMark = mkAsher;

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
              shot: "S_Asher_Step", cue: "asher-steps-up"),

            L("Ethan", "That's enough! Both of you! Baena, shut your mouth.",
              "07_Ethan_Enough", shot: "S_Ethan_Block", cue: "ethan-blocks"),

            L("Ethan", "We need every body we can get, and Asher volunteered.",
              null, shot: "S_Ethan_Block", cont: true),

            L("Ethan", "If you don't feel like starving to death in this hole, learn to work as a team!",
              null, shot: "S_Ethan_Block", cont: true),

            L("Baena", "...", null, shot: "S_Baena", cue: "baena-scoffs"),

            L("Ethan", "Don't let him get under your skin, Asher. You ready to roll?",
              "08_Ethan_ReadyToRoll", shot: "S_Ethan_Two"),

            // No recording for this one - it plays as a caption, the way
            // "Get moving." does at the end of Stage 2.
            L("Asher", "...Ready.", null, shot: "S_Asher_Step"),

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
    /// A shot that keeps its subject framed wherever they actually stand.
    ///
    /// Baking the rotation assumes everybody hits their mark to the centimetre,
    /// and nobody does: a walk ends where the clip ends, a shove lands where the
    /// bodies allow. Measured across the scene, the subject was regularly
    /// eighteen to twenty-nine degrees out of a frame whose half-angle is
    /// thirteen - close enough to have been aimed at, far enough to be off the
    /// side of the screen.
    ///
    /// Handing the camera the subject's head instead moves the problem from
    /// something that must be predicted to something that is simply followed.
    static CinemachineCamera Tracking(Transform parent, string name, Vector3 at, Transform subject, float fov)
    {
        var cam = Shot(parent, name, at, subject.position, fov);

        cam.LookAt = subject;
        var aim = cam.GetComponent<CinemachineRotationComposer>();
        if (aim == null) aim = cam.gameObject.AddComponent<CinemachineRotationComposer>();

        // Dead zone wide enough that breathing does not drift the frame, and no
        // damping: this is a cut-to-cut scene, and a camera easing onto its
        // subject after every cut reads as a mistake rather than as camerawork.
        aim.Composition.DeadZone.Enabled = true;
        aim.Composition.DeadZone.Size = new Vector2(0.18f, 0.18f);
        aim.Damping = Vector2.zero;

        EditorUtility.SetDirty(aim);
        return cam;
    }

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

        // A plain shot points where it was told and nowhere else. Clearing these
        // matters on a rebuild: a shot that used to track somebody would keep
        // following them from a position chosen for a different frame.
        cam.LookAt = null;
        var stale = go.GetComponent<CinemachineRotationComposer>();
        if (stale != null) Object.DestroyImmediate(stale);

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

    // Out in front of somebody, swung off their centre line by `swing` degrees.
    //
    // Straight down the nose is the one angle that reads as a mugshot, so the
    // default is a three-quarter view: enough of the far cheek to see a face,
    // enough of the near one to see an expression on it.
    static Vector3 InFrontOf(Transform who, float back, float swing)
    {
        Vector3 f = who.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
        f.Normalize();

        Vector3 dir = Quaternion.AngleAxis(swing, Vector3.up) * f;
        return new Vector3(who.position.x, 0f, who.position.z) + dir * back + Vector3.up * Eye;
    }

    /// The best spot in front of somebody that can actually see their face.
    ///
    /// Half this garage is people working at benches pushed against walls, and
    /// "in front of" a man facing a wall is inside the wall. Alex is the clear
    /// case: he stands at the robot desk with a metre of room behind the desk
    /// and the whole garage behind him, so the one angle that shows his face is
    /// also the one angle with brickwork in it.
    ///
    /// Rather than hand-pick a number per character - which stops being true the
    /// first time somebody nudges a desk - this tries the angles a camera
    /// operator would try, throws away the ones with something in the way, and
    /// keeps the one that gives up the least of the face. A wide swing showing
    /// three-quarters of a head beats a perfect front view of a wall.
    static Vector3 Framed(Transform who, float idealBack)
    {
        Vector3 head = Head(who);
        float[] swings = { 25f, -25f, 40f, -40f, 55f, -55f, 70f, -70f, 85f, -85f, 10f, -10f };
        float[] backs = { idealBack, idealBack * 0.8f, idealBack * 1.2f, idealBack * 0.62f };

        Vector3 best = InFrontOf(who, idealBack, 25f);
        float bestScore = float.MinValue;

        foreach (float back in backs)
            foreach (float swing in swings)
            {
                Vector3 at = InFrontOf(who, back, swing);

                Vector3 toHead = head - at;
                float dist = toHead.magnitude;
                if (dist < 0.5f) continue;

                // Anything between the lens and the face disqualifies the angle,
                // and so does a lens that has ended up inside something solid.
                if (Physics.Raycast(at, toHead.normalized, dist - 0.2f)) continue;
                if (Physics.CheckSphere(at, 0.25f)) continue;

                Vector3 f = who.forward; f.y = 0f; f.Normalize();
                Vector3 toCam = -toHead; toCam.y = 0f; toCam.Normalize();

                // How much of the face is showing, against how far the shot has
                // drifted from the size it was asked for.
                //
                // The penalty has to be steep. Walking the camera in always wins
                // a little more face - there is less room for a shoulder to get
                // in the way - so a gentle penalty ends up choosing a nose
                // filling the screen over the medium shot that was wanted. Half
                // a point per metre keeps the requested size unless something is
                // genuinely standing in front of it.
                float score = Vector3.Dot(f, toCam) - 0.45f * Mathf.Abs(back - idealBack);
                if (score > bestScore) { bestScore = score; best = at; }
            }

        return best;
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
        t.SetPositionAndRotation(Grounded(pos), rot);
        return t;
    }

    /// Drops a spot onto whatever floor is under it.
    ///
    /// The marks were being derived from Ethan's own position, and Ethan stands
    /// three centimetres into the concrete, so every character sent to a mark
    /// inherited his three centimetres. It is small enough to argue about and
    /// big enough to see: a boot with no sole showing, on every one of them, in
    /// shots framed at a metre and a half.
    static Vector3 Grounded(Vector3 pos)
    {
        RaycastHit hit;
        if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out hit, 6f))
            return new Vector3(pos.x, hit.point.y, pos.z);
        return pos;
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
        // Anywhere in the scene, not only at the top of it. The cast used to sit
        // at the root of Sandbox; in Chapter 1 they hang under the stage set
        // that switches them on and off, and a search that only reads roots
        // quietly finds nobody and reports the cast as missing.
        //
        // Still requires a skinned mesh: several of these names also belong to a
        // camera or a mark, and a shot framed on a marker is a shot of nothing.
        foreach (var r in s.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name && t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                    return t;
        return null;
    }

    static Transform Bone(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var d = Bone(c, name); if (d != null) return d; }
        return null;
    }
}
