using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-click setup for the main character "Asher".
// Run from the menu:  Tools ▸ THE AFTER ▸ Setup Asher
//
// It will:
//   1. Configure Adventurer.fbx import (Generic rig + animations).
//   2. Build an Animator Controller (Idle / Walk driven by a "Speed" float).
//   3. Spawn Asher in the scene with CharacterController + PlayerMovement.
//   4. Add a ground plane (if missing) and a CameraFollow camera.
public static class AsherSetup
{
    const string ModelPath = "Assets/Models/Adventurer.fbx";
    const string ControllerPath = "Assets/Animations/AsherController.controller";

    [MenuItem("Tools/THE AFTER/Setup Asher")]
    public static void Setup()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (model == null)
        {
            EditorUtility.DisplayDialog("Setup Asher",
                "Could not find " + ModelPath + ".\nMake sure Adventurer.fbx is in Assets/Models.", "OK");
            return;
        }

        ConfigureModelImport();
        var controller = BuildAnimatorController();

        // Reload after possible reimport.
        model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);

        // --- Spawn Asher ---
        var existing = GameObject.Find("Asher");
        if (existing != null)
            Object.DestroyImmediate(existing);

        GameObject asher = (GameObject)PrefabUtility.InstantiatePrefab(model);
        asher.name = "Asher";
        asher.transform.position = Vector3.zero;
        asher.transform.rotation = Quaternion.identity;

        // Animator
        var animator = asher.GetComponent<Animator>();
        if (animator == null) animator = asher.AddComponent<Animator>();
        if (controller != null) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // CharacterController sized to the model
        var cc = asher.GetComponent<CharacterController>();
        if (cc == null) cc = asher.AddComponent<CharacterController>();
        var bounds = GetRenderBounds(asher);
        float height = Mathf.Max(0.2f, bounds.size.y);
        cc.height = height;
        cc.radius = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 0.4f, 0.15f, height * 0.4f);
        cc.center = new Vector3(0f, height * 0.5f, 0f);

        // Movement
        if (asher.GetComponent<PlayerMovement>() == null)
            asher.AddComponent<PlayerMovement>();

        // --- Ground ---
        if (GameObject.Find("Ground") == null)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
        }

        // --- Camera ---
        var cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
        }
        if (cam.GetComponent<CameraFollow>() == null)
            cam.gameObject.AddComponent<CameraFollow>();
        cam.transform.position = asher.transform.position + new Vector3(0f, 6f, -8f);
        cam.transform.LookAt(asher.transform.position + Vector3.up);

        Selection.activeGameObject = asher;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[AsherSetup] Done. Asher is in the scene. Press Play and use WASD to walk.");
    }

    static void ConfigureModelImport()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }
        if (!importer.importAnimation)
        {
            importer.importAnimation = true;
            changed = true;
        }
        changed |= ApplyLooping(importer);
        if (changed) importer.SaveAndReimport();
    }

    // Looping movement clips (idle/walk/run/jog/sprint) must have Loop Time on,
    // otherwise they play once and freeze on the last frame while the character
    // keeps moving. One-shot clips (attack, death, jump...) are left alone.
    [MenuItem("Tools/THE AFTER/Fix Animation Looping")]
    public static void FixAnimationLooping()
    {
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null)
        {
            EditorUtility.DisplayDialog("Fix Animation Looping",
                "Could not find " + ModelPath + ".", "OK");
            return;
        }
        if (ApplyLooping(importer))
        {
            importer.SaveAndReimport();
            Debug.Log("[AsherSetup] Loop Time enabled on idle/walk/run clips. The freeze is fixed.");
        }
        else
        {
            Debug.Log("[AsherSetup] No looping changes were needed.");
        }
    }

    static readonly string[] LoopKeywords = { "idle", "walk", "run", "jog", "sprint" };

    static bool ApplyLooping(ModelImporter importer)
    {
        // Start from existing overrides, or the auto-generated default clips.
        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
            clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            return false;

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            string n = clips[i].name.ToLower();
            bool shouldLoop = LoopKeywords.Any(k => n.Contains(k));
            if (shouldLoop && !clips[i].loopTime)
            {
                clips[i].loopTime = true;
                changed = true;
            }
        }
        if (changed)
            importer.clipAnimations = clips;
        return changed;
    }

    static AnimatorController BuildAnimatorController()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");

        // Collect real animation clips from the model.
        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();

        if (clips.Count == 0)
        {
            Debug.LogWarning("[AsherSetup] No animation clips found in the model. " +
                             "Movement will still work, but without a walk animation.");
            return null;
        }

        Debug.Log("[AsherSetup] Clips found: " + string.Join(", ", clips.Select(c => c.name)));

        AnimationClip idle = PickClip(clips, "idle") ?? clips[0];
        AnimationClip walk = PickClip(clips, "walk")
                          ?? PickClip(clips, "run")
                          ?? PickClip(clips, "jog")
                          ?? clips[clips.Count > 1 ? 1 : 0];

        Debug.Log("[AsherSetup] Idle = " + idle.name + " | Walk = " + walk.name);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        var walkState = sm.AddState("Walk");
        walkState.motion = walk;
        sm.defaultState = idleState;

        var toWalk = idleState.AddTransition(walkState);
        toWalk.hasExitTime = false;
        toWalk.duration = 0.1f;
        toWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var toIdle = walkState.AddTransition(idleState);
        toIdle.hasExitTime = false;
        toIdle.duration = 0.1f;
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    [MenuItem("Tools/THE AFTER/Add Jump To Animator")]
    public static void AddJumpToAnimator()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Add Jump",
                "AsherController not found. Run Setup Asher first.", "OK");
            return;
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();

        // This pack has no real jump clip, so fall back to Roll / Flip as a
        // dynamic stand-in (played in the air it reads as a tuck jump).
        AnimationClip jump = PickClip(clips, "jump")
                          ?? PickClip(clips, "flip")
                          ?? PickClip(clips, "roll");
        if (jump == null)
        {
            Debug.LogWarning("[AsherSetup] No jump/flip/roll clip found. Clips: " +
                             string.Join(", ", clips.Select(c => c.name)));
            EditorUtility.DisplayDialog("Add Jump",
                "No jump/flip/roll clip was found in the model.", "OK");
            return;
        }

        if (!controller.parameters.Any(p => p.name == "Jump"))
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var jumpState = sm.states.FirstOrDefault(s => s.state.name == "Jump").state;
        if (jumpState == null)
            jumpState = sm.AddState("Jump");
        jumpState.motion = jump;

        // Any State -> Jump (fires on the trigger from anywhere).
        if (!sm.anyStateTransitions.Any(t => t.destinationState == jumpState))
        {
            var toJump = sm.AddAnyStateTransition(jumpState);
            toJump.hasExitTime = false;
            toJump.duration = 0.05f;
            toJump.canTransitionToSelf = false;
            toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        }

        // Jump -> Idle once the clip has mostly played.
        var idleState = sm.states.FirstOrDefault(s => s.state.name == "Idle").state;
        if (idleState != null && !jumpState.transitions.Any(t => t.destinationState == idleState))
        {
            var toIdle = jumpState.AddTransition(idleState);
            toIdle.hasExitTime = true;
            toIdle.exitTime = 0.8f;
            toIdle.duration = 0.15f;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[AsherSetup] Jump state added (clip: " + jump.name + ").");
    }

    [MenuItem("Tools/THE AFTER/Add Sprint (Run) To Animator")]
    public static void AddSprintToAnimator()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            EditorUtility.DisplayDialog("Add Sprint",
                "AsherController not found. Run Setup Asher first.", "OK");
            return;
        }

        var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();

        // Prefer the forward "Run" clip, not Run_Back / Run_Left / etc.
        AnimationClip run = clips.FirstOrDefault(c => c.name.ToLower().EndsWith("run"))
                          ?? PickClip(clips, "sprint")
                          ?? PickClip(clips, "run");
        if (run == null)
        {
            EditorUtility.DisplayDialog("Add Sprint", "No run/sprint clip found.", "OK");
            return;
        }

        var sm = controller.layers[0].stateMachine;
        var walkState = sm.states.FirstOrDefault(s => s.state.name == "Walk").state;
        if (walkState == null)
        {
            EditorUtility.DisplayDialog("Add Sprint",
                "No Walk state found. Run Setup Asher first.", "OK");
            return;
        }

        var runState = sm.states.FirstOrDefault(s => s.state.name == "Run").state;
        if (runState == null)
            runState = sm.AddState("Run");
        runState.motion = run;

        const float threshold = 5f;  // walk ~4, sprint ~7

        if (!walkState.transitions.Any(t => t.destinationState == runState))
        {
            var toRun = walkState.AddTransition(runState);
            toRun.hasExitTime = false;
            toRun.duration = 0.12f;
            toRun.AddCondition(AnimatorConditionMode.Greater, threshold, "Speed");
        }
        if (!runState.transitions.Any(t => t.destinationState == walkState))
        {
            var toWalk = runState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0.12f;
            toWalk.AddCondition(AnimatorConditionMode.Less, threshold, "Speed");
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[AsherSetup] Sprint added (Run clip: " + run.name +
                  ", Walk<->Run threshold Speed=" + threshold + ").");
    }

    [MenuItem("Tools/THE AFTER/Build Main Menu Scene")]
    public static void BuildMainMenuScene()
    {
        const string menuPath = "Assets/Scenes/MainMenu.unity";
        const string gamePath = "Assets/Scenes/Sandbox.unity";

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var mgr = new GameObject("MenuManager");
        mgr.AddComponent<MainMenuUI>();

        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.09f, 0.08f, 1f);
        }

        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(scene, menuPath);

        // Build Settings: MainMenu = 0, Sandbox = 1.
        var list = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(menuPath, true)
        };
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(gamePath) != null)
            list.Add(new EditorBuildSettingsScene(gamePath, true));
        EditorBuildSettings.scenes = list.ToArray();

        Debug.Log("[AsherSetup] MainMenu built & set as startup scene (index 0). " +
                  "Press Play here to see the menu; PLAY loads Sandbox.");
    }

    const string SuitPath = "Assets/Models/Male_Suit.fbx";
    const string LoganControllerPath = "Assets/Animations/LoganController.controller";

    [MenuItem("Tools/THE AFTER/Setup Logan NPC")]
    public static void SetupLogan()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(SuitPath) == null)
        {
            EditorUtility.DisplayDialog("Setup Logan",
                "Male_Suit.fbx not found in Assets/Models.", "OK");
            return;
        }

        // Work in the game scene.
        if (EditorSceneManager.GetActiveScene().path != "Assets/Scenes/Sandbox.unity")
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity", OpenSceneMode.Single);
        }

        ConfigureSuitImport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(SuitPath);
        var controller = BuildLoganController();

        var existing = GameObject.Find("Logan");
        if (existing != null) Object.DestroyImmediate(existing);

        GameObject logan = (GameObject)PrefabUtility.InstantiatePrefab(model);
        logan.name = "Logan";
        logan.transform.position = new Vector3(2.5f, 0f, 1.5f);
        logan.transform.rotation = Quaternion.Euler(0f, 200f, 0f);

        var animator = logan.GetComponent<Animator>();
        if (animator == null) animator = logan.AddComponent<Animator>();
        if (controller != null) animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;

        // Scale Logan to match Asher's height (the suit model imports larger).
        float loganH = Mathf.Max(0.2f, GetRenderBounds(logan).size.y);
        float targetH = 1.88f;
        var asherGo = GameObject.Find("Asher");
        var asherCC = asherGo != null ? asherGo.GetComponent<CharacterController>() : null;
        if (asherCC != null) targetH = asherCC.height;
        float scale = targetH / loganH;
        logan.transform.localScale = Vector3.one * scale;

        // Collider in LOCAL units (transform scale brings it to world height).
        var cap = logan.GetComponent<CapsuleCollider>();
        if (cap == null) cap = logan.AddComponent<CapsuleCollider>();
        cap.height = loganH; cap.center = new Vector3(0f, loganH * 0.5f, 0f);
        cap.radius = loganH * 0.18f;

        if (logan.GetComponent<NPCInteractable>() == null)
            logan.AddComponent<NPCInteractable>();

        var asher = GameObject.Find("Asher");
        if (asher != null && asher.GetComponent<PlayerInteractor>() == null)
            asher.AddComponent<PlayerInteractor>();

        if (Object.FindAnyObjectByType<DialogueManager>() == null)
            new GameObject("DialogueManager", typeof(DialogueManager));

        Selection.activeGameObject = logan;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[AsherSetup] Logan is in the scene. Play, walk up to him and press E.");
    }

    static void ConfigureSuitImport()
    {
        var importer = AssetImporter.GetAtPath(SuitPath) as ModelImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Generic)
        { importer.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (!importer.importAnimation) { importer.importAnimation = true; changed = true; }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0) clips = importer.defaultClipAnimations;
        string[] kw = { "idle", "walk", "run", "wave", "interact", "talk" };
        bool clipChanged = false;
        if (clips != null)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                string n = clips[i].name.ToLower();
                if (!clips[i].loopTime && kw.Any(k => n.Contains(k)))
                { clips[i].loopTime = true; clipChanged = true; }
            }
            if (clipChanged) { importer.clipAnimations = clips; changed = true; }
        }
        if (changed) importer.SaveAndReimport();
    }

    static AnimatorController BuildLoganController()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations"))
            AssetDatabase.CreateFolder("Assets", "Animations");

        var clips = AssetDatabase.LoadAllAssetsAtPath(SuitPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToList();
        if (clips.Count == 0)
        {
            Debug.LogWarning("[AsherSetup] Male_Suit has no animation clips.");
            return null;
        }
        Debug.Log("[AsherSetup] Logan clips: " + string.Join(", ", clips.Select(c => c.name)));

        AnimationClip idle = PickClip(clips, "idle") ?? clips[0];
        AnimationClip talk = PickClip(clips, "talk")
                          ?? PickClip(clips, "wave")
                          ?? PickClip(clips, "interact")
                          ?? PickClip(clips, "idle_neutral")
                          ?? idle;
        Debug.Log("[AsherSetup] Logan Idle = " + idle.name + " | Talk = " + talk.name);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(LoganControllerPath);
        controller.AddParameter("Talking", AnimatorControllerParameterType.Bool);
        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        var talkState = sm.AddState("Talk");
        talkState.motion = talk;
        sm.defaultState = idleState;

        var toTalk = idleState.AddTransition(talkState);
        toTalk.hasExitTime = false; toTalk.duration = 0.15f;
        toTalk.AddCondition(AnimatorConditionMode.If, 0f, "Talking");

        var toIdle = talkState.AddTransition(idleState);
        toIdle.hasExitTime = false; toIdle.duration = 0.15f;
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Talking");

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static AnimationClip PickClip(List<AnimationClip> clips, string keyword)
    {
        return clips.FirstOrDefault(c => c.name.ToLower().Contains(keyword));
    }

    static Bounds GetRenderBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position + Vector3.up, new Vector3(0.5f, 1.8f, 0.5f));
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }
}
