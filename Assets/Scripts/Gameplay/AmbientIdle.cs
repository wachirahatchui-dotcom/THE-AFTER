using UnityEngine;

// Keeps a standing character alive while the player walks around them.
//
// The same problem SeatedIdle solves, one room along: these rigs have no
// Animator controller behind them, so anybody not being driven by a Timeline is
// a statue. A workshop full of statues reads as a bug long before it reads as a
// quiet moment.
//
// What it is not: a replacement for animation. It layers small movement over
// whatever pose the bones are already in, so the pose has to be right first -
// this motion on somebody leaning over a bench looks like working, and the very
// same motion on somebody standing to attention looks like twitching.
//
// Two things carried over from SeatedIdle, because they are what make it safe:
//
//   * The rest pose is read when the component switches on, so it adds to the
//     authored pose rather than replacing it.
//   * Rotations are applied about axes taken from the character's own facing,
//     never the bone's local axes. A rig converted out of Blender can have any
//     bone pointing any way: "turn about his up" survives that, "rotate about
//     local Y" means something different on every bone.
//
// Runs in LateUpdate so it wins over an Animator, and must be switched off while
// a Timeline drives the same bones, or the two fight over every frame.
public class AmbientIdle : MonoBehaviour
{
    public enum Mood
    {
        Breathing,   // alive, and not much else
        Working,     // hands busy at a bench, head down on the job
        Talking,     // turned to somebody, gesturing
        Listening,   // turned to somebody, nodding along
    }

    [Header("What they are doing")]
    public Mood mood = Mood.Breathing;

    [Tooltip("Who they are turned towards. Talking and Listening use it; the others ignore it.")]
    public Transform lookAt;

    [Header("Bones (found by name when left empty)")]
    public Transform frame;
    public Transform chest, waist, head;
    public Transform upperArmL, lowerArmL, upperArmR, lowerArmR;

    [Header("Breath")]
    public float breathDegrees = 1.5f;
    public float breathsPerMinute = 13f;

    [Header("Weight")]
    [Tooltip("A slow lean, as somebody standing shifts from one foot to the other.")]
    public float swayDegrees = 1.3f;
    public float swaySeconds = 9f;

    [Header("Head")]
    public float driftDegrees = 2f;
    public float driftSeconds = 6f;

    [Tooltip("How far the head turns when it glances somewhere else.")]
    public float glanceDegrees = 11f;
    public Vector2 glanceEvery = new Vector2(4f, 9f);
    public float glanceSeconds = 1.3f;

    [Tooltip("How far the head tips down over the work, in Working.")]
    public float workHeadTilt = 14f;

    [Header("Hands (Working only)")]
    public float handWorkDegrees = 5f;
    public float elbowWorkDegrees = 9f;

    [Tooltip("Seconds for one full back-and-forth of the working hands.")]
    public float handWorkSeconds = 1.6f;

    [Header("Gesture (Talking only)")]
    public float gestureDegrees = 9f;
    public float gestureSeconds = 2.2f;

    Quaternion chestRest, waistRest, headRest;
    Quaternion upLRest, loLRest, upRRest, loRRest;
    bool ready;

    float phase;
    float glanceAt, glanceFrom, glanceTo, glanceSince;

    void OnEnable()
    {
        Bind();
        Capture();

        // Offset per character, so a room full of people does not breathe in step.
        phase = Random.Range(0f, 90f);

        glanceFrom = glanceTo = 0f;
        glanceSince = Time.time;
        glanceAt = Time.time + Random.Range(glanceEvery.x, glanceEvery.y);
    }

    void Bind()
    {
        if (frame == null) frame = transform;
        if (chest == null) chest = Find("Spine02") ?? Find("Spine01") ?? Find("Chest");
        if (waist == null) waist = Find("Waist") ?? Find("Spine") ?? Find("Hips");
        if (head == null) head = Find("Head");
        if (upperArmL == null) upperArmL = Find("L_Upperarm");
        if (lowerArmL == null) lowerArmL = Find("L_Forearm");
        if (upperArmR == null) upperArmR = Find("R_Upperarm");
        if (lowerArmR == null) lowerArmR = Find("R_Forearm");
    }

    /// Reads the current pose as the one to move around. Call it again after
    /// anything else has re-posed the character.
    public void Capture()
    {
        if (chest != null) chestRest = chest.localRotation;
        if (waist != null) waistRest = waist.localRotation;
        if (head != null) headRest = head.localRotation;
        if (upperArmL != null) upLRest = upperArmL.localRotation;
        if (lowerArmL != null) loLRest = lowerArmL.localRotation;
        if (upperArmR != null) upRRest = upperArmR.localRotation;
        if (lowerArmR != null) loRRest = lowerArmR.localRotation;

        ready = chest != null || waist != null || head != null;
    }

    void LateUpdate()
    {
        if (!ready) return;
        float t = Time.time + phase;

        Breathe(t);
        Sway(t);
        MoveHead(t);

        if (mood == Mood.Working) Hands(t);
        else if (mood == Mood.Talking) Gesture(t);
        else RestoreArms();
    }

