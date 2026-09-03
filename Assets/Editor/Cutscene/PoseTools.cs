using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Posing maths shared by the cutscene builders.
//
// Hand-typed euler angles are guesswork twice over: bone axes differ per rig, and
// a number that looks right in the inspector still has to be checked against where
// the furniture actually is. These helpers let a pose be stated as places - hips on
// that seat, hand on that book - and solve the angles from the scene.
//
// Pure maths on Transforms: no components, nothing left behind in the scene.
public static class PoseTools
{
    // Points a bone at a world position by rotating it the same way its own
    // direction has to turn. Never assumes which local axis runs down the bone.
    public static void Aim(Transform bone, Transform childForDirection, Vector3 worldTarget)
    {
        if (bone == null || childForDirection == null) return;

        Vector3 current = childForDirection.position - bone.position;
        Vector3 wanted = worldTarget - bone.position;
        if (current.sqrMagnitude < 1e-8f || wanted.sqrMagnitude < 1e-8f) return;

        bone.rotation = Quaternion.FromToRotation(current.normalized, wanted.normalized) * bone.rotation;
    }

    // Two-bone IK. `pole` is the direction the joint should break towards - the
    // knee forwards, the elbow down and back - without which the limb folds
    // through the body. Out of reach, the limb straightens and points, which is
    // what a reach that cannot land should look like anyway.
    public static void TwoBone(Transform upper, Transform lower, Transform tip,
                               Vector3 target, Vector3 pole)
    {
        if (upper == null || lower == null || tip == null) return;

        float upperLen = Vector3.Distance(upper.position, lower.position);
        float lowerLen = Vector3.Distance(lower.position, tip.position);

        Vector3 root = upper.position;
        Vector3 toTarget = target - root;
        float dist = toTarget.magnitude;
        if (dist < 1e-5f) return;

        float reach = upperLen + lowerLen;
        if (dist >= reach * 0.999f) dist = reach * 0.999f;

        Vector3 dir = toTarget.normalized;
        Vector3 aim = root + dir * dist;

        float a = (upperLen * upperLen - lowerLen * lowerLen + dist * dist) / (2f * dist);
        float h = Mathf.Sqrt(Mathf.Max(0f, upperLen * upperLen - a * a));

        Vector3 poleDir = Vector3.ProjectOnPlane(pole, dir);
        if (poleDir.sqrMagnitude < 1e-6f) poleDir = Vector3.ProjectOnPlane(Vector3.up, dir);
        poleDir.Normalize();

        Vector3 joint = root + dir * a + poleDir * h;

        Aim(upper, lower, joint);
        Aim(lower, tip, aim);
    }

    // The rest pose comes from the imported model, never from the bones in the
    // scene - those have already been posed by whatever ran last. The root is
    // deliberately excluded: it carries where the character stands and which way
    // it faces, which is staging rather than pose.
    public static Dictionary<string, Quaternion> ReadRestPose(GameObject sceneInstance)
    {
        var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(sceneInstance);
        var map = new Dictionary<string, Quaternion>();
        if (source == null) return map;

        foreach (var t in source.GetComponentsInChildren<Transform>())
            if (t != source.transform) map[t.name] = t.localRotation;
        return map;
    }

    public static void ApplyRestPose(GameObject sceneInstance, Dictionary<string, Quaternion> rest)
    {
        foreach (var t in sceneInstance.GetComponentsInChildren<Transform>())
        {
            if (t == sceneInstance.transform) continue;
            if (rest.TryGetValue(t.name, out var q)) t.localRotation = q;
        }
    }

    /// Bone lookup by name, for rigs driven as Generic rather than Humanoid.
    public static Dictionary<string, Transform> BonesOf(GameObject root)
    {
        var map = new Dictionary<string, Transform>();
        foreach (var t in root.GetComponentsInChildren<Transform>())
            map[t.name] = t;
        return map;
    }

    /// The path an AnimationClip curve needs: from the animated root down to the bone.
    public static string PathTo(Transform root, Transform bone)
    {
        var stack = new List<string>();
        for (var t = bone; t != null && t != root; t = t.parent) stack.Add(t.name);
        stack.Reverse();
        return string.Join("/", stack);
    }
}
