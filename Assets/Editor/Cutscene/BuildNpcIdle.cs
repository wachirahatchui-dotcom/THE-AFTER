using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using System.Text;
using System.Collections.Generic;

// Generates the looping idle each garage NPC plays when nothing else is driving
// them, and wires it up so it just runs.
//
// Authored as poses rather than as curves. These rigs came out of a converter
// with every bone pointing wherever it liked, so a hand-typed rotation on one
// of them means something different on the next; "hands on the bench, bent over
// it, looking down" is a description that survives that, and the pose tools turn
// it into whatever local rotations this particular skeleton needs. The clip then
// holds exactly what was posed, so nothing has to be solved again at runtime.
//
// Each moment in a routine is a row. Adding a character is a routine; changing
// how somebody moves is editing numbers in one, not rewriting anything.
//
// Menu: THE AFTER > NPC > Build Idle Animations
public static class BuildNpcIdle
{
    const string ClipDir = "Assets/Animations/NPC/";

    // One posed instant. Everything is in the character's own terms - metres in
    // front of him, metres to his left - so a routine reads the same whichever
    // way round the room he happens to be standing.
    struct Moment
    {
        public float t;

        // How far over he is folded, 0 upright and 1 doubled over. The pose tools
        // spread it down the spine so it bends rather than hinging at the waist.
        public float lean;

        // Hips, relative to standing: down when he stoops, sideways when his
        // weight goes onto one leg.
        public float hipDrop, hipSide;

        // Hands, in his own frame: forward, left, up from the floor.
        public Vector3 leftHand, rightHand;

        // What he is looking at, same frame. Mostly the bench; sometimes his own
        // hand when he has picked something up.
        public Vector3 lookAt;

        // Feet only move when the weight does, and barely.
        public float leftFootSide, rightFootSide;
    }

    // Ethan at his workbench: stooping over it, hunting along it, straightening
    // up to look at what he found, going back down. Measured against the bench in
    // the scene - its surface is about 1.2 up and a metre in front of him.
    //
    // Seven seconds is long enough that the eye does not catch the repeat while
    // the player crosses the garage, and short enough to author honestly.
    static Moment[] EthanRoutine()
    {
        return new Moment[]
        {
            // Standing over the bench, both hands resting on it.
            new Moment { t = 0.0f, lean = 0.22f, hipDrop = 0.02f,
                leftHand  = new Vector3(0.62f,  0.20f, 1.16f),
                rightHand = new Vector3(0.62f, -0.20f, 1.16f),
                lookAt    = new Vector3(0.85f,  0.00f, 1.05f) },

            // Down into it, hands apart, head over the work.
            new Moment { t = 0.9f, lean = 0.78f, hipDrop = 0.15f,
                leftHand  = new Vector3(0.70f,  0.30f, 1.06f),
                rightHand = new Vector3(0.72f, -0.26f, 1.04f),
                lookAt    = new Vector3(0.80f,  0.00f, 0.95f) },

            // Reaching along it for something, right arm out.
            new Moment { t = 1.9f, lean = 0.88f, hipDrop = 0.19f, hipSide = -0.04f,
                leftHand  = new Vector3(0.66f,  0.26f, 1.08f),
                rightHand = new Vector3(0.80f, -0.48f, 1.02f),
                lookAt    = new Vector3(0.80f, -0.40f, 0.98f),
                rightFootSide = -0.04f },

            // Up, holding whatever it was, looking at his own hand.
            new Moment { t = 2.8f, lean = 0.30f, hipDrop = 0.04f,
                leftHand  = new Vector3(0.58f,  0.22f, 1.14f),
                rightHand = new Vector3(0.42f, -0.14f, 1.28f),
                lookAt    = new Vector3(0.42f, -0.14f, 1.30f) },

            // Weight onto the other leg while he thinks about it.
            new Moment { t = 3.7f, lean = 0.25f, hipDrop = 0.03f, hipSide = 0.05f,
                leftHand  = new Vector3(0.60f,  0.24f, 1.15f),
                rightHand = new Vector3(0.50f, -0.16f, 1.22f),
                lookAt    = new Vector3(0.70f,  0.10f, 1.10f),
                leftFootSide = 0.05f },

            // Back down, this time reaching left.
            new Moment { t = 4.6f, lean = 0.74f, hipDrop = 0.14f, hipSide = 0.03f,
                leftHand  = new Vector3(0.78f,  0.46f, 1.02f),
                rightHand = new Vector3(0.66f, -0.22f, 1.08f),
                lookAt    = new Vector3(0.78f,  0.38f, 0.98f),
                leftFootSide = 0.05f },

            // Deepest of the stoop, both hands working low.
            new Moment { t = 5.5f, lean = 0.92f, hipDrop = 0.20f,
                leftHand  = new Vector3(0.74f,  0.24f, 1.00f),
                rightHand = new Vector3(0.74f, -0.24f, 1.00f),
                lookAt    = new Vector3(0.78f,  0.00f, 0.92f) },

            // Coming back up towards where he started.
            new Moment { t = 6.4f, lean = 0.38f, hipDrop = 0.06f,
                leftHand  = new Vector3(0.64f,  0.22f, 1.14f),
                rightHand = new Vector3(0.64f, -0.22f, 1.13f),
                lookAt    = new Vector3(0.82f,  0.00f, 1.04f) },

            // Closes the loop: identical to t = 0, so the seam is invisible.
            new Moment { t = 7.3f, lean = 0.22f, hipDrop = 0.02f,
                leftHand  = new Vector3(0.62f,  0.20f, 1.16f),
                rightHand = new Vector3(0.62f, -0.20f, 1.16f),
                lookAt    = new Vector3(0.85f,  0.00f, 1.05f) },
        };
    }

