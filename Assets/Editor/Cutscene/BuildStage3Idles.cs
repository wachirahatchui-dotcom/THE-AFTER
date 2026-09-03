using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;
using System.Collections.Generic;

// Puts the garage crew into their working poses and switches on the idle that
// keeps them moving.
//
// The pose is baked into the scene rather than driven at runtime. These models
// arrive in an A-pose - arms hanging out at roughly forty-five degrees - and no
// amount of layered motion turns that into somebody working at a bench. The
// hands have to be put on the bench first, once, and saved; AmbientIdle then
// adds breath and busy hands on top of that.
//
// Baking also means the poses are visible in the Scene view, so a hand through a
// desk is something to see and drag rather than something to guess at.
//
// Menu: THE AFTER > Cutscene > Build Stage 3 Idles
public static class BuildStage3Idles
{
    // Where each character stands, what they are doing, and where their hands go.
    class Station
    {
        public string character;
        public string prop;                 // the bench or desk they work at
        public AmbientIdle.Mood mood;
        public string faces;                // character they turn to, for Talking/Listening
        public float handHeight;            // world Y for the hands
        public float reach = 0.42f;         // how far in front of the chest the hands sit
        public float lean = 8f;             // degrees the trunk tips over the work
    }

    static readonly Station[] Crew =
    {
        // Ethan is head-down at the workbench when the player walks in - the spec
        // has him busy, and the whole conversation opens with him noticing them.
        new Station { character = "Ethan", prop = "workbench",
                      mood = AmbientIdle.Mood.Working, handHeight = 5.02f, lean = 10f },

        // Sydney is rummaging for something; Baena is leaning on the same desk
        // talking at her, which is why he is the one already wound up when Asher
        // arrives.
        new Station { character = "Sydney", prop = "Sydney Desk",
                      mood = AmbientIdle.Mood.Working, handHeight = 5.05f, lean = 12f },

        new Station { character = "Baena", prop = null, faces = "Sydney",
                      mood = AmbientIdle.Mood.Talking, handHeight = 4.85f, reach = 0.30f, lean = 2f },

        new Station { character = "Alex", prop = "Maker Robot Hand Desk",
                      mood = AmbientIdle.Mood.Working, handHeight = 4.95f, lean = 9f },
    };

    [MenuItem("THE AFTER/Cutscene/Build Stage 3 Idles")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new StringBuilder();
        log.AppendFormat("=== จัดท่า Stage 3 ใน {0} ===\n", scene.name);

        foreach (var st in Crew)
        {
            var go = FindRoot(scene, st.character);
            if (go == null) { log.AppendFormat("{0}: ไม่เจอในซีน\n", st.character); continue; }

            Undo.RegisterFullObjectHierarchyUndo(go, "Build Stage 3 Idles");

            var bones = PoseTools.BonesOf(go);
            log.AppendFormat("\n{0}\n", st.character);

            // ---- which way they face ----
            Vector3 faceTarget;
            if (st.faces != null)
            {
                var other = FindRoot(scene, st.faces);
                faceTarget = other != null ? other.transform.position : go.transform.position + go.transform.forward;
                log.AppendFormat("   หันหา {0}\n", st.faces);
            }
            else
            {
                var prop = FindDeep(scene, st.prop);
                faceTarget = prop != null ? PropCentre(prop) : go.transform.position + go.transform.forward;
                log.AppendFormat("   หันหา {0}\n", st.prop);
            }

            Vector3 flat = faceTarget - go.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.0001f)
                go.transform.rotation = Quaternion.LookRotation(flat.normalized);

            log.AppendFormat("   yaw = {0}\n", go.transform.eulerAngles.y.ToString("F0"));

            // ---- lean over the work ----
            var waist = Get(bones, "Waist");
            if (waist != null && st.lean != 0f)
            {
                // About the character's own right, so the trunk tips forward
                // whichever way they ended up facing.
                Vector3 axis = waist.parent != null
                    ? waist.parent.InverseTransformDirection(go.transform.right).normalized
                    : go.transform.right;
                waist.localRotation = Quaternion.AngleAxis(st.lean, axis) * waist.localRotation;
                log.AppendFormat("   ก้ม {0} องศา\n", st.lean.ToString("F0"));
            }

            // ---- hands onto the work ----
            PlaceHand(go, bones, st, left: true, log: log);
            PlaceHand(go, bones, st, left: false, log: log);

            // ---- the idle itself ----
            var animator = go.GetComponent<Animator>();
            if (animator == null)
            {
                // No controller on purpose: nothing to play. It is here because a
                // Timeline animation track will not bind to an object without one,
                // and the cutscene needs these four.
                animator = Undo.AddComponent<Animator>(go);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                log.AppendLine("   ใส่ Animator (ไว้ให้ Timeline ผูกตอนคัตซีน)");
            }

            var idle = go.GetComponent<AmbientIdle>();
            if (idle == null) idle = Undo.AddComponent<AmbientIdle>(go);

            idle.mood = st.mood;
            idle.frame = go.transform;
            if (st.faces != null)
            {
                var other = FindRoot(scene, st.faces);
                if (other != null)
                {
                    var h = FindBone(other.transform, "Head");
                    idle.lookAt = h != null ? h : other.transform;
                }
            }

            // Read the pose that was just built as the one to move around.
            idle.Capture();
            EditorUtility.SetDirty(idle);

            log.AppendFormat("   idle = {0}\n", st.mood);
        }

