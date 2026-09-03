using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

// Puts Asher Kid on the bed and records his half of Stage 1.
//
// He is rigged already - Tripo's auto-rig, 41 bones with its own naming
// (Hip / Spine01 / L_Upperarm / NeckTwist01) rather than the Mixamo names Matha
// uses - so nothing here assumes a naming convention it has not looked up.
//
// The pose is stated as places: hips on the mattress, heels down the bed, hands
// by his sides, head turned to his mother. Everything else is solved.
public static class BuildChapter1Asher
{
    const string ModelPath = "Assets/Models/Characters/Asher Kid/Asher Kid.fbx";
    const string ModelFolder = "Assets/Models/Characters/Asher Kid";

    const float MattressTop = 0.966f;
    const float TargetHeight = 1.20f;

    // Across the bed: he sleeps on his mother's side so she can reach him.
    const float LieX = 46.42f;

    /// Bone names as this particular rig spells them.
    const string Hips = "Hip";
    const string Spine1 = "Spine01";
    const string Spine2 = "Spine02";
    const string Neck = "NeckTwist01";
    const string Head = "Head";

    public static GameObject Place(Bounds bedBounds, StringBuilder log)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        if (mi == null) { log.AppendLine("หา " + ModelPath + " ไม่เจอ"); return null; }

        // Textures and material, then the height. Scale is solved from baked
        // vertices - SkinnedMeshRenderer.bounds pads the box by about 20%.
        if (mi.globalScale == 1f)
        {
            log.AppendLine(TripoModelSetup.Setup(ModelFolder));
            mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            float h = MeasureHeight();
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.globalScale = TargetHeight / h;
            AssetDatabase.WriteImportSettingsIfDirty(ModelPath);
            AssetDatabase.ImportAsset(ModelPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            log.AppendLine("Asher Kid: สูง " + h.ToString("F3") + " -> " + TargetHeight
                         + "  (scaleFactor " + mi.globalScale.ToString("F4") + ")");
        }

        GameObject kid = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Asher Kid") kid = t.gameObject;

        if (kid == null)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            kid = (GameObject)PrefabUtility.InstantiatePrefab(src);
            kid.name = "Asher Kid";
            Undo.RegisterCreatedObjectUndo(kid, "Add Asher Kid");
        }

        var anim = kid.GetComponent<Animator>();
        if (anim == null) anim = kid.AddComponent<Animator>();
        anim.applyRootMotion = false;

        Undo.RecordObject(kid.transform, "Lay Asher Kid down");
        kid.transform.localScale = Vector3.one;

        // On his back, head towards the headboard, face to the ceiling.
        kid.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
        kid.transform.position = new Vector3(LieX, MattressTop, bedBounds.center.z);

