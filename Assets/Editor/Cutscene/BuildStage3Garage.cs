using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using System.Text;
using System.Collections.Generic;

// Builds the garage argument as a cutscene: one timeline that owns the shots,
// the voices and the captions together.
//
// The version this replaces rode the dialogue box - the player pressed Next and
// the scene moved on. That works while every line has its own recording, and
// falls apart the moment one does not. Baena's four insults are a single
// twenty-three second take, and Ethan's three lines are one ten second take, so
// four captions were being paced by a button while one clip played underneath
// at its own speed. There is no ordering of those two that stays together.
//
// On a timeline the question does not arise. A caption is a range in seconds on
// the same ruler the audio clip sits on, so it is pinned to the voice whatever
// the player does. Skipping moves the playhead, and the caption and the voice
// move with it because they are the same measurement.
//
// Everything is worked out from the real length of the real audio files, so
// re-recording a line and running this again re-times the whole scene.
//
// Menu: THE AFTER > Cutscene > Build Stage 3 Garage
public static class BuildStage3Garage
{
    const string Root = "Cutscene_Stage3_Garage";
    const string VoiceDir = "Assets/Audio/Voice/Ch1_Scene3/";
    const string TimelinePath = "Assets/Cutscenes/Chapter1/Chapter1_Stage3_Garage.playable";
    const string ClipDir = "Assets/Cutscenes/Chapter1/Garage/";

    // How far through the shot list this build goes.
    //
    // Built in pieces on purpose: nineteen shots landing at once is nineteen
    // things to be wrong about at the same time, and no way to tell which one
    // spoiled the take. Raise it as each block is approved.
    const int ShotsBuilt = 5;

    // Heads on these rigs sit about 1.45 above the floor.
    const float Eye = 1.45f;

    // ------------------------------------------------------------------ script

    class Line
    {
        public string who;
        public string text;
        public string clip;      // null for a line with no recording yet
        public bool shares;      // continues the clip the previous line started
        public float silent;     // seconds to hold when there is no clip
        public float gapAfter;   // beat before the next line starts

        public float start, end; // filled in by Schedule()
    }

    static List<Line> Script()
    {
        return new List<Line>
        {
            new Line { who = "Ethan", clip = "01_Ethan_Welcome", gapAfter = 0.40f,
                text = "There you are, Asher. Logan told me you'd step up. We're running critically low on hands." },

            new Line { who = "Asher", clip = "02_Asher_GladToHelp", gapAfter = 0.40f,
                text = "Glad to help." },

            new Line { who = "Ethan", clip = "03_Ethan_YouCertain", gapAfter = 0.35f,
                text = "You certain you can handle yourself out there?" },

            new Line { who = "Asher", clip = "04_Asher_ImReady", gapAfter = 0.90f,
                text = "I'm ready." },

            // Baena's four, all one take. The clip is named once and the three
            // that follow share it - this is the pairing that could not be kept
            // in step by hand.
            new Line { who = "Baena", clip = "05_Baena_Sneer",
                text = "Is this a damn joke, Ethan?" },
            new Line { who = "Baena", shares = true,
                text = "We're so desperate we're dragging along a scrawny runt whose mother vanished to die in a ditch, and who spends his days clinging to an old man?" },
            new Line { who = "Baena", shares = true,
                text = "He looks like he rolled out of bed five minutes ago." },
            new Line { who = "Baena", shares = true, gapAfter = 0.35f,
                text = "You sure he won't wet himself the second he smells the savages out there?" },

            new Line { who = "Asher", clip = "06_Asher_SayThatAgain", gapAfter = 0.30f,
                text = "Say that to my face again, you bastard!" },

            new Line { who = "Ethan", clip = "07_Ethan_Enough",
                text = "That's enough! Both of you! Baena, shut your mouth." },
            new Line { who = "Ethan", shares = true,
                text = "We need every body we can get, and Asher volunteered." },
            new Line { who = "Ethan", shares = true, gapAfter = 0.60f,
                text = "If you don't feel like starving to death in this hole, learn to work as a team!" },

            // No recording yet. Held rather than cut, because the pause is the
            // beat where he gives up the argument and turns for the truck.
            new Line { who = "Baena", silent = 2.0f, gapAfter = 0.40f, text = "..." },

            new Line { who = "Ethan", clip = "08_Ethan_ReadyToRoll", gapAfter = 0.50f,
                text = "Don't let him get under your skin, Asher. You ready to roll?" },

            new Line { who = "Asher", silent = 2.0f, gapAfter = 0.60f, text = "...Ready." },

            new Line { who = "Sydney", clip = "09_Sydney_LoadUp", gapAfter = 0.30f,
                text = "Come on, boys! Load up before the daylight scorches our skulls!" },

            new Line { who = "Alex", clip = "10_Alex_GearLocked",
                text = "Gear is locked down. Let's move." },
        };
    }

