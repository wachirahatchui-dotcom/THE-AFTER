using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Asher's movement: WASD to walk, Shift to run, Space to jump.
//
// Written against the new Input System (Keyboard.current), and driving a
// CharacterController rather than physics, because a character pushed around by
// rigidbodies is a character that gets stuck on the scenery.
//
// The feel comes from four things that are easy to leave out and obvious when
// missing: speed ramps rather than snapping, the body turns towards where it is
// going instead of pivoting on the spot, a jump pressed a moment early still
// fires, and a jump pressed a moment after walking off an edge still counts.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Speed")]
    public float moveSpeed = 4f;
    public float sprintSpeed = 7f;

    [Tooltip("How fast he reaches full speed. Lower is heavier.")]
    public float acceleration = 26f;

    [Tooltip("How fast he stops. Higher than acceleration reads as deliberate.")]
    public float deceleration = 34f;

    [Tooltip("Seconds to turn towards the way he is moving. 0 pivots instantly.")]
    public float turnSmoothTime = 0.07f;

    [Tooltip("Off in first person, where the camera owns which way he faces. Two things steering one rotation fight every frame.")]
    public bool turnTowardsMovement = true;

    [Header("Jump")]
    public float jumpHeight = 1.4f;
    public float gravity = -24f;

    [Tooltip("Extra gravity on the way down, so the arc does not feel floaty.")]
    public float fallMultiplier = 1.5f;

    [Tooltip("A jump still fires this long after walking off an edge.")]
    public float coyoteTime = 0.12f;

    [Tooltip("A jump pressed this long before landing still fires on landing.")]
    public float jumpBuffer = 0.12f;

    [Tooltip("Held against the floor while grounded, so slopes do not bounce.")]
    public float groundStick = -3f;

    [Header("Audio")]
    [Tooltip("Seconds between footsteps at walking pace.")]
    public float walkStepInterval = 0.5f;

    [Tooltip("Seconds between footsteps at a run.")]
    public float runStepInterval = 0.32f;

    [Range(0f, 3f)] public float footstepVolume = 1.2f;
    [Range(0f, 3f)] public float jumpVolume = 1f;
    [Range(0f, 3f)] public float landVolume = 1f;

    [Header("Animation")]
    [Tooltip("Float parameter fed the current speed in metres per second.")]
    public string speedParameter = "Speed";

    [Tooltip("Trigger fired the moment he leaves the ground.")]
    public string jumpParameter = "Jump";

    [Tooltip("Bool held true while his feet are on something. Optional.")]
    public string groundedParameter = "Grounded";

    /// Where he is going this frame, in metres per second.
    public Vector3 Velocity => new Vector3(horizontal.x, verticalVelocity, horizontal.z);

    public bool IsGrounded { get; private set; }

    CharacterController controller;
    Animator animator;
    Transform cam;

    Vector3 horizontal;          // ground-plane velocity, metres per second
    float verticalVelocity;
    float turnVelocity;          // scratch for SmoothDampAngle

    float lastGroundedAt = -99f;
    float jumpPressedAt = -99f;
    bool wasGrounded = true;

    AudioSource audioSource;
    AudioClip[] footsteps;
    AudioClip jumpClip, landClip;
    float stepTimer;

    int speedHash, jumpHash, groundedHash;
    bool hasGroundedParam;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();

        speedHash = Animator.StringToHash(speedParameter);
        jumpHash = Animator.StringToHash(jumpParameter);
        groundedHash = Animator.StringToHash(groundedParameter);

        if (animator != null)
            foreach (var p in animator.parameters)
                if (p.nameHash == groundedHash) hasGroundedParam = true;

        SetupAudio();
    }

    void OnEnable()
    {
        // Found again on enable rather than cached once: the cutscenes hand
        // control back after changing which camera is live.
        if (Camera.main != null) cam = Camera.main.transform;
    }

    // Builds the AudioSource and loads footstep / jump / land clips from
    // Assets/Resources/SFX by name, so nothing has to be wired in the Inspector.
    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;   // his own footsteps, heard from inside his head

        var steps = new List<AudioClip>();
        foreach (var clip in Resources.LoadAll<AudioClip>("SFX"))
        {
            string n = clip.name.ToLowerInvariant();
            if (n.Contains("footstep") || n.Contains("step")) steps.Add(clip);
            else if (n.Contains("jump")) jumpClip = clip;
            else if (n.Contains("land")) landClip = clip;
        }
        footsteps = steps.ToArray();
    }

    void Update()
    {
        if (controller == null || !controller.enabled) return;

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        var kb = Keyboard.current;
        bool frozen = DialogueManager.IsActive || PauseMenuUI.IsPaused;

        Vector2 stick = Vector2.zero;
        bool sprinting = false;

        if (kb != null && !frozen)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) stick.y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) stick.y -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) stick.x += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) stick.x -= 1f;
            if (stick.sqrMagnitude > 1f) stick.Normalize();

            sprinting = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.spaceKey.wasPressedThisFrame) jumpPressedAt = Time.time;
        }

        Move(stick, sprinting, dt);
        Fall(dt);

        controller.Move(new Vector3(horizontal.x, verticalVelocity, horizontal.z) * dt);

        IsGrounded = controller.isGrounded;
        if (IsGrounded) lastGroundedAt = Time.time;

        Footsteps(sprinting, dt);
        Animate();
        wasGrounded = IsGrounded;
    }

    // Ground movement, stated relative to the camera so "forward" means "away
    // from the viewer" rather than some fixed compass direction.
    void Move(Vector2 stick, bool sprinting, float dt)
    {
        Vector3 wanted = Vector3.zero;

        if (stick.sqrMagnitude > 0.0001f)
        {
            Vector3 forward = Vector3.forward, right = Vector3.right;
            if (cam != null)
            {
                forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            }

            Vector3 dir = (forward * stick.y + right * stick.x).normalized;
            wanted = dir * (sprinting ? sprintSpeed : moveSpeed);

            // Turn towards where he is heading. Smoothed, because a character who
            // changes facing in one frame reads as a sprite rather than a body.
            //
            // Skipped in first person: there the mouse decides which way he faces
            // and strafing should slide him sideways, not spin him to face that
            // way. Leaving this on would also fight the camera for the rotation.
            if (turnTowardsMovement)
            {
                float target = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, target,
                                                    ref turnVelocity, turnSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }

        float rate = wanted.sqrMagnitude > horizontal.sqrMagnitude ? acceleration : deceleration;
        horizontal = Vector3.MoveTowards(horizontal, wanted, rate * dt);
    }

    void Fall(float dt)
    {
        bool grounded = controller.isGrounded;

        // Held down rather than zeroed: a controller with no downward velocity
        // steps off every small rise and reports itself airborne on slopes.
        if (grounded && verticalVelocity < 0f) verticalVelocity = groundStick;

        bool canJump = grounded || Time.time - lastGroundedAt <= coyoteTime;
        bool wantsJump = Time.time - jumpPressedAt <= jumpBuffer;

        if (canJump && wantsJump)
        {
            // v = sqrt(2 * h * g): reaches exactly jumpHeight at the top.
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * Mathf.Abs(gravity));
            jumpPressedAt = -99f;
            lastGroundedAt = -99f;

            if (animator != null) animator.SetTrigger(jumpHash);
            if (audioSource != null && jumpClip != null)
            {
                audioSource.pitch = 1f;
                audioSource.PlayOneShot(jumpClip, jumpVolume);
            }
        }
        else
        {
            float g = gravity * (verticalVelocity < 0f ? fallMultiplier : 1f);
            verticalVelocity += g * dt;
        }
    }

    void Footsteps(bool sprinting, float dt)
    {
        bool moving = horizontal.magnitude > 0.4f;

        if (IsGrounded && moving)
        {
            stepTimer -= dt;
            if (stepTimer <= 0f)
            {
                PlayFootstep();
                stepTimer = sprinting ? runStepInterval : walkStepInterval;
            }
        }
        else stepTimer = 0f;   // the next step lands promptly once he moves again

        if (!wasGrounded && IsGrounded && audioSource != null && landClip != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(landClip, landVolume);
        }
    }

    void PlayFootstep()
    {
        if (audioSource == null || footsteps == null || footsteps.Length == 0) return;
        var clip = footsteps[Random.Range(0, footsteps.Length)];
        audioSource.pitch = Random.Range(0.92f, 1.08f);   // varied, so it is not a metronome
        audioSource.PlayOneShot(clip, footstepVolume);
    }

    void Animate()
    {
        if (animator == null) return;
        animator.SetFloat(speedHash, horizontal.magnitude);
        if (hasGroundedParam) animator.SetBool(groundedHash, IsGrounded);
    }
}
