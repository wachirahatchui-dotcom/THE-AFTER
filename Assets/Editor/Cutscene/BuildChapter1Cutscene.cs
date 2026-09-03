using System.Collections.Generic;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Builds Chapter 1 Stage 1: Matha at the bedside telling a story, the camera
// moves, and the Timeline that plays them.
//
// Everything is derived from where the furniture actually is. The chair decides
// where she sits, the bed decides where the child will lie and therefore where
// her hand reaches and where the camera looks. Move a prop, run the menu item
// again, and the whole sequence re-fits itself.
//
// She tells the story with her hands rather than from a book - the book model
// available carried a long flat strip that read as anything but a storybook, and
// a told story needs no prop.
//
// What it produces is ordinary Unity data - CinemachineCameras, an AnimationClip,
// a TimelineAsset - so any of it can be re-timed or re-framed by hand afterwards
// without going back through this script.
//
// Menu: THE AFTER > Cutscene > Build Chapter 1 Stage 1
public static class BuildChapter1Cutscene
{
    const string Menu = "THE AFTER/Cutscene/Build Chapter 1 Stage 1";
    const string AssetDir = "Assets/Cutscenes/Chapter1";

    // Measured off the chair and bed meshes.
    const float SeatTop = 0.80f;
    const float FloorY = 0.415f;
    const float HipAboveSeat = 0.10f;
    const float MattressTop = 0.966f;

    // Where the child will lie once his model arrives: on his mother's side of the
    // bed, head towards the headboard. Her reach and the cameras both aim here.
    const float ChildHeadFromPillow = 0.30f;
    const float ChildHeadAboveSheet = 0.13f;
    const float ChildLieX = 46.42f;

    // The beats, in seconds - worked out from the recordings rather than fixed.
    //
    // They used to be constants adding up to a fourteen second scene. The voice
    // for it runs eighty. Stretching the old numbers by six would have given a
    // hand that takes nine seconds to reach a child's head; instead every beat is
    // pinned to the line it belongs to, and the one that matters most - the kiss -
    // is placed where the script puts it, inside the last speech.
    static float TSettled, TTellA, TTellB, TReach, TStrokeDown, TStrokeUp, TWithdraw, TTellC, TEnd;

    const string VoiceDir = "Assets/Audio/Voice/Ch1_Scene1";

    /// One spoken line: the clip, when it starts, and who says it.
    class Say
    {
        public AudioClip clip;
        public float start, end;
        public bool matha;
        public string[] captions;
    }

    /// The scene's script, split into caption-sized pieces.
    ///
    /// Split by sentence rather than by clock: a caption that changes mid-thought
    /// is harder to read than a slightly long one, and the eye is already following
    /// the voice. Each piece gets a share of its line's running time proportional
    /// to its length, which tracks speech closely enough for a scene this slow.
    static readonly string[][] Script =
    {
        new[]
        {
            "A long time ago, Asher, our world wasn't just scrap metal and toxic dust.",
            "It was covered in living things called trees, rolling green grass, and an endless, spotless blue sky.",
            "Humanity built wonders back then cities that touched the clouds, and machines with brilliant minds.",
            "Everyone believed we stood at the absolute pinnacle of creation.",
        },
        new[] { "Then why did all those tall buildings fall down, Mom?" },
        new[]
        {
            "Because progress came at the cost of greed.",
            "Once humans had everything, their hunger only grew.",
            "They consumed this world until it was hollow and dry.",
            "The creators chose to destroy their own creations out of sheer selfishness...",
            "leaving behind a dying world. Just like the one you see today.",
        },
        new[] { "Is the world really going to die? Can we never save it?" },
        new[]
        {
            "We can, sweetheart. I truly believe that.",
            "As long as a single fragment of nature survives somewhere out there...",
            "remember this, Asher. Hope... no matter how fragile it seems... is always hope.",
            "Go to sleep now, my brave boy.",
        },
    };

    /// Loads the five recordings and lays them end to end with room to breathe.
    static List<Say> ReadScript(StringBuilder log)
    {
        // The files are numbered, and the order is the conversation.
        var paths = new List<string>(System.IO.Directory.GetFiles(VoiceDir, "*.mp3"));
        paths.Sort(System.StringComparer.Ordinal);
        if (paths.Count < 5)
        {
            log.AppendLine("เจอไฟล์เสียงแค่ " + paths.Count + " ไฟล์ ใน " + VoiceDir);
            return null;
        }

        // Long enough to let a sentence land before the next one starts, and to let
        // the boy's questions feel like they took him a moment to ask.
        const float Lead = 1.5f, Gap = 0.9f, Tail = 3.2f;

        var said = new List<Say>();
        float at = Lead;
        for (int i = 0; i < 5; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i].Replace('\\', '/'));
            if (clip == null) { log.AppendLine("โหลดไม่ได้: " + paths[i]); return null; }

            // Lines 2 and 4 are the boy; the rest are his mother.
            bool matha = i != 1 && i != 3;

