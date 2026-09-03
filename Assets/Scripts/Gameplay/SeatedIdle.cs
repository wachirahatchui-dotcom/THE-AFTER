using UnityEngine;

// Keeps a character alive when nothing else is driving them.
//
// Logan's Animator has no controller and no avatar: Timeline writes to his bones
// directly during a cutscene and lets go of them the moment it stops. That is
// what leaves him frozen mid-pose at the fire while the player walks over - the
// scene has ended and nothing has taken over.
//
// Authoring a looping clip is the usual answer, but this rig has no humanoid
// avatar to retarget one onto, so there is nothing to loop. Instead this adds
// the movements a person actually makes while sitting still: breath, a slow
// shift of weight, the head drifting off and coming back.
//
// Two things make it safe to layer over a cutscene's last frame:
//
//   * The rest pose is read from the bones when this switches on, so it adds to
//     wherever Timeline left them rather than snapping to a pose chosen here.
//   * Every rotation is applied about a world-derived axis, not the bone's own
//     local axes. A rig converted out of Blender can have any bone pointing any
//     way, and "rotate 14 degrees about local Y" means something different on
//     every one of them - "turn about the character's up" does not.
//
// It runs in LateUpdate so it wins over an Animator, and must be switched off
// while a Timeline is driving the same bones or the two will fight.
public class SeatedIdle : MonoBehaviour
{
    [Header("Bones (left empty, they are looked up by name)")]
    public Transform chest;
    public Transform waist;
    public Transform head;

    [Tooltip("Whose up and right the motion is measured against. The character root.")]
    public Transform frame;

    [Header("Breath")]
    [Tooltip("How far the chest tips back at the top of a breath.")]
    public float breathDegrees = 1.7f;
    public float breathsPerMinute = 12f;

    [Header("Weight")]
    [Tooltip("A slow lean side to side, as somebody sitting shifts their weight.")]
    public float swayDegrees = 1.2f;
    public float swaySeconds = 8.5f;

    [Header("Head")]
    [Tooltip("A constant small drift, so the head is never perfectly still.")]
    public float driftDegrees = 2.2f;
    public float driftSeconds = 5.5f;

    [Tooltip("Every so often he looks somewhere else - at the fire, off into the dark.")]
    public float glanceDegrees = 13f;
    public Vector2 glanceEvery = new Vector2(4.5f, 10f);
    public float glanceSeconds = 1.4f;

    Quaternion chestRest, waistRest, headRest;
    bool ready;

    float phase;
    float glanceAt, glanceFrom, glanceTo, glanceFromTime;

    void OnEnable()
    {
        Bind();
        Capture();

        // Offset per character, so two people idling in the same shot do not
        // breathe in unison like a machine.
        phase = Random.Range(0f, 60f);

        glanceFrom = glanceTo = 0f;
        glanceFromTime = Time.time;
        glanceAt = Time.time + Random.Range(glanceEvery.x, glanceEvery.y);
    }

    void Bind()
    {
        if (frame == null) frame = transform;
        if (chest == null) chest = Find("Spine02") ?? Find("Spine01") ?? Find("Chest");
        if (waist == null) waist = Find("Waist") ?? Find("Spine") ?? Find("Hips");
        if (head == null) head = Find("Head");
    }

    /// Reads the current pose as the one to move around. Call it again after
    /// anything else has repositioned the character.
    public void Capture()
    {
        if (chest != null) chestRest = chest.localRotation;
        if (waist != null) waistRest = waist.localRotation;
        if (head != null) headRest = head.localRotation;
        ready = chest != null || waist != null || head != null;
    }

    void LateUpdate()
    {
        if (!ready) return;

        float t = Time.time + phase;

        if (chest != null)
        {
            float breath = Mathf.Sin(t * Mathf.PI * 2f * (breathsPerMinute / 60f));
            Apply(chest, chestRest, Right(chest), breath * breathDegrees);
        }

        if (waist != null)
        {
            float sway = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, swaySeconds));
            Apply(waist, waistRest, Forward(waist), sway * swayDegrees);
        }

        if (head != null)
        {
            if (Time.time >= glanceAt)
            {
                glanceFrom = glanceTo;
                glanceFromTime = Time.time;

                // Back to centre about half the time, so he is not permanently
                // swinging between two extremes.
                glanceTo = Random.value < 0.45f ? 0f : Random.Range(-glanceDegrees, glanceDegrees);
                glanceAt = Time.time + Random.Range(glanceEvery.x, glanceEvery.y);
            }

            float k = Mathf.Clamp01((Time.time - glanceFromTime) / Mathf.Max(0.05f, glanceSeconds));
            k = k * k * (3f - 2f * k);   // eased, so the head turns rather than snaps
            float turn = Mathf.Lerp(glanceFrom, glanceTo, k);

            float drift = Mathf.Sin(t * Mathf.PI * 2f / Mathf.Max(0.1f, driftSeconds)) * driftDegrees;

            var rot = Quaternion.AngleAxis(turn, Up(head)) * Quaternion.AngleAxis(drift * 0.5f, Right(head));
            head.localRotation = rot * headRest;
        }
    }

    // Rotating about an axis expressed in the parent's space, pre-multiplied so
    // the bone turns in the world direction meant rather than about whichever way
    // its own axes happen to point.
    static void Apply(Transform bone, Quaternion rest, Vector3 axisInParent, float degrees)
    {
        bone.localRotation = Quaternion.AngleAxis(degrees, axisInParent) * rest;
    }

    Vector3 Up(Transform bone) => InParent(bone, frame != null ? frame.up : Vector3.up);
    Vector3 Right(Transform bone) => InParent(bone, frame != null ? frame.right : Vector3.right);
    Vector3 Forward(Transform bone) => InParent(bone, frame != null ? frame.forward : Vector3.forward);

    static Vector3 InParent(Transform bone, Vector3 worldAxis)
    {
        return bone.parent == null ? worldAxis : bone.parent.InverseTransformDirection(worldAxis).normalized;
    }

    Transform Find(string name)
    {
        return FindDeep(transform, name);
    }

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