        foreach (var smr in kid.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            smr.forceMatrixRecalculationPerRender = true;
            smr.updateWhenOffscreen = true;
        }
        return kid;
    }

    static float MeasureHeight()
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(go);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var m = new UnityEngine.Mesh();
            smr.BakeMesh(m, true);
            foreach (var v in m.vertices)
            {
                float y = smr.transform.TransformPoint(v).y;
                if (y < lo) lo = y;
                if (y > hi) hi = y;
            }
            Object.DestroyImmediate(m);
        }
        Object.DestroyImmediate(inst);
        return hi - lo;
    }

    public static Bounds Baked(GameObject go)
    {
        bool first = true;
        var bb = new Bounds();
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var m = new UnityEngine.Mesh();
            smr.BakeMesh(m, true);
            foreach (var v in m.vertices)
            {
                var w = smr.transform.TransformPoint(v);
                if (first) { bb = new Bounds(w, Vector3.zero); first = false; }
                else bb.Encapsulate(w);
            }
            Object.DestroyImmediate(m);
        }
        return bb;
    }

    /// Records the lying pose. Returns the clip; the head position it settles at
    /// is written to `headOut` so the mother's reach and the cameras can aim there.
    public static AnimationClip BuildClip(GameObject kid, Bounds bedBounds, GameObject matha,
                                          float endTime, out Vector3 headOut, StringBuilder log)
    {
        var rest = PoseTools.ReadRestPose(kid);
        var b = PoseTools.BonesOf(kid);
        Transform B(string n) => b.TryGetValue(n, out var t) ? t : null;

        var tracked = new List<Transform>();
        foreach (var name in new[]
        {
            Hips, "Pelvis", "Waist", Spine1, Spine2, Neck, "NeckTwist02", Head,
            "L_Clavicle", "L_Upperarm", "L_Forearm", "L_Hand",
            "R_Clavicle", "R_Upperarm", "R_Forearm", "R_Hand",
            "L_Thigh", "L_Calf", "L_Foot", "L_ToeBase",
            "R_Thigh", "R_Calf", "R_Foot", "R_ToeBase",
        })
            if (B(name) != null) tracked.Add(B(name));

        var rec = new PoseRecorder(kid.transform, B(Hips), tracked);

        Vector3 downTheBed = Vector3.back;
        Vector3 towardsMother = Vector3.ProjectOnPlane(
            matha.transform.position - kid.transform.position, Vector3.up).normalized;

        // Settle him first so the sliding below measures a real lying body.
        Lie(kid, rest, b, downTheBed, towardsMother, 0f, log);

        var bounds = Baked(kid);
        Undo.RecordObject(kid.transform, "Seat Asher on the mattress");
        kid.transform.position += new Vector3(LieX - bounds.center.x,
                                              MattressTop - bounds.min.y,
                                              (bedBounds.max.z - 0.22f) - bounds.max.z);

        // Mostly still, with a slow turn of the head towards his mother and back -
        // a child listening rather than a mannequin.
        Lie(kid, rest, b, downTheBed, towardsMother, 0.15f, null); rec.Capture(0f);
        Lie(kid, rest, b, downTheBed, towardsMother, 0.85f, null); rec.Capture(3.0f);
        Lie(kid, rest, b, downTheBed, towardsMother, 1.00f, null); rec.Capture(7.4f);
        Lie(kid, rest, b, downTheBed, towardsMother, 0.90f, null); rec.Capture(10.5f);
        Lie(kid, rest, b, downTheBed, towardsMother, 0.70f, null); rec.Capture(endTime);

        Lie(kid, rest, b, downTheBed, towardsMother, 0.15f, null);

        headOut = B(Head).position;
        log.AppendLine("Asher นอนที่ " + kid.transform.position.ToString("F3")
                     + "   หัวอยู่ที่ " + headOut.ToString("F3"));
        log.AppendLine("  ขนาดตอนนอน " + Baked(kid).size.ToString("F3"));

        return rec.Build("AsherKid_Stage1");
    }

    // `turn` is how far his head has come round to face his mother, 0..1.
    static void Lie(GameObject kid, Dictionary<string, Quaternion> rest, Dictionary<string, Transform> b,
                    Vector3 downTheBed, Vector3 towardsMother, float turn, StringBuilder log)
    {
        PoseTools.ApplyRestPose(kid, rest);
        Transform B(string n) => b.TryGetValue(n, out var t) ? t : null;

        Vector3 towardsHead = -downTheBed;
        var hips = B(Hips);

        // Which side the bones named L_ are actually on is not worth deriving from
        // the model's facing - rigs get mirrored. Asking the thigh bones settles it.
        Vector3 hisLeft = Vector3.ProjectOnPlane(
            B("L_Thigh").position - B("R_Thigh").position, Vector3.up).normalized;

        // A body on its back is a straight line; the rest pose is a standing one
        // whose spine curves, which laid flat leaves the chest sunk below the hips.
        var column = new[] { Hips, "Waist", Spine1, Spine2, Neck, "NeckTwist02", Head };
        for (int i = 0; i < column.Length - 1; i++)
        {
            var bone = B(column[i]);
            var child = B(column[i + 1]);
            if (bone == null || child == null) continue;
            float len = Vector3.Distance(bone.position, child.position);
            if (len < 1e-5f) continue;
            PoseTools.Aim(bone, child, bone.position + towardsHead * len);
        }

        // Straightening leaves him rolled, because the hip bones are not level with
        // each other in the rest pose. Rolling about his own long axis flattens him.
        Level(B("L_Thigh"), B("R_Thigh"), hips, towardsHead, log, "สะโพก");
        Level(B("L_Upperarm"), B("R_Upperarm"), B(Spine2) ?? B(Spine1), towardsHead, log, "ไหล่");

        foreach (var side in new[] { "L", "R" })
        {
            Vector3 sideDir = hisLeft * (side == "L" ? 1f : -1f);

            var thigh = B(side + "_Thigh");
            var calf = B(side + "_Calf");
            var foot = B(side + "_Foot");
            if (thigh != null && calf != null && foot != null)
            {
                // Legs stretched out, not folded. A fixed 42 cm reach was shorter
                // than his leg actually is, which bent the knee hard enough to look
                // wrong - so the target is measured off the bones and set just shy
                // of full extension, leaving only the slight bend a resting leg has.
                float legLength = Vector3.Distance(thigh.position, calf.position)
                                + Vector3.Distance(calf.position, foot.position);
                Vector3 ankle = thigh.position + downTheBed * (legLength * 0.985f)
                              - Vector3.up * 0.008f;
                PoseTools.TwoBone(thigh, calf, foot, ankle, Vector3.up * 0.35f + downTheBed * 0.9f);

                var toe = B(side + "_ToeBase");
                if (toe != null)
                    PoseTools.Aim(foot, toe, foot.position + downTheBed * 0.06f + Vector3.up * 0.05f);
            }

            var upper = B(side + "_Upperarm");
            var fore = B(side + "_Forearm");
            var hand = B(side + "_Hand");
            if (upper != null && fore != null && hand != null)
            {
                Vector3 handAt = hips.position + sideDir * 0.115f
                               + downTheBed * 0.04f - Vector3.up * 0.03f;
                PoseTools.TwoBone(upper, fore, hand, handAt, sideDir * 0.8f - Vector3.up * 0.4f);
            }
        }

        // His face points at the ceiling; the turn brings it round to his mother.
        var head = B(Head);
        var neck = B(Neck);
        if (head != null && turn > 0f)
        {
            Vector3 wanted = Vector3.Slerp(Vector3.up, towardsMother + Vector3.up * 0.35f, turn).normalized;
            var q = Quaternion.FromToRotation(Vector3.up, wanted);
            head.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.65f) * head.rotation;
            if (neck != null)
                neck.rotation = Quaternion.Slerp(Quaternion.identity, q, 0.35f) * neck.rotation;
        }
    }

    static void Level(Transform left, Transform right, Transform pivot,
                      Vector3 axis, StringBuilder log, string label)
    {
        if (left == null || right == null || pivot == null) return;

        Vector3 across = left.position - right.position;
        Vector3 inPlane = Vector3.ProjectOnPlane(across, axis);
        Vector3 flat = Vector3.ProjectOnPlane(inPlane, Vector3.up);
        if (inPlane.sqrMagnitude < 1e-6f || flat.sqrMagnitude < 1e-6f) return;

        float roll = Vector3.SignedAngle(inPlane, flat, axis);
        pivot.rotation = Quaternion.AngleAxis(roll, axis) * pivot.rotation;
        log?.AppendLine("        ปรับ" + label + " " + roll.ToString("F0") + " องศา ให้นอนราบ");
    }
}
