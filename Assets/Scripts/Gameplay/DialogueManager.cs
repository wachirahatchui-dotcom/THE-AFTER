using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// Runs a conversation. Owns the state, not the pixels.
//
//  - Shows a "Press E" prompt when the player is near an NPC.
//  - Begin: locks the camera on the NPC, plays their talk animation, opens the
//    dialogue box and starts typing the first line.
//  - Once the box is open it is driven entirely by its own two buttons: NEXT
//    advances (the first press while a line is typing completes that line
//    instead of moving on), SKIP leaves the conversation. No keyboard is read
//    here - E only ever opens a conversation, from PlayerInteractor.
//
// Everything visual lives in UI/Dialogue/:
//   DialogueView          builds and drives the box
//   DialogueTextRevealer  the typing pace
//   TypewriterFade        the per-character fade
//   DialogueAnimations    the entrance / exit presets and when each one plays
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    public static bool IsActive { get; private set; }

    // True only while a line's voice clip is actively playing.
    public bool IsSpeaking { get { return voice != null && voice.isPlaying; } }

    // True while the current line is still being typed out. NPCs read this to
    // gesture only while text is appearing, then stop once the line is complete.
    public bool IsTyping
    {
        get { return view != null && view.Revealer != null && view.Revealer.IsRevealing; }
    }

    [Tooltip("Overrides the box open / close duration. 0 = follow MenuTheme.asset.")]
    [SerializeField] float fadeDuration = 0f;

    [Tooltip("Characters revealed per second at Dialogue Speed = 1. 0 = show instantly.")]
    [SerializeField] float typeSpeed = 35f;

    DialogueView view;
    CameraFollow cam;
    AudioSource voice;

    NPCInteractable npc;
    int index;
    Coroutine autoAdvanceRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        IsActive = false;

        cam = Object.FindAnyObjectByType<CameraFollow>();

        voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false;
        voice.spatialBlend = 0f;   // 2D so the line is always clearly audible

        EnsureEventSystem();

        view = gameObject.AddComponent<DialogueView>();
        view.onAdvance = Advance;
        view.onSkip = Skip;
        view.durationOverride = fadeDuration;
        view.CharactersPerSecond = typeSpeed;
        view.Build();
        view.Revealer.onRevealed = OnLineRevealed;
    }

    void EnsureEventSystem()
    {
        var es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null)
            es = new GameObject("EventSystem", typeof(EventSystem),
                typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();

        // Without assigned actions the module will not deliver mouse clicks to UI.
        var module = es.GetComponent<InputSystemUIInputModule>();
        if (module != null && module.actionsAsset == null)
            module.AssignDefaultActions();
    }

    // ----------------------------------------------------------------- API
    public void ShowHint(NPCInteractable near)
    {
        if (IsActive) { view.HidePrompt(); return; }

        // Hidden entirely when the player has turned interaction hints off.
        if (near != null && GameSettings.ShowInteractHints)
            view.ShowPrompt("Press  E  to talk to " + near.npcName);
        else
            view.HidePrompt();
    }

    public void Begin(NPCInteractable target, Transform player)
    {
        npc = target;
        index = 0;
        IsActive = true;
        view.HidePrompt();

        // Read before MarkSeen: "have we met before?" is what picks the
        // entrance animation, and a first meeting gets the ceremonial one.
        bool firstMeeting = !DialogueProgress.HasSeen(target.npcName);

        // Recorded so saves can restore story state, and so NPCs can branch on
        // DialogueProgress.HasSeen(name) for repeat conversations.
        DialogueProgress.MarkSeen(target.npcName);

        // Turn the NPC to face the player so the camera sees their face.
        if (player != null)
        {
            Vector3 dir = player.position - npc.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                npc.transform.rotation = Quaternion.LookRotation(dir);
        }

        npc.SetTalking(true);
        if (cam != null) cam.EnterDialogue(npc.transform);

        view.Open(firstMeeting);
        ShowLine();
    }

    void ShowLine()
    {
        string line = (npc.lines != null && index < npc.lines.Length) ? npc.lines[index] : "...";
        bool last = npc.lines == null || index >= npc.lines.Length - 1;

        bool speakerChanged = view.SetSpeaker(npc.npcName);

        PlayVoice();

        // OnLineRevealed is reached through DialogueTextRevealer.onRevealed,
        // which fires immediately for a line that arrives complete (Instant
        // speed, subtitles off) and from the coroutine otherwise.
        view.ShowLine(line, index, speakerChanged, last);
    }

    // The arrow starts breathing only once the player actually has a decision
    // to make, which is the moment the line stops appearing.
    void OnLineRevealed()
    {
        if (!IsActive) return;

        view.SetArrowPulsing(true);

        if (GameSettings.DialogueAutoAdvance)
        {
            CancelAutoAdvance();
            autoAdvanceRoutine = StartCoroutine(AutoAdvanceAfterDelay());
        }
    }

    // Waits out the configured delay, then moves on by itself. Cancelled the
    // moment the player advances manually.
    IEnumerator AutoAdvanceAfterDelay()
    {
        float t = 0f;
        float wait = GameSettings.DialogueAutoDelay;

        // A voiced line should never be cut off by the timer.
        while (t < wait || IsSpeaking)
        {
            if (!IsActive) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        autoAdvanceRoutine = null;
        Advance();
    }

    void CancelAutoAdvance()
    {
        if (autoAdvanceRoutine != null) StopCoroutine(autoAdvanceRoutine);
        autoAdvanceRoutine = null;
    }

    void PlayVoice()
    {
        if (voice == null || npc == null || npc.lineClips == null) return;

        if (index < npc.lineClips.Length && npc.lineClips[index] != null)
        {
            voice.Stop();
            voice.clip = npc.lineClips[index];
            voice.volume = GameSettings.VoiceVolume;   // its own slider
            voice.Play();
        }
    }

    public void Advance()
    {
        if (!IsActive) return;

        // A manual advance always wins over the auto-advance timer.
        CancelAutoAdvance();

        // First press while typing just reveals the full line instead of advancing.
        if (IsTyping)
        {
            view.CompleteLine();
            OnLineRevealed();
            return;
        }

        if (npc != null && npc.lines != null && index < npc.lines.Length - 1)
        {
            index++;
            ShowLine();
        }
        else Close(DialogueAnimations.Exit.Finished);
    }

    // Leave the whole conversation, however far through it we are.
    public void Skip()
    {
        if (!IsActive) return;

        bool midway = npc != null && npc.lines != null && index < npc.lines.Length - 1;
        Close(midway ? DialogueAnimations.Exit.Skipped : DialogueAnimations.Exit.Finished);
    }

    public void End()
    {
        Close(DialogueAnimations.Exit.Finished);
    }

    // Pulled out of the conversation by the world rather than by the player.
    // Kept separate so the box can sink away instead of folding shut.
    public void Interrupt()
    {
        Close(DialogueAnimations.Exit.Interrupted);
    }

    void Close(DialogueAnimations.Exit reason)
    {
        CancelAutoAdvance();

        if (npc != null) npc.SetTalking(false);
        if (cam != null) cam.ExitDialogue();
        if (voice != null) voice.Stop();

        IsActive = false;
        npc = null;

        view.Close(reason);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsActive = false;
    }
}