            said.Add(new Say { clip = clip, start = at, end = at + clip.length,
                               matha = matha, captions = Script[i] });
            at += clip.length + Gap;
        }

        TEnd = at - Gap + Tail;

        log.AppendLine("บทพูด 5 ประโยค:");
        foreach (var s in said)
            log.AppendLine("   " + (s.matha ? "Matha" : "Asher") + "  "
                         + s.start.ToString("F1").PadLeft(6) + " - " + s.end.ToString("F1").PadLeft(6)
                         + "   " + s.clip.name);
        log.AppendLine("   ฉากยาวรวม " + TEnd.ToString("F1") + " วิ (เดิม 14.0)");
        return said;
    }

    /// Pins the acting to the lines.
    static void LayOutBeats(List<Say> said, StringBuilder log)
    {
        Say L(int i) => said[Mathf.Clamp(i, 0, said.Count - 1)];

        TSettled = 0f;

        // Two storytelling gestures inside her opening speech, spread across it.
        TTellA = L(0).start + 1.2f;
        TTellB = Mathf.Lerp(L(0).start, L(0).end, 0.55f);

        // A third while she explains what went wrong.
        TTellC = Mathf.Lerp(L(2).start, L(2).end, 0.45f);

        // The kiss. The script puts it in the middle of her last speech, between
        // "somewhere out there" and "remember this, Asher" - so the hand goes out
        // before that and comes back after it, rather than at some tidy round
        // number that would land on nothing.
        float kiss = Mathf.Lerp(L(4).start, L(4).end, 0.42f);
        TReach = kiss - 2.0f;
        TStrokeDown = kiss;
        TStrokeUp = kiss + 1.8f;
        TWithdraw = kiss + 4.2f;

        log.AppendLine("จังหวะการแสดง:");
        log.AppendLine("   เล่าเรื่อง " + TTellA.ToString("F1") + ", " + TTellB.ToString("F1")
                     + ", " + TTellC.ToString("F1") + " วิ");
        log.AppendLine("   ยื่นมือ " + TReach.ToString("F1") + "  ลูบหัว " + TStrokeDown.ToString("F1")
                     + "  จูบหน้าผาก ~" + kiss.ToString("F1")
                     + "  ชักมือกลับ " + TWithdraw.ToString("F1") + " วิ");
    }

    [MenuItem(Menu)]
    public static void Build() => Debug.Log(BuildAndReport());

    public static string BuildAndReport()
    {
        var log = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();

        GameObject chair = null, bed = null, root = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t.name == "Chair") chair = t.gameObject;
            else if (t.name == "Bed") bed = t.gameObject;
            else if (t.name == "Cutscene_Ch1" && t.parent == null) root = t.gameObject;
        }
        if (chair == null || bed == null) return "หา Chair หรือ Bed ในซีนไม่เจอ";
        if (root == null)
        {
            root = new GameObject("Cutscene_Ch1");
            Undo.RegisterCreatedObjectUndo(root, "Cutscene root");
        }

        EnsureFolder(AssetDir);

        var said = ReadScript(log);
        if (said == null) return log + "\nหาไฟล์เสียงพูดไม่ครบ - ยังไม่สร้าง";
        LayOutBeats(said, log);

        // No book in this version - anything left over from the earlier pass goes.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t != null && (t.name == "book" || t.name == "Book 1"))
            {
                log.AppendLine("ลบหนังสือออกจากซีน: " + t.name);
                Undo.DestroyObjectImmediate(t.gameObject);
            }

        var bedBounds = bed.GetComponentInChildren<Renderer>().bounds;

        var matha = PlaceMatha(chair, bedBounds, log);
        if (matha == null) return log + "\nวาง Matha ไม่สำเร็จ";

        // Asher goes down first: his head is what the mother's hand reaches for and
        // what two of the three cameras are aimed at. Without his model the point is
        // estimated from the bed instead, so the sequence still builds.
        Vector3 childHead = new Vector3(ChildLieX,
                                        MattressTop + ChildHeadAboveSheet,
                                        bedBounds.max.z - 0.25f - ChildHeadFromPillow);
        AnimationClip asherClip = null;
        GameObject asher = BuildChapter1Asher.Place(bedBounds, log);
        if (asher != null)
        {
            asherClip = BuildChapter1Asher.BuildClip(asher, bedBounds, matha, TEnd, out childHead, log);
            asherClip = SaveClip(asherClip, AssetDir + "/AsherKid_Stage1.anim", log);
        }
        else log.AppendLine("หัวเด็ก (ประมาณจากเตียง): " + childHead.ToString("F3"));

        var clip = SaveClip(BuildMathaClip(matha, chair, childHead, log),
                            AssetDir + "/Matha_Stage1.anim", log);

        var cams = BuildCameras(root, matha, bedBounds, childHead, log);
        BuildTimeline(root, matha, clip, asher, asherClip, cams, said, log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ------------------------------------------------------------------ actress

    static GameObject PlaceMatha(GameObject chair, Bounds bedBounds, StringBuilder log)
    {
        GameObject matha = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Matha (1)") matha = t.gameObject;

        if (matha == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Characters/Matha/Matha.prefab");
            if (src == null) { log.AppendLine("หา Matha.prefab ไม่เจอ"); return null; }
            matha = (GameObject)PrefabUtility.InstantiatePrefab(src);
            matha.name = "Matha (1)";
            Undo.RegisterCreatedObjectUndo(matha, "Add Matha");
        }

        // Timeline drives the bones through transform curves, which a humanoid
        // avatar would override. Generic is what lets the clip through.
        var mi = (ModelImporter)AssetImporter.GetAtPath("Assets/Models/Characters/Matha/Matha.fbx");
        if (mi != null && mi.animationType != ModelImporterAnimationType.Generic)
        {
            mi.animationType = ModelImporterAnimationType.Generic;
            AssetDatabase.WriteImportSettingsIfDirty(mi.assetPath);
            AssetDatabase.ImportAsset(mi.assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("ตั้ง Matha เป็น Generic rig (Timeline ต้องการแบบนี้)");
        }

        var anim = matha.GetComponent<Animator>();
        if (anim == null) anim = matha.AddComponent<Animator>();
        anim.applyRootMotion = false;

        Undo.RecordObject(matha.transform, "Seat Matha");
        matha.transform.localScale = Vector3.one;
        float seatX = chair.transform.position.x;
        matha.transform.position = new Vector3(seatX, FloorY, chair.transform.position.z);
        matha.transform.rotation = Quaternion.LookRotation(
            new Vector3(bedBounds.center.x - seatX, 0f, 0f).normalized, Vector3.up);

        foreach (var smr in matha.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.forceMatrixRecalculationPerRender = true;
            smr.updateWhenOffscreen = true;
        }

        log.AppendLine("Matha นั่งที่ " + matha.transform.position.ToString("F3")
                     + " หันหน้า " + matha.transform.forward.ToString("F2"));
        return matha;
    }

    // -------------------------------------------------------------------- clip

    static AnimationClip BuildMathaClip(GameObject matha, GameObject chair,
                                        Vector3 childHead, StringBuilder log)
    {
        var rest = PoseTools.ReadRestPose(matha);
        var b = PoseTools.BonesOf(matha);
        Transform B(string n) => b.TryGetValue(n, out var t) ? t : null;

        var tracked = new List<Transform>
        {
            B("Hips"), B("Spine"), B("Chest"), B("Neck"), B("Head"),
            B("LeftShoulder"), B("LeftArm"), B("LeftForeArm"), B("LeftHand"),
            B("RightShoulder"), B("RightArm"), B("RightForeArm"), B("RightHand"),
            B("LeftUpLeg"), B("LeftLeg"), B("LeftFoot"), B("LeftToeBase"),
            B("RightUpLeg"), B("RightLeg"), B("RightFoot"), B("RightToeBase"),
        };
        var rec = new PoseRecorder(matha.transform, B("Hips"), tracked);

        Vector3 fwd = matha.transform.forward;
        Vector3 left = -matha.transform.right;
        Vector3 up = Vector3.up;
        Vector3 seat = new Vector3(chair.transform.position.x, SeatTop, chair.transform.position.z);

        // The stroking hand is the one on the child's side; the other keeps telling.
        bool childOnLeft = Vector3.Dot(childHead - matha.transform.position, left) > 0f;
        string strokeSide = childOnLeft ? "Left" : "Right";
        string tellSide = childOnLeft ? "Right" : "Left";
        log.AppendLine("มือที่ลูบหัว: " + strokeSide + "   มือที่ทำท่าเล่า: " + tellSide);

        // A told story lives in the hands: they open, lift and settle between
        // phrases. Each beat gives both hands a place to be.
        void Beat(float time, float lean, float perch,
                  Vector2 tellHand, Vector2 otherHand, float reach, Vector3 lookAt)
        {
            Sit(matha, rest, b, seat, fwd, left, up, lean, perch);

            Hand(b, tellSide, seat, fwd, left, up, tellHand);
            if (reach <= 0f) Hand(b, strokeSide, seat, fwd, left, up, otherHand);
            else
            {
                Vector3 resting = HandPoint(seat, fwd, left, up, otherHand,
                                            strokeSide == "Left" ? 1f : -1f);
                Vector3 palm = Vector3.Lerp(resting, childHead + up * 0.06f, reach);
                PoseTools.TwoBone(B(strokeSide + "Arm"), B(strokeSide + "ForeArm"), B(strokeSide + "Hand"),
                                  palm, -up * 0.5f + fwd * 0.3f);
            }

            LookAt(matha, b, lookAt);
            rec.Capture(time);
        }

        // tellHand / otherHand are (forward, height) offsets from the seat, in metres.
        Beat(TSettled,     10f, 0.03f, new Vector2(0.24f, 0.26f), new Vector2(0.22f, 0.16f), 0f, childHead);
        Beat(TTellA,       14f, 0.05f, new Vector2(0.34f, 0.40f), new Vector2(0.26f, 0.20f), 0f, childHead);
        Beat(TTellB,       12f, 0.04f, new Vector2(0.30f, 0.22f), new Vector2(0.30f, 0.30f), 0f, childHead);
        Beat(TReach,       22f, 0.08f, new Vector2(0.26f, 0.24f), new Vector2(0.26f, 0.22f), 0.55f, childHead);
        Beat(TStrokeDown,  28f, 0.10f, new Vector2(0.24f, 0.20f), new Vector2(0.26f, 0.22f), 1.00f, childHead);
        Beat(TStrokeUp,    26f, 0.10f, new Vector2(0.24f, 0.20f), new Vector2(0.26f, 0.22f), 0.88f, childHead);
        Beat(TWithdraw,    18f, 0.06f, new Vector2(0.28f, 0.26f), new Vector2(0.24f, 0.18f), 0.40f, childHead);
        Beat(TTellC,       13f, 0.03f, new Vector2(0.32f, 0.36f), new Vector2(0.24f, 0.20f), 0f, childHead);
        Beat(TEnd,         11f, 0.03f, new Vector2(0.26f, 0.26f), new Vector2(0.22f, 0.17f), 0f, childHead);

        // Leave her in the opening pose so the scene view shows the start.
        Sit(matha, rest, b, seat, fwd, left, up, 10f, 0.03f);
        Hand(b, tellSide, seat, fwd, left, up, new Vector2(0.24f, 0.26f));
        Hand(b, strokeSide, seat, fwd, left, up, new Vector2(0.22f, 0.16f));
        LookAt(matha, b, childHead);

        return rec.Build("Matha_Stage1");
    }

    static Vector3 HandPoint(Vector3 seat, Vector3 fwd, Vector3 left, Vector3 up,
                             Vector2 offset, float sideSign)
        => seat + fwd * offset.x + up * (HipAboveSeat + offset.y) + left * (0.155f * sideSign);

    static void Hand(Dictionary<string, Transform> b, string side, Vector3 seat,
                     Vector3 fwd, Vector3 left, Vector3 up, Vector2 offset)
    {
        float sign = side == "Left" ? 1f : -1f;
        Vector3 target = HandPoint(seat, fwd, left, up, offset, sign);
        PoseTools.TwoBone(b[side + "Arm"], b[side + "ForeArm"], b[side + "Hand"],
                          target, -up * 0.8f - fwd * 0.2f);
    }

    static void Sit(GameObject m, Dictionary<string, Quaternion> rest, Dictionary<string, Transform> b,
                    Vector3 seat, Vector3 fwd, Vector3 left, Vector3 up, float lean, float perch)
    {
        PoseTools.ApplyRestPose(m, rest);

        var hips = b["Hips"];
        hips.position = seat + up * HipAboveSeat + fwd * (perch - 0.04f);

        // Unity right = cross(up, forward). Getting this backwards turns every
        // forward lean into an arch and every downward glance into a look up.
        Vector3 rightAxis = Vector3.Cross(Vector3.up, fwd).normalized;
        hips.rotation = Quaternion.AngleAxis(-8f, rightAxis) * hips.rotation;
        b["Spine"].rotation = Quaternion.AngleAxis(lean, rightAxis) * b["Spine"].rotation;
        b["Chest"].rotation = Quaternion.AngleAxis(lean * 0.55f, rightAxis) * b["Chest"].rotation;

        foreach (var side in new[] { "Left", "Right" })
        {
            float s = side == "Left" ? 0.085f : -0.085f;
            Vector3 ankle = seat + fwd * 0.34f + left * s;
            ankle.y = FloorY + 0.07f;

            PoseTools.TwoBone(b[side + "UpLeg"], b[side + "Leg"], b[side + "Foot"],
                              ankle, fwd + up * 0.35f);
            if (b.TryGetValue(side + "ToeBase", out var toe))
                PoseTools.Aim(b[side + "Foot"], toe, ankle + fwd * 0.14f + Vector3.down * 0.045f);
        }
    }

    static void LookAt(GameObject m, Dictionary<string, Transform> b, Vector3 target)
    {
        var head = b["Head"];
        var neck = b["Neck"];

        Vector3 facing = m.transform.forward;
        Vector3 wanted = (target - head.position).normalized;
        Vector3 flat = Vector3.ProjectOnPlane(wanted, Vector3.up).normalized;
        if (flat.sqrMagnitude < 1e-6f) return;

        float yaw = Mathf.Clamp(Vector3.SignedAngle(facing, flat, Vector3.up), -70f, 70f);
        float pitch = Mathf.Clamp(
            Vector3.SignedAngle(flat, wanted, Vector3.Cross(Vector3.up, flat).normalized), -45f, 40f);

        Vector3 rightAxis = Vector3.Cross(Vector3.up, facing).normalized;
        neck.rotation = Quaternion.AngleAxis(yaw * 0.4f, Vector3.up)
                      * Quaternion.AngleAxis(pitch * 0.4f, rightAxis) * neck.rotation;
        head.rotation = Quaternion.AngleAxis(yaw * 0.6f, Vector3.up)
                      * Quaternion.AngleAxis(pitch * 0.6f, rightAxis) * head.rotation;
    }

    // ----------------------------------------------------------------- cameras

    class Cams { public CinemachineCamera wide, stroke, face; }

    static Cams BuildCameras(GameObject root, GameObject matha, Bounds bedBounds,
                             Vector3 childHead, StringBuilder log)
    {
        var main = Camera.main;
        if (main != null && main.GetComponent<CinemachineBrain>() == null)
        {
            Undo.AddComponent<CinemachineBrain>(main.gameObject);
            log.AppendLine("ใส่ CinemachineBrain ให้ Main Camera");
        }

        var holder = root.transform.Find("Cameras");
        if (holder == null)
        {
            var go = new GameObject("Cameras");
            Undo.RegisterCreatedObjectUndo(go, "Cameras");
            go.transform.SetParent(root.transform, false);
            holder = go.transform;
        }

        CinemachineCamera Make(string name, Vector3 at, Vector3 look, float fov)
        {
            var t = holder.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Add " + name);
                go.transform.SetParent(holder, false);
            }
            else go = t.gameObject;

            var cam = go.GetComponent<CinemachineCamera>();
            if (cam == null) cam = Undo.AddComponent<CinemachineCamera>(go);
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(look - at));
            var lens = cam.Lens;
            lens.FieldOfView = fov;
            cam.Lens = lens;
            cam.Priority = 0;
            log.AppendLine("กล้อง " + name + " ที่ " + at.ToString("F2") + "  fov " + fov);
            return cam;
        }

        // A camera looking at her has to stand on the side she faces. She faces the
        // bed, so anything placed behind her shoulder sees only hair.
        Vector3 fwd = matha.transform.forward;
        // Read from the bone, not guessed from her root: she is seated, so a
        // standing head height aims the camera at the wall above her.
        Vector3 herHead = matha.transform.position + Vector3.up * 1.05f;
        foreach (var t in matha.GetComponentsInChildren<Transform>())
            if (t.name == "Head") herHead = t.position;

        // The opening: the whole tableau, bed and mother in one frame, from the
        // foot of the bed.
        Vector3 tableau = new Vector3(bedBounds.center.x, MattressTop + 0.15f, bedBounds.center.z);
        var wide = Make("CM_Wide",
                        new Vector3(bedBounds.max.x + 1.35f, MattressTop + 0.95f, bedBounds.min.z - 0.55f),
                        tableau + Vector3.up * 0.12f, 46f);

        // Closer, facing her across the foot of the bed, for the hand on his head.
        var stroke = Make("CM_Stroke",
                          new Vector3(bedBounds.center.x + 0.30f, herHead.y + 0.28f, bedBounds.min.z - 0.70f),
                          Vector3.Lerp(childHead, herHead, 0.55f), 40f);

        // Her face and hands as she tells it - the beat the sequence ends on now
        // that there is no book to push into.
        var face = Make("CM_Face",
                        herHead + fwd * 1.05f + Vector3.up * 0.12f + matha.transform.right * 0.34f,
                        herHead - Vector3.up * 0.10f, 34f);

        return new Cams { wide = wide, stroke = stroke, face = face };
    }

    // ---------------------------------------------------------------- timeline

    // Writes a clip over the existing asset when there is one, so the Timeline's
    // reference to it survives a rebuild.
    static AnimationClip SaveClip(AnimationClip clip, string path, StringBuilder log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
        else AssetDatabase.CreateAsset(clip, path);
        log.AppendLine("คลิป: " + path);
        return clip;
    }

    static void BuildTimeline(GameObject root, GameObject matha, AnimationClip clip,
                              GameObject asher, AnimationClip asherClip,
                              Cams cams, List<Say> said, StringBuilder log)
    {
        string path = AssetDir + "/Chapter1_Stage1.playable";
        var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
        if (timeline == null)
        {
            timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(timeline, path);
        }
        else
        {
            foreach (var t in new List<TrackAsset>(timeline.GetOutputTracks()))
                timeline.DeleteTrack(t);
        }
        timeline.editorSettings.frameRate = 30f;

        var animTrack = timeline.CreateTrack<AnimationTrack>(null, "Matha");
        var animClip = animTrack.CreateClip(clip);
        animClip.start = 0d;
        animClip.duration = TEnd;

        AnimationTrack asherTrack = null;
        if (asher != null && asherClip != null)
        {
            asherTrack = timeline.CreateTrack<AnimationTrack>(null, "Asher Kid");
            var ac = asherTrack.CreateClip(asherClip);
            ac.start = 0d;
            ac.duration = TEnd;
        }

        var camTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
        var order = new List<CinemachineCamera>();

        void Shot(CinemachineCamera cam, double start, double end, double blendIn)
        {
            double from = start - blendIn;
            var c = camTrack.CreateClip<CinemachineShot>();
            c.start = from;
            c.duration = System.Math.Max(0.2d, end - from);
            c.blendInDuration = blendIn;
            ((CinemachineShot)c.asset).VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
            c.displayName = cam.name;
            order.Add(cam);
        }

        // The edit, over eighty seconds instead of fourteen.
        //
        // Three setups is not many for a scene this long, so they are spent where
        // they say something: wide while she talks, his face when he speaks or when
        // what she says lands on him, and the close on her hand kept back for the
        // one moment it belongs to. Everything dissolves rather than cuts - it is a
        // bedtime story being remembered, not a conversation being covered.
        Say S(int i) => said[Mathf.Clamp(i, 0, said.Count - 1)];

        Shot(cams.wide, 0.0, S(0).start + 12.0, 0.0);
        Shot(cams.face, S(0).start + 12.0, S(0).start + 20.0, 1.2);          // listening
        Shot(cams.wide, S(0).start + 20.0, S(1).start - 0.6, 1.2);
        Shot(cams.face, S(1).start - 0.6, S(2).start - 0.8, 1.0);            // he asks
        Shot(cams.wide, S(2).start - 0.8, TTellC + 3.0, 1.2);
        Shot(cams.face, TTellC + 3.0, S(2).end - 1.0, 1.2);                  // it lands on him
        Shot(cams.wide, S(2).end - 1.0, S(3).start - 0.6, 1.2);
        Shot(cams.face, S(3).start - 0.6, S(4).start - 0.8, 1.0);            // he asks again
        Shot(cams.wide, S(4).start - 0.8, TReach - 0.5, 1.2);
        Shot(cams.stroke, TReach - 0.5, TWithdraw + 1.0, 1.4);               // the hand, the kiss
        Shot(cams.face, TWithdraw + 1.0, TEnd, 1.6);                         // go to sleep now

        var voiceTracks = AddVoices(timeline, said, log);

        var sound = BuildSound(timeline, log);

        var dirGo = root.transform.Find("Timeline")?.gameObject;
        if (dirGo == null)
        {
            dirGo = new GameObject("Timeline");
            Undo.RegisterCreatedObjectUndo(dirGo, "Timeline");
            dirGo.transform.SetParent(root.transform, false);
        }
        var director = dirGo.GetComponent<PlayableDirector>();
        if (director == null) director = Undo.AddComponent<PlayableDirector>(dirGo);
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;

        director.SetGenericBinding(animTrack, matha.GetComponent<Animator>());
        if (asherTrack != null) director.SetGenericBinding(asherTrack, asher.GetComponent<Animator>());
        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) director.SetGenericBinding(camTrack, brain);

        int i = 0;
        foreach (var c in camTrack.GetClips())
        {
            var shot = (CinemachineShot)c.asset;
            director.SetReferenceValue(shot.VirtualCamera.exposedName, order[i]);
            i++;
        }

        BindSound(root, director, sound, log);
        BindVoices(root, director, voiceTracks, log);
        FillSubtitles(dirGo, said, log);
        FillStills(dirGo, said, log);

        if (dirGo.GetComponent<CutsceneStage1>() == null)
            Undo.AddComponent<CutsceneStage1>(dirGo);

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);
        log.AppendLine("Timeline: " + path + "   ยาว " + TEnd + " วินาที");
        log.AppendLine("แทร็ก: Matha (แอนิเมชัน), Camera (Cinemachine 3 ช็อต), เสียง 5 ชั้น");
    }

    // ----------------------------------------------------------------- stills

    const string ArtDir = "Assets/Art/Cutscene/Chapter1";

    /// The drawings, laid over the two stretches where she is describing a world
    /// the camera cannot show.
    ///
    /// Her voice carries on underneath, so these are not a separate scene - they
    /// are what the scene looks like while she talks. The room comes back for the
    /// boy's questions, which is the only time anything is happening in it.
    ///
    /// Which picture goes where is decided by what is in it against what she is
    /// saying, not by the order the files happened to be numbered in.
    static void FillStills(GameObject dirGo, List<Say> said, StringBuilder log)
    {
        var stills = dirGo.GetComponent<CutsceneStills>();
        if (stills == null) stills = Undo.AddComponent<CutsceneStills>(dirGo);

        Sprite Art(int n)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/" + n + ".png");
            if (s == null) log.AppendLine("   !! หาภาพไม่เจอ: " + n + ".png");
            return s;
        }

        Say L(int i) => said[Mathf.Clamp(i, 0, said.Count - 1)];

        // She has been talking in the room for a few seconds before the pictures
        // take the screen. Opening on them would throw away the shot of the two of
        // them that the scene is actually about.
        float aStart = L(0).start + 3.0f;
        float aEnd = L(0).end;
        float aLen = L(0).end - L(0).start;

        float bStart = L(2).start;
        float bEnd = L(2).end;
        float bLen = L(2).end - L(2).start;

        // The cuts land on sentence boundaries - the same points the captions
        // change - so a picture arrives with the thought it belongs to.
        float a1 = L(0).start + aLen * 0.215f;   // ...trees, green grass, blue sky
        float a2 = L(0).start + aLen * 0.513f;   // ...cities that touched the clouds
        float b1 = L(2).start + bLen * 0.372f;   // ...consumed until hollow and dry
        float b2 = L(2).start + bLen * 0.783f;   // ...just like the one you see today

        var list = new List<CutsceneStills.Still>
        {
            // What the world is now, before she says what it was.
            new CutsceneStills.Still { image = Art(1), start = aStart, end = a1 },
            // The living world: grass, trees, an unbroken sky.
            new CutsceneStills.Still { image = Art(3), start = a1, end = a2 },
            // The wonders: towers into the cloud.
            new CutsceneStills.Still { image = Art(4), start = a2, end = aEnd },

            // Creators destroying their own creations.
            new CutsceneStills.Still { image = Art(6), start = bStart, end = b1 },
            // What was left of the cities.
            new CutsceneStills.Still { image = Art(2), start = b1, end = b2 },
            // Somebody still trying to grow something in it - today.
            new CutsceneStills.Still { image = Art(5), start = b2, end = bEnd },
        };

        Undo.RecordObject(stills, "Stills");
        stills.blocks = new[]
        {
            new CutsceneStills.Block { start = aStart, end = aEnd, fade = 0.9f },
            new CutsceneStills.Block { start = bStart, end = bEnd, fade = 0.9f },
        };
        stills.stills = list.ToArray();
        EditorUtility.SetDirty(stills);

        log.AppendLine("ภาพวาดในฉาก:");
        log.AppendLine("   ช่วงแรก  " + aStart.ToString("F1") + " - " + aEnd.ToString("F1")
                     + " วิ   ภาพ 1, 3, 4   (แม่เล่าถึงโลกที่เคยมี)");
        log.AppendLine("   ช่วงสอง  " + bStart.ToString("F1") + " - " + bEnd.ToString("F1")
                     + " วิ   ภาพ 6, 2, 5   (แม่เล่าว่ามันพังยังไง)");
        log.AppendLine("   กลับมาที่เตียงตอน Asher ถาม และตอนแม่ให้ความหวัง");
    }

    // ------------------------------------------------------------------ voices

    /// One track per speaker, so their levels can be set apart later.
    static AudioTrack[] AddVoices(TimelineAsset timeline, List<Say> said, StringBuilder log)
    {
        var mathaTrack = timeline.CreateTrack<AudioTrack>(null, "VO Matha");
        var asherTrack = timeline.CreateTrack<AudioTrack>(null, "VO Asher");

        foreach (var s in said)
        {
            var track = s.matha ? mathaTrack : asherTrack;
            var c = track.CreateClip(s.clip);
            c.start = s.start;
            c.duration = s.clip.length;
            c.displayName = s.clip.name;
        }

        log.AppendLine("เสียงพูด: Matha 3 ประโยค, Asher 2 ประโยค");
        return new[] { mathaTrack, asherTrack };
    }

    /// Voices play through objects of their own, not through the actors.
    ///
    /// A source parented to a character goes silent the moment anything switches
    /// that character off - which is exactly how Asher's lines went missing in the
    /// camp scene. Flat 2D as well: this is a memory, and distance from a remembered
    /// voice means nothing.
    static void BindVoices(GameObject root, PlayableDirector director,
                           AudioTrack[] tracks, StringBuilder log)
    {
        var holder = root.transform.Find("Voices");
        if (holder == null)
        {
            var go = new GameObject("Voices");
            Undo.RegisterCreatedObjectUndo(go, "Voices");
            go.transform.SetParent(root.transform, false);
            holder = go.transform;
        }

        AudioSource Make(string name)
        {
            var t = holder.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Add " + name);
                go.transform.SetParent(holder, false);
            }
            else go = t.gameObject;

            var src = go.GetComponent<AudioSource>();
            if (src == null) src = Undo.AddComponent<AudioSource>(go);
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = 1f;
            return src;
        }

        director.SetGenericBinding(tracks[0], Make("Voice Matha"));
        director.SetGenericBinding(tracks[1], Make("Voice Asher"));
        log.AppendLine("เสียงพูดอยู่ที่ Cutscene_Ch1/Voices (แยกจากตัวละคร)");
    }

    // --------------------------------------------------------------- subtitles

    /// Splits each spoken line into captions and times them across it.
    ///
    /// Share of the line proportional to share of the characters. It is not lip
    /// sync, but for a scene read this slowly it keeps the words on screen within
    /// a beat of the words being said, and it costs nothing to re-time by hand
    /// afterwards because the result is plain data on the component.
    static void FillSubtitles(GameObject dirGo, List<Say> said, StringBuilder log)
    {
        var subs = dirGo.GetComponent<CutsceneSubtitles>();
        if (subs == null) subs = Undo.AddComponent<CutsceneSubtitles>(dirGo);

        var all = new List<CutsceneSubtitles.Caption>();
        foreach (var s in said)
        {
            if (s.captions == null || s.captions.Length == 0) continue;

            int total = 0;
            foreach (var piece in s.captions) total += Mathf.Max(1, piece.Length);

            float at = s.start;
            for (int i = 0; i < s.captions.Length; i++)
            {
                float share = (s.end - s.start) * Mathf.Max(1, s.captions[i].Length) / total;

                // The last one runs a little past the voice, so the closing words
                // are still readable as the line finishes.
                float stop = i == s.captions.Length - 1 ? s.end + 0.6f : at + share;

                all.Add(new CutsceneSubtitles.Caption { start = at, end = stop, text = s.captions[i] });
                at += share;
            }
        }

        Undo.RecordObject(subs, "Subtitles");
        subs.captions = all.ToArray();
        EditorUtility.SetDirty(subs);
        log.AppendLine("ซับไตเติล " + all.Count + " ท่อน (ปิดได้ที่ Settings > Subtitles)");
    }

    // ------------------------------------------------------------------- sound

    /// One layer of sound: a Timeline track and the AudioSource it plays through.
    /// The source is what carries the volume, which is why each layer needs one of
    /// its own rather than sharing.
    class Layer
    {
        public AudioTrack track;
        public string source;
        public float volume;
    }

    const string SfxDir = "Assets/Audio/SFX/Ch1_Scene1";

    /// Lays the scene's sound under the pictures.
    ///
    /// The five recordings are not interchangeable, and measuring them says so.
    /// The chair and the cloth are long takes holding dozens of separate hits -
    /// loud-to-quiet ratios of 8.1 and 5.9 - so they are cut to single moments and
    /// placed on the beats the animation already has. The wind, the lamp and the
    /// boy's breathing sit nearly flat at 1.9 to 2.0, which is what a bed of
    /// atmosphere is, so they run underneath the whole thing.
    ///
    /// The offsets into the chair and cloth takes are the loudest onsets found by
    /// scanning their envelopes, backed up 50 ms to catch the attack. Starting at
    /// the head of either file would open on handling noise.
    static List<Layer> BuildSound(TimelineAsset timeline, StringBuilder log)
    {
        var layers = new List<Layer>();

        AudioClip Clip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + name + ".mp3");
            if (c == null) log.AppendLine("   !! หาเสียงไม่เจอ: " + name);
            return c;
        }

        Layer Track(string trackName, string sourceName, float volume)
        {
            var l = new Layer
            {
                track = timeline.CreateTrack<AudioTrack>(null, trackName),
                source = sourceName,
                volume = volume
            };
            layers.Add(l);
            return l;
        }

        // A cue, stated as: where it lands in the scene, how far into the recording
        // to start, and how long to hold.
        void Cue(Layer l, AudioClip clip, double at, double from, double length,
                 double fadeIn, double fadeOut)
        {
            if (clip == null) return;
            var c = l.track.CreateClip(clip);
            c.start = at;
            c.duration = length;
            c.clipIn = from;
            c.easeInDuration = fadeIn;
            c.easeOutDuration = fadeOut;
        }

        // --- the bed ---------------------------------------------------------
        var wind = Clip("เสียงลม");
        var windLayer = Track("SFX Wind", "SFX Wind", 0.22f);
        if (wind != null)
        {
            // Nine seconds of wind under a fourteen second scene, so it goes round
            // once. The two passes overlap and cross-fade; a hard restart of a
            // continuous sound is the one thing that makes it audible as a loop.
            double lap = wind.length;
            Cue(windLayer, wind, 0d, 0d, lap, 1.2d, 0.8d);
            Cue(windLayer, wind, lap - 0.8d, 0.4d, TEnd - lap + 0.8d, 0.8d, 1.5d);
        }

        Cue(Track("SFX Lamp", "SFX Lamp", 0.28f), Clip("เสียงโคมไฟ"), 0d, 0d, TEnd, 1.5d, 1.5d);
        // The boy's breathing, brought up where it can actually be heard. At 0.32 it
        // sat under the lamp and the wind and did nothing; a sleeping child in a
        // quiet room is one of the few sounds in the scene worth noticing.
        Cue(Track("SFX Breath", "SFX Breath", 0.50f), Clip("เสียงหายใจ"), 0d, 0d, TEnd, 1.0d, 1.2d);

        // --- the moments -----------------------------------------------------
        var chair = Clip("เก้าอี้ไม้");
        var chairLayer = Track("SFX Chair", "SFX Chair", 0.65f);
        Cue(chairLayer, chair, 0.30d, 1.10d, 1.70d, 0.05d, 0.40d);   // settling into it
        Cue(chairLayer, chair, TReach - 0.15d, 17.90d, 1.90d, 0.05d, 0.50d);  // leaning in to reach

        // No bedding rustle in this scene. The hand going to his hair and coming
        // back reads perfectly well off the picture, and putting cloth under it
        // only drew attention to a movement that wants none.

        log.AppendLine("เสียง:");
        log.AppendLine("   บรรยากาศตลอดฉาก - ลม, โคมไฟ, ลมหายใจเด็ก");
        log.AppendLine("   เก้าอี้ไม้ ที่ 0.3 วิ (นั่งลง) และ " + (TReach - 0.15f).ToString("F1") + " วิ (โน้มตัวไปลูบหัว)");
        return layers;
    }

    /// Gives every layer an AudioSource to play through, and points its track at it.
    static void BindSound(GameObject root, PlayableDirector director,
                          List<Layer> layers, StringBuilder log)
    {
        var holder = root.transform.Find("Sound");
        if (holder == null)
        {
            var go = new GameObject("Sound");
            Undo.RegisterCreatedObjectUndo(go, "Sound");
            go.transform.SetParent(root.transform, false);
            holder = go.transform;
        }

        foreach (var l in layers)
        {
            var t = holder.Find(l.source);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(l.source);
                Undo.RegisterCreatedObjectUndo(go, "Add " + l.source);
                go.transform.SetParent(holder, false);
            }
            else go = t.gameObject;

            var src = go.GetComponent<AudioSource>();
            if (src == null) src = Undo.AddComponent<AudioSource>(go);
            src.playOnAwake = false;
            src.volume = l.volume;

            // Flat 2D, like the voices. A positioned source in a memory of a
            // bedroom only buys distance attenuation that can silence it outright,
            // which is exactly how Asher's lines went missing in Stage 3.
            src.spatialBlend = 0f;

            director.SetGenericBinding(l.track, src);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string acc = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = acc + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(acc, parts[i]);
            acc = next;
        }
    }
}