    // Seconds of walking before the first line, and of room after the last.
    const float Opening = 4.0f;
    const float Closing = 3.0f;

    // When Asher's feet stop, measured on the same ruler as everything else.
    //
    // After the first line starts, not before it. Ethan greets a man who is
    // still crossing the last of the floor, which is what people do - waiting
    // for somebody to arrive and come to a halt before saying hello to them is
    // a thing only cutscenes do. It also covers the cut: he is walking in the
    // wide, still walking when it cuts to Ethan, and stopped by the time the
    // camera comes round to him.
    const float AsherStops = 5.4f;

    // Far enough back that the walk is a walk. Three metres over five seconds is
    // a man dawdling; this is about a metre a second, which is somebody with
    // somewhere to be.
    const float AsherWalks = 5.2f;

    // ------------------------------------------------------------------- shots

    enum Move { Cut, DollyIn, Pan, PushIn, PullBack, TurnTo }

    class Shot
    {
        public string name;
        public int line;          // which line it covers; -1 = the opening, -2 = the close
        public string on;         // whose face it holds
        public float back;        // metres from them
        public float swing;       // degrees off their centre line
        public float rise;        // metres above eye height
        public float fov;
        public Move move;
        public string alsoOn;     // second person, for a two-shot

        // TurnTo: the camera stays where it is and swings its aim onto somebody
        // else. Placed at the previous shot's position so the join between the
        // two reads as one camera turning rather than as a cut.
        public string turnTo;
        public string sameSpotAs;

        // Side: framed square on to the line between two people, so both are in
        // profile with the gap between them showing. The angle you use when the
        // point of the shot is the distance between two men rather than either
        // of their faces.
        public bool side;
    }

    static List<Shot> Shots()
    {
        return new List<Shot>
        {
            // Asher walks in, and the camera walks in with him.
            new Shot { name = "G01_Arrive",     line = -1, on = "Ethan",  back = 7.0f, swing = 28f, rise = 1.1f, fov = 44f, move = Move.DollyIn },

            // Ethan speaks while Asher is still covering the last stride. Set
            // wide of Ethan's centre line rather than square on him, so the
            // space Asher is walking into is in the frame and he arrives in
            // shot instead of appearing between two cuts.
            new Shot { name = "G02_Ethan",      line =  0, on = "Ethan",  back = 2.3f, swing = 48f, fov = 36f, move = Move.Cut },

            // The same camera, turning onto Asher now he has stopped. Standing
            // in one place and looking at the other person is what a person in
            // the room would do, and it holds the geography a cut would break.
            new Shot { name = "G03_Asher",      line =  1, on = "Ethan",  back = 2.3f, swing = 48f, fov = 36f,
                       move = Move.TurnTo, turnTo = "Asher", sameSpotAs = "G02_Ethan" },

            // Both of them side on, with the gap between them showing.
            new Shot { name = "G04_Two",        line =  2, on = "Ethan",  back = 2.9f, rise = 0.1f, fov = 40f,
                       move = Move.Pan, alsoOn = "Asher", side = true },

            new Shot { name = "G05_Asher_Tight",line =  3, on = "Asher",  back = 1.15f, swing = -22f, fov = 26f, move = Move.Cut },
        };
    }

