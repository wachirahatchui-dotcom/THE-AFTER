using UnityEngine;

// Put this on an NPC (e.g. Logan). Holds the lines they speak and drives
// their "Talking" animator bool while a dialogue is open.
public class NPCInteractable : MonoBehaviour
{
    // One line of a conversation, with its own speaker and its own clip.
    //
    // The older `lines` array below is a list of strings all spoken by this NPC,
    // with the audio found by matching a file in Resources/Voice against the
    // line's own text. That is enough for two lines of "come find me" and gives
    // out on a real scene: a conversation has more than one person in it, and a
    // filename cannot hold a speech long enough to be worth writing - Windows
    // stops at 260 characters and one of Baena's is longer than that on its own.
    [System.Serializable]
    public class Line
    {
        [Tooltip("Who says it. Left empty, the NPC's own name is used.")]
        public string speaker;

        [TextArea(2, 5)]
        public string text;

        [Tooltip("Spoken audio for this line. Left empty, the line is read in silence.")]
        public AudioClip voice;

        [Tooltip("Tick for a line that carries on the clip the line before it started - a long speech cut into readable pieces. The audio plays on underneath them instead of restarting at each one.")]
        public bool continuesVoice;
    }

    public string npcName = "Logan";

    [Tooltip("The conversation. Anything in here is used instead of the plain lines below, and every entry carries its own speaker and its own clip.")]
    public Line[] script;

    [TextArea(2, 4)]
    [Tooltip("The simple form: every line spoken by this NPC, audio matched by filename in Resources/Voice. Ignored when `script` has anything in it.")]
    public string[] lines =
    {
        "Hey Asher. You're finally awake.",
        "Eat something, then come find me. We have work to do."
    };

    /// Whether this NPC is using the multi-speaker form.
    public bool HasScript { get { return script != null && script.Length > 0; } }

    public int LineCount
    {
        get { return HasScript ? script.Length : (lines == null ? 0 : lines.Length); }
    }

    public string TextAt(int i)
    {
        if (HasScript) return i >= 0 && i < script.Length ? script[i].text : "...";
        return lines != null && i >= 0 && i < lines.Length ? lines[i] : "...";
    }

    public string SpeakerAt(int i)
    {
        if (!HasScript || i < 0 || i >= script.Length) return npcName;
        var who = script[i].speaker;
        return string.IsNullOrEmpty(who) ? npcName : who;
    }

    public AudioClip ClipAt(int i)
    {
        if (HasScript) return i >= 0 && i < script.Length ? script[i].voice : null;
        return lineClips != null && i >= 0 && i < lineClips.Length ? lineClips[i] : null;
    }

    /// True when this line shares the clip the line before it started.
    public bool ContinuesVoiceAt(int i)
    {
        return HasScript && i > 0 && i < script.Length && script[i].continuesVoice;
    }

    public float interactRange = 3f;

    [Header("Talking motion (procedural head/body movement while speaking)")]
    [Tooltip("How far the head nods up/down, in degrees.")]
    public float headNodAmount = 7f;
    [Tooltip("How far the head turns side to side, in degrees.")]
    public float headTurnAmount = 5f;
    [Tooltip("Overall speed of the talking head movement.")]
    public float talkSpeed = 6f;
    [Tooltip("Subtle upper-body sway, in degrees. 0 to disable.")]
    public float bodySwayAmount = 1.5f;
    [Tooltip("How far the upper arms swing while gesturing, in degrees. 0 to disable arms.")]
    public float armGestureAmount = 12f;
    [Tooltip("How far the elbows bend while gesturing, in degrees.")]
    public float elbowGestureAmount = 14f;
    [Tooltip("Gesture strength once a line finishes typing (0 = fully stop, 1 = keep gesturing).")]
    [Range(0f, 1f)] public float idleGestureLevel = 0f;
    [Tooltip("Seconds to ease the motion in / out when talking starts or stops.")]
    public float talkBlendTime = 0.2f;
    [Tooltip("Seconds to ramp gestures up/down as each spoken line starts and stops.")]
    public float speakBlendTime = 0.12f;

    // One voice clip per line (same order as `lines`). Filled automatically at
    // runtime from Assets/Resources/Voice by matching the clip name to the line
    // text, so you don't have to wire anything in the Inspector.
    [HideInInspector] public AudioClip[] lineClips;

    private Animator animator;

