using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// The conversation in the garage, shot like a scene rather than played like a
// conversation.
//
// A plain dialogue leaves the player standing there with the camera on their
// own shoulder. This one takes the controls away, cuts between framed shots as
// the speaker changes, and hands everything back on black at the end.
//
// It rides DialogueManager rather than replacing it: the box, the typing, the
// NEXT and SKIP buttons and the voice playback are all already built and already
// work. What this adds is what a conversation has no opinion about - which
// camera is live, who moves when, and who is allowed to touch the controls.
//
// The shot for each line and the movement on each line are written on the lines
// themselves, in NPCInteractable.Line.shot and .cue, so the whole scene reads in
// one place in the Inspector instead of being spread between a script and a
// timeline.
public class Stage3Cutscene : MonoBehaviour
{
    [Header("Who is in it")]
    public NPCInteractable ethan;
    public Transform player;
    public Transform baena;
    public Transform sydney;

    [Header("Player control to suspend")]
    [Tooltip("Movement, interaction, the walking camera - everything switched off for the duration.")]
    public Behaviour[] playerControl;

    [Tooltip("A CharacterController is a Collider rather than a Behaviour, so it cannot ride in the array above.")]
    public CharacterController playerBody;

    [Header("Cameras")]
    [Tooltip("The shots, by name. A line names one of these in its shot field.")]
    public CinemachineCamera[] shots;

    [Tooltip("Switched on for the duration so the shots have something to feed, and off again at the end.")]
    public CinemachineBrain brain;

    [Header("Movement written into the scene")]
    [Tooltip("Where Baena ends up once he has walked over. He starts at his desk, eight metres away, which is too far for the argument to land.")]
    public Transform baenaMark;

    [Tooltip("Where Asher gets to before Ethan stops him.")]
    public Transform asherStepMark;

    [Tooltip("Where Ethan plants himself between them.")]
    public Transform ethanBlockMark;

    [Header("Timing")]
    public float walkSeconds = 2.2f;
    public float stepSeconds = 0.7f;
    public float closingFade = 1.6f;

    [Header("Walk")]
    // Asher's walk drives every one of them. The five rigs came out of the same
    // converter with the same forty-one bones under the same paths, so a clip
    // authored against one binds to all of them - checked binding by binding
    // rather than assumed. Without this a character crossing the floor slides
    // with their legs still.
    [Tooltip("Played on whoever is crossing the floor. Any clip built for these rigs works on all of them.")]
    public AnimationClip walkClip;

    [Tooltip("Steps per second the clip was authored at, used to keep the feet from skating.")]
    public float walkClipSpeed = 1.35f;

    // Idles are switched off while a character is being moved or posed, and back
    // on afterwards, so the two are never writing to the same bones at once.
    readonly List<AmbientIdle> paused = new List<AmbientIdle>();

    readonly Dictionary<string, CinemachineCamera> byName =
        new Dictionary<string, CinemachineCamera>();

    bool running;
    Vector3 baenaHome;
    Quaternion baenaHomeRot;

    void Awake()
    {
        foreach (var c in shots)
            if (c != null) byName[c.name] = c;

        // Nothing is live until the scene starts; the walking camera owns the
        // view until then.
        foreach (var c in shots) if (c != null) c.Priority = 0;
        if (brain != null) brain.enabled = false;
    }

    // Subscribed in Start rather than OnEnable: DialogueManager builds itself in
    // Awake, so by Start it is there to be hooked.
    void Start()
    {
        var dm = DialogueManager.Instance;
        if (dm != null) dm.onBegan += OnBegan;
    }

    void OnDestroy()
    {
        var dm = DialogueManager.Instance;
        if (dm != null)
        {
            dm.onBegan -= OnBegan;
            dm.onLine -= OnLine;
            dm.onClosed -= OnClosed;
        }
    }

    void OnBegan(NPCInteractable who)
    {
        // Every conversation in the game comes through here; only Ethan's is a
        // scene.
        if (who != ethan) return;
        Begin();
    }

    /// Takes the controls and puts the first shot up. Called when the player
    /// presses E on Ethan.
    public void Begin()
    {
        if (running) return;
        running = true;

        var dm = DialogueManager.Instance;
        if (dm == null) { running = false; return; }

        SetControl(false);

        if (brain != null) brain.enabled = true;

        // Baena is at his desk when the scene opens and has to be in it by the
        // time he speaks. He gets his walk during the four lines before his.
        if (baena != null)
        {
            baenaHome = baena.position;
            baenaHomeRot = baena.rotation;
        }

        dm.onLine += OnLine;
        dm.onClosed += OnClosed;
    }

