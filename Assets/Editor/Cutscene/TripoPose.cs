using System.Collections.Generic;
using UnityEngine;

// Posing for Tripo's auto-rig - the 42-bone skeleton Asher, Logan and Asher Kid
// all share (Hip / Waist / Spine01 / L_Upperarm / NeckTwist01 / Head), which is
// its own naming rather than the Mixamo names Matha carries.
//
// Everything is stated as places in the world - hips on that log, feet on that
// floor, a hand out there - and solved with PoseTools. No euler angles are typed
// anywhere: bone axes differ per rig, and an angle that reads right in the
// inspector still has to be checked against where the furniture actually is.
public static class TripoPose
{
    public const string Hips = "Hip";
    public const string Waist = "Waist";
    public const string Spine1 = "Spine01";
    public const string Spine2 = "Spine02";
    public const string Neck = "NeckTwist01";
    public const string Neck2 = "NeckTwist02";
    public const string Head = "Head";

    /// The bones worth writing curves for: spine, both arms, both legs. The twist
    /// bones are left out - they follow their parents and only bloat the clip.
    public static readonly string[] Tracked =
    {
        Hips, "Pelvis", Waist, Spine1, Spine2, Neck, Neck2, Head,
        "L_Clavicle", "L_Upperarm", "L_Forearm", "L_Hand",
        "R_Clavicle", "R_Upperarm", "R_Forearm", "R_Hand",
        "L_Thigh", "L_Calf", "L_Foot", "L_ToeBase",
        "R_Thigh", "R_Calf", "R_Foot", "R_ToeBase",
    };

    /// The ankle bone sits about this far above the sole, so a foot planted on a
    /// floor wants its ankle target raised by it.
    const float AnkleAboveSole = 0.075f;

    public static Transform Bone(Dictionary<string, Transform> b, string n)
        => b.TryGetValue(n, out var t) ? t : null;

    /// Which side the bones named L_ are actually on. Not worth deriving from the
    /// model's facing - rigs get mirrored, and then every reach goes the wrong way.
    public static Vector3 HisLeft(Dictionary<string, Transform> b)
    {
        var l = Bone(b, "L_Thigh");
        var r = Bone(b, "R_Thigh");
        if (l == null || r == null) return Vector3.left;
        return Vector3.ProjectOnPlane(l.position - r.position, Vector3.up).normalized;
    }

