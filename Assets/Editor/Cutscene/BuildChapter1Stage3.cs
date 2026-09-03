using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Builds Chapter 1 Stage 3, which is two cutscenes with the player's first walk
// between them:
//
//   3a  Asher wakes on the bedroll, answers Logan, and gets up off the mat
//   --  the player walks to the fire on WASD and presses E
//   3b  the rest of the conversation, at the fire
//
// The recordings drive the timing. Their lengths decide when each line lands and
// the shots are cut to fit them, so re-recording a line and running this again
// re-times the whole thing. The first three lines belong to 3a because the third
// is Logan telling him to come and eat, which is the cue to start walking.
//
// The staging is read off the bunker: the log Logan sits on is named in the
// model, the fire is found from the stones ringing it, and Asher's seat is the
// other log nearest the bedroll. Move a prop and rebuild, and everything follows.
//
// Menu: THE AFTER > Cutscene > Build Chapter 1 Stage 3
public static class BuildChapter1Stage3
{
    const string Menu = "THE AFTER/Cutscene/Build Chapter 1 Stage 3";
    const string AssetDir = "Assets/Cutscenes/Chapter1";
    const string VoiceDir = "Assets/Audio/Voice/Ch1_Scene2";
    const string RootName = "Cutscene_Ch1_Stage3";

    const string SeatLoganName = "Wood Log Logan Sit";
    const string BedRollName = "Bed Roll";

    const string AsherModel = "Assets/Models/Characters/Asher/Asher.fbx";
    const string LoganModel = "Assets/Models/Characters/Logan/Logan.fbx";

    /// Lines 1-3 play before the walk; the rest after it.
    const int LinesBeforeWalk = 3;

    const float Lead = 2.0f;      // he wakes a beat before anyone speaks
    const float Gap = 0.6f;       // silence between lines
    const float WakeTail = 5.4f;  // room after line 3 for him to get up
    // Long enough for the fire cutaway, the reveal, and the whole business of the
    // bowl being picked up and handed over before Logan starts the long question.
    const float TalkLead = 4.5f;
    const float TalkTail = 2.2f;

    /// The moment the bowl leaves Logan's hand and is in Asher's. Shared, because
    /// two characters and a prop all have to agree on it to the frame.
    const float TakeAt = 3.6f;

    /// Set down on the floor shortly before he gets up to go.
    static float PutDownAt(List<Line> lines)
        => lines[Mathf.Clamp(3, 0, lines.Count - 1)].start - 1.6f;

    /// Hand offsets from the shoulder that more than one beat needs.
    static readonly Vector3 DownToFloor = new Vector3(0.30f, 0.16f, -0.46f);

    /// Arms hanging at his sides.
    ///
    /// Deliberately aimed further down than the arm is long. TripoPose.Arm reels
    /// an out-of-range target back to just short of straight, which is exactly
    /// what a hanging arm is; asking instead for a point the arm can comfortably
    /// touch leaves the elbow cocked for the whole shot, and a standing man with
    /// both elbows bent 55 degrees reads as deformed.
    static readonly Vector3 StandArm = new Vector3(0.03f, 0.12f, -0.53f);

    static Vector3 MirrorArm(Vector3 v) => new Vector3(v.x, -v.y, v.z);

    /// Where the second bowl waits: on the floor beside Logan, on the side he
    /// will be handing it across from. One function, so the bowl and the man
    /// reaching for it cannot end up disagreeing about where it is.
    static Vector3 BowlGroundSpot(Camp camp)
    {
        Vector3 towardAsher = Vector3.ProjectOnPlane(camp.sitAsher - camp.sitLogan, Vector3.up).normalized;
        Vector3 p = camp.sitLogan + towardAsher * 0.42f - camp.faceLogan * 0.30f;
        p.y = camp.floorY;
        return p;
    }

    static Vector3 BowlFloorLook(Camp camp) => BowlGroundSpot(camp) + Vector3.up * 0.10f;

    const float HipAboveSeat = 0.06f;

    class Line { public AudioClip clip; public float start, end; public bool logan; }

    class Camp
    {
        public Vector3 fire;
        public float fireTop, floorY;
        public Transform seatLogan, seatAsher;
        public Vector3 sitLogan, sitAsher;
        public Vector3 faceLogan, faceAsher;

        public Bounds bedRoll;
        public float bedTop;
        public Vector3 bedCentre;
        public Vector3 toFire;    // from the bedroll towards the camp, horizontal
        public Vector3 across;    // across the bedroll
        public Vector3 headEnd;   // where his head lies
        public Vector3 standAt;   // where he ends up on his feet
    }

    [MenuItem(Menu)]
    public static void Build() => Debug.Log(BuildAndReport());

    public static string BuildAndReport()
    {
        var log = new StringBuilder();
        EnsureFolder(AssetDir);

        var camp = ReadCamp(log);
        if (camp == null) return log.ToString();

        var lines = ReadVoiceLines(log);
        if (lines.Count < LinesBeforeWalk + 1) { log.AppendLine("บทพูดไม่ครบ"); return log.ToString(); }

        var root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Stage 3 root");
        }

        // The single-timeline version this replaced left its director behind, and
        // a second director bound to the same characters and the same brain is
        // exactly the fight that put a black screen over the scene.
        var stale = root.transform.Find("Timeline");
        if (stale != null)
        {
            Undo.DestroyObjectImmediate(stale.gameObject);
            AssetDatabase.DeleteAsset(AssetDir + "/Chapter1_Stage3.playable");
            AssetDatabase.DeleteAsset(AssetDir + "/Asher_Stage3.anim");
            AssetDatabase.DeleteAsset(AssetDir + "/Logan_Stage3.anim");
            log.AppendLine("ลบ Timeline เดิมที่รวมสองช่วงไว้ด้วยกันออกแล้ว");
        }

        var logan = Prepare("Logan", LoganModel, camp.sitLogan, log);
        var asher = Prepare("Asher", AsherModel, camp.sitAsher, log);
        if (logan == null || asher == null) return log.ToString();

        BuildFireLight(root, camp, log);
        var voices = BuildVoiceSources(root, camp, log);
        var bowls = BuildBowls(root, camp, logan, log);

        // Exactly as found, to be put back at the end - see the note down there.
        Vector3 bowlWasAt = Vector3.zero, bowlWasScaled = Vector3.one;
        Quaternion bowlWasTurned = Quaternion.identity;
        if (bowls != null && bowls.offered != null)
        {
            bowlWasAt = bowls.offered.transform.position;
            bowlWasTurned = bowls.offered.transform.rotation;
            bowlWasScaled = bowls.offered.transform.localScale;
        }

        // The two halves of the dialogue, each rebased to start at zero.
        var wakeLines = Rebase(lines.Take(LinesBeforeWalk), Lead);
        var talkLines = Rebase(lines.Skip(LinesBeforeWalk), TalkLead);

        float wakeEnd = wakeLines[wakeLines.Count - 1].end + WakeTail;
        float talkEnd = talkLines[talkLines.Count - 1].end + TalkTail;

        // Both men are put in their seats before the cameras are placed. The
        // cameras aim at where the heads actually are, and a character left
        // wherever the last run dropped him aims every shot at that instead -
        // which is how the wide ended up framing a pillar at the world origin.
        SeatForFraming(logan, LoganModel, camp.sitLogan, camp.faceLogan, camp, log);
        SeatForFraming(asher, AsherModel, camp.sitAsher, camp.faceAsher, camp, log);

        var cams = BuildCameras(root, camp, asher, logan, log);

        var wakeAsher = SaveClip(BuildWakeAsherClip(asher, camp, wakeLines, wakeEnd, log),
                                 AssetDir + "/Asher_Stage3a.anim", log);
        var wakeLogan = SaveClip(BuildLoganClip(logan, camp, wakeLines, wakeEnd, "Logan_Stage3a", log),
                                 AssetDir + "/Logan_Stage3a.anim", log);
        var talkAsher = SaveClip(BuildTalkAsherClip(asher, camp, talkLines, talkEnd, log),
                                 AssetDir + "/Asher_Stage3b.anim", log);
        var talkLogan = SaveClip(BuildLoganClip(logan, camp, talkLines, talkEnd, "Logan_Stage3b", log),
                                 AssetDir + "/Logan_Stage3b.anim", log);

        // Built last of the four: it reads the finished poses to find the hands.
        AnimationClip bowlClip = null;
        if (bowls != null)
            bowlClip = SaveClip(BuildBowlClip(bowls, logan, asher, talkLogan, talkAsher,
                                              camp, talkLines, talkEnd, log),
                                AssetDir + "/Bowl_Stage3b.anim", log);

        var wakeGo = BuildWakeTimeline(root, camp, cams, logan, asher, voices,
                                       wakeLogan, wakeAsher, wakeLines, wakeEnd, log);
        var talkGo = BuildTalkTimeline(root, camp, cams, logan, asher, voices,
                                       talkLogan, talkAsher, bowls, bowlClip,
                                       talkLines, talkEnd, log);

        BuildAmbience(root, camp, log);

        var control = BuildPlayerRig(asher, camp, log);
        var campfire = BuildCampfireTrigger(root, camp, log);
        BuildRunner(root, camp, wakeGo, talkGo, asher, control, campfire, log);

        // Put the bowl back exactly where the build found it.
        //
        // Building scrubs the timeline to sample where Asher's hand goes, and that
        // drags the bowl along, leaving it parked at whichever frame was evaluated
        // last - halfway up to a hand, hanging in the air. Nothing drives it until
        // the campfire scene plays, so without this it hangs there for the whole
        // walk up to the fire.
        //
        // What goes back is what was there, not a stored value. That is the whole
        // difference: restoring what was found leaves a hand-placed bowl alone,
        // while restoring a remembered spot silently undoes the placing.
        if (bowls != null && bowls.offered != null)
        {
            bowls.offered.transform.position = bowlWasAt;
            bowls.offered.transform.rotation = bowlWasTurned;
            bowls.offered.transform.localScale = bowlWasScaled;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        return log.ToString();
    }

    static List<Line> Rebase(IEnumerable<Line> source, float lead)
    {
        var list = new List<Line>();
        float t = lead;
        foreach (var l in source)
        {
            float len = l.end - l.start;
            list.Add(new Line { clip = l.clip, start = t, end = t + len, logan = l.logan });
            t += len + Gap;
        }
        return list;
    }

    // ------------------------------------------------------------------- camp