    void OnLine(int index)
    {
        if (ethan == null) return;

        string shot = ethan.ShotAt(index);
        if (!string.IsNullOrEmpty(shot)) Cut(shot);

        string cue = ethan.CueAt(index);
        if (!string.IsNullOrEmpty(cue)) StartCoroutine(Play(cue));
    }

    /// Makes one camera live and every other one dormant. Cinemachine blends by
    /// priority, and the brain is set to cut rather than ease, so this is a cut.
    void Cut(string name)
    {
        CinemachineCamera want;
        if (!byName.TryGetValue(name, out want))
        {
            Debug.LogWarning("[Stage3Cutscene] ไม่มีกล้องชื่อ " + name);
            return;
        }

        foreach (var c in shots)
            if (c != null) c.Priority = c == want ? 20 : 0;
    }

    IEnumerator Play(string cue)
    {
        switch (cue)
        {
            case "baena-walks-over":
                yield return Walk(baena, baenaMark, walkSeconds);
                break;

            case "asher-steps-up":
                yield return Walk(player, asherStepMark, stepSeconds);
                break;

            case "ethan-blocks":
                yield return Walk(ethan.transform, ethanBlockMark, stepSeconds);
                break;

            case "baena-leaves":
                // Turns his back and walks off the way he came. The scene does
                // not wait for him - the next line starts over his exit.
                if (baena != null) StartCoroutine(Walk(baena, null, walkSeconds, baenaHome, baenaHomeRot));
                break;

            case "sydney-pats-baena":
                yield return Pat(sydney, baena);
                break;

            case "baena-scoffs":
                // Scoffs, then turns for the vehicle. The scene does not wait for
                // the walk - Ethan's next line starts over his exit, which is what
                // makes it read as being dismissed rather than excused.
                yield return Scoff(baena);
                if (baena != null)
                    StartCoroutine(Walk(baena, null, walkSeconds, baenaHome, baenaHomeRot));
                break;
        }
    }

    /// A hand up and out to somebody's shoulder, and back down. Driven off the
    /// upper arm rather than an IK target: the hand only has to read as reaching
    /// in the right direction for half a second at this camera distance.
    IEnumerator Pat(Transform who, Transform at)
    {
        if (who == null || at == null) yield break;

        var idle = who.GetComponent<AmbientIdle>();
        if (idle != null) { idle.enabled = false; }

        var arm = Bone(who, "R_Upperarm");
        var fore = Bone(who, "R_Forearm");
        if (arm == null) { if (idle != null) { idle.enabled = true; idle.Capture(); } yield break; }

        Quaternion armRest = arm.localRotation;
        Quaternion foreRest = fore != null ? fore.localRotation : Quaternion.identity;

        // Which way is up and forward for this arm, in its parent's space, so
        // the reach works whichever way the rig has the bone pointing.
        Vector3 lift = InParent(arm, who.right);
        Vector3 across = InParent(fore, who.right);

        float t = 0f;
        const float dur = 0.85f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);   // out and back