    void Breathe(float t)
    {
        if (chest == null) return;
        float breath = Mathf.Sin(t * Mathf.PI * 2f * (breathsPerMinute / 60f));
        chest.localRotation = Quaternion.AngleAxis(breath * breathDegrees, Right(chest)) * chestRest;
    }

    void Sway(float t)
    {
        if (waist == null) return;
        float sway = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, swaySeconds));
        waist.localRotation = Quaternion.AngleAxis(sway * swayDegrees, Forward(waist)) * waistRest;
    }

    void MoveHead(float t)
    {
        if (head == null) return;

        float turn;

        if ((mood == Mood.Talking || mood == Mood.Listening) && lookAt != null)
        {
            // Turned towards whoever they are with, plus a little movement so it
            // is not a stare. Measured in the character's own frame, so it holds
            // however they happen to be rotated in the room.
            Vector3 flat = Vector3.ProjectOnPlane(lookAt.position - head.position, frame.up);
            float want = Vector3.SignedAngle(frame.forward, flat, frame.up);

            // A neck only goes so far. Past this they would turn their body, and
            // that is a pose decision rather than something to fake here.
            want = Mathf.Clamp(want, -55f, 55f);

            float bob = Mathf.Sin(t * Mathf.PI * 2f / 3.1f) * (mood == Mood.Listening ? 3.5f : 2f);
            turn = want + bob;
        }
        else
        {
            if (Time.time >= glanceAt)
            {
                glanceFrom = glanceTo;
                glanceSince = Time.time;

                // Back to centre about half the time, so the head is not forever
                // swinging between two extremes.
                glanceTo = Random.value < 0.45f ? 0f : Random.Range(-glanceDegrees, glanceDegrees);
                glanceAt = Time.time + Random.Range(glanceEvery.x, glanceEvery.y);
            }

            float k = Mathf.Clamp01((Time.time - glanceSince) / Mathf.Max(0.05f, glanceSeconds));
            k = k * k * (3f - 2f * k);   // eased, so the head turns rather than snaps
            turn = Mathf.Lerp(glanceFrom, glanceTo, k);
        }

        float drift = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, driftSeconds)) * driftDegrees;
        float tilt = mood == Mood.Working ? workHeadTilt + drift : drift * 0.5f;

        head.localRotation = Quaternion.AngleAxis(turn, Up(head))
                           * Quaternion.AngleAxis(tilt, Right(head))
                           * headRest;
    }

    void Hands(float t)
    {
        // The hands do not work in step: one holds while the other does the
        // fiddly part, and they trade off. Two different periods read as that
        // without having to decide which hand is doing which.
        float a = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, handWorkSeconds));
        float b = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, handWorkSeconds * 1.37f) + 1.1f);

        Turn(upperArmL, upLRest, Right(upperArmL), a * handWorkDegrees);
        Turn(lowerArmL, loLRest, Right(lowerArmL), (a * 0.5f + 0.5f) * elbowWorkDegrees);

        Turn(upperArmR, upRRest, Right(upperArmR), b * handWorkDegrees);
        Turn(lowerArmR, loRRest, Right(lowerArmR), (b * 0.5f + 0.5f) * elbowWorkDegrees);
    }

    void Gesture(float t)
    {
        float g = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, gestureSeconds));

        // Opposite phase, so the arms alternate instead of flapping together.
        Turn(upperArmL, upLRest, Right(upperArmL), g * gestureDegrees);
        Turn(lowerArmL, loLRest, Right(lowerArmL), Mathf.Abs(g) * gestureDegrees * 0.7f);
        Turn(upperArmR, upRRest, Right(upperArmR), -g * gestureDegrees);
        Turn(lowerArmR, loRRest, Right(lowerArmR), Mathf.Abs(g) * gestureDegrees * 0.7f);
    }

    void RestoreArms()
    {
        if (upperArmL != null) upperArmL.localRotation = upLRest;
        if (lowerArmL != null) lowerArmL.localRotation = loLRest;
        if (upperArmR != null) upperArmR.localRotation = upRRest;
        if (lowerArmR != null) lowerArmR.localRotation = loRRest;
    }

    static void Turn(Transform bone, Quaternion rest, Vector3 axisInParent, float degrees)
    {
        if (bone == null) return;
        bone.localRotation = Quaternion.AngleAxis(degrees, axisInParent) * rest;
    }

    Vector3 Up(Transform b) { return InParent(b, frame != null ? frame.up : Vector3.up); }
    Vector3 Right(Transform b) { return InParent(b, frame != null ? frame.right : Vector3.right); }
    Vector3 Forward(Transform b) { return InParent(b, frame != null ? frame.forward : Vector3.forward); }

    static Vector3 InParent(Transform bone, Vector3 worldAxis)
    {
        if (bone == null) return worldAxis;
        return bone.parent == null ? worldAxis
             : bone.parent.InverseTransformDirection(worldAxis).normalized;
    }

    Transform Find(string name) { return FindDeep(transform, name); }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t)
        {
            var d = FindDeep(c, name);
            if (d != null) return d;
        }
        return null;
    }
}