    [MenuItem("THE AFTER/NPC/Build Idle Animations")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new StringBuilder();
        log.AppendFormat("=== ท่ายืนของ NPC ใน {0} ===\n", scene.name);

        System.IO.Directory.CreateDirectory(ClipDir);

        Make(scene, "Ethan", "Ethan_Idle_Searching", EthanRoutine(), log);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    static void Make(UnityEngine.SceneManagement.Scene scene, string who, string clipName,
                     Moment[] routine, StringBuilder log)
    {
        Transform t = null;
        foreach (var r in scene.GetRootGameObjects())
            foreach (var x in r.GetComponentsInChildren<Transform>(true))
                if (x.name == who && x.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) t = x;

        if (t == null) { log.AppendFormat("  !! ไม่เจอ {0}\n", who); return; }

        var go = t.gameObject;
        bool wasOn = go.activeSelf;
        if (!wasOn) go.SetActive(true);

        // Everything the posing is about to change, so it can all go back. The
        // character's own placement in the room is the user's, not ours.
        Vector3 homePos = t.position;
        Quaternion homeRot = t.rotation;

        var bones = PoseTools.BonesOf(go);
        var rest = PoseTools.ReadRestPose(go);

        var hip = TripoPose.Bone(bones, TripoPose.Hips);
        if (hip == null) { log.AppendFormat("  !! {0} ไม่มีกระดูกสะโพก\n", who); return; }

        // His own frame, taken once: forward is where he faces, left is across it.
        Vector3 face = t.forward; face.y = 0f; face.Normalize();
        Vector3 left = Vector3.Cross(Vector3.up, face).normalized;
        Vector3 floor = homePos;

        float standHip = hip.position.y - floor.y;

        var lFoot = TripoPose.Bone(bones, "L_Foot");
        var rFoot = TripoPose.Bone(bones, "R_Foot");
        float lSide = lFoot != null ? Vector3.Dot(lFoot.position - floor, left) : 0.15f;
        float rSide = rFoot != null ? Vector3.Dot(rFoot.position - floor, left) : -0.15f;
        float footFwd = lFoot != null ? Vector3.Dot(lFoot.position - floor, face) : 0f;

        // The ankle bone is not on the floor - it sits a boot's height above it.
        // Aiming the leg solve at floor level instead drives the whole leg down
        // by that much and buries the sole, which is small enough to look like a
        // rendering fault rather than like the wrong number.
        float ankleUp = lFoot != null ? lFoot.position.y - floor.y : 0.03f;

        log.AppendFormat("  {0}: สะโพกสูง {1:F3}  เท้าซ้าย/ขวา ห่างแกนกลาง {2:+0.00}/{3:+0.00}\n",
            who, standHip, lSide, rSide);

        var recorder = new PoseRecorder(t, hip, TripoPose.TrackedBones(bones));

        foreach (var m in routine)
        {
            Vector3 hipPoint = floor
                             + Vector3.up * (standHip - m.hipDrop)
                             + left * m.hipSide;

            Vector3 lf = floor + left * (lSide + m.leftFootSide) + face * footFwd + Vector3.up * ankleUp;
            Vector3 rf = floor + left * (rSide + m.rightFootSide) + face * footFwd + Vector3.up * ankleUp;

            TripoPose.Body(go, rest, bones, hipPoint, face, Vector3.up, lf, rf, m.lean);

            // Body places the hips by moving the whole object, and this clip must
            // not move him - he is standing where the level put him. So the root
            // goes back and the same hip placement is carried on the hip bone,
            // which the recorder does write.
            //
            // Leaving it as it was cost twenty centimetres of floating boot: the
            // legs had been solved for a root that had dropped, the clip then
            // threw the drop away, and at playback the feet hung in the air by
            // exactly the distance he was supposed to have crouched.
            t.SetPositionAndRotation(homePos, homeRot);
            hip.position = hipPoint;

            // Solved again now the root is where it will be when this plays, so
            // the feet land on the floor rather than near it.
            Plant(bones, "L", lf, face);
            Plant(bones, "R", rf, face);

            TripoPose.Arm(bones, "L", Point(floor, face, left, m.leftHand),
                          TripoPose.ElbowPole(left, face, Vector3.up, true));
            TripoPose.Arm(bones, "R", Point(floor, face, left, m.rightHand),
                          TripoPose.ElbowPole(left, face, Vector3.up, false));

            TripoPose.LookAt(go, bones, Point(floor, face, left, m.lookAt), 1f);

            recorder.Capture(m.t);
        }

        var clip = recorder.Build(clipName, true);

        string path = ClipDir + clipName + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            clip = existing;
        }
        else AssetDatabase.CreateAsset(clip, path);
        EditorUtility.SetDirty(clip);

        // Back exactly where he was. The pose tools move the whole object to put
        // the hips where they were asked for, and leaving him there would mean
        // this tool quietly relocates people every time it runs.
        PoseTools.ApplyRestPose(go, rest);
        t.SetPositionAndRotation(homePos, homeRot);
        TripoPose.KeepSkinFresh(go);
        if (!wasOn) go.SetActive(false);

        var controller = Controller(clipName, clip);

        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(go);
        animator.runtimeAnimatorController = controller;

        // These rigs are Generic. Humanoid would retarget the clip through an
        // avatar and throw away the transform curves it is made of.
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        EditorUtility.SetDirty(animator);

        log.AppendFormat("  {0}: {1} คีย์  ยาว {2:F2} วิ  ลูป  -> {3}\n",
            who, routine.Length, clip.length, path);
    }