            arm.localRotation = Quaternion.AngleAxis(-48f * k, lift) * armRest;
            if (fore != null) fore.localRotation = Quaternion.AngleAxis(-28f * k, across) * foreRest;
            yield return null;
        }

        arm.localRotation = armRest;
        if (fore != null) fore.localRotation = foreRest;

        if (idle != null) { idle.enabled = true; idle.Capture(); }
    }

    /// The shrug Baena gives before he walks off - a turn of the head and a hand
    /// thrown out, held a beat.
    IEnumerator Scoff(Transform who)
    {
        if (who == null) yield break;

        var idle = who.GetComponent<AmbientIdle>();
        if (idle != null) idle.enabled = false;

        var arm = Bone(who, "R_Upperarm");
        var head = Bone(who, "Head");
        Quaternion armRest = arm != null ? arm.localRotation : Quaternion.identity;
        Quaternion headRest = head != null ? head.localRotation : Quaternion.identity;

        float t = 0f;
        const float dur = 1.1f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);

            if (arm != null)
                arm.localRotation = Quaternion.AngleAxis(-32f * k, InParent(arm, who.right)) * armRest;
            if (head != null)
                head.localRotation = Quaternion.AngleAxis(26f * k, InParent(head, who.up)) * headRest;
            yield return null;
        }

        if (arm != null) arm.localRotation = armRest;
        if (head != null) head.localRotation = headRest;

        if (idle != null) { idle.enabled = true; idle.Capture(); }
    }

    static Vector3 InParent(Transform bone, Vector3 worldAxis)
    {
        if (bone == null) return worldAxis;
        return bone.parent == null ? worldAxis
             : bone.parent.InverseTransformDirection(worldAxis).normalized;
    }

    static Transform Bone(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var d = Bone(c, name); if (d != null) return d; }
        return null;
    }

    IEnumerator Walk(Transform who, Transform mark, float seconds)
    {
        if (who == null || mark == null) yield break;
        yield return Walk(who, mark, seconds, mark.position, mark.rotation);
    }

    IEnumerator Walk(Transform who, Transform mark, float seconds,
                     Vector3 toPos, Quaternion toRot)
    {
        if (who == null) yield break;

        // The idle writes bones in LateUpdate and would fight anything moving
        // the body underneath it, so it stands down for the crossing.
        var idle = who.GetComponent<AmbientIdle>();
        if (idle != null && idle.enabled) { idle.enabled = false; paused.Add(idle); }

        var cc = who.GetComponent<CharacterController>();
        bool hadCc = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        Vector3 from = who.position;
        Quaternion fromRot = who.rotation;

        // Face the way they are going while they go.
        Vector3 flat = toPos - from; flat.y = 0f;
        Quaternion facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(flat) : fromRot;

        // Legs, so the crossing is a walk rather than a slide. Sampled by hand
        // frame by frame instead of through a controller: these rigs have no
        // controller, and adding one for a single clip would mean a state
        // machine, a parameter and an asset per character.
        var animator = who.GetComponent<Animator>();
        bool stepping = walkClip != null && animator != null;
        float clipT = 0f;

        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / seconds);
            float eased = k * k * (3f - 2f * k);

            who.position = Vector3.Lerp(from, toPos, eased);
            who.rotation = Quaternion.Slerp(fromRot, facing, Mathf.Clamp01(k * 3f));

            if (stepping)
            {
                clipT += Time.deltaTime * walkClipSpeed;
                walkClip.SampleAnimation(who.gameObject, clipT % walkClip.length);
            }
            yield return null;
        }

        who.position = toPos;
        who.rotation = toRot;

        if (cc != null) cc.enabled = hadCc;

        if (idle != null)
        {
            // Re-read the pose before switching back on, or the idle would keep
            // moving around the pose it was captured in before the walk.
            idle.enabled = true;
            idle.Capture();
            paused.Remove(idle);
        }
    }


    void OnClosed()
    {
        var dm = DialogueManager.Instance;
        if (dm != null)
        {
            dm.onLine -= OnLine;
            dm.onClosed -= OnClosed;
        }
        StartCoroutine(HandBack());
    }

    IEnumerator HandBack()
    {
        // Ticked while the picture is still up, so the player sees the task
        // close rather than finding the tracker already empty on the far side of
        // the fade.
        var quest = QuestUI.I;
        if (quest != null) quest.Complete();

        // Out on black, and the swap back to the walking camera happens behind
        // it. Letting the brain drop the last shot in plain view snaps the
        // picture to whatever the follow camera was pointing at.
        if (ScreenFader.I != null) ScreenFader.I.FadeOut(closingFade);

        float t = 0f;
        while (t < closingFade) { t += Time.deltaTime; yield return null; }

        foreach (var c in shots) if (c != null) c.Priority = 0;
        if (brain != null) brain.enabled = false;

        SetControl(true);

        foreach (var idle in paused) if (idle != null) { idle.enabled = true; idle.Capture(); }
        paused.Clear();

        // Whoever ended the scene away from their bench stops miming work.
        //
        // Working is a pose plus hand movement, and the pose belongs to a
        // particular bench: play it two metres away and the character is
        // tightening a bolt in mid-air. They are about to load up and leave
        // anyway, so standing and breathing is both correct and what the script
        // has them doing.
        SettleAwayFromWork(ethan != null ? ethan.transform : null, ethanBlockMark);
        SettleAwayFromWork(baena, baenaMark);

        yield return null;

        if (ScreenFader.I != null) ScreenFader.I.FadeIn(closingFade);
        running = false;
    }

    /// Drops a character out of Working once they are standing somewhere other
    /// than the bench that pose was built around.
    void SettleAwayFromWork(Transform who, Transform mark)
    {
        if (who == null || mark == null) return;

        var idle = who.GetComponent<AmbientIdle>();
        if (idle == null || idle.mood != AmbientIdle.Mood.Working) return;

        // Half a metre of slack: somebody who never really left their bench
        // should carry on working at it.
        if (Vector3.Distance(who.position, mark.position) > 0.5f) return;

        idle.mood = AmbientIdle.Mood.Breathing;
        idle.Capture();
    }

    void SetControl(bool on)
    {
        if (playerControl != null)
            foreach (var b in playerControl) if (b != null) b.enabled = on;

        if (playerBody != null) playerBody.enabled = on;
    }
}