    // ------------------------------------------------------------------- build

    [MenuItem("THE AFTER/Cutscene/Build Stage 3 Garage")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new StringBuilder();
        log.AppendFormat("=== Stage 3 Garage: สร้างช็อต 1-{0} ===\n", ShotsBuilt);

        var ethan  = Person(scene, "Ethan");
        var asher  = Person(scene, "Asher");
        var baena  = Person(scene, "Baena");
        var sydney = Person(scene, "Sydney");
        var alex   = Person(scene, "Alex");

        if (ethan == null || asher == null)
        {
            Debug.LogError("[Stage3Garage] ไม่เจอ Ethan หรือ Asher ในซีนนี้");
            return;
        }

        var cast = new Dictionary<string, Transform>
        {
            { "Ethan", ethan }, { "Asher", asher }, { "Baena", baena },
            { "Sydney", sydney }, { "Alex", alex }
        };

        var lines = Script();
        var clips = LoadClips(lines, log);
        float end = Schedule(lines);

        log.AppendFormat("บทเต็ม {0} บรรทัด  ยาวทั้งฉาก {1:F2} วินาที\n", lines.Count, end);

        // ---- the group everything hangs off ----
        var root = GameObject.Find(Root);
        if (root == null)
        {
            root = new GameObject(Root);
            Undo.RegisterCreatedObjectUndo(root, "Build Stage 3 Garage");
        }
        var cams = Child(root.transform, "Cameras");
        var marks = Child(root.transform, "Marks");

        // ---- where people stand ----
        //
        // Ethan works facing his bench, so the room is behind him. Asher comes in
        // from the room and Ethan turns to meet him, which is both what a person
        // does and what lets a camera see either of their faces.
        Vector3 bench = ethan.forward; bench.y = 0f; bench.Normalize();
        Vector3 intoRoom = -bench;

        Vector3 ethanAt = Grounded(ethan.position);
        Vector3 asherAt = Grounded(ethanAt + intoRoom * 1.8f);
        Vector3 asherFrom = Grounded(asherAt + intoRoom * AsherWalks);

        var mkAsher = Mark(marks, "Asher Talk Mark", asherAt, Look(asherAt, ethanAt));
        var mkFrom  = Mark(marks, "Asher Walk Start", asherFrom, Look(asherFrom, ethanAt));
        var mkEthan = Mark(marks, "Ethan Turn Mark", ethanAt, Look(ethanAt, asherAt));

        log.AppendFormat("Asher เดินจาก {0} ไป {1}  ({2:F2} m)\n",
            asherFrom.ToString("F1"), asherAt.ToString("F1"), Vector3.Distance(asherFrom, asherAt));

        // Where each person will be standing while the shots are worked out.
        // Only the two in this block have moved; the rest are where they live.
        var standing = new Dictionary<string, (Vector3 pos, Quaternion rot)>
        {
            { "Ethan", (ethanAt, Look(ethanAt, asherAt)) },
            { "Asher", (asherAt, Look(asherAt, ethanAt)) },
        };

        // ---- the shots ----
        var shots = Shots();
        var made = new List<CinemachineCamera>();
        var moving = new List<(CinemachineCamera cam, Shot shot, Vector3 from, Vector3 to, Quaternion rFrom, Quaternion rTo)>();

        foreach (var s in shots)
        {
            Vector3 look; Quaternion rot;
            Vector3 at = Place(s, cast, standing, out look, out rot);

            var cam = MakeShot(cams, s.name, at, rot, s.fov);
            made.Add(cam);

            if (s.move != Move.Cut)
            {
                // The move is worked out as a second position for the same shot,
                // and the difference between the two is what gets animated.
                Vector3 at2 = at; Quaternion rot2 = rot;
                MoveEnd(s, cast, standing, at, look, ref at2, ref rot2);
                moving.Add((cam, s, at, at2, rot, rot2));
            }
        }
        log.AppendFormat("สร้างกล้อง {0} ตัว (เคลื่อนกล้อง {1})\n", made.Count, moving.Count);

        // ---- the timeline ----
        System.IO.Directory.CreateDirectory(ClipDir);
        var timeline = FreshTimeline(TimelinePath);

        var director = DirectorOn(root, timeline);

        float builtEnd = BuiltEnd(lines, shots);
        AddVoices(timeline, director, lines, clips, root, builtEnd, log);
        AddCaptions(root, lines, builtEnd, log);
        AddCameras(timeline, director, made, shots, lines, builtEnd, moving, log);
        AddAsherWalk(timeline, director, asher, mkFrom, mkAsher, log);

        timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
        timeline.fixedDuration = builtEnd;

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);

