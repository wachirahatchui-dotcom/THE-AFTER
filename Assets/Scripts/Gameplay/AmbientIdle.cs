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

    // The bones that make the difference between somebody alive and a bust on a
    // plinth. A chest that rises with nothing else moving reads as a breathing
    // statue; the shoulders have to carry the breath up, the neck has to bend
    // where the head turns, and the wrists have to be doing something or the
    // hands hang off the arms like gloves.
    public Transform neck, clavicleL, clavicleR, handL, handR, hip;

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

    [Header("Shoulders, neck and wrists")]
    [Tooltip("How far the shoulders ride up on the breath.")]
    public float shoulderBreathDegrees = 1.8f;

    [Tooltip("How much of the head's turn the neck takes. The head alone swivelling on a fixed neck is the classic doll look.")]
    [Range(0f, 1f)] public float neckShare = 0.4f;

    [Tooltip("Wrist movement. Small - hands read as busy long before the numbers get large.")]
    public float wristDegrees = 7f;

    [Header("Beats")]
    // Waves alone never stop looking like waves. However many of them are
    // stacked, the motion stays smooth and evenly spread, and a viewer reads
    // that as a loop within a few seconds even when it never actually repeats.
    //
    // What breaks it is something happening: a shoulder rolled out, weight
    // dumped onto the other foot, a pause. Those are events with a beginning and
    // an end, and they are why a person standing still does not look animated.
    [Tooltip("Seconds between one shoulder rolling out. Range - it is picked fresh each time.")]
    public Vector2 shoulderRollEvery = new Vector2(11f, 24f);
    public float shoulderRollDegrees = 6f;
    public float shoulderRollSeconds = 1.5f;

    [Tooltip("Seconds between shifting weight onto the other foot.")]
    public Vector2 weightShiftEvery = new Vector2(7f, 16f);
    public float weightShiftDegrees = 2.4f;
    public float weightShiftSeconds = 1.1f;

    [Header("Variation")]
    [Tooltip("Scales this character's tempo. Left at 0 a value is picked per character, so four people in one room are not one animation played four times.")]
    public float tempo = 0f;

    Quaternion chestRest, waistRest, headRest;
    Quaternion upLRest, loLRest, upRRest, loRRest;
    Quaternion neckRest, clavLRest, clavRRest, handLRest, handRRest, hipRest;
    bool ready;

    float phase;
    float glanceAt, glanceFrom, glanceTo, glanceSince;

    // Each beat is a timer, a target and an eased ride between the two.
    float rollAt, rollFrom, rollTo, rollSince;
    float shiftAt, shiftFrom, shiftTo, shiftSince;

    const float Tau = Mathf.PI * 2f;

    void OnEnable()
    {
        Bind();
        Capture();

        // Offset per character, so a room full of people does not breathe in step.
        phase = Random.Range(0f, 90f);

        glanceFrom = glanceTo = 0f;
        glanceSince = Time.time;
        glanceAt = Time.time + Random.Range(glanceEvery.x, glanceEvery.y);

        // Beats start already scheduled and part-way through their first wait,
        // so a room full of people does not roll its shoulders in unison the
        // moment the stage switches on.
        rollSince = shiftSince = Time.time;
        rollAt = Time.time + Random.Range(0f, shoulderRollEvery.y);
        shiftAt = Time.time + Random.Range(0f, weightShiftEvery.y);
        shiftTo = Random.value < 0.5f ? 1f : -1f;

        if (tempo <= 0f) tempo = Random.Range(0.82f, 1.22f);
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

        // Optional: a rig without them still works, it just moves less.
        if (neck == null) neck = Find("NeckTwist01") ?? Find("Neck");
        if (clavicleL == null) clavicleL = Find("L_Clavicle");
        if (clavicleR == null) clavicleR = Find("R_Clavicle");
        if (handL == null) handL = Find("L_Hand");
        if (handR == null) handR = Find("R_Hand");
        if (hip == null) hip = Find("Hip") ?? Find("Pelvis");
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

        if (neck != null) neckRest = neck.localRotation;
        if (clavicleL != null) clavLRest = clavicleL.localRotation;
        if (clavicleR != null) clavRRest = clavicleR.localRotation;
        if (handL != null) handLRest = handL.localRotation;
        if (handR != null) handRRest = handR.localRotation;
        if (hip != null) hipRest = hip.localRotation;

        ready = chest != null || waist != null || head != null;
    }

    /// A wave that does not repeat on any timescale anybody watches.
    ///
    /// One sine is a loop, and reads as one within about three seconds however
    /// slow it is. Three sines whose periods are in irrational ratios sum to
    /// something that wanders instead: the pattern only comes back around after
    /// a time nobody is standing there for. Costs two extra sines a frame.
    static float Wave(float t, float period, float seed)
    {
        float w = Mathf.Max(0.05f, period);
        return Mathf.Sin((t / w + seed) * Tau) * 0.62f
             + Mathf.Sin((t / (w * 0.6180f) + seed * 1.7f) * Tau) * 0.26f
             + Mathf.Sin((t / (w * 2.7183f) + seed * 0.4f) * Tau) * 0.12f;
    }

    /// Eased 0..1 through a beat that started at `since`.
    static float Beat(float since, float length)
    {
        float k = Mathf.Clamp01((Time.time - since) / Mathf.Max(0.05f, length));
        return k * k * (3f - 2f * k);
    }

    void LateUpdate()
    {
        if (!ready) return;
        float t = (Time.time + phase) * (tempo > 0f ? tempo : 1f);

        Beats();

        Breathe(t);
        Sway(t);
        MoveHead(t);
        Shoulders(t);

        if (mood == Mood.Working) Hands(t);
        else if (mood == Mood.Talking) Gesture(t);
        else RestoreArms();

        Wrists(t);
    }

    /// The scheduled, discrete movements - the ones with a start and a finish.
    void Beats()
    {
        if (Time.time >= rollAt)
        {
            rollFrom = rollTo;
            // Out and back rather than out and stay: a shoulder that rolls and
            // holds is a shrug, and a permanent shrug is a posture change.
            rollTo = Mathf.Approximately(rollTo, 0f) ? (Random.value < 0.5f ? 1f : -1f) : 0f;
            rollSince = Time.time;
            rollAt = Time.time + (Mathf.Approximately(rollTo, 0f)
                ? Random.Range(shoulderRollEvery.x, shoulderRollEvery.y)
                : shoulderRollSeconds);
        }

        if (Time.time >= shiftAt)
        {
            shiftFrom = shiftTo;
            shiftTo = -shiftTo;          // onto the other foot
            shiftSince = Time.time;
            shiftAt = Time.time + Random.Range(weightShiftEvery.x, weightShiftEvery.y);
        }
    }

    float Roll { get { return Mathf.Lerp(rollFrom, rollTo, Beat(rollSince, shoulderRollSeconds)); } }
    float Weight { get { return Mathf.Lerp(shiftFrom, shiftTo, Beat(shiftSince, weightShiftSeconds)); } }

    void Breathe(float t)
    {
        if (chest == null) return;
        float breath = Wave(t, 60f / Mathf.Max(1f, breathsPerMinute), 0.13f);
        chest.localRotation = Quaternion.AngleAxis(breath * breathDegrees, Right(chest)) * chestRest;
    }

    void Sway(float t)
    {
        // The wave is the drift; the beat is the decision. Together they read as
        // somebody standing rather than somebody oscillating.
        float sway = Wave(t, swaySeconds, 0.47f) * swayDegrees + Weight * weightShiftDegrees;

        if (waist != null)
            waist.localRotation = Quaternion.AngleAxis(sway, Forward(waist)) * waistRest;

        // The hips take the weight and the shoulders lean back the other way,
        // which is what standing on one leg actually looks like from the front.
        if (hip != null)
            hip.localRotation = Quaternion.AngleAxis(-Weight * weightShiftDegrees * 0.8f, Forward(hip)) * hipRest;
    }

    /// Shoulders ride the breath, and roll out on their own beat.
    void Shoulders(float t)
    {
        float breath = Wave(t, 60f / Mathf.Max(1f, breathsPerMinute), 0.13f) * shoulderBreathDegrees;
        float roll = Roll * shoulderRollDegrees;

        // Only one shoulder rolls at a time - both together is a shrug, which
        // means something the scene has not asked for.
        Turn(clavicleL, clavLRest, Forward(clavicleL), breath + Mathf.Max(0f, roll));
        Turn(clavicleR, clavRRest, Forward(clavicleR), -breath - Mathf.Max(0f, -roll));
    }

    /// Wrists. Barely moving, and the difference between hands and gloves.
    void Wrists(float t)
    {
        if (mood == Mood.Working)
        {
            Turn(handL, handLRest, Right(handL), Wave(t, handWorkSeconds * 0.8f, 0.9f) * wristDegrees);
            Turn(handR, handRRest, Right(handR), Wave(t, handWorkSeconds * 0.93f, 2.2f) * wristDegrees);
        }
        else if (mood == Mood.Talking)
        {
            // Leading the arm rather than following it: a hand turns over before
            // the elbow finishes moving, which is most of what makes a gesture
            // look meant.
            Turn(handL, handLRest, Up(handL), Wave(t + 0.18f, gestureSeconds, 1.4f) * wristDegrees * 1.3f);
            Turn(handR, handRRest, Up(handR), -Wave(t + 0.18f, gestureSeconds, 1.4f) * wristDegrees * 1.3f);
        }
        else
        {
            Turn(handL, handLRest, Right(handL), Wave(t, 7.5f, 3.1f) * wristDegrees * 0.35f);
            Turn(handR, handRRest, Right(handR), Wave(t, 8.9f, 1.9f) * wristDegrees * 0.35f);
        }
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

        float drift = Wave(t, driftSeconds, 0.31f) * driftDegrees;
        float tilt = mood == Mood.Working ? workHeadTilt + drift : drift * 0.5f;

        // A small roll of the head, off its own wave. Two axes moving is a head;
        // one axis moving is a turret.
        float roll = Wave(t, driftSeconds * 1.63f, 2.7f) * driftDegrees * 0.45f;

        // The neck takes a share of the turn so the whole column bends. Without
        // it the head swivels on a fixed neck, which is the single clearest tell
        // that nothing here is animated.
        float neckTurn = neck != null ? turn * neckShare : 0f;
        if (neck != null)
            neck.localRotation = Quaternion.AngleAxis(neckTurn, Up(neck))
                               * Quaternion.AngleAxis(tilt * 0.35f, Right(neck))
                               * neckRest;

        head.localRotation = Quaternion.AngleAxis(turn - neckTurn, Up(head))
                           * Quaternion.AngleAxis(tilt * (neck != null ? 0.75f : 1f), Right(head))
                           * Quaternion.AngleAxis(roll, Forward(head))
                           * headRest;
    }

    void Hands(float t)
    {
        // The hands do not work in step: one holds while the other does the
        // fiddly part, and they trade off. Different periods read as that
        // without having to decide which hand is doing which.
        float a = Wave(t, handWorkSeconds, 0.0f);
        float b = Wave(t, handWorkSeconds * 1.37f, 1.1f);

        Turn(upperArmL, upLRest, Right(upperArmL), a * handWorkDegrees);
        Turn(lowerArmL, loLRest, Right(lowerArmL), (a * 0.5f + 0.5f) * elbowWorkDegrees);

        Turn(upperArmR, upRRest, Right(upperArmR), b * handWorkDegrees);
        Turn(lowerArmR, loRRest, Right(lowerArmR), (b * 0.5f + 0.5f) * elbowWorkDegrees);
    }

    void Gesture(float t)
    {
        float g = Wave(t, gestureSeconds, 0.0f);

        // Opposite phase, so the arms alternate instead of flapping together,
        // and a slower second wave underneath so the whole gesture wanders in
        // size the way talking hands do rather than beating out a tempo.
        float size = 0.7f + 0.3f * Wave(t, gestureSeconds * 3.4f, 1.9f);

        Turn(upperArmL, upLRest, Right(upperArmL), g * gestureDegrees * size);
        Turn(lowerArmL, loLRest, Right(lowerArmL), Mathf.Abs(g) * gestureDegrees * 0.7f * size);
        Turn(upperArmR, upRRest, Right(upperArmR), -g * gestureDegrees * size);
        Turn(lowerArmR, loRRest, Right(lowerArmR), Mathf.Abs(g) * gestureDegrees * 0.7f * size);
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
