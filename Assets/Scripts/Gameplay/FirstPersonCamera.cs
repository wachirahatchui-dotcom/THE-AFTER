using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

// The camera while the player has the controls: behind Asher's eyes.
//
// It owns which way he faces. In first person the mouse turns the body and the
// head independently - yaw goes to the body so he walks where he looks, pitch
// stays on the camera so looking up does not tip him over. PlayerMovement is
// told to stop steering him towards his own movement, or the two would fight
// over the same rotation every frame.
//
// Cutscenes switch this off and let Cinemachine have the camera, which is what
// puts the view back outside his head for the acting.
public class FirstPersonCamera : MonoBehaviour
{
    [Header("Who")]
    [Tooltip("Asher's root. Found by the Player tag when left empty.")]
    public Transform player;

    [Header("View")]
    [Tooltip("Height of the eyes above his feet.")]
    public float eyeHeight = 1.62f;

    [Tooltip("Forward of the head's centre, so the skull is behind the lens.")]
    public float eyeForward = 0.10f;

    [Tooltip("Overridden by the game's own mouse sensitivity setting when there is one.")]
    public float sensitivity = 0.14f;

    public float minPitch = -80f;
    public float maxPitch = 80f;

    public enum BodyView
    {
        /// Nothing but a shadow. Simple, and what most shooters do.
        ShadowOnly,

        /// Arms, torso and legs are there to look down at; only the head is taken
        /// out of the way.
        SeeYourOwnBody,
    }

    [Header("Body")]
    [Tooltip("Whether you can look down and see yourself. Either way the head is never in front of the lens.")]
    public BodyView bodyView = BodyView.SeeYourOwnBody;

    [Tooltip("The camera rides the head bone, so the body stays put under the view as he walks. 0 follows it exactly and bobs hard; higher smooths the bob out.")]
    public float bobSmoothing = 12f;

    [Tooltip("Eyes above the head bone. The bone sits at the top of the neck, not at eye level, so riding it alone puts the view a head too low.")]
    public float eyeAboveHeadBone = 0.235f;

    [Tooltip("Cursor is captured while walking around and released when the view is handed back.")]
    public bool captureCursor = true;

    [Header("During a cutscene")]
    [Tooltip("How far the mouse may turn his head while a scene is playing, in degrees. The body never turns and the camera stays with the cutscene. 0 freezes him completely.")]
    [Range(0f, 30f)]
    public float cutsceneHeadLook = 9f;