        // Sydney answers Baena now and then, so she looks his way rather than
        // through him - set after both exist so the reference resolves.
        var syd = FindRoot(scene, "Sydney");
        var bae = FindRoot(scene, "Baena");
        if (syd != null && bae != null)
        {
            var idle = syd.GetComponent<AmbientIdle>();
            var head = FindBone(bae.transform, "Head");
            if (idle != null && head != null)
            {
                idle.lookAt = head;
                EditorUtility.SetDirty(idle);
                log.AppendLine("\nSydney มี Baena เป็นเป้าสายตา (ตอนหันมาตอบ)");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(log.ToString());
    }

    // The hand goes in front of the chest, out to its own side, at bench height.
    static void PlaceHand(GameObject go, Dictionary<string, Transform> bones,
                          Station st, bool left, StringBuilder log)
    {
        string side = left ? "L" : "R";
        var upper = Get(bones, side + "_Upperarm");
        var lower = Get(bones, side + "_Forearm");
        var hand = Get(bones, side + "_Hand");
        if (upper == null || lower == null || hand == null) return;

        Vector3 outward = go.transform.right * (left ? -1f : 1f) * 0.18f;
        Vector3 target = upper.position
                       + go.transform.forward * st.reach
                       + outward;
        target.y = st.handHeight;

        // Elbows out and down, which is where they sit on somebody leaning over
        // a bench. Pointing the pole up puts the arm into a chicken wing.
        Vector3 pole = (go.transform.right * (left ? -1f : 1f) - go.transform.up * 0.6f).normalized;

        PoseTools.TwoBone(upper, lower, hand, target, pole);

        log.AppendFormat("   มือ{0} -> {1}\n", left ? "ซ้าย" : "ขวา", hand.position.ToString("F2"));
    }

    static Vector3 PropCentre(Transform prop)
    {
        var rends = prop.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return prop.position;
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.center;
    }

    static GameObject FindRoot(UnityEngine.SceneManagement.Scene s, string name)
    {
        foreach (var r in s.GetRootGameObjects())
            if (r.name == name && r.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                return r;
        return null;
    }

    static Transform FindDeep(UnityEngine.SceneManagement.Scene s, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var r in s.GetRootGameObjects())
        {
            var d = FindBone(r.transform, name);
            if (d != null) return d;
        }
        return null;
    }

    static Transform FindBone(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var d = FindBone(c, name);
            if (d != null) return d;
        }
        return null;
    }

    static Transform Get(Dictionary<string, Transform> bones, string name)
    {
        Transform t;
        return bones.TryGetValue(name, out t) ? t : null;
    }
}
