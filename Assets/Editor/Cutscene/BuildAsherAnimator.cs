using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Builds Asher's animator from the baked locomotion clips.
//
// Idle, walk and run live in one blend tree on Speed rather than as three states
// with transitions between them. Speed is a continuous number coming out of
// PlayerMovement and a blend tree reads it as one: he eases from stand to walk to
// run and back without a transition ever having to fire. Three separate states
// pop at every threshold and stutter when a value sits on the boundary.
//
// The thresholds come from PlayerMovement itself, so changing his walking speed
// and rebuilding keeps the footfalls matched to the distance covered.
//
// Menu: THE AFTER > Cutscene > Build Asher Animator
public static class BuildAsherAnimator
{
    const string Menu = "THE AFTER/Cutscene/Build Asher Animator";
    const string ClipDir = "Assets/Animations/Asher";
    const string ControllerPath = "Assets/Animations/Asher/Asher.controller";
    const string AsherPath = "Assets/Models/Characters/Asher/Asher.fbx";

    public const string Speed = "Speed";
    public const string Jump = "Jump";
    public const string Grounded = "Grounded";

    [MenuItem(Menu)]
    public static void Build()
    {
        var log = new StringBuilder();
        var clips = new List<(string, AnimationClip, bool)>();
        foreach (var (n, loop) in new[] { ("Asher_Idle", true), ("Asher_Walk", true),
                                          ("Asher_Run", true), ("Asher_Jump", false) })
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipDir + "/" + n + ".anim");
            if (c != null) clips.Add((n, c, loop));
            else log.AppendLine("!! ไม่เจอ " + n);
        }

        var avatar = AssetDatabase.LoadAllAssetsAtPath(AsherPath).OfType<Avatar>().FirstOrDefault();
        log.Append(BuildFromClips(clips, avatar, log));
        Debug.Log(log.ToString());
    }

    public static string BuildFromClips(List<(string name, AnimationClip clip, bool loop)> made,
                                        Avatar avatar, StringBuilder outer)
    {
        var log = new StringBuilder();

        AnimationClip Get(string n) => made.FirstOrDefault(m => m.name == n).clip;
        var idle = Get("Asher_Idle");
        var walk = Get("Asher_Walk");
        var run = Get("Asher_Run");
        var jump = Get("Asher_Jump");

        if (idle == null || walk == null || run == null)
        {
            log.AppendLine("!! คลิป Idle/Walk/Run ไม่ครบ ไม่ได้สร้าง controller");
            return log.ToString();
        }

        float walkSpeed = 4f, runSpeed = 7f;
        var mover = Object.FindFirstObjectByType<PlayerMovement>();
        if (mover != null) { walkSpeed = mover.moveSpeed; runSpeed = mover.sprintSpeed; }

        // Rebuilt from nothing each time. Editing a controller in place leaves
        // orphaned states and stale transitions behind, invisible until something
        // misbehaves at runtime.
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        controller.AddParameter(Speed, AnimatorControllerParameterType.Float);
        controller.AddParameter(Jump, AnimatorControllerParameterType.Trigger);
        controller.AddParameter(Grounded, AnimatorControllerParameterType.Bool);

        var machine = controller.layers[0].stateMachine;

        controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = Speed;
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, walkSpeed);
        tree.AddChild(run, runSpeed);

        var locomotion = machine.states[0].state;
        locomotion.name = "Locomotion";
        machine.defaultState = locomotion;

        log.AppendLine("Locomotion: blend tree บน " + Speed
                     + " (ยืน 0 / เดิน " + walkSpeed + " / วิ่ง " + runSpeed + ")");

        if (jump != null)
        {
            var jumpState = machine.AddState("Jump");
            jumpState.motion = jump;

            // Fires the instant the trigger is set - waiting for the current clip
            // to reach an exit point would delay the leap past the take-off.
            var toJump = locomotion.AddTransition(jumpState);
            toJump.hasExitTime = false;
            toJump.duration = 0.06f;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, Jump);

            var toGround = jumpState.AddTransition(locomotion);
            toGround.hasExitTime = true;
            toGround.exitTime = 0.72f;
            toGround.duration = 0.12f;
            toGround.AddCondition(AnimatorConditionMode.If, 0f, Grounded);

            // A plain fallback, so a landing that is somehow never reported cannot
            // strand him in the jump forever.
            var fallback = jumpState.AddTransition(locomotion);
            fallback.hasExitTime = true;
            fallback.exitTime = 0.98f;
            fallback.duration = 0.12f;

            log.AppendLine("Jump: เข้าเมื่อ trigger, ออกเมื่อ Grounded (มีทางออกสำรองกันค้าง)");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        log.AppendLine("สร้าง " + ControllerPath);

        int given = 0;
        foreach (var mv in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var anim = mv.GetComponentInChildren<Animator>();
            if (anim == null) anim = Undo.AddComponent<Animator>(mv.gameObject);

            Undo.RecordObject(anim, "Give Asher his animator");

            // Without an avatar the animator cannot say which transform is which
            // bone, and the mesh sits in its bind pose whatever is playing.
            if (avatar != null) anim.avatar = avatar;
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;   // PlayerMovement does the travelling
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(anim);

            given++;
            log.AppendLine("ใส่ให้ " + mv.gameObject.name
                         + " (avatar " + (avatar != null ? avatar.name : "ไม่มี") + ")");
        }
        if (given == 0) log.AppendLine("(ยังไม่มี PlayerMovement ในซีนให้ใส่)");

        return log.ToString();
    }
}
