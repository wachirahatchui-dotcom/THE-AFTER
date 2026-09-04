using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Text;
using System.Collections.Generic;

// Puts everybody's feet on whatever they are standing on.
//
// A character is placed by dragging them in the Scene view, and a drag is
// accurate to about a centimetre on a good day. A centimetre does not matter.
// Three or four do: a boot with no sole showing, or a heel hanging in the air,
// in a shot framed at a metre and a half. It is the kind of error that is
// invisible while you are placing them and obvious the moment a camera cuts.
//
// Two things make this harder than a raycast.
//
// The character's own origin is not their feet. These rigs put the origin
// wherever the exporter felt like it, and the pose moves the feet around under
// it, so the only honest question is where the lowest bit of skin actually is -
// which means baking the skinned mesh and reading it, not trusting bounds
// (bounds run about twenty per cent fat on these rigs).
//
// And half the furniture in this game has no collider. Matha's bedroom has
// thirty-five meshes and not one collider on any of them, so a ray fired down
// from somebody asleep on the bed sails through the mattress, through the floor,
// and reports the ground plane eighty metres below as the thing they are lying
// on. So the support surface is read from mesh vertices directly. Slower, and it
// works on a room nobody has got round to adding colliders to yet.
//
// Menu: THE AFTER > Fix > Ground Characters
public static class GroundCharacters
{
    // Past this, something is wrong that moving somebody will not fix - they are
    // on a piece of scenery that is switched off, or the thing under them is not
    // the thing holding them up. Better to say so than to drop somebody through
    // a bed.
    const float MaxLift = 0.30f;

    // How far to either side of the lowest point counts as "under" it.
    const float Reach = 0.25f;

    [MenuItem("THE AFTER/Fix/Ground Characters")]
    public static void Run() { Do(true); }

    [MenuItem("THE AFTER/Fix/Ground Characters (report only)")]
    public static void Report() { Do(false); }

    static void Do(bool apply)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var log = new StringBuilder();
        log.AppendFormat("=== เท้ากับพื้น: {0} ===\n", scene.name);

        // Colliders on a switched-off object do not exist, and neither do its
        // meshes as far as a search is concerned. Everything comes on for the
        // measurement and goes back exactly as it was.
        var was = new Dictionary<GameObject, bool>();
        foreach (var r in scene.GetRootGameObjects())
        {
            if (!(r.name.Contains("Stage") && r.name.Contains("Set"))) continue;
            foreach (Transform t in r.transform)
            {
                was[t.gameObject] = t.gameObject.activeSelf;
                t.gameObject.SetActive(true);
            }
        }
        Physics.SyncTransforms();

        var people = People(scene);
        var scenery = Scenery(scene, people);
        int moved = 0, fine = 0, skipped = 0;

        var baked = new UnityEngine.Mesh();

        foreach (var who in people)
        {
            Vector3 low = Lowest(who, baked);
            if (float.IsInfinity(low.y)) continue;

            float support;
            string what;
            if (!Support(low, scenery, out support, out what))
            {
                log.AppendFormat("  {0,-12} ไม่มีอะไรรองรับใต้ตัว - ข้าม\n", who.name);
                skipped++;
                continue;
            }

            float gap = low.y - support;

            if (Mathf.Abs(gap) < 0.02f)
            {
                log.AppendFormat("  {0,-12} โอเค ({1:+0.000;-0.000} m บน {2})\n", who.name, gap, what);
                fine++;
                continue;
            }

            if (Mathf.Abs(gap) > MaxLift)
            {
                log.AppendFormat("  {0,-12} ห่าง {1:+0.00;-0.00} m บน {2} - มากเกินกว่าจะแก้เอง ไปดูด้วยตา\n",
                    who.name, gap, what);
                skipped++;
                continue;
            }

            log.AppendFormat("  {0,-12} {1} {2:F1} ซม. บน {3}{4}\n",
                who.name, gap > 0 ? "ลอย" : "จม", Mathf.Abs(gap) * 100f, what,
                apply ? string.Format("  ->  ขยับ {0:+0.000;-0.000}", -gap) : "");

            if (apply)
            {
                Undo.RecordObject(who, "Ground Characters");
                who.position -= Vector3.up * gap;
                EditorUtility.SetDirty(who);
            }
            moved++;
        }