    // Point the view somewhere without the player having moved the mouse.
    //
    // The yaw lives here rather than on the body: this camera writes it to
    // the player transform every frame, so anything that turns the player
    // directly is overwritten on the very next update. A teleport that sets
    // him down facing into a room has to say so here, or he arrives looking
    // at the wall he just walked through.
    public void SetYaw(float degrees)
    {
        yaw = degrees;
        if (player != null) player.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    float yaw, pitch;
    PlayerMovement movement;
    SkinnedMeshRenderer[] body;
    ShadowCastingMode[] wasCasting;
    Behaviour brain;
    bool standingDown;

    Transform headBone;
    Vector3 headScale = Vector3.one;
    bool headShrunk;
    float smoothedHeight;
    bool eyeSeeded;
    float headYaw, headPitch;

    void OnEnable()
    {
        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (player == null) { enabled = false; return; }

        movement = player.GetComponent<PlayerMovement>();

        // Pick up wherever he is already facing rather than snapping to zero.
        yaw = player.eulerAngles.y;
        pitch = 0f;

        // The camera is the one steering him now.
        if (movement != null) movement.turnTowardsMovement = false;

        // Whoever else might be steering this camera. A cutscene switches its brain
        // on, and that is the signal to keep out of the way.
        if (brain == null) brain = GetComponent("CinemachineBrain") as Behaviour;

        if (headBone == null)
            foreach (var t in player.GetComponentsInChildren<Transform>(true))
                if (t.name == "Head")
                {
                    headBone = t;

                    // Never take a collapsed head as the size to restore later. If
                    // this component was already hiding the head when it was
                    // re-enabled - a domain reload, or a disable that never ran -
                    // remembering 0.0001 would make the head gone for good.
                    headScale = t.localScale.sqrMagnitude < 0.01f ? Vector3.one : t.localScale;
                    break;
                }

        eyeSeeded = false;
        ApplyBodyView(true);
        SetCursor(true);
    }

    void OnDisable()
    {
        if (movement != null) movement.turnTowardsMovement = true;
        ApplyBodyView(false);
        SetCursor(false);
    }

    /// A little life during a cutscene, and no more than that.
    ///
    /// The body stays exactly where the scene put it and the camera belongs to
    /// Cinemachine; all the mouse gets is a few degrees of the character's own
    /// head, springing back to where the animation is looking as soon as it is let
    /// go. Enough that sitting through a conversation does not feel like being
    /// switched off, without letting the player turn away from the scene.
    void CutsceneHeadLook()
    {
        if (headBone == null || cutsceneHeadLook <= 0f) return;

        bool frozen = DialogueManager.IsActive || InventoryUI.IsOpen || PauseMenuUI.IsPaused;
        var mouse = Mouse.current;

        if (mouse != null && !frozen)
        {
            Vector2 delta = mouse.delta.ReadValue();
            headYaw = Mathf.Clamp(headYaw + delta.x * sensitivity * 0.5f, -cutsceneHeadLook, cutsceneHeadLook);
            headPitch = Mathf.Clamp(headPitch - delta.y * sensitivity * 0.5f, -cutsceneHeadLook, cutsceneHeadLook);
        }

        // Drifts back on its own, so a look never becomes a pose he is stuck in.
        float ease = 1f - Mathf.Exp(-2.5f * Time.deltaTime);
        headYaw = Mathf.Lerp(headYaw, 0f, ease);
        headPitch = Mathf.Lerp(headPitch, 0f, ease);

        // After the animation has had its say - LateUpdate is the only place this
        // addition survives the clip that just wrote the bone.
        headBone.rotation = Quaternion.AngleAxis(headYaw, Vector3.up)
                          * Quaternion.AngleAxis(headPitch, headBone.right)
                          * headBone.rotation;
    }

    void LateUpdate()
    {
        if (player == null) return;

        // A live Cinemachine brain means a cutscene owns the camera. Two scripts
        // writing one transform is a fight nobody wins, and this one would also
        // keep the head collapsed and keep overwriting which way the character
        // faces - so the actor would play the scene turned the wrong way with no
        // head. Standing down is not optional politeness; it is the whole
        // handover. It is done here rather than left to whoever remembers to
        // disable this component, because forgetting that is exactly what
        // decapitated him.
        // Two independent reasons to keep out of the way, because relying on one of
        // them is what let the body keep turning during the campfire conversation:
        // a live brain, and a cutscene saying outright that it is running. The
        // second does not care whether anybody remembered to switch this component
        // off, which is the whole point of having it.
        bool someoneElseIsDriving = (brain != null && brain.isActiveAndEnabled)
                                 || CutsceneStage1.IsPlaying
                                 || CutsceneStage3.IsPlaying;
        if (someoneElseIsDriving)
        {
            if (!standingDown)
            {
                standingDown = true;
                headYaw = headPitch = 0f;
                ApplyBodyView(false);
                SetCursor(false);
                if (movement != null) movement.turnTowardsMovement = true;
            }

            CutsceneHeadLook();
            return;
        }

        if (standingDown)
        {
            standingDown = false;

            // Pick the view back up from wherever the cutscene left him, so control
            // resumes looking the way the last shot had him looking.
            yaw = player.eulerAngles.y;
            pitch = 0f;
            eyeSeeded = false;

            if (movement != null) movement.turnTowardsMovement = false;
            ApplyBodyView(true);
            SetCursor(true);
        }

        sensitivity = GameSettings.MouseSensitivity > 0f
                    ? GameSettings.MouseSensitivity : sensitivity;

        // The mouse stops turning him while a panel is open, but the camera keeps
        // sitting where it should - a view that drifts during a conversation is
        // worse than one that simply holds still.
        bool frozen = DialogueManager.IsActive || InventoryUI.IsOpen || PauseMenuUI.IsPaused;

        var mouse = Mouse.current;
        if (mouse != null && !frozen)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
        }

        SetCursor(!frozen);

        // Yaw on the body, pitch on the camera. Putting pitch on the body would
        // tilt the whole character and send him walking into the floor.
        player.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Where the eyes are. With the body visible the camera rides the head
        // bone: a camera held at a fixed height while the body bobs underneath it
        // makes his shoulders slide up and down through the bottom of the screen.
        // Riding the bone keeps him still relative to the view, and the bob that
        // remains is the one his own walk is making.
        Vector3 eye = headBone != null && bodyView == BodyView.SeeYourOwnBody
                    ? headBone.position + Vector3.up * eyeAboveHeadBone
                    : player.position + Vector3.up * eyeHeight;

        // Smooth the bob only, and only up and down.
        //
        // Smoothing all three axes lags the camera behind by roughly speed divided
        // by bobSmoothing - a third of a metre at a walk and over half a metre at a
        // run. That reads as the body walking out from under the view, which is
        // exactly what it is. Across the ground the camera has to track him
        // frame-for-frame; it is only the up-and-down of his stride that wants
        // taking the edge off.
        if (!eyeSeeded) { smoothedHeight = eye.y; eyeSeeded = true; }
        smoothedHeight = bobSmoothing > 0f
                       ? Mathf.Lerp(smoothedHeight, eye.y, 1f - Mathf.Exp(-bobSmoothing * Time.deltaTime))
                       : eye.y;

        Vector3 at = new Vector3(eye.x, smoothedHeight, eye.z);

        Quaternion look = Quaternion.Euler(pitch, yaw, 0f);
        transform.SetPositionAndRotation(at + look * Vector3.forward * eyeForward, look);

        // Held down every frame rather than set once: an animator that ever writes
        // a scale curve would put the head back, and it would come back in the
        // middle of the screen.
        if (headShrunk && headBone != null) headBone.localScale = headScale * 0.0001f;
    }

    void ApplyBodyView(bool firstPerson)
    {
        if (player == null) return;

        if (body == null)
        {
            body = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            wasCasting = new ShadowCastingMode[body.Length];
            for (int i = 0; i < body.Length; i++) wasCasting[i] = body[i].shadowCastingMode;
        }

        bool hideWholeBody = firstPerson && bodyView == BodyView.ShadowOnly;
        for (int i = 0; i < body.Length; i++)
        {
            if (body[i] == null) continue;
            // Shadows only rather than switched off: he still throws a shadow on
            // the ground, which is most of what tells you where you are standing.
            body[i].shadowCastingMode = hideWholeBody ? ShadowCastingMode.ShadowsOnly : wasCasting[i];
        }

        // The model is one skinned mesh, so there is no head submesh to switch
        // off on its own. Collapsing the head bone does it instead: the skin
        // follows its bones, so a bone scaled to nothing takes its geometry with
        // it and leaves the rest of the body untouched.
        bool wantShrunk = firstPerson && bodyView == BodyView.SeeYourOwnBody;
        if (headBone != null && wantShrunk != headShrunk)
        {
            headBone.localScale = wantShrunk ? headScale * 0.0001f : headScale;
            headShrunk = wantShrunk;
        }
    }

    void SetCursor(bool captured)
    {
        if (!captureCursor) return;
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }
}
