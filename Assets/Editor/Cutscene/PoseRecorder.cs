using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Turns a series of posed moments into an AnimationClip.
//
// The workflow is: pose the skeleton in the scene however you like, call
// Capture() to record that moment at a time, pose it again, capture again. At
// the end, Build() writes one clip with a keyframe per captured moment.
//
// Reading the bones after each pose rather than working in angles means the
// clip contains exactly what was previewed, and the IK never has to be
// reproduced at runtime.
public class PoseRecorder
{
    readonly Transform root;
    readonly List<Transform> bones = new List<Transform>();
    readonly List<string> paths = new List<string>();

    // time -> rotation per bone, and the root bone's position (the only one that
    // translates - hips move when someone sits down).
    readonly List<float> times = new List<float>();
    readonly List<Quaternion[]> rotations = new List<Quaternion[]>();
    readonly List<Vector3> hipPositions = new List<Vector3>();
    readonly Transform hips;

    // Some poses are solved by moving the whole object rather than a bone: sitting
    // down puts the root wherever the hips have to land, and standing up moves it
    // again. That travel lives on the root transform, so unless it is recorded too
    // the clip plays the bones of a man getting up while he stays sat down.
    readonly bool captureRoot;
    readonly List<Vector3> rootPositions = new List<Vector3>();
    readonly List<Quaternion> rootRotations = new List<Quaternion>();

    public PoseRecorder(Transform animatedRoot, Transform hipsBone, IEnumerable<Transform> boneList,
                        bool captureRoot = false)
    {
        root = animatedRoot;
        hips = hipsBone;
        this.captureRoot = captureRoot;
        foreach (var b in boneList)
        {
            if (b == null) continue;
            bones.Add(b);
            paths.Add(PoseTools.PathTo(root, b));
        }
    }

    public void Capture(float time)
    {
        var snap = new Quaternion[bones.Count];
        for (int i = 0; i < bones.Count; i++) snap[i] = bones[i].localRotation;
        times.Add(time);
        rotations.Add(snap);
        hipPositions.Add(hips != null ? hips.localPosition : Vector3.zero);
        rootPositions.Add(root.localPosition);
        rootRotations.Add(root.localRotation);
    }

    public AnimationClip Build(string clipName, bool loop = false)
    {
        var clip = new AnimationClip { name = clipName, frameRate = 30f };

        for (int i = 0; i < bones.Count; i++)
        {
            var cx = new AnimationCurve();
            var cy = new AnimationCurve();
            var cz = new AnimationCurve();
            var cw = new AnimationCurve();

            for (int k = 0; k < times.Count; k++)
            {
                var q = rotations[k][i];
                cx.AddKey(times[k], q.x);
                cy.AddKey(times[k], q.y);
                cz.AddKey(times[k], q.z);
                cw.AddKey(times[k], q.w);
            }

            Smooth(cx); Smooth(cy); Smooth(cz); Smooth(cw);

            clip.SetCurve(paths[i], typeof(Transform), "localRotation.x", cx);
            clip.SetCurve(paths[i], typeof(Transform), "localRotation.y", cy);
            clip.SetCurve(paths[i], typeof(Transform), "localRotation.z", cz);
            clip.SetCurve(paths[i], typeof(Transform), "localRotation.w", cw);
        }

        if (hips != null)
        {
            string hipPath = PoseTools.PathTo(root, hips);
            var px = new AnimationCurve();
            var py = new AnimationCurve();
            var pz = new AnimationCurve();
            for (int k = 0; k < times.Count; k++)
            {
                px.AddKey(times[k], hipPositions[k].x);
                py.AddKey(times[k], hipPositions[k].y);
                pz.AddKey(times[k], hipPositions[k].z);
            }
            Smooth(px); Smooth(py); Smooth(pz);
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.x", px);
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.y", py);
            clip.SetCurve(hipPath, typeof(Transform), "localPosition.z", pz);
        }

        if (captureRoot)
        {
            // Curves at the empty path drive the animated object's own transform.
            var rx = new AnimationCurve(); var ry = new AnimationCurve(); var rz = new AnimationCurve();
            var qx = new AnimationCurve(); var qy = new AnimationCurve();
            var qz = new AnimationCurve(); var qw = new AnimationCurve();

            for (int k = 0; k < times.Count; k++)
            {
                rx.AddKey(times[k], rootPositions[k].x);
                ry.AddKey(times[k], rootPositions[k].y);
                rz.AddKey(times[k], rootPositions[k].z);
                qx.AddKey(times[k], rootRotations[k].x);
                qy.AddKey(times[k], rootRotations[k].y);
                qz.AddKey(times[k], rootRotations[k].z);
                qw.AddKey(times[k], rootRotations[k].w);
            }

            Smooth(rx); Smooth(ry); Smooth(rz);
            Smooth(qx); Smooth(qy); Smooth(qz); Smooth(qw);

            clip.SetCurve("", typeof(Transform), "localPosition.x", rx);
            clip.SetCurve("", typeof(Transform), "localPosition.y", ry);
            clip.SetCurve("", typeof(Transform), "localPosition.z", rz);
            clip.SetCurve("", typeof(Transform), "localRotation.x", qx);
            clip.SetCurve("", typeof(Transform), "localRotation.y", qy);
            clip.SetCurve("", typeof(Transform), "localRotation.z", qz);
            clip.SetCurve("", typeof(Transform), "localRotation.w", qw);
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        // Quaternion curves keyed straight from poses come out linear and read as
        // robotic. Smoothing the tangents is what makes a gesture look eased.
        clip.EnsureQuaternionContinuity();
        return clip;
    }

    static void Smooth(AnimationCurve c)
    {
        for (int i = 0; i < c.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(c, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        c.SmoothTangents(0, 0f);
        if (c.length > 1) c.SmoothTangents(c.length - 1, 0f);
    }
}