    // Procedural talking motion state.
    private Transform headBone, spineBone;
    private Transform upperArmL, upperArmR, lowerArmL, lowerArmR;
    private bool isTalking;
    private float talkWeight;   // 0 = still, 1 = fully talking (eases with talkBlendTime)
    private float speakWeight;  // follows dialogue: ~1 while a line is spoken, idleGestureLevel between
    private float talkPhase;    // advancing sine phase

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        FindTalkBones();
        LoadVoiceClips();
    }

    // Locate the head (and an upper-spine) bone by name so the talking motion
    // works without wiring anything in the Inspector.
    void FindTalkBones()
    {
        var all = GetComponentsInChildren<Transform>();
        foreach (var t in all)
        {
            string n = t.name.ToLowerInvariant();
            if (headBone == null && n == "head") headBone = t;
            if (spineBone == null && (n == "spine" || n == "spine2" || n == "chest"
                || n == "torso" || n == "abdomen")) spineBone = t;
            if (upperArmL == null && IsArm(n, "upper", "l")) upperArmL = t;
            if (upperArmR == null && IsArm(n, "upper", "r")) upperArmR = t;
            if (lowerArmL == null && IsArm(n, "lower", "l")) lowerArmL = t;
            if (lowerArmR == null && IsArm(n, "lower", "r")) lowerArmR = t;
        }
        // Fallback: any bone that merely contains "head".
        if (headBone == null)
            foreach (var t in all)
                if (t.name.ToLowerInvariant().Contains("head")) { headBone = t; break; }
    }

    // Match arm bones across common naming styles: "upperarm.l", "lowerarm_r",
    // "forearm.l", "left arm", etc.
    static bool IsArm(string n, string segment, string side)
    {
        bool isSide = n.EndsWith("." + side) || n.EndsWith("_" + side) || n.EndsWith(side)
            || n.Contains(side == "l" ? "left" : "right");
        if (!isSide) return false;
        if (segment == "upper")
            return n.Contains("upperarm") || n.Contains("upper_arm") || n.Contains("uparm");
        return n.Contains("lowerarm") || n.Contains("lower_arm") || n.Contains("forearm");
    }

    // Apply the nod/sway AFTER the Animator has posed the skeleton this frame.
    void LateUpdate()
    {
        // Overall on/off while a conversation is open.
        float talkTarget = isTalking ? 1f : 0f;
        talkWeight = talkBlendTime <= 0f
            ? talkTarget
            : Mathf.MoveTowards(talkWeight, talkTarget, Time.deltaTime / talkBlendTime);

        // Gesture strength follows the dialogue: lively while the line is being
        // typed out, settling to idleGestureLevel once the text is complete.
        bool gesturing = DialogueManager.Instance != null && DialogueManager.Instance.IsTyping;
        float speakTarget = gesturing ? 1f : idleGestureLevel;
        speakWeight = speakBlendTime <= 0f
            ? speakTarget
            : Mathf.MoveTowards(speakWeight, speakTarget, Time.deltaTime / speakBlendTime);

        if (talkWeight <= 0.001f) return;

        talkPhase += Time.deltaTime * talkSpeed;
        float g = talkWeight * speakWeight;   // combined gesture intensity

        if (headBone != null)
        {
            float nod  = Mathf.Sin(talkPhase) * headNodAmount * g;
            float turn = Mathf.Sin(talkPhase * 0.6f) * headTurnAmount * g;
            headBone.localRotation *= Quaternion.Euler(nod, turn, 0f);
        }
        if (spineBone != null && bodySwayAmount > 0f)
        {
            float sway = Mathf.Sin(talkPhase * 0.5f) * bodySwayAmount * g;
            spineBone.localRotation *= Quaternion.Euler(0f, sway, 0f);
        }

        // Arms gesture in opposite phase so they alternate naturally.
        if (armGestureAmount > 0f)
        {
            float swing = Mathf.Sin(talkPhase * 0.9f);
            ApplyArm(upperArmL, lowerArmL,  swing, g);
            ApplyArm(upperArmR, lowerArmR, -swing, g);
        }
    }

    void ApplyArm(Transform upper, Transform lower, float swing, float g)
    {
        if (upper != null)
        {
            float s = swing * armGestureAmount * g;
            upper.localRotation *= Quaternion.Euler(s, s * 0.4f, s * 0.5f);
        }
        if (lower != null && elbowGestureAmount > 0f)
        {
            // Elbow bends forward on its own faster rhythm (never hyperextends).
            float bend = (Mathf.Sin(talkPhase * 1.6f) * 0.5f + 0.5f) * elbowGestureAmount * g;
            lower.localRotation *= Quaternion.Euler(bend, 0f, 0f);
        }
    }

    void LoadVoiceClips()
    {
        // A script carries its own clips, chosen in the Inspector. Nothing to
        // look up, and nothing that depends on a filename matching a sentence.
        if (HasScript) return;

        if (lines == null) return;
        lineClips = new AudioClip[lines.Length];

        var all = Resources.LoadAll<AudioClip>("Voice");
        for (int i = 0; i < lines.Length; i++)
        {
            string key = Normalize(lines[i]);
            foreach (var clip in all)
            {
                if (Normalize(clip.name) == key) { lineClips[i] = clip; break; }
            }
        }
    }

    // Strip everything except letters/digits so "Hey Asher. You're finally
    // awake." matches the file "Hey Asher. You're finally awake..mp3".
    static string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder();
        foreach (char ch in s.ToLowerInvariant())
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    public void SetTalking(bool talking)
    {
        isTalking = talking;
        if (animator != null)
            animator.SetBool("Talking", talking);
    }
}