        log.AppendFormat("\nส่วนที่สร้างแล้วยาว {0:F2} วินาที  (ทั้งฉากจะเป็น {1:F2})\n", builtEnd, end);
        Debug.Log(log.ToString());
    }

    /// Where the built portion stops - the end of the last line any built shot
    /// covers, plus its gap.
    static float BuiltEnd(List<Line> lines, List<Shot> shots)
    {
        float last = Opening;
        foreach (var s in shots)
        {
            if (s.line < 0) continue;
            var l = lines[s.line];
            if (l.end + l.gapAfter > last) last = l.end + l.gapAfter;
        }
        return last;
    }

    // --------------------------------------------------------------- timing

    static Dictionary<string, AudioClip> LoadClips(List<Line> lines, StringBuilder log)
    {
        var map = new Dictionary<string, AudioClip>();
        foreach (var l in lines)
        {
            if (string.IsNullOrEmpty(l.clip) || map.ContainsKey(l.clip)) continue;
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + l.clip + ".mp3");
            if (c == null) log.AppendFormat("  !! ไม่เจอไฟล์เสียง {0}\n", l.clip);
            else map[l.clip] = c;
        }
        return map;
    }

    /// Lays every line out on the ruler, and returns where the scene ends.
    ///
    /// A run of lines sharing one clip splits that clip's length between them by
    /// how much text each has. It is an estimate - somebody speaks a short line
    /// slowly and a long one fast - but it is an estimate that lands within about
    /// a syllable, and every boundary is a number in the Timeline window
    /// afterwards, so a caption that sits wrong is a drag away from sitting right.
    static float Schedule(List<Line> lines)
    {
        var clips = new Dictionary<string, AudioClip>();
        foreach (var l in lines)
            if (!string.IsNullOrEmpty(l.clip) && !clips.ContainsKey(l.clip))
                clips[l.clip] = AssetDatabase.LoadAssetAtPath<AudioClip>(VoiceDir + l.clip + ".mp3");

        float t = Opening;

        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l.shares) continue;          // handled with the line that owns the clip

            // How many lines ride on this one clip, and how much text between them.
            int n = 1; int chars = l.text.Length;
            for (int j = i + 1; j < lines.Count && lines[j].shares; j++) { n++; chars += lines[j].text.Length; }

            float length = l.silent > 0f ? l.silent
                         : (!string.IsNullOrEmpty(l.clip) && clips.ContainsKey(l.clip) && clips[l.clip] != null)
                            ? clips[l.clip].length : 2.0f;

            if (n == 1)
            {
                l.start = t; l.end = t + length;
                t = l.end + l.gapAfter;
            }
            else
            {
                float at = t;
                for (int k = 0; k < n; k++)
                {
                    var piece = lines[i + k];
                    float share = length * (piece.text.Length / (float)chars);
                    piece.start = at;
                    piece.end = at + share;
                    at = piece.end;
                }
                t = lines[i + n - 1].end + lines[i + n - 1].gapAfter;
            }
        }

        return t + Closing;
    }

    // ---------------------------------------------------------------- framing

    /// Where a shot's camera goes, and which way it points.
    static Vector3 Place(Shot s, Dictionary<string, Transform> cast,
                         Dictionary<string, (Vector3 pos, Quaternion rot)> standing,
                         out Vector3 lookAt, out Quaternion rot)
    {
        Vector3 pos; Quaternion facing;
        Where(s.on, cast, standing, out pos, out facing);

        lookAt = pos + Vector3.up * Eye;

        // A two-shot aims between the pair rather than at one of them, or the
        // second person sits on the edge of frame with their nose cropped.
        Vector3 p2 = pos;
        bool pair = !string.IsNullOrEmpty(s.alsoOn);
        if (pair)
        {
            Quaternion q2;
            Where(s.alsoOn, cast, standing, out p2, out q2);
            lookAt = Vector3.Lerp(lookAt, p2 + Vector3.up * Eye, 0.5f);
        }

        Vector3 at;

        if (s.side && pair)
        {
            // Square on to the line between them. Both end up in profile with
            // the gap in the middle of frame, which is the whole content of the
            // shot: two men a certain distance apart, deciding about each other.
            Vector3 between = p2 - pos; between.y = 0f;
            Vector3 outward = Vector3.Cross(Vector3.up, between).normalized;

            // Flattened first. The height is added once, from the floor - added
            // on top of a midpoint that already carries a height puts the camera
            // five metres up looking down at the tops of two heads.
            Vector3 mid = Vector3.Lerp(pos, p2, 0.5f);
            mid.y = 0f;

            // Whichever side has room. A shot of a wall between two heads is not
            // the shot, however correct the angle is.
            Vector3 tryA = mid + outward * s.back;
            Vector3 tryB = mid - outward * s.back;
            Vector3 lift = Vector3.up * (pos.y + Eye + s.rise);

            bool aClear = !Physics.CheckSphere(tryA + lift, 0.3f);
            at = (aClear ? tryA : tryB) + lift;
        }
        else
        {
            Vector3 f = facing * Vector3.forward; f.y = 0f; f.Normalize();
            Vector3 dir = Quaternion.AngleAxis(s.swing, Vector3.up) * f;
            at = new Vector3(pos.x, 0f, pos.z) + dir * s.back + Vector3.up * (pos.y + Eye + s.rise);
        }

        rot = Quaternion.LookRotation((lookAt - at).normalized);
        return at;
    }

    /// The far end of a moving shot.
    static void MoveEnd(Shot s, Dictionary<string, Transform> cast,
                        Dictionary<string, (Vector3 pos, Quaternion rot)> standing,
                        Vector3 at, Vector3 lookAt, ref Vector3 at2, ref Quaternion rot2)
    {
        Vector3 toward = (lookAt - at); toward.y = 0f;
        float dist = toward.magnitude;
        toward.Normalize();

        switch (s.move)
        {
            case Move.DollyIn:
                // A third of the way in over the shot. Enough to feel like
                // arriving; not so much that the framing changes size on you.
                at2 = at + toward * (dist * 0.34f);
                break;

            case Move.PushIn:
                at2 = at + toward * (dist * 0.18f);
                break;

            case Move.PullBack:
                at2 = at - toward * (dist * 0.30f);
                break;

            case Move.Pan:
                // The lens turns rather than the body moves: a slow arc around
                // the pair, which is what sells two people sizing each other up.
                at2 = RotateAbout(at, lookAt, 9f);
                break;

            case Move.TurnTo:
                {
                    // The body does not move at all. Only the aim swings, from
                    // whoever was speaking onto whoever answers.
                    Vector3 p; Quaternion q;
                    Where(s.turnTo, cast, standing, out p, out q);
                    at2 = at;
                    rot2 = Quaternion.LookRotation(((p + Vector3.up * Eye) - at).normalized);
                    return;
                }
        }

        rot2 = Quaternion.LookRotation((lookAt - at2).normalized);
    }

    static Vector3 RotateAbout(Vector3 point, Vector3 pivot, float degrees)
    {
        Vector3 d = point - pivot;
        return pivot + Quaternion.AngleAxis(degrees, Vector3.up) * d;
    }

    static void Where(string who, Dictionary<string, Transform> cast,
                      Dictionary<string, (Vector3 pos, Quaternion rot)> standing,
                      out Vector3 pos, out Quaternion rot)
    {
        if (standing.ContainsKey(who)) { pos = standing[who].pos; rot = standing[who].rot; return; }
        var t = cast.ContainsKey(who) ? cast[who] : null;
        pos = t != null ? t.position : Vector3.zero;
        rot = t != null ? t.rotation : Quaternion.identity;
    }

    // ----------------------------------------------------------------- pieces

    static void AddVoices(TimelineAsset timeline, PlayableDirector director,
                          List<Line> lines, Dictionary<string, AudioClip> clips,
                          GameObject root, float until, StringBuilder log)
    {
        var track = timeline.CreateTrack<AudioTrack>(null, "Voice");
        var src = root.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(root);
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        int n = 0;
        foreach (var l in lines)
        {
            if (l.shares || string.IsNullOrEmpty(l.clip)) continue;
            if (l.start >= until) continue;
            if (!clips.ContainsKey(l.clip) || clips[l.clip] == null) continue;

            var c = track.CreateClip(clips[l.clip]);
            c.start = l.start;
            c.duration = clips[l.clip].length;
            c.displayName = l.clip;
            n++;
        }

        director.SetGenericBinding(track, src);
        log.AppendFormat("เสียงพากย์ {0} ไฟล์\n", n);
    }

    static void AddCaptions(GameObject root, List<Line> lines, float until, StringBuilder log)
    {
        var subs = root.GetComponent<CutsceneSubtitles>();
        if (subs == null) subs = Undo.AddComponent<CutsceneSubtitles>(root);
        Undo.RecordObject(subs, "Build Stage 3 Garage");

        var list = new List<CutsceneSubtitles.Caption>();
        foreach (var l in lines)
        {
            if (l.start >= until) continue;
            list.Add(new CutsceneSubtitles.Caption
            {
                start = l.start,
                end = Mathf.Min(l.end, until),
                text = l.text
            });
        }

        subs.captions = list.ToArray();
        EditorUtility.SetDirty(subs);

        log.AppendFormat("ซับ {0} บรรทัด\n", list.Count);
        foreach (var c in list)
            log.AppendFormat("   {0,6:F2} - {1,6:F2}  ({2,5:F2} วิ)  {3}\n",
                c.start, c.end, c.end - c.start,
                c.text.Length > 46 ? c.text.Substring(0, 46) + "..." : c.text);
    }

    static void AddCameras(TimelineAsset timeline, PlayableDirector director,
                           List<CinemachineCamera> made, List<Shot> shots, List<Line> lines,
                           float until,
                           List<(CinemachineCamera cam, Shot shot, Vector3 from, Vector3 to, Quaternion rFrom, Quaternion rTo)> moving,
                           StringBuilder log)
    {
        var track = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
        var order = new List<CinemachineCamera>();

        // Without this the track has nowhere to send its shots and the scene
        // plays out on whatever the camera was already pointing at.
        var brain = Object.FindAnyObjectByType<CinemachineBrain>();
        if (brain != null) director.SetGenericBinding(track, brain);
        else log.AppendLine("  !! ไม่เจอ CinemachineBrain - กล้องจะไม่ทำงาน");

        log.AppendLine("ช็อต:");
        for (int i = 0; i < shots.Count; i++)
        {
            var s = shots[i];
            float from = s.line < 0 ? 0f : lines[s.line].start;
            float to = i + 1 < shots.Count
                     ? (shots[i + 1].line < 0 ? Opening : lines[shots[i + 1].line].start)
                     : until;

            if (s.line == -1) { from = 0f; to = Opening; }

            var c = track.CreateClip<CinemachineShot>();
            c.start = from;
            c.duration = Mathf.Max(0.1f, to - from);
            c.blendInDuration = 0d;      // cuts, not blends
            ((CinemachineShot)c.asset).VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
            c.displayName = s.name;
            order.Add(made[i]);

            log.AppendFormat("   {0,-16} {1,6:F2} - {2,6:F2}  ({3,5:F2} วิ)  {4}\n",
                s.name, from, to, to - from, s.move == Move.Cut ? "นิ่ง" : s.move.ToString());
        }

        int k = 0;
        foreach (var c in track.GetClips())
            director.SetReferenceValue(((CinemachineShot)c.asset).VirtualCamera.exposedName, order[k++]);

        // Moving shots get their own animation track: two keys, eased, on the
        // camera's own transform. Cinemachine is happy to be moved underneath -
        // it is the shot that is being dollied, not the aim that is being fought.
        foreach (var m in moving)
        {
            int idx = shots.IndexOf(m.shot);
            float from = m.shot.line < 0 ? 0f : lines[m.shot.line].start;
            float to = idx + 1 < shots.Count
                     ? (shots[idx + 1].line < 0 ? Opening : lines[shots[idx + 1].line].start)
                     : until;
            if (m.shot.line == -1) { from = 0f; to = Opening; }

            // A turn lands early and holds; a dolly takes the whole shot.
            float over = m.shot.move == Move.TurnTo
                ? Mathf.Min(0.55f, (to - from) * 0.45f)
                : (to - from);

            var clip = MoveClip(m.shot.name, m.from, m.to, m.rFrom, m.rTo, to - from, over);

            var anim = m.cam.GetComponent<Animator>();
            if (anim == null) anim = Undo.AddComponent<Animator>(m.cam.gameObject);

            var at = timeline.CreateTrack<AnimationTrack>(null, "Move " + m.shot.name);
            var ac = at.CreateClip(clip);
            ac.start = from;
            ac.duration = to - from;
            ac.displayName = m.shot.name;
            at.trackOffset = TrackOffset.ApplySceneOffsets;

            director.SetGenericBinding(at, anim);
        }
    }

    /// A two-key clip that carries a camera from one framing to another, with
    /// the whole move done in the first `over` seconds and held after.
    ///
    /// A turn onto the next speaker has to arrive before they finish talking.
    /// "Glad to help." is a second and a quarter, and a camera still swinging
    /// when the line ends is a camera that missed it.
    static AnimationClip MoveClip(string name, Vector3 a, Vector3 b, Quaternion ra, Quaternion rb,
                                  float length, float over = 0f)
    {
        if (over <= 0f || over > length) over = length;
        return MoveClipInner(name, a, b, ra, rb, length, over);
    }

    static AnimationClip MoveClipInner(string name, Vector3 a, Vector3 b, Quaternion ra, Quaternion rb,
                                       float length, float over)
    {
        string path = ClipDir + name + "_Move.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.frameRate = 30f;

        // Eased at both ends. A camera that starts and stops at full speed reads
        // as a machine; the whole point of a slow move is that it is not noticed.
        Curve(clip, "m_LocalPosition.x", a.x, b.x, length, over);
        Curve(clip, "m_LocalPosition.y", a.y, b.y, length, over);
        Curve(clip, "m_LocalPosition.z", a.z, b.z, length, over);
        Curve(clip, "m_LocalRotation.x", ra.x, rb.x, length, over);
        Curve(clip, "m_LocalRotation.y", ra.y, rb.y, length, over);
        Curve(clip, "m_LocalRotation.z", ra.z, rb.z, length, over);
        Curve(clip, "m_LocalRotation.w", ra.w, rb.w, length, over);

        EditorUtility.SetDirty(clip);
        return clip;
    }

    static void Curve(AnimationClip clip, string prop, float a, float b, float length, float over)
    {
        AnimationCurve curve;
        if (over >= length - 0.001f)
        {
            curve = new AnimationCurve(new Keyframe(0f, a), new Keyframe(length, b));
            curve.SmoothTangents(0, 0f);
            curve.SmoothTangents(1, 0f);
        }
        else
        {
            // Move, then hold. The third key is what stops the value creeping on
            // after the movement is supposed to have finished.
            curve = new AnimationCurve(new Keyframe(0f, a), new Keyframe(over, b), new Keyframe(length, b));
            curve.SmoothTangents(0, 0f);
            curve.SmoothTangents(1, 0f);
            curve.SmoothTangents(2, 0f);
        }
        clip.SetCurve("", typeof(Transform), prop, curve);
    }

    /// Asher covering the last three metres to the bench while the camera dollies
    /// in behind him. Root motion only for now - the legs come with the walk clip
    /// in the next block.
    static void AddAsherWalk(TimelineAsset timeline, PlayableDirector director,
                             Transform asher, Transform from, Transform to, StringBuilder log)
    {
        var clip = MoveClip("Asher_Arrive", from.position, to.position, from.rotation, to.rotation, AsherStops);

        var anim = asher.GetComponent<Animator>();
        if (anim == null) { log.AppendLine("  !! Asher ไม่มี Animator - ข้ามการเดิน"); return; }

        var track = timeline.CreateTrack<AnimationTrack>(null, "Asher เดินเข้ามา");
        var ac = track.CreateClip(clip);
        ac.start = 0d;
        ac.duration = AsherStops;
        ac.displayName = "Asher เดินเข้ามา";
        track.trackOffset = TrackOffset.ApplySceneOffsets;

        director.SetGenericBinding(track, anim);

        float dist = Vector3.Distance(from.position, to.position);
        log.AppendFormat("Asher เดิน {0:F2} m ใน {1:F2} วิ ({2:F2} m/s)  หยุดหลัง Ethan เริ่มพูด {3:F2} วิ\n",
            dist, AsherStops, dist / AsherStops, AsherStops - Opening);
    }

    // ------------------------------------------------------------------ plumbing

    static PlayableDirector DirectorOn(GameObject root, TimelineAsset timeline)
    {
        var d = root.GetComponent<PlayableDirector>();
        if (d == null) d = Undo.AddComponent<PlayableDirector>(root);
        d.playableAsset = timeline;
        d.playOnAwake = false;
        d.extrapolationMode = DirectorWrapMode.None;
        return d;
    }

    static TimelineAsset FreshTimeline(string path)
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
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
        return timeline;
    }

    static CinemachineCamera MakeShot(Transform parent, string name, Vector3 at, Quaternion rot, float fov)
    {
        var t = parent.Find(name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Stage 3 Garage");
        }

        go.transform.SetPositionAndRotation(at, rot);

        var cam = go.GetComponent<CinemachineCamera>();
        if (cam == null) cam = go.AddComponent<CinemachineCamera>();
        cam.Priority = 0;
        var lens = cam.Lens; lens.FieldOfView = fov; cam.Lens = lens;

        // Timeline owns which shot is live, so nothing here should be tracking
        // anybody on its own.
        cam.LookAt = null;
        cam.Follow = null;
        var stale = go.GetComponent<CinemachineRotationComposer>();
        if (stale != null) Object.DestroyImmediate(stale);

        EditorUtility.SetDirty(cam);
        return cam;
    }

    static Transform Mark(Transform parent, string name, Vector3 pos, Quaternion rot)
    {
        var t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Build Stage 3 Garage");
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
        Undo.RegisterCreatedObjectUndo(go, "Build Stage 3 Garage");
        return go.transform;
    }

    static Vector3 Grounded(Vector3 p)
    {
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 2.5f, Vector3.down, out hit, 6f))
            return new Vector3(p.x, hit.point.y, p.z);
        return p;
    }

    static Quaternion Look(Vector3 from, Vector3 at)
    {
        Vector3 d = at - from; d.y = 0f;
        return d.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(d.normalized);
    }

    static Transform Person(UnityEngine.SceneManagement.Scene s, string name)
    {
        foreach (var r in s.GetRootGameObjects())
            foreach (var t in r.GetComponentsInChildren<Transform>(true))
                if (t.name == name && t.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                    return t;
        return null;
    }
}