    static Camp ReadCamp(StringBuilder log)
    {
        var c = new Camp();

        Transform seat = null, bed = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == SeatLoganName) seat = t;
            if (t.name == BedRollName) bed = t;
        }

        if (seat == null) { log.AppendLine("หา " + SeatLoganName + " ในซีนไม่เจอ"); return null; }
        var seatRenderer = seat.GetComponent<Renderer>();
        if (seatRenderer == null) { log.AppendLine(SeatLoganName + " ไม่มี Renderer"); return null; }
        if (bed == null || bed.GetComponent<Renderer>() == null) { log.AppendLine("หา " + BedRollName + " ไม่เจอ"); return null; }

        c.seatLogan = seat;
        Vector3 seatCentre = seatRenderer.bounds.center;
        var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // The floor: the widest flat thing under the camp. The roof of the bunker
        // has exactly the same footprint, so "widest and flat" alone picks the
        // ceiling and quietly moves the whole scene eight metres up - it has to be
        // below the seat as well.
        float seatTop = seatRenderer.bounds.max.y;
        float widest = 0f;
        c.floorY = seatTop - 0.4f;
        foreach (var r in all)
        {
            if (Vector3.Distance(r.bounds.center, seatCentre) > 14f) continue;
            var s = r.bounds.size;
            if (s.y > 1.2f || r.bounds.max.y > seatTop) continue;
            float area = s.x * s.z;
            if (area > widest) { widest = area; c.floorY = r.bounds.max.y; }
        }

        // The fire: the cluster of small pieces - stones and sticks - ringing the
        // seat. Blender named them in Thai, so they are found by shape instead.
        var pieces = new List<Renderer>();
        foreach (var r in all)
        {
            if (r.transform == seat) continue;
            var s = r.bounds.size;
            if (s.x > 0.7f || s.y > 0.7f || s.z > 0.7f) continue;
            if (r.bounds.center.y > c.floorY + 1.2f) continue;
            if (Vector3.Distance(r.bounds.center, seatCentre) > 3f) continue;
            pieces.Add(r);
        }
        if (pieces.Count < 3) { log.AppendLine("หากองไฟไม่เจอ (เจอ " + pieces.Count + " ชิ้น)"); return null; }

        var fire = pieces[0].bounds;
        foreach (var r in pieces) fire.Encapsulate(r.bounds);
        c.fire = new Vector3(fire.center.x, c.floorY, fire.center.z);
        c.fireTop = fire.max.y;

        c.bedRoll = bed.GetComponent<Renderer>().bounds;
        c.bedTop = c.bedRoll.max.y;
        c.bedCentre = new Vector3(c.bedRoll.center.x, c.bedTop, c.bedRoll.center.z);
        c.toFire = Vector3.ProjectOnPlane(c.fire - c.bedCentre, Vector3.up).normalized;
        c.across = Vector3.Cross(Vector3.up, c.toFire).normalized;

        // His feet point down the camp, so his head is at the end furthest from
        // the fire and sitting up already faces him the right way.
        float halfLength = Mathf.Max(c.bedRoll.size.x, c.bedRoll.size.z) * 0.5f;
        c.headEnd = c.bedCentre - c.toFire * (halfLength - 0.34f);
        c.standAt = new Vector3(c.bedCentre.x, c.floorY, c.bedCentre.z) + c.toFire * 0.95f;

        // Asher's seat: another log around the same fire - long, low, and not
        // Logan's. Of those, the one nearest the bedroll, since that is the side
        // he walks from.
        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (var r in all)
        {
            if (r.transform == seat) continue;
            var s = r.bounds.size;
            bool longEnough = Mathf.Max(s.x, s.z) > 0.9f && Mathf.Min(s.x, s.z) < 0.9f;
            bool logHigh = s.y > 0.25f && s.y < 0.7f;
            if (!longEnough || !logHigh) continue;
            if (Vector3.Distance(r.bounds.center, c.fire) > 2.6f) continue;

            float d = Vector3.Distance(r.bounds.center, c.bedRoll.center);
            if (d < bestDist) { bestDist = d; best = r.transform; }
        }
        if (best == null) { log.AppendLine("หาท่อนไม้ให้ Asher นั่งไม่เจอ"); return null; }
        c.seatAsher = best;

        Vector3 SitOn(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            return new Vector3(r.bounds.center.x, r.bounds.max.y + HipAboveSeat, r.bounds.center.z);
        }

        c.sitLogan = SitOn(seat);
        c.sitAsher = SitOn(best);
        c.faceLogan = Vector3.ProjectOnPlane(c.fire - c.sitLogan, Vector3.up).normalized;
        c.faceAsher = Vector3.ProjectOnPlane(c.fire - c.sitAsher, Vector3.up).normalized;

        log.AppendLine("แคมป์:");
        log.AppendLine("   พื้น y=" + c.floorY.ToString("F3") + "   กองไฟ " + c.fire.ToString("F2"));
        log.AppendLine("   ที่นอน " + c.bedCentre.ToString("F2") + " ผิวบน y=" + c.bedTop.ToString("F3"));
        log.AppendLine("   หัวนอนที่ " + c.headEnd.ToString("F2") + "   ลุกไปยืนที่ " + c.standAt.ToString("F2"));
        log.AppendLine("   Logan นั่ง " + seat.name + " " + c.sitLogan.ToString("F2"));
        log.AppendLine("   Asher นั่ง " + best.name + " " + c.sitAsher.ToString("F2"));
        return c;
    }

    // ------------------------------------------------------------------ voice

    static List<Line> ReadVoiceLines(StringBuilder log)
    {
        var lines = new List<Line>();
        var paths = AssetDatabase.FindAssets("t:AudioClip", new[] { VoiceDir })
                                 .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();

        float t = Lead;
        foreach (var p in paths)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
            if (clip == null) continue;

            string file = System.IO.Path.GetFileName(p);
            bool logan = file.StartsWith("1") || file.StartsWith("3")
                      || file.StartsWith("4") || file.StartsWith("6");

            lines.Add(new Line { clip = clip, start = t, end = t + clip.length, logan = logan });
            t += clip.length + Gap;
        }

        if (lines.Count == 0) { log.AppendLine("ไม่เจอไฟล์เสียงใน " + VoiceDir); return lines; }

        log.AppendLine();
        log.AppendLine("บทพูด " + lines.Count + " ประโยค (1-" + LinesBeforeWalk + " อยู่ใน 3a, ที่เหลืออยู่ 3b):");
        for (int i = 0; i < lines.Count; i++)
            log.AppendLine("   " + (i < LinesBeforeWalk ? "3a " : "3b ")
                         + (lines[i].logan ? "Logan" : "Asher") + "  ยาว "
                         + (lines[i].end - lines[i].start).ToString("F2") + "   " + lines[i].clip.name);
        return lines;
    }

    // The voices live on their own objects, not on the men.
    //
    // Asher is switched off while the camera is his own eyes, and an AudioSource
    // on a disabled GameObject does not play - his first line landed in that gap
    // and was simply silent. These are always on, and 2D, because cutscene
    // dialogue that fades with the camera angle is dialogue the player misses.
    static AudioSource[] BuildVoiceSources(GameObject root, Camp camp, StringBuilder log)
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

        var sources = new[] { Make("Voice Logan"), Make("Voice Asher") };
        log.AppendLine("เสียงพูดอยู่ที่ " + RootName + "/Voices (แยกจากตัวละคร จะได้ไม่เงียบตอนโมเดลถูกปิด)");
        return sources;
    }

    // --------------------------------------------------------------- subtitles

    /// The camp scene's script, one entry per recording, split into caption-sized
    /// pieces. Split by sentence rather than by clock: a caption that changes
    /// mid-thought is harder to read than a slightly long one.
    static readonly string[][] Script =
    {
        new[] { "You've been dreaming about Matha a lot lately, kid." },
        new[] { "I just... have a bad feeling about things, Uncle." },
        new[] { "Shake it off. Eat up. Breakfast is ready." },
        new[]
        {
            "Our shelter's rations won't last more than two or three days.",
            "If you think you've got the guts, go join my old pal Ethan downstairs.",
            "He's waiting at the lowest parking garage level.",
            "They've only got three or four volunteers ready.",
            "Sigh... Plenty of mouths to feed around here, but damn few spines.",
        },
        new[]
        {
            "Uh... alright. If they're that short on hands, I'll go.",
            "But I never said I was fearless, Uncle.",
        },
        new[]
        {
            "Just make sure you come back in one piece.",
            "I'd hate having to picture what your corpse looks like.",
        },
        new[] { "Right... I'll go pack my gear." },
    };

    /// Logan's last words, which have no recording.
    ///
    /// The script has eight lines and there are seven takes. Rather than drop the
    /// line, it goes up as a caption on its own over the wide shot of Asher getting
    /// to his feet - which is the one moment in the scene where a line without a
    /// voice reads as someone speaking off camera rather than as a missing file.
    const string ClosingLine = "Get moving.";

    /// Captions for one half of the scene.
    ///
    /// `first` says which recording this timeline starts at, because the lines were
    /// split between the waking scene and the conversation and each half was
    /// re-timed from zero - so the script has to be indexed from the original
    /// order, not from the position within the half.
    static void AddSubtitles(GameObject dirGo, List<Line> lines, int first, float end,
                             StringBuilder log)
    {
        var subs = dirGo.GetComponent<CutsceneSubtitles>();
        if (subs == null) subs = Undo.AddComponent<CutsceneSubtitles>(dirGo);

        var all = new List<CutsceneSubtitles.Caption>();
        for (int i = 0; i < lines.Count; i++)
        {
            int s = first + i;
            if (s >= Script.Length) break;

            var pieces = Script[s];
            var line = lines[i];

            int total = 0;
            foreach (var p in pieces) total += Mathf.Max(1, p.Length);

            float at = line.start;
            for (int k = 0; k < pieces.Length; k++)
            {
                float share = (line.end - line.start) * Mathf.Max(1, pieces[k].Length) / total;

                // The last piece of a line outstays the voice a little, so the
                // closing words are still readable as it finishes.
                float stop = k == pieces.Length - 1 ? line.end + 0.5f : at + share;

                all.Add(new CutsceneSubtitles.Caption { start = at, end = stop, text = pieces[k] });
                at += share;
            }
        }

        // The half that finishes the script carries the unvoiced closing line.
        if (first + lines.Count >= Script.Length && all.Count > 0)
        {
            float after = all[all.Count - 1].end + 0.4f;
            float until = Mathf.Min(end - 0.2f, after + 2.2f);

            if (until > after + 0.6f)
            {
                all.Add(new CutsceneSubtitles.Caption { start = after, end = until, text = ClosingLine });
                log.AppendLine("   \"" + ClosingLine + "\" เป็นซับอย่างเดียว ที่ "
                             + after.ToString("F1") + " - " + until.ToString("F1") + " วิ (ไม่มีไฟล์เสียง)");
            }
            else log.AppendLine("   !! ไม่มีที่ว่างท้ายฉากให้ \"" + ClosingLine + "\"");
        }

        Undo.RecordObject(subs, "Subtitles");
        subs.captions = all.ToArray();
        EditorUtility.SetDirty(subs);
        log.AppendLine("ซับไตเติล " + all.Count + " ท่อน บน " + dirGo.name);
    }

    // -------------------------------------------------------------------- sound

    const string SfxDir = "Assets/Audio/SFX/Ch1_Scene2";

    static AudioClip Sfx(string name, StringBuilder log)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + name + ".mp3");
        if (c == null) log.AppendLine("   !! หาไฟล์เสียงไม่เจอ: " + name);
        return c;
    }

    /// The cloth recording lives with the bedroom scene's effects; it is the same
    /// coat moving either way, so there is no reason for a second copy of it.
    static AudioClip Cloth(StringBuilder log)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(
            "Assets/Audio/SFX/Ch1_Scene1/เสียงขยับผ้า.mp3");
        if (c == null) log.AppendLine("   !! หาเสียงขยับผ้าไม่เจอ");
        return c;
    }

    /// Brushes of cloth on one track: him shifting on the bedroll, him coming up
    /// onto his feet. Quiet on purpose - they are there to give the movements some
    /// weight, not to be sound effects.
    ///
    /// Each cue takes a different slice of the take. The same 1.3 seconds played
    /// twice in one scene stops sounding like cloth and starts sounding like a
    /// sample.
    /// The cloth cues exactly as somebody left them, read before the rebuild.
    ///
    /// These get nudged by hand until they land on the movement, and a generated
    /// guess at the timing is worth nothing next to that. Found ones are put back
    /// untouched; only a track that has never existed gets times from this script.
    static List<(double at, double from, double length, double easeIn, double easeOut)>
        SavedCloth(string timelinePath)
    {
        var old = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        if (old == null) return null;

        foreach (var t in old.GetOutputTracks())
        {
            if (!(t is AudioTrack) || t.name != "SFX Cloth") continue;

            var found = new List<(double, double, double, double, double)>();
            foreach (var c in t.GetClips())
                found.Add((c.start, c.clipIn, c.duration, c.easeInDuration, c.easeOutDuration));
            return found.Count > 0 ? found : null;
        }
        return null;
    }

    static void ClothCues(TimelineAsset timeline, PlayableDirector director, GameObject root,
                          (double at, double from, double length, string why)[] cues,
                          List<(double at, double from, double length, double easeIn, double easeOut)> kept,
                          StringBuilder log)
    {
        var clip = Cloth(log);
        if (clip == null) return;

        var track = timeline.CreateTrack<AudioTrack>(null, "SFX Cloth");

        if (kept != null)
        {
            foreach (var k in kept)
            {
                var c = track.CreateClip(clip);
                c.start = k.at;
                c.duration = k.length;
                c.clipIn = k.from;
                c.easeInDuration = k.easeIn;
                c.easeOutDuration = k.easeOut;
            }
            log.AppendLine("เสียงขยับผ้า: คงเวลาที่ตั้งไว้เอง " + kept.Count + " จุด ("
                         + string.Join(", ", kept.ConvertAll(k => k.at.ToString("F1") + " วิ")) + ")");
        }
        else
        {
            foreach (var cue in cues)
            {
                var c = track.CreateClip(clip);
                c.start = cue.at;
                c.duration = cue.length;
                c.clipIn = cue.from;
                c.easeInDuration = 0.05d;
                c.easeOutDuration = System.Math.Min(0.55d, cue.length * 0.45d);
                log.AppendLine("เสียงขยับผ้า: วางครั้งแรกที่ " + cue.at.ToString("F1") + " วิ - " + cue.why);
            }
        }

        var src = SoundSource(root, "SFX Cloth");
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 0f;
        src.volume = 0.26f;
        director.SetGenericBinding(track, src);
    }

    /// An AudioSource of its own under the cutscene's Sound folder. Each layer gets
    /// one because volume lives on the source, not on a Timeline clip.
    static AudioSource SoundSource(GameObject root, string name)
    {
        var holder = root.transform.Find("Sound");
        if (holder == null)
        {
            var go = new GameObject("Sound");
            Undo.RegisterCreatedObjectUndo(go, "Sound");
            go.transform.SetParent(root.transform, false);
            holder = go.transform;
        }

        var t = holder.Find(name);
        GameObject obj;
        if (t == null)
        {
            obj = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(obj, "Add " + name);
            obj.transform.SetParent(holder, false);
        }
        else obj = t.gameObject;

        var src = obj.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(obj);
        return src;
    }

    /// The fire and the weather.
    ///
    /// These belong to the place rather than to any cutscene, so they play
    /// themselves and keep going: the camp is on screen through the waking scene,
    /// the walk and the talk, and an ambience owned by a Timeline would stop dead
    /// the moment control came back to the player.
    ///
    /// Measured, both are near-flat recordings - a loudest-to-quietest ratio of 1.5
    /// for the fire and 1.9 for the wind - which is what makes them safe to loop.
    static void BuildAmbience(GameObject root, Camp camp, StringBuilder log)
    {
        var fire = SoundSource(root, "SFX Campfire");
        fire.clip = Sfx("เสียงcampfire stage 3", log);
        fire.loop = true;
        fire.playOnAwake = true;
        fire.volume = 0.18f;

        // The fire has a place in the world, so it gets quieter from the bedroll
        // and louder as he walks up to it. That distance is half the reason to
        // walk at all.
        fire.spatialBlend = 1f;
        fire.rolloffMode = AudioRolloffMode.Logarithmic;
        fire.minDistance = 2.2f;
        fire.maxDistance = 24f;
        fire.transform.position = camp.fire + Vector3.up * 0.35f;

        var wind = SoundSource(root, "SFX Wind");
        wind.clip = Sfx("เสียงลม", log);
        wind.loop = true;
        wind.playOnAwake = true;
        wind.volume = 0.16f;
        wind.spatialBlend = 0f;      // weather is not somewhere, it is everywhere

        log.AppendLine("บรรยากาศ: กองไฟ (3D ที่ " + fire.transform.position.ToString("F2")
                     + " ได้ยินไกล " + fire.maxDistance + " ม.) + ลม (2D) - เล่นวนเองตลอด ไม่ขึ้นกับ cutscene");
    }

    // ------------------------------------------------------------------ actors

    static GameObject Prepare(string name, string modelPath, Vector3 near, StringBuilder log)
    {
        // A name is not enough to identify anybody. The scene carries an older
        // tutorial Logan on a different rig entirely - Quaternius bones, "Hips"
        // rather than "Hip" - and picking that one by name poses a skeleton that
        // has none of the bones this scene asks for.
        GameObject go = null;
        float best = float.MaxValue;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.parent != null || t.name != name) continue;

            bool rigMatches = false;
            foreach (var child in t.GetComponentsInChildren<Transform>(true))
                if (child.name == TripoPose.Hips) { rigMatches = true; break; }
            if (!rigMatches) { log.AppendLine("ข้าม " + name + " ที่ " + t.position.ToString("F1") + " (คนละริก)"); continue; }

            float d = Vector3.Distance(t.position, near);
            if (d < best) { best = d; go = t.gameObject; }
        }

        if (go == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (src == null) { log.AppendLine("หา " + modelPath + " ไม่เจอ"); return null; }
            go = (GameObject)PrefabUtility.InstantiatePrefab(src);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
        }

        // An earlier run can have left him switched off by an activation track.
        if (!go.activeSelf) { Undo.RecordObject(go, "Enable " + name); go.SetActive(true); }

        var mi = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (mi != null && mi.animationType != ModelImporterAnimationType.Generic)
        {
            mi.animationType = ModelImporterAnimationType.Generic;
            AssetDatabase.WriteImportSettingsIfDirty(modelPath);
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            log.AppendLine(name + ": ตั้ง rig เป็น Generic");
        }

        var anim = go.GetComponent<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(go);
        anim.applyRootMotion = false;

        Undo.RecordObject(go.transform, "Pose " + name);
        go.transform.localScale = Vector3.one;
        TripoPose.KeepSkinFresh(go);
        return go;
    }

    static Dictionary<string, Quaternion> RestPoseOf(GameObject go, string modelPath, StringBuilder log)
    {
        var rest = PoseTools.ReadRestPose(go);
        if (rest.Count > 0) return rest;

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (src == null) { log.AppendLine("อ่านท่าเริ่มต้นของ " + go.name + " ไม่ได้"); return rest; }
        foreach (var t in src.GetComponentsInChildren<Transform>())
            if (t != src.transform) rest[t.name] = t.localRotation;
        return rest;
    }

    static float BoneChain(Dictionary<string, Transform> b, params string[] names)
    {
        float total = 0f;
        for (int i = 0; i < names.Length - 1; i++)
        {
            var a = TripoPose.Bone(b, names[i]);
            var c = TripoPose.Bone(b, names[i + 1]);
            if (a != null && c != null) total += Vector3.Distance(a.position, c.position);
        }
        return total;
    }

    // ------------------------------------------------- Asher, waking and rising

    static AnimationClip BuildWakeAsherClip(GameObject go, Camp camp, List<Line> lines,
                                            float end, StringBuilder log)
    {
        var rest = RestPoseOf(go, AsherModel, log);
        var b = PoseTools.BonesOf(go);
        var rec = new PoseRecorder(go.transform, TripoPose.Bone(b, TripoPose.Hips),
                                   TripoPose.TrackedBones(b), captureRoot: true);

        PoseTools.ApplyRestPose(go, rest);
        float hipHeight = TripoPose.RestHipHeight(go, rest, b);
        float torsoLen = BoneChain(b, TripoPose.Hips, TripoPose.Waist, TripoPose.Spine1,
                                      TripoPose.Spine2, TripoPose.Neck, TripoPose.Head);
        float legLen = BoneChain(b, "L_Thigh", "L_Calf", "L_Foot");

        Vector3 along = camp.toFire;      // down the bed, towards the camp

        // Across the bed, and specifically towards HIS left, because that is what
        // every caller below means by a positive sideways offset.
        //
        // camp.across is derived from the bedroll's own bounds, so which of the two
        // ways it points is an accident of how the model was authored - and it came
        // out pointing to his right. Every left/right pair in this clip was
        // therefore swapped: his left foot was placed on his right, so his legs
        // crossed each other from the first frame to the last.
        // Derived from the way he faces instead, which is the same axis and has a
        // sign that means something. It holds while he is lying down too: his head
        // is up-bed and he faces the ceiling, and that works out to the same left.
        Vector3 side = Vector3.Cross(along, Vector3.up).normalized;

        float mat = camp.bedTop;

        // Everything is placed in the bed's own frame: along it, across it, above
        // it. That way the whole sequence follows if the bedroll is moved.
        Vector3 At(Vector3 origin, float f, float s, float up)
            => origin + along * f + side * s + Vector3.up * up;

        // His head is at the head end, so his hips are a torso further down the bed.
        Vector3 lieHip = camp.headEnd + along * torsoLen;
        lieHip.y = mat + 0.12f;

        Vector3 standHip = new Vector3(camp.standAt.x, camp.floorY + hipHeight, camp.standAt.z);
        Vector3 fireAt = camp.fire + Vector3.up * ((camp.fireTop - camp.floorY) * 0.6f);
        Vector3 loganFace = camp.sitLogan + Vector3.up * 0.72f;

        // Hands are given as offsets from the shoulder, not from the hips.
        //
        // The hips are the wrong origin for an arm: they sit half a torso below
        // the shoulder the arm actually hangs from, so a hand asked for "beside
        // the hip" is further from the shoulder than the arm is long. The solver
        // answers an unreachable target by straightening the limb and pointing,
        // and that is what laid both of Asher's arms flat through his own body.
        //
        // Measured from the shoulder, an offset that looks reachable is reachable.
        // The offset is (down the bed, out to HIS left, up), resolved after the
        // body has been posed so it follows wherever the shoulder ended up.
        //
        // The sideways axis has to be his, not the bed's. camp.across happens to
        // point to his right, so every offset here used to be mirrored: hands that
        // should have hung beside his hips met in front of his crotch instead, and
        // the hand that plants on the mat to push himself up planted across his
        // own body. Both read as a twisted arm, which is exactly what they were.
        void Key(float t, Vector3 hip, Vector3 face, Vector3 up,
                 Vector3 footL, Vector3 footR, float lean,
                 Vector3 handOffL, Vector3 handOffR, Vector3 lookAt, float look)
        {
            TripoPose.Body(go, rest, b, hip, face, up, footL, footR, lean);

            Vector3 hisLeft = TripoPose.HisLeft(b);

            Vector3 FromShoulder(string bone, Vector3 o)
            {
                var shoulder = TripoPose.Bone(b, bone);
                Vector3 at = shoulder != null ? shoulder.position : hip;
                return at + along * o.x + hisLeft * o.y + Vector3.up * o.z;
            }

            TripoPose.Arm(b, "L", FromShoulder("L_Upperarm", handOffL),
                          TripoPose.ElbowPole(hisLeft, face, up, true));
            TripoPose.Arm(b, "R", FromShoulder("R_Upperarm", handOffR),
                          TripoPose.ElbowPole(hisLeft, face, up, false));
            TripoPose.LookAt(go, b, lookAt, look);

            if (t >= 0f) rec.Capture(t);
        }

        // --- flat on his back -------------------------------------------------
        Vector3 lieUp = -along;                       // hip to head runs up the bed
        Vector3 lieFeetL = At(lieHip, legLen * 0.96f, 0.11f, 0f);
        Vector3 lieFeetR = At(lieHip, legLen * 0.96f, -0.11f, 0f);
        lieFeetL.y = mat + 0.07f; lieFeetR.y = mat + 0.07f;
        // Arms lying alongside him, hands down by the hips on the mat. Stated from
        // the shoulder, so a quarter of a metre out and a little under half a
        // metre down the bed is somewhere an arm can actually get to.
        Vector3 lieArmL = new Vector3(0.40f, 0.14f, -0.13f);
        Vector3 lieArmR = new Vector3(0.40f, -0.14f, -0.13f);
        Vector3 ceiling = lieHip + Vector3.up * 2.4f;

        Key(0.0f, lieHip, Vector3.up, lieUp, lieFeetL, lieFeetR, 0f, lieArmL, lieArmR, ceiling, 0.1f);

        // The startle: the chest comes off the mat and drops back, and the hands
        // tighten in towards him rather than staying slack.
        Key(0.9f, lieHip + Vector3.up * 0.02f, Vector3.up,
            Vector3.Slerp(lieUp, Vector3.up, 0.12f).normalized,
            lieFeetL, lieFeetR, -0.18f,
            new Vector3(0.30f, 0.19f, -0.10f), new Vector3(0.30f, -0.19f, -0.10f), ceiling, 0.35f);

        Key(1.9f, lieHip, Vector3.up, lieUp, lieFeetL, lieFeetR, 0f, lieArmL, lieArmR, ceiling, 0.15f);

        // Logan calls; his head turns down the bed towards the fire.
        Key(lines[0].start + 1.0f, lieHip, Vector3.up, lieUp, lieFeetL, lieFeetR, 0f,
            lieArmL, lieArmR, fireAt, 0.75f);

        // --- up on his elbows -------------------------------------------------
        float propT = lines[0].end + 0.2f;
        Vector3 propUp = Vector3.Slerp(lieUp, Vector3.up, 0.40f).normalized;
        Vector3 propFace = Vector3.Slerp(Vector3.up, along, 0.40f).normalized;
        // Weight on the elbows: hands back behind the shoulders and pressed down
        // onto the mat, which is what is holding him up.
        Vector3 propArmL = new Vector3(-0.16f, 0.24f, -0.34f);
        Vector3 propArmR = new Vector3(-0.16f, -0.24f, -0.34f);

        Key(propT, lieHip, propFace, propUp,
            At(lieHip, legLen * 0.90f, 0.12f, mat + 0.07f - lieHip.y),
            At(lieHip, legLen * 0.90f, -0.12f, mat + 0.07f - lieHip.y),
            0.1f, propArmL, propArmR, loganFace, 0.8f);

        // --- sitting up, hands behind him on the mat --------------------------
        float sitT = lines[1].start + 0.4f;
        Vector3 sitHip = new Vector3(lieHip.x, mat + 0.14f, lieHip.z);
        Vector3 sitFootL = At(sitHip, 0.58f, 0.15f, 0f); sitFootL.y = mat + 0.06f;
        Vector3 sitFootR = At(sitHip, 0.58f, -0.15f, 0f); sitFootR.y = mat + 0.06f;
        // Sat up with his hands planted behind him, still taking his weight.
        Vector3 sitArmL = new Vector3(-0.20f, 0.22f, -0.40f);
        Vector3 sitArmR = new Vector3(-0.20f, -0.22f, -0.40f);

        Key(sitT, sitHip, along, Vector3.Slerp(lieUp, Vector3.up, 0.90f).normalized,
            sitFootL, sitFootR, 0.15f, sitArmL, sitArmR, loganFace, 0.9f);

        // --- sitting properly, feet on the floor, listening -------------------
        Vector3 kneeFootL = At(sitHip, 0.62f, 0.16f, 0f); kneeFootL.y = camp.floorY + 0.075f;
        Vector3 kneeFootR = At(sitHip, 0.62f, -0.16f, 0f); kneeFootR.y = camp.floorY + 0.075f;

        // Hands come off the mat and rest forward on his knees.
        Vector3 kneeArmL = new Vector3(0.30f, 0.15f, -0.33f);
        Vector3 kneeArmR = new Vector3(0.30f, -0.15f, -0.33f);

        Key(lines[1].end, sitHip, along, Vector3.up, kneeFootL, kneeFootR, 0.24f,
            kneeArmL, kneeArmR, loganFace, 0.85f);
        Key(lines[2].start + 1.6f, sitHip, along, Vector3.up, kneeFootL, kneeFootR, 0.30f,
            kneeArmL, kneeArmR, fireAt, 0.7f);
        Key(lines[2].end, sitHip, along, Vector3.up, kneeFootL, kneeFootR, 0.20f,
            kneeArmL, kneeArmR, loganFace, 0.85f);

        // --- getting up -------------------------------------------------------
        //
        // The hand goes down on the mat first and the weight goes forward over the
        // feet, then the legs do the rest. Standing straight up out of a sitting
        // pose without that shift reads as a puppet lifted on a string, which is
        // the whole reason this is four keys and not one.
        float up0 = lines[2].end + 0.4f;

        // The left hand goes down flat beside him and takes the push; the right
        // stays forward on the knee.
        Key(up0 + 0.6f, new Vector3(sitHip.x, mat + 0.16f, sitHip.z) + along * 0.08f,
            along, Vector3.up, kneeFootL, kneeFootR, 0.70f,
            new Vector3(0.02f, 0.26f, -0.42f), new Vector3(0.28f, -0.18f, -0.30f),
            camp.standAt + Vector3.up * 0.3f, 0.5f);

        Vector3 standFootL = At(camp.standAt, 0f, 0.16f, 0.075f);
        Vector3 standFootR = At(camp.standAt, 0f, -0.16f, 0.075f);

        Vector3 crouchHip = Vector3.Lerp(sitHip, standHip, 0.55f);
        Key(up0 + 1.5f, crouchHip, along, Vector3.up, standFootL, standFootR, 0.55f,
            new Vector3(-0.02f, 0.24f, -0.40f), new Vector3(0.22f, -0.18f, -0.34f),
            camp.standAt + Vector3.up * 0.5f, 0.45f);

        Vector3 nearlyHip = Vector3.Lerp(sitHip, standHip, 0.90f);
        Key(up0 + 2.2f, nearlyHip, along, Vector3.up, standFootL, standFootR, 0.26f,
            new Vector3(0.10f, 0.20f, -0.40f), new Vector3(0.08f, -0.20f, -0.42f),
            loganFace, 0.6f);

        // Standing, arms hanging. The same offsets the talking clip stands him up
        // with, so the two clips cannot drift apart into two different stances.
        Key(up0 + 3.0f, standHip, along, Vector3.up, standFootL, standFootR, 0.05f,
            StandArm, MirrorArm(StandArm), loganFace, 0.8f);
        Key(end, standHip, along, Vector3.up, standFootL, standFootR, 0.03f,
            StandArm, MirrorArm(StandArm), loganFace, 0.7f);

        // Left lying down, which is where the scene finds him when it starts.
        Key(-1f, lieHip, Vector3.up, lieUp, lieFeetL, lieFeetR, 0f, lieArmL, lieArmR, ceiling, 0.1f);

        log.AppendLine("Asher 3a: นอนที่ " + lieHip.ToString("F2")
                     + " -> ลุกยืนที่ " + standHip.ToString("F2")
                     + "   (ลำตัว " + torsoLen.ToString("F2") + " ม., ขา " + legLen.ToString("F2")
                     + " ม., สะโพกตอนยืนสูง " + hipHeight.ToString("F2") + " ม.)");
        return rec.Build("Asher_Stage3a");
    }

    // ----------------------------------------------- Asher, talking at the fire

    static AnimationClip BuildTalkAsherClip(GameObject go, Camp camp, List<Line> lines,
                                            float end, StringBuilder log)
    {
        var rest = RestPoseOf(go, AsherModel, log);
        var b = PoseTools.BonesOf(go);
        var rec = new PoseRecorder(go.transform, TripoPose.Bone(b, TripoPose.Hips),
                                   TripoPose.TrackedBones(b), captureRoot: true);

        Vector3 fwd = camp.faceAsher;
        Vector3 feet = camp.sitAsher + fwd * 0.46f;
        Vector3 fireAt = camp.fire + Vector3.up * ((camp.fireTop - camp.floorY) * 0.6f);
        Vector3 loganFace = camp.sitLogan + Vector3.up * 0.72f;
        float hipHeight = TripoPose.RestHipHeight(go, rest, b);

        // Hand positions, measured from the shoulder rather than the hips. An arm
        // hangs from the shoulder, and a target stated from the hips sits further
        // away than the arm is long - which the solver answers by locking the limb
        // straight and pointing it through the body.
        Vector3 lap = new Vector3(0.24f, 0.14f, -0.30f);   // hands loose in his lap
        Vector3 knees = new Vector3(0.30f, 0.15f, -0.33f);
        Vector3 toFire = new Vector3(0.34f, 0.12f, -0.22f);
        Vector3 open = new Vector3(0.30f, 0.20f, -0.15f);
        Vector3 hold = new Vector3(0.26f, 0.13f, -0.26f);   // cradling the bowl
        Vector3 reach = new Vector3(0.34f, 0.10f, -0.14f);  // out to take it
        Vector3 eat = new Vector3(0.21f, 0.07f, 0.06f);    // bowl up in front of his mouth
        Vector3 Mirror(Vector3 v) => new Vector3(v.x, -v.y, v.z);

        void Beat(float t, float rise, float lean, Vector3 lookAt, float look,
                  Vector3 leftOff, Vector3 rightOff)
        {
            Vector3 stood = new Vector3(feet.x, camp.floorY + hipHeight, feet.z);
            Vector3 hipPoint = Vector3.Lerp(camp.sitAsher, stood, rise);

            TripoPose.Upright(go, rest, b, hipPoint, feet, camp.floorY, fwd, lean);

            Vector3 hisLeft = TripoPose.HisLeft(b);
            Vector3 FromShoulder(string bone, Vector3 o)
            {
                var s2 = TripoPose.Bone(b, bone);
                Vector3 at = s2 != null ? s2.position : hipPoint;
                return at + fwd * o.x + hisLeft * o.y + Vector3.up * o.z;
            }

            TripoPose.Arm(b, "L", FromShoulder("L_Upperarm", leftOff),
                          TripoPose.ElbowPole(hisLeft, fwd, Vector3.up, true));
            TripoPose.Arm(b, "R", FromShoulder("R_Upperarm", rightOff),
                          TripoPose.ElbowPole(hisLeft, fwd, Vector3.up, false));
            TripoPose.LookAt(go, b, lookAt, look);

            if (t >= 0f) rec.Capture(t);
        }

        Line L(int i) => lines[Mathf.Clamp(i, 0, lines.Count - 1)];
        float s = L(0).start, len = L(0).end - L(0).start;

        // Sat down with nothing yet - Logan is still reaching for the second bowl.
        Beat(0f, 0f, 0.26f, fireAt, 0.75f, lap, Mirror(lap));
        Beat(TakeAt - 0.6f, 0f, 0.18f, loganFace, 0.90f, reach, Mirror(lap));

        // Taken. From here the left hand is holding it and the bowl track follows.
        Beat(TakeAt, 0f, 0.16f, loganFace, 0.85f, hold, Mirror(lap));
        Beat(s, 0f, 0.24f, loganFace, 0.85f, hold, Mirror(knees));

        // Listening through the long one, eating while he does. A mouthful is two
        // keys - up and back down - so it reads as a movement rather than a jump.
        Beat(s + len * 0.10f, 0f, 0.26f, loganFace, 0.90f, hold, Mirror(knees));
        Beat(s + len * 0.18f, 0f, 0.30f, fireAt, 0.45f, eat, Mirror(knees));   // only the hand with the bowl comes up
        Beat(s + len * 0.26f, 0f, 0.26f, loganFace, 0.85f, hold, Mirror(knees));
        Beat(s + len * 0.40f, 0f, 0.34f, fireAt, 0.75f, hold, Mirror(toFire));
        Beat(s + len * 0.50f, 0f, 0.30f, fireAt, 0.45f, eat, Mirror(knees));   // only the hand with the bowl comes up
        Beat(s + len * 0.58f, 0f, 0.20f, loganFace, 0.95f, hold, Mirror(knees));
        Beat(s + len * 0.74f, 0f, 0.30f, fireAt, 0.70f, hold, Mirror(toFire));
        Beat(s + len * 0.84f, 0f, 0.28f, fireAt, 0.45f, eat, Mirror(knees));   // only the hand with the bowl comes up
        Beat(s + len * 0.94f, 0f, 0.24f, loganFace, 0.85f, hold, Mirror(knees));

        // His answer - he stops eating and leans in for it.
        Beat(L(1).start + 0.6f, 0f, 0.12f, loganFace, 0.95f, hold, Mirror(open));
        Beat(L(1).start + 3.6f, 0f, 0.20f, loganFace, 0.85f, hold, Mirror(open));
        Beat(L(1).end, 0f, 0.26f, fireAt, 0.70f, hold, Mirror(knees));

        // Taking the sarcasm - eyes down, another mouthful, then back up to him.
        Beat(L(2).start + 1.4f, 0f, 0.34f, fireAt, 0.80f, hold, Mirror(knees));
        Beat(L(2).start + 3.2f, 0f, 0.30f, fireAt, 0.45f, eat, Mirror(knees));   // only the hand with the bowl comes up
        Beat(L(2).start + 5.0f, 0f, 0.24f, loganFace, 0.90f, hold, Mirror(knees));
        Beat(L(2).end, 0f, 0.16f, loganFace, 0.85f, hold, Mirror(knees));

        // Setting the bowl down on the floor beside him, then getting up.
        Beat(PutDownAt(lines), 0f, 0.62f, camp.floorY * Vector3.up + camp.sitAsher, 0.55f,
             DownToFloor, Mirror(knees));
        Beat(PutDownAt(lines) + 0.7f, 0f, 0.30f, loganFace, 0.60f, lap, Mirror(lap));

        float up = L(3).start - 0.5f;
        Beat(up, 0f, 0.20f, loganFace, 0.70f, lap, Mirror(lap));
        Beat(up + 0.55f, 0.10f, 0.72f, fireAt, 0.40f, lap, Mirror(lap));
        Beat(up + 1.15f, 0.70f, 0.34f, loganFace, 0.35f, open, Mirror(lap));
        Beat(up + 1.70f, 1f, 0.06f, loganFace, 0.65f, StandArm, Mirror(StandArm));
        Beat(end, 1f, 0.04f, loganFace, 0.55f, StandArm, Mirror(StandArm));

        Beat(-1f, 0f, 0.26f, fireAt, 0.75f, lap, Mirror(lap));

        log.AppendLine("Asher 3b: นั่งที่ " + camp.sitAsher.ToString("F2")
                     + "   รับถ้วยที่ " + TakeAt.ToString("F1") + " วิ"
                     + "   วางถ้วยที่ " + PutDownAt(lines).ToString("F1") + " วิ");
        return rec.Build("Asher_Stage3b");
    }

    // ------------------------------------------------------------------ Logan

    static AnimationClip BuildLoganClip(GameObject go, Camp camp, List<Line> lines,
                                        float end, string clipName, StringBuilder log)
    {
        var rest = RestPoseOf(go, LoganModel, log);
        var b = PoseTools.BonesOf(go);
        var rec = new PoseRecorder(go.transform, TripoPose.Bone(b, TripoPose.Hips),
                                   TripoPose.TrackedBones(b), captureRoot: true);

        Vector3 fwd = camp.faceLogan;
        Vector3 feet = camp.sitLogan + fwd * 0.46f;
        Vector3 fireAt = camp.fire + Vector3.up * ((camp.fireTop - camp.floorY) * 0.6f);
        Vector3 asherFace = camp.sitAsher + Vector3.up * 0.72f;
        Vector3 bedAt = camp.bedCentre + Vector3.up * 0.45f;

        // From the shoulder, not the hips - see the note on Asher's beats. His left
        // hand keeps his own bowl, so the gestures all belong to the right.
        Vector3 hold = new Vector3(0.26f, 0.12f, -0.24f);   // his own bowl
        Vector3 knees = new Vector3(0.30f, 0.15f, -0.33f);
        Vector3 toFire = new Vector3(0.34f, 0.12f, -0.22f);
        Vector3 open = new Vector3(0.30f, 0.22f, -0.14f);
        Vector3 point = new Vector3(0.40f, 0.16f, -0.05f);
        Vector3 shrug = new Vector3(0.18f, 0.28f, -0.10f);
        Vector3 eat = new Vector3(0.21f, 0.07f, 0.06f);    // bowl up in front of his mouth
        Vector3 offer = new Vector3(0.36f, 0.10f, -0.20f);  // held out to Asher
        Vector3 Mirror(Vector3 v) => new Vector3(v.x, -v.y, v.z);

        void Beat(float t, float lean, Vector3 lookAt, float look, Vector3 leftOff, Vector3 rightOff)
        {
            TripoPose.Upright(go, rest, b, camp.sitLogan, feet, camp.floorY, fwd, lean);

            Vector3 hisLeft = TripoPose.HisLeft(b);
            Vector3 FromShoulder(string bone, Vector3 o)
            {
                var s2 = TripoPose.Bone(b, bone);
                Vector3 at = s2 != null ? s2.position : camp.sitLogan;
                return at + fwd * o.x + hisLeft * o.y + Vector3.up * o.z;
            }

            TripoPose.Arm(b, "L", FromShoulder("L_Upperarm", leftOff),
                          TripoPose.ElbowPole(hisLeft, fwd, Vector3.up, true));
            TripoPose.Arm(b, "R", FromShoulder("R_Upperarm", rightOff),
                          TripoPose.ElbowPole(hisLeft, fwd, Vector3.up, false));
            TripoPose.LookAt(go, b, lookAt, look);

            if (t >= 0f) rec.Capture(t);
        }

        Line L(int i) => lines[Mathf.Clamp(i, 0, lines.Count - 1)];

        // Warming his hands over the pot before anything happens.
        Beat(0f, 0.22f, fireAt, 0.85f, toFire, Mirror(toFire));

        if (lines.Count <= LinesBeforeWalk)
        {
            // 3a - bowl already in his left hand, waiting on the lad to wake up.
            Beat(L(0).start - 0.5f, 0.16f, bedAt, 0.75f, hold, Mirror(knees));
            Beat(L(0).start + 0.9f, 0.10f, bedAt, 0.95f, hold, Mirror(open));
            Beat(L(0).end, 0.14f, bedAt, 0.85f, hold, Mirror(knees));

            // A mouthful while Asher answers - he is not waiting politely.
            Beat(L(1).start + 1.2f, 0.24f, fireAt, 0.55f, eat, Mirror(knees));
            Beat(L(1).end, 0.20f, bedAt, 0.80f, hold, Mirror(toFire));

            Beat(L(2).start + 0.6f, 0.12f, bedAt, 0.85f, hold, Mirror(open));
            Beat(L(2).start + 2.2f, 0.18f, fireAt, 0.70f, hold, Mirror(open));
            Beat(L(2).end, 0.22f, bedAt, 0.75f, hold, Mirror(open));
            Beat(end, 0.20f, bedAt, 0.80f, hold, Mirror(knees));
        }
        else
        {
            // 3b - he fetches the second bowl up off the floor and hands it over
            // before he says anything. The reach and the offer are the beats the
            // bowl's own track is keyed against.
            Beat(0.6f, 0.30f, camp.sitAsher, 0.30f, hold, Mirror(knees));
            Beat(TakeAt - 2.2f, 0.72f, BowlFloorLook(camp), 0.55f, hold, Mirror(DownToFloor));
            Beat(TakeAt - 1.4f, 0.55f, BowlFloorLook(camp), 0.45f, hold, Mirror(DownToFloor));
            Beat(TakeAt - 0.5f, 0.22f, asherFace, 0.85f, hold, Mirror(offer));
            Beat(TakeAt, 0.18f, asherFace, 0.90f, hold, Mirror(offer));
            Beat(TakeAt + 0.7f, 0.20f, asherFace, 0.75f, hold, Mirror(knees));

            float s = L(0).start, len = L(0).end - L(0).start;
            Beat(s - 0.6f, 0.20f, fireAt, 0.80f, hold, Mirror(knees));
            Beat(s + len * 0.18f, 0.10f, asherFace, 0.90f, hold, Mirror(open));
            Beat(s + len * 0.32f, 0.24f, asherFace, 0.80f, hold, Mirror(open));
            Beat(s + len * 0.40f, 0.28f, fireAt, 0.50f, eat, Mirror(knees));
            Beat(s + len * 0.46f, 0.14f, fireAt, 0.70f, hold, Mirror(toFire));
            Beat(s + len * 0.60f, 0.28f, asherFace, 0.95f, hold, Mirror(point));
            Beat(s + len * 0.74f, 0.18f, asherFace, 0.85f, hold, Mirror(open));
            Beat(s + len * 0.88f, 0.12f, fireAt, 0.75f, hold, Mirror(knees));
            Beat(L(0).end + 0.4f, 0.22f, asherFace, 0.70f, hold, Mirror(knees));

            Beat(L(1).start + 1.0f, 0.16f, asherFace, 0.95f, hold, Mirror(knees));
            Beat(L(1).start + 4.5f, 0.26f, fireAt, 0.50f, eat, Mirror(knees));
            Beat(L(1).end, 0.24f, asherFace, 0.85f, hold, Mirror(knees));

            Beat(L(2).start + 0.8f, 0.10f, asherFace, 0.90f, hold, Mirror(shrug));
            Beat(L(2).start + 3.4f, 0.26f, fireAt, 0.80f, hold, Mirror(knees));
            Beat(L(2).start + 6.0f, 0.14f, asherFace, 0.85f, hold, Mirror(open));
            Beat(L(2).end, 0.20f, fireAt, 0.75f, hold, Mirror(toFire));

            Beat(L(3).start + 0.6f, 0.12f, asherFace, 0.80f, hold, Mirror(knees));
            Beat(end, 0.26f, fireAt, 0.90f, eat, Mirror(knees));
        }

        Beat(-1f, 0.22f, fireAt, 0.85f, hold, Mirror(toFire));
        return rec.Build(clipName);
    }

    // ------------------------------------------------------------------ bowls

    const string BowlPath = "Assets/Models/Food/bowlCereal.obj";


    class Bowls
    {
        public GameObject held;        // Logan's own, riding in his hand
        public GameObject offered;     // the one that changes hands, animated
        public Vector3 byLogan;        // where it waits on the floor
        public Vector3 byAsher;        // where Asher sets it down at the end
        public float liftOffFloor;     // half its height, so it rests rather than sinks
    }

    // Two bowls: one Logan is already holding, one waiting on the floor for Asher.
    //
    // His own is parented to his hand and never animated - a prop that follows the
    // bone needs no keys, and keys on a prop that also has a parent is a fight
    // waiting to happen. The other one has to travel between three places, so it
    // gets an Animator and a track of its own.
    static Bowls BuildBowls(GameObject root, Camp camp, GameObject logan, StringBuilder log)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(BowlPath);
        if (src == null) { log.AppendLine("!! หา " + BowlPath + " ไม่เจอ"); return null; }

        // How tall it is, so it can sit on a surface instead of through it.
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(src);
        probe.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var pb = new Bounds(Vector3.zero, Vector3.zero);
        bool first = true;
        foreach (var r in probe.GetComponentsInChildren<Renderer>())
        { if (first) { pb = r.bounds; first = false; } else pb.Encapsulate(r.bounds); }
        Object.DestroyImmediate(probe);

        var b = new Bowls { liftOffFloor = -pb.min.y };

        // A bowl already in the scene has been placed by hand, and hand placement
        // beats anything worked out here - the size and the offset in the palm are
        // judged by eye, and this script has no way to arrive at them. Existing
        // bowls are found and left exactly as they are; only a missing one is made.
        bool freshLogan = false, freshAsher = false;

        GameObject Spawn(string name, Transform parent, ref bool fresh)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name == name) return t.gameObject;

            var made = (GameObject)PrefabUtility.InstantiatePrefab(src);
            made.name = name;
            Undo.RegisterCreatedObjectUndo(made, "Add " + name);
            made.transform.SetParent(parent, true);
            fresh = true;
            return made;
        }

        // Logan's, riding in the hand he is not going to gesture with.
        var loganHand = FindBone(logan, "L_Hand");
        b.held = Spawn("Bowl Logan", loganHand != null ? loganHand : logan.transform, ref freshLogan);
        if (freshLogan && loganHand != null)
        {
            b.held.transform.localPosition = new Vector3(0.05f, 0.04f, 0.01f);
            b.held.transform.localRotation = Quaternion.identity;
        }

        // The waiting one lives at the top of the scene so nothing else moves it.
        b.offered = Spawn("Bowl Asher", root.transform, ref freshAsher);
        var anim = b.offered.GetComponent<Animator>();
        if (anim == null) anim = Undo.AddComponent<Animator>(b.offered);
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        Vector3 towardAsher = Vector3.ProjectOnPlane(camp.sitAsher - camp.sitLogan, Vector3.up).normalized;

        b.byLogan = BowlGroundSpot(camp);
        b.byLogan.y = camp.floorY + b.liftOffFloor;

        b.byAsher = camp.sitAsher + camp.faceAsher * 0.34f - towardAsher * 0.20f;
        b.byAsher.y = camp.floorY + b.liftOffFloor;

        // Where the bowl rests is whatever the bowl's own Position says. Nothing
        // else gets a vote.
        //
        // There was a marker object here that remembered a resting place and put
        // the bowl back on it every build. It was meant to stop the position
        // drifting; what it actually did was take the position away from the person
        // setting it - move the bowl, run a build, watch it jump back to a spot
        // recorded days ago. A remembered value that overrules a hand-placed one is
        // not a safeguard, it is an override.
        //
        // This reads before anything in this build evaluates a timeline, which is
        // the only thing that moves the bowl on its own, and the value is put back
        // at the end of the build so a scrub cannot leave it somewhere else.
        if (freshAsher)
        {
            Undo.RecordObject(b.offered.transform, "Place new bowl");
            b.offered.transform.position = b.byLogan;
            b.offered.transform.rotation = Quaternion.identity;
        }
        else b.byLogan = b.offered.transform.position;

        log.AppendLine("ถ้วย Logan: " + (freshLogan ? "สร้างใหม่ ผูกกับมือซ้าย" : "มีอยู่แล้ว - ไม่แตะตำแหน่ง/ขนาด"));
        log.AppendLine("ถ้วย Asher: " + (freshAsher ? "สร้างใหม่ วางที่ " + b.byLogan.ToString("F2")
                                                    : "ใช้ตำแหน่งที่คุณตั้งไว้ " + b.byLogan.ToString("F2")));
        return b;
    }

    /// Sits a character down where the scene says he sits, so anything that
    /// measures him - the cameras, most of all - measures the right thing.
    static void SeatForFraming(GameObject go, string modelPath, Vector3 seat,
                               Vector3 face, Camp camp, StringBuilder log)
    {
        var rest = RestPoseOf(go, modelPath, log);
        var b = PoseTools.BonesOf(go);
        TripoPose.Upright(go, rest, b, seat, seat + face * 0.46f, camp.floorY, face, 0.22f);
    }

    static Transform FindBone(GameObject go, string name)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    // The bowl's journey: the floor, Logan's hand, Asher's hand, the floor again.
    //
    // Where a hand will be is not guessed. The finished character clips are
    // sampled at each moment and the bone read off, so the bowl is keyed to the
    // exact point the pose put the palm - and it stays in the hand through the
    // eating, because it is sampled densely enough to follow it up and down.
    static AnimationClip BuildBowlClip(Bowls bowls, GameObject logan, GameObject asher,
                                       AnimationClip loganClip, AnimationClip asherClip,
                                       Camp camp, List<Line> lines, float end, StringBuilder log)
    {
        var px = new AnimationCurve();
        var py = new AnimationCurve();
        var pz = new AnimationCurve();

        void Key(float t, Vector3 p)
        {
            px.AddKey(t, p.x); py.AddKey(t, p.y); pz.AddKey(t, p.z);
        }

        // A bowl rides above the palm, not inside it.
        Vector3 cupped = Vector3.up * 0.045f;

        Vector3 HandAt(GameObject who, AnimationClip clip, string bone, float t)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(who, clip, t);
            AnimationMode.EndSampling();
            var h = FindBone(who, bone);
            return h != null ? h.position + cupped : bowls.byLogan;
        }

        float put = PutDownAt(lines);
        int samples = 0;

        AnimationMode.StartAnimationMode();
        try
        {
            Key(0f, bowls.byLogan);
            Key(TakeAt - 2.4f, bowls.byLogan);

            // Lifted off the floor by Logan, held out, taken.
            Key(TakeAt - 1.5f, HandAt(logan, loganClip, "R_Hand", TakeAt - 1.5f));
            Key(TakeAt - 0.5f, HandAt(logan, loganClip, "R_Hand", TakeAt - 0.5f));
            Key(TakeAt - 0.15f, HandAt(logan, loganClip, "R_Hand", TakeAt - 0.15f));

            // From the handover to the moment he puts it down it lives in Asher's
            // left hand, sampled often enough that raising it to his mouth carries
            // the bowl with it.
            for (float t = TakeAt; t <= put; t += 0.2f)
            {
                Key(t, HandAt(asher, asherClip, "L_Hand", t));
                samples++;
            }

            Key(put + 0.55f, bowls.byAsher);
            Key(end, bowls.byAsher);
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }

        void Smooth(AnimationCurve c)
        {
            for (int i = 0; i < c.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            }
        }
        Smooth(px); Smooth(py); Smooth(pz);

        var clip = new AnimationClip { name = "Bowl_Stage3b", frameRate = 30f };
        clip.SetCurve("", typeof(Transform), "localPosition.x", px);
        clip.SetCurve("", typeof(Transform), "localPosition.y", py);
        clip.SetCurve("", typeof(Transform), "localPosition.z", pz);

        log.AppendLine("ถ้วยที่ยื่นให้: อยู่กับพื้นจนถึง " + (TakeAt - 1.5f).ToString("F1")
                     + " วิ -> มือ Logan -> มือ Asher ที่ " + TakeAt.ToString("F1") + " วิ"
                     + " (ตามมือ " + samples + " จุด) -> วางพื้นที่ " + (put + 0.55f).ToString("F1") + " วิ");
        return clip;
    }

    // ------------------------------------------------------------------ light

    static void BuildFireLight(GameObject root, Camp camp, StringBuilder log)
    {
        var t = root.transform.Find("Campfire Light");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("Campfire Light");
            Undo.RegisterCreatedObjectUndo(go, "Campfire light");
            go.transform.SetParent(root.transform, false);
        }
        else go = t.gameObject;

        var light = go.GetComponent<Light>();
        if (light == null) light = Undo.AddComponent<Light>(go);

        go.transform.position = camp.fire + Vector3.up * ((camp.fireTop - camp.floorY) * 0.7f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.30f);
        light.intensity = 14f;
        light.range = 9.5f;
        light.shadows = LightShadows.Soft;
        log.AppendLine("ไฟกองไฟที่ " + go.transform.position.ToString("F2") + " (ลบทิ้งได้)");
    }

    // ---------------------------------------------------------------- cameras

    class Cams
    {
        public CinemachineCamera povWake, bedSide, bedWide, getUp;
        public CinemachineCamera fire, wide, logan, asher;
        public CinemachineCamera loganOts, asherOts, fireLow;
    }

    static Cams BuildCameras(GameObject root, Camp camp, GameObject asher, GameObject logan,
                             StringBuilder log)
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

        // `up` is worth stating for any shot that looks near-vertical: with the
        // look direction almost parallel to world up, LookRotation has nothing
        // left to resolve the roll from and the frame comes out canted at random.
        CinemachineCamera Make(string name, Vector3 at, Vector3 look, float fov, Vector3? up = null)
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

            Vector3 dir = look - at;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            go.transform.SetPositionAndRotation(at, Quaternion.LookRotation(dir.normalized, up ?? Vector3.up));

            var lens = cam.Lens;
            lens.FieldOfView = fov;
            cam.Lens = lens;
            cam.Priority = 0;
            log.AppendLine("   " + name + " ที่ " + at.ToString("F2") + "  fov " + fov);
            return cam;
        }

        // Finds somewhere the subject can actually be seen from.
        //
        // Offsets alone are not enough around the bedroll: it sits among crates
        // and pillars, and a camera placed by arithmetic ended up looking at the
        // side of a green pillar with the whole shot behind it. This starts from
        // the angle wanted and walks around until the line to the subject is
        // clear, which is the difference between a composed shot and a wall.
        Vector3 ClearShot(Vector3 target, Vector3 wanted, float distance, float height, string label)
        {
            wanted = Vector3.ProjectOnPlane(wanted, Vector3.up).normalized;

            for (int step = 0; step < 24; step++)
            {
                // Out from the preferred angle in both directions, nearest first.
                float deg = ((step + 1) / 2) * 15f * (step % 2 == 0 ? 1f : -1f);
                Vector3 dir = Quaternion.AngleAxis(deg, Vector3.up) * wanted;

                for (float d = distance; d >= distance * 0.55f; d -= distance * 0.15f)
                {
                    Vector3 at = target + dir * d + Vector3.up * height;
                    if (Physics.Linecast(at, target)) continue;

                    if (step > 0 || !Mathf.Approximately(d, distance))
                        log.AppendLine("   (" + label + " ขยับ " + deg.ToString("F0")
                                     + " องศา ระยะ " + d.ToString("F1") + " ม. เพื่อให้พ้นสิ่งบัง)");
                    return at;
                }
            }

            log.AppendLine("   (" + label + " หามุมโล่งไม่เจอ ใช้ค่าตั้งต้น)");
            return target + wanted * distance + Vector3.up * height;
        }

        // Heads read off the posed bones rather than guessed from the roots: both
        // men are sitting, and a standing head height aims every single at the
        // wall above them.
        Vector3 HeadOf(GameObject go, Vector3 fallback)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name == TripoPose.Head) return t.position;
            return fallback;
        }
        Vector3 loganHead = HeadOf(logan, camp.sitLogan + Vector3.up * 0.62f);
        Vector3 asherHead = HeadOf(asher, camp.sitAsher + Vector3.up * 0.62f);

        log.AppendLine();
        log.AppendLine("กล้อง:");

        // --- 3a, at the bedroll ----------------------------------------------
        Vector3 along = camp.toFire, side = camp.across;

        // Lying on his back, the top of his head points away from his feet, so
        // that is which way is up for this shot - not world up, which is almost
        // exactly where the lens is already pointing.
        Vector3 eyeLying = camp.headEnd + Vector3.up * 0.11f;
        var povWake = Make("CM_POV_Wake", eyeLying,
                           eyeLying + Vector3.up * 0.94f + along * 0.36f, 58f, -along);

        Vector3 chest = camp.headEnd + along * 0.38f + Vector3.up * 0.30f;
        var bedSide = Make("CM_BedSide",
                           ClearShot(chest, side + along * 0.45f, 2.4f, 0.85f, "CM_BedSide"),
                           chest, 42f);

        // From beyond his head, looking down the bed past his feet - which is the
        // way he is being called, so the fire is in the shot he is being sent to.
        Vector3 bedMid = camp.bedCentre + Vector3.up * 0.35f;
        var bedWide = Make("CM_BedWide",
                           ClearShot(bedMid, -along + side * 0.65f, 3.2f, 1.5f, "CM_BedWide"),
                           bedMid + along * 0.9f, 50f);

        Vector3 standChest = camp.standAt + Vector3.up * 1.05f;
        var getUp = Make("CM_GetUp",
                         ClearShot(standChest, side + along * 0.2f, 2.7f, 0.35f, "CM_GetUp"),
                         standChest, 42f);

        // --- 3b, at the fire --------------------------------------------------
        Vector3 flame = camp.fire + Vector3.up * ((camp.fireTop - camp.floorY) * 0.5f);

        // Straight down is the one framing that can promise nobody walks into it,
        // whatever the logs are arranged like. It covers the moment Asher is put
        // back on his log at the start of 3b.
        var fireCam = Make("CM_Fire", flame + Vector3.up * 2.0f + camp.faceLogan * 0.30f,
                           flame, 40f, camp.faceLogan);

        // The wide stands on the open side of the fire - the one with no log
        // against it. Both men face the fire, so the way they face, summed, points
        // across it to the empty side; the other side is the backs of their heads.
        Vector3 mid = (loganHead + asherHead) * 0.5f;
        Vector3 openSide = (camp.faceLogan + camp.faceAsher).normalized;
        var wide = Make("CM_CampWide",
                        camp.fire + openSide * 3.9f + Vector3.up * 2.35f,
                        mid - Vector3.up * 0.25f, 47f);

        // The two singles. Each stands in front of the man it is on, and both on
        // the same side of the line between them, so he looks left and Asher looks
        // right and the cuts between them read as a conversation. Both stand back
        // beyond the fire: a lens any closer has flames filling the bottom of
        // frame from thirty centimetres away.
        Vector3 loganRight = Vector3.Cross(Vector3.up, camp.faceLogan).normalized;
        var loganCam = Make("CM_Logan",
                            loganHead + camp.faceLogan * 2.45f + loganRight * 0.60f + Vector3.up * 0.14f,
                            loganHead - Vector3.up * 0.04f, 38f);

        Vector3 asherRight = Vector3.Cross(Vector3.up, camp.faceAsher).normalized;
        var asherCam = Make("CM_Asher",
                            asherHead + camp.faceAsher * 2.65f + asherRight * 0.50f + Vector3.up * 0.12f,
                            asherHead - Vector3.up * 0.04f, 38f);

        // Over-the-shoulder pairs.
        //
        // A conversation cut only between two singles goes flat after a couple of
        // exchanges: nothing in frame says the two men are sitting together, so it
        // reads as two people filmed separately. Putting the listener's shoulder in
        // the corner of frame says it in one shot, and gives the edit a second
        // framing per man to cut to.
        //
        // The camera stands just past the listener's head, which is why the
        // distance is measured between them rather than guessed.
        float apart = Vector3.Distance(loganHead, asherHead);

        var loganOts = Make("CM_LoganOTS",
                            ClearShot(loganHead, (asherHead - loganHead) + asherRight * 0.55f,
                                      apart + 0.55f, 0.30f, "CM_LoganOTS"),
                            loganHead - Vector3.up * 0.03f, 36f);

        var asherOts = Make("CM_AsherOTS",
                            ClearShot(asherHead, (loganHead - asherHead) + loganRight * 0.55f,
                                      apart + 0.55f, 0.28f, "CM_AsherOTS"),
                            asherHead - Vector3.up * 0.03f, 36f);

        // Down at the fire looking up past the flames. The one shot in the scene
        // that is about the place rather than the faces, which is what the gaps
        // between lines want.
        var fireLow = Make("CM_FireLow",
                           ClearShot(mid, openSide, 2.9f, -0.62f, "CM_FireLow"),
                           mid - Vector3.up * 0.12f, 46f);

        return new Cams
        {
            povWake = povWake, bedSide = bedSide, bedWide = bedWide, getUp = getUp,
            fire = fireCam, wide = wide, logan = loganCam, asher = asherCam,
            loganOts = loganOts, asherOts = asherOts, fireLow = fireLow
        };
    }

    // --------------------------------------------------------------- timeline

    static AnimationClip SaveClip(AnimationClip clip, string path, StringBuilder log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null) { EditorUtility.CopySerialized(clip, existing); clip = existing; }
        else AssetDatabase.CreateAsset(clip, path);
        log.AppendLine("คลิป: " + path);
        return clip;
    }

    static TimelineAsset FreshTimeline(string path)
    {
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

    /// The track offset somebody set by hand, read before the timeline is rebuilt.
    ///
    /// Rebuilding deletes every track and makes new ones, so anything tuned in the
    /// Timeline window is gone unless it is carried across. That is what kept
    /// throwing away the bowl's alignment with Asher's hand: it was never the
    /// bowl's transform that was set, it was this.
    static (Vector3 pos, Quaternion rot)? SavedOffset(string timelinePath, string trackName)
    {
        var old = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
        if (old == null) return null;

        foreach (var t in old.GetOutputTracks())
            if (t is AnimationTrack a && a.name == trackName)
                return (a.position, a.rotation);
        return null;
    }

    static AnimationTrack AnimTrack(TimelineAsset timeline, string name, AnimationClip clip, float end,
                                    (Vector3 pos, Quaternion rot)? keepOffset = null)
    {
        var track = timeline.CreateTrack<AnimationTrack>(null, name);
        track.trackOffset = TrackOffset.ApplyTransformOffsets;

        if (keepOffset.HasValue)
        {
            // Left exactly as it was found. A hand-placed offset is a decision, and
            // recomputing it is overruling that decision every time this runs.
            track.position = keepOffset.Value.pos;
            track.rotation = keepOffset.Value.rot;

            var kept = track.CreateClip(clip);
            kept.start = 0d;
            kept.duration = end;
            return track;
        }

        // Timeline reads a clip's root curves as motion - how far the object has
        // travelled from the clip's own first frame - and adds that to the track's
        // offset. A zero offset therefore lands every character on the world
        // origin, however carefully the clip recorded where they should be.
        // Setting the offset to where the clip starts turns the delta back into
        // the absolute path it was recorded as.
        track.position = RootStart(clip, out var startRotation);
        track.rotation = startRotation;

        var c = track.CreateClip(clip);
        c.start = 0d;
        c.duration = end;
        return track;
    }

    /// Where a clip puts its animated object on frame one.
    static Vector3 RootStart(AnimationClip clip, out Quaternion rotation)
    {
        var pos = Vector3.zero;
        var rot = new Vector4(0f, 0f, 0f, 1f);
        bool anyRot = false;

        foreach (var bind in AnimationUtility.GetCurveBindings(clip))
        {
            if (bind.path != "") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, bind);
            if (curve == null || curve.length == 0) continue;
            float v = curve.keys[0].value;

            switch (bind.propertyName)
            {
                case "m_LocalPosition.x": pos.x = v; break;
                case "m_LocalPosition.y": pos.y = v; break;
                case "m_LocalPosition.z": pos.z = v; break;
                case "m_LocalRotation.x": rot.x = v; anyRot = true; break;
                case "m_LocalRotation.y": rot.y = v; anyRot = true; break;
                case "m_LocalRotation.z": rot.z = v; anyRot = true; break;
                case "m_LocalRotation.w": rot.w = v; anyRot = true; break;
            }
        }

        rotation = anyRot ? new Quaternion(rot.x, rot.y, rot.z, rot.w).normalized
                          : Quaternion.identity;
        return pos;
    }

    static void AddVoices(TimelineAsset timeline, PlayableDirector director,
                          List<Line> lines, AudioSource[] voices)
    {
        var vLogan = timeline.CreateTrack<AudioTrack>(null, "VO Logan");
        var vAsher = timeline.CreateTrack<AudioTrack>(null, "VO Asher");

        foreach (var l in lines)
        {
            var track = l.logan ? vLogan : vAsher;
            var c = track.CreateClip(l.clip);
            c.start = l.start;
            c.duration = l.clip.length;
            c.displayName = l.clip.name;
        }

        director.SetGenericBinding(vLogan, voices[0]);
        director.SetGenericBinding(vAsher, voices[1]);
    }

    static PlayableDirector DirectorOn(GameObject root, string name, TimelineAsset timeline)
    {
        var t = root.transform.Find(name);
        GameObject go;
        if (t == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            go.transform.SetParent(root.transform, false);
        }
        else go = t.gameObject;

        var director = go.GetComponent<PlayableDirector>();
        if (director == null) director = Undo.AddComponent<PlayableDirector>(go);
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.None;

        var stage = go.GetComponent<CutsceneStage3>();
        if (stage == null) stage = Undo.AddComponent<CutsceneStage3>(go);
        stage.playOnStart = false;
        return director;
    }

    static GameObject BuildWakeTimeline(GameObject root, Camp camp, Cams cams,
                                        GameObject logan, GameObject asher, AudioSource[] voices,
                                        AnimationClip loganClip, AnimationClip asherClip,
                                        List<Line> lines, float end, StringBuilder log)
    {
        string path = AssetDir + "/Chapter1_Stage3a.playable";

        // Read before the rebuild wipes it. Cue times nudged by hand until they sat
        // on the movement outrank anything this script would work out for itself.
        var savedCloth = SavedCloth(path);

        var timeline = FreshTimeline(path);

        var loganTrack = AnimTrack(timeline, "Logan", loganClip, end);
        var asherTrack = AnimTrack(timeline, "Asher", asherClip, end);

        Line L(int i) => lines[Mathf.Clamp(i, 0, lines.Count - 1)];

        // The cut away from Asher's own eyes. He is switched on at exactly this
        // instant and not a frame later: while the camera is his point of view he
        // must not be visible, and the moment it stops being his point of view he
        // has to be already there. Anything in between and he is seen appearing
        // out of nothing.
        float reveal = L(1).start - 0.6f;

        var visTrack = timeline.CreateTrack<ActivationTrack>(null, "Asher visible");
        visTrack.postPlaybackState = ActivationTrack.PostPlaybackState.Active;
        var visClip = visTrack.CreateDefaultClip();
        visClip.start = reveal;
        visClip.duration = end - reveal;

        var camTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
        var shots = new List<CinemachineCamera>();

        // A blend in Timeline is an overlap, not a setting: two clips that merely
        // touch end to end cut, however large blendInDuration is written.
        void Shot(CinemachineCamera cam, double start, double stop, double blendIn)
        {
            double from = start - blendIn;
            var c = camTrack.CreateClip<CinemachineShot>();
            c.start = from;
            c.duration = System.Math.Max(0.1d, stop - from);
            c.blendInDuration = blendIn;
            ((CinemachineShot)c.asset).VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
            c.displayName = cam.name;
            shots.Add(cam);
        }

        float getUp = L(2).end + 0.2f;

        Shot(cams.povWake, 0d, reveal, 0d);
        Shot(cams.bedSide, reveal, L(2).start - 0.4f, 0d);
        Shot(cams.bedWide, L(2).start - 0.4f, getUp, 0d);
        Shot(cams.getUp, getUp, end, 0d);

        var director = DirectorOn(root, "Timeline 3a", timeline);
        director.SetGenericBinding(loganTrack, logan.GetComponent<Animator>());
        director.SetGenericBinding(asherTrack, asher.GetComponent<Animator>());
        director.SetGenericBinding(visTrack, asher);
        AddVoices(timeline, director, lines, voices);

        // His own breathing, over the shot from inside his eyes. It starts with him
        // and thins out as Logan talks him back down, which is the whole shape of
        // the scene - a man startled awake, then not.
        var breathClip = Sfx("เสียงหายใจ", log);
        if (breathClip != null)
        {
            var breathTrack = timeline.CreateTrack<AudioTrack>(null, "SFX Breath");
            var b = breathTrack.CreateClip(breathClip);
            b.start = 0d;
            b.duration = Mathf.Min(breathClip.length, end);
            b.easeOutDuration = 4.0d;

            var breath = SoundSource(root, "SFX Breath");
            breath.playOnAwake = false;
            breath.loop = false;
            breath.spatialBlend = 0f;   // it is his own breath, heard from inside
            breath.volume = 0.42f;
            director.SetGenericBinding(breathTrack, breath);
            log.AppendLine("เสียงหายใจ Asher: 0 - " + b.duration.ToString("F1") + " วิ (จางหายช่วง 4 วิสุดท้าย)");
        }

        // Two moments of bedding. He shifts his weight on the bedroll while Logan
        // is still talking at him, then pushes himself off it and onto his feet.
        ClothCues(timeline, director, root, new[]
        {
            ((double)(L(2).start + 0.9f), 25.10d, 1.60d, "ขยับตัวบนที่นอน"),
            ((double)(L(2).end + 1.4f),    7.35d, 1.30d, "ลุกจากที่นอน"),
        }, savedCloth, log);

        AddSubtitles(director.gameObject, lines, 0, end, log);

        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) director.SetGenericBinding(camTrack, brain);

        int i = 0;
        foreach (var c in camTrack.GetClips())
            director.SetReferenceValue(((CinemachineShot)c.asset).VirtualCamera.exposedName, shots[i++]);

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);

        log.AppendLine();
        log.AppendLine("Stage 3a: " + path + "   ยาว " + end.ToString("F1") + " วิ   "
                     + shots.Count + " ช็อต   Asher โผล่ที่ " + reveal.ToString("F1") + " วิ");
        return director.gameObject;
    }

    static GameObject BuildTalkTimeline(GameObject root, Camp camp, Cams cams,
                                        GameObject logan, GameObject asher, AudioSource[] voices,
                                        AnimationClip loganClip, AnimationClip asherClip,
                                        Bowls bowls, AnimationClip bowlClip,
                                        List<Line> lines, float end, StringBuilder log)
    {
        string path = AssetDir + "/Chapter1_Stage3b.playable";

        // Read before the rebuild wipes them. The bowl's offset is set by hand in
        // the Timeline window to line the bowl up with Asher's hand, and the cloth
        // cues are nudged until they sit on the movement. Neither is this script's
        // decision to make.
        var bowlOffset = SavedOffset(path, "Bowl");
        var savedCloth = SavedCloth(path);

        var timeline = FreshTimeline(path);

        var loganTrack = AnimTrack(timeline, "Logan", loganClip, end);
        var asherTrack = AnimTrack(timeline, "Asher", asherClip, end);

        AnimationTrack bowlTrack = null;
        if (bowls != null && bowlClip != null)
        {
            bowlTrack = AnimTrack(timeline, "Bowl", bowlClip, end, bowlOffset);
            log.AppendLine(bowlOffset.HasValue
                ? "ถ้วย: คง Track Offset ที่ตั้งไว้เอง " + bowlOffset.Value.pos.ToString("F4")
                : "ถ้วย: ยังไม่เคยตั้ง Track Offset - คำนวณให้ครั้งแรก");
        }

        var camTrack = timeline.CreateTrack<CinemachineTrack>(null, "Camera");
        var shots = new List<CinemachineCamera>();

        void Shot(CinemachineCamera cam, double start, double stop, double blendIn)
        {
            double from = start - blendIn;
            var c = camTrack.CreateClip<CinemachineShot>();
            c.start = from;
            c.duration = System.Math.Max(0.1d, stop - from);
            c.blendInDuration = blendIn;
            ((CinemachineShot)c.asset).VirtualCamera.exposedName = System.Guid.NewGuid().ToString();
            c.displayName = cam.name;
            shots.Add(cam);
        }

        Line L(int i) => lines[Mathf.Clamp(i, 0, lines.Count - 1)];

        // The edit follows who is talking.
        //
        // Two rules do most of the work. Whoever is speaking is on screen, and the
        // same framing never lands twice running - so each man has a single and an
        // over-the-shoulder, and the two alternate. The rest is breaking up the
        // long speeches: Logan's first question runs twenty-six seconds, and no
        // framing survives that. It is cut into pieces of about seven seconds,
        // going to the listener in between, which is also where the scene actually
        // lives - it is about how the other man takes it.
        bool ots = false;
        CinemachineCamera Speaking(bool byLogan)
        {
            ots = !ots;
            return byLogan ? (ots ? cams.loganOts : cams.logan)
                           : (ots ? cams.asherOts : cams.asher);
        }
        CinemachineCamera Listening(bool loganSpeaking)
            => loganSpeaking ? cams.asher : cams.logan;

        // Opening on the fire is not decoration: the player walks up on his own
        // feet and stops wherever he likes, and this is the shot that covers Asher
        // being put back on his log for the scene. Then wide for the whole business
        // of the bowl coming up off the floor and across.
        Shot(cams.fire, 0d, 1.4d, 0d);
        Shot(cams.wide, 1.4d, L(0).start - 0.5d, 0.4d);

        double cut = L(0).start - 0.5d;

        for (int spoken = 0; spoken < lines.Count; spoken++)
        {
            var line = L(spoken);
            double span = line.end - cut;
            if (span < 0.4d) continue;

            int pieces = Mathf.Clamp(Mathf.RoundToInt((float)span / 7f), 1, 4);
            double pieceStart = cut;

            for (int p = 0; p < pieces; p++)
            {
                double pieceEnd = p == pieces - 1 ? line.end : pieceStart + span / pieces;
                Shot(p % 2 == 0 ? Speaking(line.logan) : Listening(line.logan),
                     pieceStart, pieceEnd, p == 0 ? 0.45d : 0d);
                pieceStart = pieceEnd;
            }
            cut = line.end;

            // A pause between lines gets somewhere to breathe rather than sitting
            // on the face of a man who has stopped talking. The last pause is left
            // alone - the closing wide wants it.
            bool more = spoken + 1 < lines.Count;
            double next = more ? L(spoken + 1).start : end;
            if (more && next - line.end > 1.6d)
            {
                Shot(spoken % 2 == 0 ? cams.fireLow : cams.wide, line.end, next - 0.3d, 0.5d);
                cut = next - 0.3d;
            }
        }

        // He puts the bowl down and stands up, which wants room to happen in.
        if (end - cut > 0.3d) Shot(cams.wide, cut, end, 0.6d);

        var director = DirectorOn(root, "Timeline 3b", timeline);
        director.SetGenericBinding(loganTrack, logan.GetComponent<Animator>());
        director.SetGenericBinding(asherTrack, asher.GetComponent<Animator>());
        if (bowlTrack != null)
            director.SetGenericBinding(bowlTrack, bowls.offered.GetComponent<Animator>());
        AddVoices(timeline, director, lines, voices);

        // He sets the bowl down and comes up off the log here - the same rustle the
        // waking scene uses, because it is the same coat and the same movement.
        ClothCues(timeline, director, root, new[]
        {
            (L(3).start + 0.65d, 7.35d, 1.30d, "ลุกจากขอนไม้"),
        }, savedCloth, log);

        // The conversation picks up at the fourth recording, so the script has to
        // be indexed from there rather than from zero.
        AddSubtitles(director.gameObject, lines, LinesBeforeWalk, end, log);

        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        if (brain != null) director.SetGenericBinding(camTrack, brain);

        int i = 0;
        foreach (var c in camTrack.GetClips())
            director.SetReferenceValue(((CinemachineShot)c.asset).VirtualCamera.exposedName, shots[i++]);

        EditorUtility.SetDirty(timeline);
        EditorUtility.SetDirty(director);

        log.AppendLine("Stage 3b: " + path + "   ยาว " + end.ToString("F1") + " วิ   " + shots.Count + " ช็อต");
        return director.gameObject;
    }

    // --------------------------------------------------------------- gameplay

    // Everything the player needs to walk Asher around, left switched off. The
    // runner turns it on when Stage 3a hands over, and off again for 3b - the
    // CharacterController writes to the same transform Timeline is animating, and
    // whichever runs second wins.
    static Behaviour[] BuildPlayerRig(GameObject asher, Camp camp, StringBuilder log)
    {
        var controller = asher.GetComponent<CharacterController>();
        if (controller == null) controller = Undo.AddComponent<CharacterController>(asher);
        controller.height = 1.72f;
        controller.radius = 0.28f;
        controller.center = new Vector3(0f, 0.88f, 0f);
        controller.enabled = false;

        var move = asher.GetComponent<PlayerMovement>();
        if (move == null) move = Undo.AddComponent<PlayerMovement>(asher);
        move.enabled = false;

        var interact = asher.GetComponent<PlayerInteractor>();
        if (interact == null) interact = Undo.AddComponent<PlayerInteractor>(asher);
        interact.enabled = false;

        if (asher.tag != "Player")
        {
            Undo.RecordObject(asher, "Tag Asher as the player");
            asher.tag = "Player";
        }

        var main = Camera.main;
        CameraFollow follow = null;
        if (main != null)
        {
            follow = main.GetComponent<CameraFollow>();
            if (follow == null) follow = Undo.AddComponent<CameraFollow>(main.gameObject);
            follow.target = asher.transform;
            follow.enabled = false;
        }

        log.AppendLine("ระบบเดิน: CharacterController + PlayerMovement + PlayerInteractor บน Asher (ปิดไว้)"
                     + (follow != null ? " / CameraFollow บน Main Camera (ปิดไว้)" : ""));

        // The CharacterController is left out: it is a Collider rather than a
        // Behaviour, so it cannot travel in this array. The runner holds it in a
        // field of its own and switches it with the rest.
        return new Behaviour[] { move, interact };
    }

    static CampfireInteractable BuildCampfireTrigger(GameObject root, Camp camp, StringBuilder log)
    {
        var t = root.transform.Find("Campfire Interact");
        GameObject go;
        if (t == null)
        {
            go = new GameObject("Campfire Interact");
            Undo.RegisterCreatedObjectUndo(go, "Campfire interact");
            go.transform.SetParent(root.transform, false);
        }
        else go = t.gameObject;

        go.transform.position = camp.fire + Vector3.up * 0.4f;

        var trigger = go.GetComponent<CampfireInteractable>();
        if (trigger == null) trigger = Undo.AddComponent<CampfireInteractable>(go);
        trigger.interactRange = 3.2f;

        // E and nothing else.
        //
        // There was a second, tighter ring that started the scene on its own, as a
        // net for a player who walked up and never pressed anything. It costs more
        // than it saves: walking a step too close takes the scene away from you
        // before you have chosen to have it, and the whole point of the tutorial is
        // that the player is the one who acts. The prompt appearing is the offer;
        // pressing the key is the answer.
        trigger.autoRange = 0f;
        trigger.armed = false;

        log.AppendLine("จุดกด E อยู่ที่กองไฟ " + go.transform.position.ToString("F2")
                     + "  (ขึ้นข้อความที่ " + trigger.interactRange + " ม. - เข้าฉากด้วยการกด E เท่านั้น)");
        return trigger;
    }

    static void BuildRunner(GameObject root, Camp camp, GameObject wakeGo, GameObject talkGo,
                            GameObject asher, Behaviour[] control,
                            CampfireInteractable campfire, StringBuilder log)
    {
        var runner = root.GetComponent<CutsceneChapter1>();
        if (runner == null) runner = Undo.AddComponent<CutsceneChapter1>(root);

        // Where the waking scene leaves him standing, written down as an object in
        // the scene rather than left implied by his transform - Timeline puts that
        // back to whatever the scene was saved with the moment its director stops.
        var markerT = root.transform.Find("Walk Start");
        GameObject marker;
        if (markerT == null)
        {
            marker = new GameObject("Walk Start");
            Undo.RegisterCreatedObjectUndo(marker, "Add Walk Start");
            marker.transform.SetParent(root.transform, false);
        }
        else marker = markerT.gameObject;

        Vector3 standing = camp.standAt;
        standing.y = camp.floorY;
        marker.transform.SetPositionAndRotation(
            standing, Quaternion.LookRotation(camp.toFire, Vector3.up));
        runner.walkStart = marker.transform;
        log.AppendLine("จุดยืนหลังลุก (Walk Start) ที่ " + standing.ToString("F2")
                     + "   ห่างกองไฟ "
                     + Vector3.ProjectOnPlane(standing - campfire.transform.position, Vector3.up)
                              .magnitude.ToString("F1") + " ม.");

        // Stage 1 lives in its own object from the earlier build.
        CutsceneStage1 stage1 = Object.FindFirstObjectByType<CutsceneStage1>();
        if (stage1 != null)
        {
            Undo.RecordObject(stage1, "Hand Stage 1 to the runner");
            stage1.playOnStart = false;
        }

        runner.stage1 = stage1;
        runner.stageWake = wakeGo.GetComponent<CutsceneStage3>();
        runner.stageTalk = talkGo.GetComponent<CutsceneStage3>();
        runner.player = asher;
        runner.playerControl = control;
        runner.playerBody = asher.GetComponent<CharacterController>();
        runner.campfire = campfire;

        var main = Camera.main;
        if (main != null)
        {
            // The camera the player actually looks through, which is the first
            // person one. CameraFollow is the older third person rig; it is still
            // on the camera because DialogueManager and SaveSystem look it up, but
            // it is not what drives the view any more.
            //
            // Wiring the wrong one is not a harmless mistake: the runner switches
            // this off for the length of a cutscene, so pointing it at the dormant
            // script left the live one running through every scene - collapsing the
            // head bone to hide it from its own lens, and overwriting which way
            // Asher faced on every frame after Timeline had set it.
            Behaviour view = main.GetComponent<FirstPersonCamera>();
            if (view == null) view = main.GetComponent<CameraFollow>();
            runner.followCamera = view;

            var mainBrain = main.GetComponent<CinemachineBrain>();
            runner.cinemachineBrain = mainBrain;

            // The camera cuts between scenes; it does not fly between them.
            //
            // The brain's default blend was an ease over two seconds, and it
            // applies whenever the live camera changes outside a Timeline - which
            // is exactly what happens when one stage hands to the next. The bedroom
            // and the camp are eighty-eight metres apart with the campfire on the
            // line between them, so that blend was a two second flight across the
            // level, straight past Asher and Logan sitting at the fire, arriving
            // just as the new scene faded up. That is the glimpse.
            //
            // Every blend that is meant to be seen is authored as overlapping clips
            // on the Cinemachine track, and Timeline mixes those itself. Nothing
            // needs the brain to blend on its own.
            var cut = mainBrain.DefaultBlend;
            cut.Style = CinemachineBlendDefinition.Styles.Cut;
            cut.Time = 0f;
            mainBrain.DefaultBlend = cut;

            log.AppendLine("กล้อง: ตั้ง DefaultBlend ของ CinemachineBrain เป็น Cut "
                         + "(เดิมเบลนด์ 2 วิ ทำให้กล้องบินข้าม 88 ม. ผ่านกองไฟตอนเปลี่ยนฉาก)");
        }

        EditorUtility.SetDirty(runner);

        log.AppendLine();
        log.AppendLine("ตัวคุมลำดับอยู่บน " + RootName + " (CutsceneChapter1)");
        log.AppendLine("   Stage 1 " + (stage1 != null ? "ต่อแล้ว (ปิด playOnStart ให้)" : "!! หาไม่เจอ !!")
                     + " -> 3a -> เดิน+กด E -> 3b -> คืนให้ผู้เล่น");
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