    /// How high the hip bone rides above the soles in the model's own rest pose.
    /// Standing up means putting the hips back at this height.
    public static float RestHipHeight(GameObject go, Dictionary<string, Quaternion> rest,
                                      Dictionary<string, Transform> b)
    {
        PoseTools.ApplyRestPose(go, rest);
        var hip = Bone(b, Hips);
        if (hip == null) return 0.9f;

        // Measured standing up, whatever the caller happened to leave him doing.
        //
        // How high a hip sits above the soles only means anything with the body the
        // right way up: it is the distance from the hip down to the lowest point of
        // him, and on a man lying on his back the lowest point is his back. Asked in
        // that state the answer comes out at well under half the real figure - and
        // the scene that builds the waking clip leaves him lying on the bedroll,
        // which is precisely the state the next clip finds him in. That is what had
        // him rising from the log at the end of the campfire scene into a crouch
        // lower than the seat he started on.
        Quaternion was = go.transform.rotation;
        go.transform.rotation = Quaternion.identity;

        try
        {
            float lowest = float.MaxValue;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var m = new UnityEngine.Mesh();
                smr.BakeMesh(m, true);
                foreach (var v in m.vertices)
                {
                    float y = smr.transform.TransformPoint(v).y;
                    if (y < lowest) lowest = y;
                }
                Object.DestroyImmediate(m);
            }
            return hip.position.y - lowest;
        }
        finally
        {
            go.transform.rotation = was;
        }
    }

    // ------------------------------------------------------------------- body

    /// The general pose. A body is stated by where its hips are, which way its
    /// face points, which way its trunk runs from hip to head, and where its two
    /// feet are - and that covers lying flat, propped on the elbows, sitting up,
    /// crouching and standing without a separate function for each.
    ///
    /// Lying on his back is `faceDir` at the ceiling and `torsoUp` along the mat;
    /// standing is `faceDir` along the floor and `torsoUp` at the ceiling. Getting
    /// up is those two slerped together, which is why the whole thing can be one
    /// function: the poses in between are the animation.
    public static void Body(GameObject go, Dictionary<string, Quaternion> rest,
                            Dictionary<string, Transform> b,
                            Vector3 hipPoint, Vector3 faceDir, Vector3 torsoUp,
                            Vector3 leftFoot, Vector3 rightFoot, float lean)
    {
        PoseTools.ApplyRestPose(go, rest);

        torsoUp = torsoUp.normalized;
        faceDir = Vector3.ProjectOnPlane(faceDir, torsoUp).normalized;
        if (faceDir.sqrMagnitude < 1e-6f) faceDir = Vector3.Cross(torsoUp, Vector3.right).normalized;

        // Unity's own convention already says this: LookRotation(where the face
        // points, which way is up for the body).
        go.transform.rotation = Quaternion.LookRotation(faceDir, torsoUp);

        // With the root at the origin and already turned, the hip bone's position
        // is its offset from the root - which is what says where the root must go.
        go.transform.position = Vector3.zero;
        var hip = Bone(b, Hips);
        if (hip == null) return;
        go.transform.position = hipPoint - hip.position;

        Vector3 hisLeft = HisLeft(b);

        foreach (var side in new[] { "L", "R" })
        {
            var thigh = Bone(b, side + "_Thigh");
            var calf = Bone(b, side + "_Calf");
            var foot = Bone(b, side + "_Foot");
            if (thigh == null || calf == null || foot == null) continue;

            Vector3 ankle = side == "L" ? leftFoot : rightFoot;

            // The knee breaks towards the face. Standing that is forwards; lying on
            // his back it is upwards - which is the same statement, and why this
            // one pole works for both. Without it the shin folds through the seat.
            PoseTools.TwoBone(thigh, calf, foot, ankle, faceDir * 1.0f + torsoUp * 0.2f);

            var toe = Bone(b, side + "_ToeBase");
            if (toe != null)
                PoseTools.Aim(foot, toe, foot.position + faceDir * 0.11f - torsoUp * 0.025f);
        }

        Lean(b, faceDir, torsoUp, lean);
    }

    /// Sitting, rising and standing: the upright cases, where the trunk runs
    /// straight up and both feet stand on one floor.
    public static void Upright(GameObject go, Dictionary<string, Quaternion> rest,
                               Dictionary<string, Transform> b,
                               Vector3 hipPoint, Vector3 feetAt, float floorY,
                               Vector3 forward, float lean, float stance = 0.16f)
    {
        forward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;

        // Which side is which has to come from the bones, and the bones have to be
        // in their rest pose to be asked - so settle them before measuring.
        PoseTools.ApplyRestPose(go, rest);
        go.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        Vector3 hisLeft = HisLeft(b);

        Vector3 sole = new Vector3(feetAt.x, floorY + AnkleAboveSole, feetAt.z);
        Body(go, rest, b, hipPoint, forward, Vector3.up,
             sole + hisLeft * stance, sole - hisLeft * stance, lean);
    }

    /// Bends the trunk towards the face. Spread over the whole column rather than
    /// hinged at one joint, which is what makes it read as a body and not a door.
    public static void Lean(Dictionary<string, Transform> b,
                            Vector3 faceDir, Vector3 torsoUp, float lean)
    {
        if (Mathf.Abs(lean) < 0.001f) return;

        // Right = cross(up, forward), in the body's own axes rather than the
        // world's - upright those are the same, lying down they are not at all.
        Vector3 rightAxis = Vector3.Cross(torsoUp, faceDir).normalized;
        if (rightAxis.sqrMagnitude < 1e-6f) return;

        var column = new[] { Waist, Spine1, Spine2 };
        var share = new[] { 0.45f, 0.35f, 0.20f };
        float degrees = lean * 34f;

        for (int i = 0; i < column.Length; i++)
        {
            var bone = Bone(b, column[i]);
            if (bone == null) continue;
            bone.rotation = Quaternion.AngleAxis(degrees * share[i], rightAxis) * bone.rotation;
        }
    }

    // ------------------------------------------------------------------- limbs

    /// Which way an elbow should break: mostly down, a little back, a little out.
    ///
    /// A pole that points purely sideways wings both arms out like a scarecrow.
    /// It goes unnoticed while the hands are down by the knees, because a nearly
    /// straight arm barely has an elbow to place - and becomes obvious the moment
    /// a hand comes up near the shoulder and the arm has to fold.
    public static Vector3 ElbowPole(Vector3 hisLeft, Vector3 faceDir, Vector3 bodyUp, bool left)
        => (left ? hisLeft : -hisLeft) * 0.45f - faceDir * 0.30f - bodyUp * 0.85f;

    /// Puts a hand at a world point. `pole` is the way the elbow should swing.
    ///
    /// A target beyond the arm's reach is pulled back inside it first. Left alone,
    /// the solver answers an unreachable target by straightening the limb and
    /// pointing at it - and a hand asked for somewhere down by the hips is exactly
    /// that, because the arm hangs from the shoulder half a torso higher up. The
    /// result is a rigid arm laid flat through the body. Reeled in, the arm keeps
    /// a bend and goes as far that way as it can, which is what an arm does.
    public static void Arm(Dictionary<string, Transform> b, string side,
                           Vector3 handAt, Vector3 pole)
    {
        var upper = Bone(b, side + "_Upperarm");
        var fore = Bone(b, side + "_Forearm");
        var hand = Bone(b, side + "_Hand");
        if (upper == null || fore == null || hand == null) return;

        float reach = Vector3.Distance(upper.position, fore.position)
                    + Vector3.Distance(fore.position, hand.position);

        Vector3 fromShoulder = handAt - upper.position;
        float want = fromShoulder.magnitude;

        // Short of full extension: an arm locked straight reads as a mannequin
        // even when the target is honestly that far away.
        //
        // How short matters more than it looks. At 0.92 the elbow can never open
        // past about 125 degrees, so every pose that should be an arm hanging by
        // his side came out with the elbow permanently cocked - which reads as a
        // twisted arm rather than a relaxed one. 0.97 still refuses to lock the
        // joint, and leaves the slight bend a hanging arm actually has.
        const float Comfortable = 0.97f;
        if (want > reach * Comfortable && want > 1e-4f)
            handAt = upper.position + fromShoulder / want * (reach * Comfortable);

        PoseTools.TwoBone(upper, fore, hand, handAt, pole);
    }

    /// Head and neck turn towards a point, the head carrying more of it than the
    /// neck. `amount` is 0..1 so a glance can be half-taken.
    ///
    /// Turning happens about the body's own up, not the world's: a man lying on
    /// his back turning to look at someone rotates his head about the line of his
    /// spine, and using world up there tips his face into the mat instead.
    public static void LookAt(GameObject go, Dictionary<string, Transform> b,
                              Vector3 target, float amount)
    {
        var head = Bone(b, Head);
        if (head == null || amount <= 0.001f) return;
        var neck = Bone(b, Neck);

        Vector3 from = go.transform.forward;
        Vector3 bodyUp = go.transform.up;
        Vector3 to = target - head.position;
        if (to.sqrMagnitude < 1e-6f) return;
        to.Normalize();

        Vector3 rightAxis = Vector3.Cross(bodyUp, from).normalized;
        if (rightAxis.sqrMagnitude < 1e-6f) return;

        float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(from, bodyUp),
                                        Vector3.ProjectOnPlane(to, bodyUp), bodyUp);
        float pitch = Vector3.SignedAngle(Vector3.ProjectOnPlane(from, rightAxis),
                                          Vector3.ProjectOnPlane(to, rightAxis), rightAxis);

        // A neck that turns further than this is a horror film, not a conversation.
        yaw = Mathf.Clamp(yaw, -78f, 78f) * amount;
        pitch = Mathf.Clamp(pitch, -42f, 42f) * amount;

        if (neck != null)
            neck.rotation = Quaternion.AngleAxis(yaw * 0.4f, bodyUp)
                          * Quaternion.AngleAxis(pitch * 0.4f, rightAxis) * neck.rotation;

        head.rotation = Quaternion.AngleAxis(yaw * 0.6f, bodyUp)
                      * Quaternion.AngleAxis(pitch * 0.6f, rightAxis) * head.rotation;
    }

    // ------------------------------------------------------------------ common

    /// Edit-mode skinning goes stale otherwise, and the model keeps its old shape
    /// in the scene view however the bones are moved.
    public static void KeepSkinFresh(GameObject go)
    {
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.forceMatrixRecalculationPerRender = true;
            smr.updateWhenOffscreen = true;
        }
    }

    public static List<Transform> TrackedBones(Dictionary<string, Transform> b)
    {
        var list = new List<Transform>();
        foreach (var n in Tracked)
        {
            var t = Bone(b, n);
            if (t != null) list.Add(t);
        }
        return list;
    }
}
