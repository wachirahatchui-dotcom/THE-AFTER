using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Records Asher's idle, walk, run and jump.
//
// The motion comes from Locomotion.fbx - the Adventurer's animation on a
// skeleton rearranged so Unity will accept it as Humanoid - and it is Unity that
// does the retargeting. Its humanoid system matches bone role to bone role and
// rescales for the fact that two characters are different sizes, which is the
// part a hand-written retarget gets wrong: copy joint rotations straight across
// and a shorter man inherits a taller man's stride, which reads as a limp.
//
// So Asher is made Humanoid, the clips are sampled onto him, his bones are read
// off frame by frame, and the result is written back as ordinary Generic clips
// against his own hierarchy. Then he goes back to Generic for good.
//
// The round trip is the point. Humanoid gives the quality; Generic is what the
// cutscenes need, because a humanoid avatar overrides the transform curves the
// posing tools produce. Baking the one into the other is how he gets both.
//
// Menu: THE AFTER > Cutscene > Build Asher Locomotion
public static class BuildAsherLocomotion
{
    const string Menu = "THE AFTER/Cutscene/Build Asher Locomotion";
    const string AsherPath = "Assets/Models/Characters/Asher/Asher.fbx";
    const string OutDir = "Assets/Animations/Asher";

    const float Fps = 30f;

    // Kevin Iglesias' Human Basic Motions, already in the project. Every clip in
    // it is authored as Humanoid, which is what lets Unity retarget them onto
    // anybody - including a Tripo rig that shares none of their bone names.
    const string AssetRoot = "Assets/Kevin Iglesias/Human Animations/Animations/Male";

    static readonly (string path, string name, bool loop)[] Wanted =
    {
        (AssetRoot + "/Idles/HumanM@Idle01.fbx",              "Asher_Idle", true),
        (AssetRoot + "/Movement/Walk/HumanM@Walk01_Forward.fbx", "Asher_Walk", true),
        (AssetRoot + "/Movement/Run/HumanM@Run01_Forward.fbx",   "Asher_Run",  true),
        (AssetRoot + "/Movement/Jump/HumanM@Jump01.fbx",         "Asher_Jump", false),
    };

    [MenuItem(Menu)]
    public static void Build() => Debug.Log(BuildAndReport());

    public static string BuildAndReport()
    {
        var log = new StringBuilder();
        EnsureFolder(OutDir);

        // Each file in the pack holds a single clip.
        AnimationClip Source(string path)
        {
            var found = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                                     .FirstOrDefault(c => !c.name.StartsWith("__preview"));
            if (found == null) log.AppendLine("!! ไม่เจอคลิปใน " + path);
            return found;
        }

        foreach (var (path, _, loop) in Wanted) SetLooping(path, loop, log);

        // --- Asher becomes humanoid just long enough to be retargeted onto -----
        var asherImporter = (ModelImporter)AssetImporter.GetAtPath(AsherPath);
        var wasType = asherImporter.animationType;

        asherImporter.animationType = ModelImporterAnimationType.Human;
        asherImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        AssetDatabase.WriteImportSettingsIfDirty(AsherPath);
        AssetDatabase.ImportAsset(AsherPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

        var humanAvatar = AssetDatabase.LoadAllAssetsAtPath(AsherPath).OfType<Avatar>().FirstOrDefault();
        log.AppendLine("Asher เป็น Humanoid ชั่วคราว: avatar valid="
                     + (humanAvatar != null && humanAvatar.isValid)
                     + " human=" + (humanAvatar != null && humanAvatar.isHuman));

        var made = new List<(string name, AnimationClip clip, bool loop)>();

        var rig = (GameObject)PrefabUtility.InstantiatePrefab(
            AssetDatabase.LoadAssetAtPath<GameObject>(AsherPath));
        rig.name = "~locomotion bake";
        rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var animator = rig.GetComponent<Animator>();
        if (animator == null) animator = rig.AddComponent<Animator>();
        animator.avatar = humanAvatar;

        try
        {
            var bones = PoseTools.BonesOf(rig);
            var tracked = TripoPose.TrackedBones(bones);
            var hips = TripoPose.Bone(bones, TripoPose.Hips);

            AnimationMode.StartAnimationMode();

            foreach (var (sourcePath, outName, loop) in Wanted)
            {
                var src = Source(sourcePath);
                if (src == null) continue;

                var rec = new PoseRecorder(rig.transform, hips, tracked);
                int frames = Mathf.Max(2, Mathf.RoundToInt(src.length * Fps));

                for (int f = 0; f <= frames; f++)
                {
                    float t = src.length * f / frames;

                    AnimationMode.BeginSampling();
                    AnimationMode.SampleAnimationClip(rig, src, t);
                    AnimationMode.EndSampling();

                    rec.Capture(t);
                }

                var built = rec.Build(outName, loop);
                var saved = SaveClip(built, OutDir + "/" + outName + ".anim");
                made.Add((outName, saved, loop));

                log.AppendLine("อบ " + outName.PadRight(12) + " จาก " + src.name.PadRight(26)
                             + " ยาว " + src.length.ToString("F2") + " วิ  " + (frames + 1) + " คีย์"
                             + (loop ? "  (วนลูป)" : ""));
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            Object.DestroyImmediate(rig);

            // Back to Generic, which is what the cutscene poses need. The avatar
            // stays: without one the mesh sits in its bind pose whatever plays.
            asherImporter.animationType = ModelImporterAnimationType.Generic;
            asherImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            AssetDatabase.WriteImportSettingsIfDirty(AsherPath);
            AssetDatabase.ImportAsset(AsherPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        var genericAvatar = AssetDatabase.LoadAllAssetsAtPath(AsherPath).OfType<Avatar>().FirstOrDefault();
        log.AppendLine("Asher กลับเป็น Generic: avatar " + (genericAvatar != null ? genericAvatar.name : "ไม่มี"));

        AssetDatabase.SaveAssets();
        log.Append(BuildAsherAnimator.BuildFromClips(made, genericAvatar, log));
        return log.ToString();
    }

    // Walk, run and idle have to be told to loop, or a walk plays once and
    // freezes on its last frame - which looks exactly like a broken animation.
    static void SetLooping(string path, bool loop, StringBuilder log)
    {
        var mi = AssetImporter.GetAtPath(path) as ModelImporter;
        if (mi == null) { log.AppendLine("!! หา " + path + " ไม่เจอ"); return; }

        var clips = mi.clipAnimations;
        if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
        if (clips.Length == 0) return;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].loopTime == loop) continue;
            clips[i].loopTime = loop;
            changed = true;
        }

        if (!changed) return;
        mi.clipAnimations = clips;
        AssetDatabase.WriteImportSettingsIfDirty(path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        log.AppendLine("ตั้งวนลูป " + (loop ? "เปิด" : "ปิด") + " ให้ " + System.IO.Path.GetFileName(path));
    }

    // Overwrites the existing asset so anything already pointing at the clip -
    // the controller most of all - keeps pointing at it.
    static AnimationClip SaveClip(AnimationClip clip, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(clip, path);
        return clip;
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