    /// One leg, solved so the ankle sits on a spot on the floor.
    ///
    /// The knee is told to break towards his face, which is what a knee does; a
    /// pole pointing anywhere else folds the shin the wrong way through the leg.
    static void Plant(Dictionary<string, Transform> bones, string side, Vector3 ankle, Vector3 face)
    {
        var thigh = TripoPose.Bone(bones, side + "_Thigh");
        var calf = TripoPose.Bone(bones, side + "_Calf");
        var foot = TripoPose.Bone(bones, side + "_Foot");
        if (thigh == null || calf == null || foot == null) return;

        PoseTools.TwoBone(thigh, calf, foot, ankle, face + Vector3.up * 0.2f);

        var toe = TripoPose.Bone(bones, side + "_ToeBase");
        if (toe != null)
            PoseTools.Aim(foot, toe, foot.position + face * 0.11f - Vector3.up * 0.025f);
    }

    /// A point described in his own terms, put back into the room's.
    static Vector3 Point(Vector3 floor, Vector3 face, Vector3 left, Vector3 v)
    {
        return floor + face * v.x + left * v.y + Vector3.up * v.z;
    }

    static AnimatorController Controller(string name, AnimationClip clip)
    {
        string path = ClipDir + name + ".controller";
        var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (existing != null)
        {
            var layer = existing.layers[0];
            var state = layer.stateMachine.defaultState;
            if (state != null) state.motion = clip;
            EditorUtility.SetDirty(existing);
            return existing;
        }

        return AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
    }
}