        foreach (var kv in was) kv.Key.SetActive(kv.Value);
        Physics.SyncTransforms();

        log.AppendFormat("\nโอเคอยู่แล้ว {0}  {1} {2}  ข้าม {3}\n",
            fine, apply ? "แก้แล้ว" : "ต้องแก้", moved, skipped);
        if (apply && moved > 0) EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(log.ToString());
    }

    /// Everything with skin on it, one entry per character rather than one per
    /// mesh - these rigs split a person across several renderers.
    static List<Transform> People(UnityEngine.SceneManagement.Scene scene)
    {
        var found = new List<Transform>();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var smr in r.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                // Up to the node that owns the whole skeleton. Stop at the top of
                // the scene or at a grouping object - the stage sets are named
                // with === so they are easy to recognise and easy to stop at.
                Transform top = smr.transform;
                while (top.parent != null
                       && !top.parent.name.StartsWith("===")
                       && top.parent.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                    top = top.parent;

                if (!found.Contains(top)) found.Add(top);
            }
        return found;
    }

    /// Every mesh in the scene that is not part of a character.
    static List<MeshFilter> Scenery(UnityEngine.SceneManagement.Scene scene, List<Transform> people)
    {
        var list = new List<MeshFilter>();
        foreach (var r in scene.GetRootGameObjects())
            foreach (var mf in r.GetComponentsInChildren<MeshFilter>(true))
            {
                bool ours = false;
                foreach (var p in people) if (mf.transform.IsChildOf(p)) { ours = true; break; }
                if (!ours && mf.sharedMesh != null) list.Add(mf);
            }
        return list;
    }

    static Vector3 Lowest(Transform who, UnityEngine.Mesh baked)
    {
        Vector3 low = Vector3.positiveInfinity;
        foreach (var smr in who.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            smr.BakeMesh(baked, true);
            foreach (var v in baked.vertices)
            {
                Vector3 w = smr.transform.TransformPoint(v);
                if (w.y < low.y) low = w;
            }
        }
        return low;
    }

    /// The highest surface sitting under a point.
    ///
    /// A ray first, because it is exact and it is what the game itself will use
    /// when the player walks there. Vertices only when the ray finds nothing -
    /// which happens in rooms nobody has added colliders to yet.
    ///
    /// The two have opposite blind spots, which is why both are here. A ray
    /// needs a collider. Vertex sampling needs vertices near the point, and a
    /// floor is usually two enormous triangles whose corners are twenty metres
    /// away in the corners of the room - so it finds nothing at all on exactly
    /// the flat ground it is most obviously standing on.
    static bool Support(Vector3 low, List<MeshFilter> scenery, out float y, out string what)
    {
        y = float.NegativeInfinity;
        what = "-";

        RaycastHit hit;
        if (Physics.Raycast(low + Vector3.up * (MaxLift + 0.05f), Vector3.down, out hit, MaxLift * 2f + 0.1f))
        {
            y = hit.point.y;
            what = hit.collider.name;
            return true;
        }

        foreach (var mf in scenery)
        {
            var rend = mf.GetComponent<Renderer>();
            if (rend == null) continue;

            var b = rend.bounds;
            if (low.x < b.min.x - Reach || low.x > b.max.x + Reach) continue;
            if (low.z < b.min.z - Reach || low.z > b.max.z + Reach) continue;

            // A surface that starts above the feet is not what they are on, and
            // one far below is the floor two storeys down.
            if (b.max.y > low.y + MaxLift) { if (b.min.y > low.y + 0.02f) continue; }
            if (b.max.y < low.y - 1.5f) continue;

            var verts = mf.sharedMesh.vertices;
            var xf = mf.transform;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 w = xf.TransformPoint(verts[i]);
                if (w.y > low.y + 0.02f) continue;
                if (w.y <= y) continue;

                float dx = w.x - low.x, dz = w.z - low.z;
                if (dx * dx + dz * dz > Reach * Reach) continue;

                y = w.y;
                what = mf.name;
            }
        }

        return !float.IsNegativeInfinity(y);
    }
}
