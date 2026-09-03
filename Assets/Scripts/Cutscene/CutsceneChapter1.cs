using System.Collections;
using UnityEngine;

// Runs Chapter 1 from the first frame to the point where the player has the
// controls for good.
//
//   Stage 1  the bedroom memory
//   Stage 2  the drawn images, shown inside Stage 1 while she narrates
//   Stage 3a Asher wakes at the camp and gets up
//   ---      the player walks to the fire and presses E
//   Stage 3b the rest of the conversation
//
// One object owns the whole run for a reason. Each stage fades the screen and
// takes over the camera, so two of them left to start themselves fight: the
// shorter one finishes, fades to black, and paints over the one still playing.
// That is exactly what a black screen halfway through Stage 3 turned out to be.
public class CutsceneChapter1 : MonoBehaviour
{
    // The scenery and cast belonging to one stage of the chapter.
    //
    // Kept as a named list rather than two hard-coded fields so a stage added
    // later needs an entry in the Inspector and a ShowOnly() call, not a new
    // pair of fields and another block of the same code.
    [System.Serializable]
    public class StageSet
    {
        [Tooltip("What this set is. For reading in the Inspector - unused at runtime.")]
        public string label;

        [Tooltip("Everything that exists only for this stage: its set, its cast, its props. Not the player, and not anything a later stage still needs.")]
        public GameObject[] objects;
    }

    [Header("Stages")]
    public CutsceneStage1 stage1;

    public CutsceneStage3 stageWake;
    public CutsceneStage3 stageTalk;

    [Header("Stage sets")]
    [Tooltip("One entry per stage, in playing order. Exactly one is live at a time - the rest are switched off.")]
    public StageSet[] stageSets;

    [Header("Player")]
    public GameObject player;

    [Tooltip("Everything that lets the player move or interact. Off during a cutscene, because Timeline is driving the same transform.")]
    public Behaviour[] playerControl;

    [Tooltip("Kept separate because a CharacterController is a Collider, not a Behaviour, so it cannot share the array above.")]
    public CharacterController playerBody;

    [Tooltip("The follow camera used while walking.")]
    public Behaviour followCamera;

    [Tooltip("The Cinemachine brain used by the cutscenes. The two cameras cannot both be steering.")]
    public Behaviour cinemachineBrain;

    [Header("The walk")]
    [Tooltip("Where Asher stands when the waking scene hands over - by the bedroll, on his feet. Left empty, he is left wherever his transform happens to be.")]
    public Transform walkStart;

    public CampfireInteractable campfire;

    [Tooltip("The controls hint, centre screen. Goes away on a timer.")]
    [TextArea]
    public string walkPrompt = "W A S D  to move        SHIFT  to run        SPACE  to jump";

    [Tooltip("The objective in the corner. Stays until he reaches the fire.")]
    public string walkObjective = "Join Logan at the campfire";

    [Tooltip("How long the controls hint stays up before it gets out of the way.")]
    public float walkPromptSeconds = 7f;

    [Header("Handover")]
    [Tooltip("Seconds to fade back up when a stage hands the screen to the player. A stage always ends on black.")]
    public float handoverFade = 0.9f;

    Coroutine promptRoutine;
    QuestUI quest;

    void Start()
    {
        // Nothing else may start itself: this object decides the order.
        if (stage1 != null) stage1.playOnStart = false;
        if (stageWake != null) stageWake.playOnStart = false;
        if (stageTalk != null) stageTalk.playOnStart = false;

        SetPlayerControl(false);
        if (campfire != null) campfire.armed = false;

        // Only the stage being played is switched on.
        //
        // The whole chapter lives in one scene, with the bedroom and the camp
        // standing eighty-odd metres apart. Unity does not draw what the camera
        // cannot see, so it is tempting to leave both of them on - but a switched
        // on object is not free just because it is out of frame. It still casts
        // shadows into shots it does not appear in, its skinned meshes are still
        // accounted for, and every one of its meshes still sits in memory. The
        // bedroom alone is two million triangles and the camp is another four.
        ShowOnly(0);

        if (stage1 != null)
        {
            stage1.onFinished += OnStage1Finished;
            stage1.Play();
        }
        else OnStage1Finished();
    }

    /// Switches on the set at this index and switches every other one off.
    /// Out-of-range hides them all, which is what an unbuilt stage should do.
    void ShowOnly(int index)
    {
        if (stageSets == null) return;

        for (int i = 0; i < stageSets.Length; i++)
        {
            var set = stageSets[i];
            if (set == null || set.objects == null) continue;

            bool on = i == index;
            foreach (var go in set.objects)
                if (go != null) go.SetActive(on);
        }
    }

    void OnStage1Finished()
    {
        if (stage1 != null) stage1.onFinished -= OnStage1Finished;

        // The swap happens here, behind the black Stage 1 always ends on, and
        // before the waking scene evaluates its first frame. Order matters:
        // Timeline cannot animate an object that is switched off, so the camp
        // has to be live before stageWake touches it.
        ShowOnly(1);

        Debug.Log("[Chapter1] Stage 1 จบ (รวมภาพวาดในตัว) - ต่อ Stage 3a");
        PlayWake();
    }

    /// The waking scene at the camp. Public so anything that gets put in front of
    /// it later can hand over here.
    public void PlayWake()
    {
        if (stageWake == null) { BeginWalk(); return; }
        stageWake.onFinished += BeginWalk;
        stageWake.Play();
    }

    void BeginWalk()
    {
        if (stageWake != null) stageWake.onFinished -= BeginWalk;

        // A stage always ends on black, because the next thing is normally another
        // stage that wants to fade up on its own opening shot. Handing over to the
        // player instead, nothing else is going to lift it - and a black screen you
        // can walk around behind is indistinguishable from a hung game.
        if (ScreenFader.I != null) ScreenFader.I.FadeIn(handoverFade);

        // Put him where the scene left him standing, rather than trusting his
        // transform to still say so.
        //
        // Timeline restores the transform of anything it drove through an Animator
        // when its director stops, so the moment the waking scene ends Asher snaps
        // back to whatever position the scene was saved with - which is down by the
        // log, half a metre under the floor and close enough to the fire to trip
        // the sit-down trigger on the first frame. Stating the spot here is the
        // difference between a walk to the campfire and no walk at all.
        //
        // The controller has to be off across the move: a CharacterController
        // writes its own position back and will drag him straight out again.
        if (walkStart != null && player != null)
        {
            bool had = playerBody != null && playerBody.enabled;
            if (playerBody != null) playerBody.enabled = false;

            player.transform.SetPositionAndRotation(walkStart.position, walkStart.rotation);

            if (playerBody != null) playerBody.enabled = had;
        }

        SetPlayerControl(true);

        if (campfire != null)
        {
            campfire.armed = true;
            campfire.player = player != null ? player.transform : null;
            campfire.onUsed += OnCampfireUsed;
        }

        // Two different jobs, two different places on screen. The controls hint
        // is a one-off that gets out of the way; the objective is a reminder that
        // has to still be there when the player looks up from wandering.
        quest = QuestUI.I;
        quest.Show(walkObjective);

        promptRoutine = StartCoroutine(ShowWalkPrompt());
    }

    IEnumerator ShowWalkPrompt()
    {
        TutorialPrompt.I.Show(walkPrompt);
        yield return new WaitForSeconds(walkPromptSeconds);

        // Taken down on a timer rather than on arrival: the campfire puts its own
        // prompt up when the player gets close, and two lines of instruction on
        // screen at once is one too many.
        TutorialPrompt.I.Hide();
        promptRoutine = null;
    }

    void OnCampfireUsed()
    {
        if (campfire != null) campfire.onUsed -= OnCampfireUsed;

        if (promptRoutine != null) { StopCoroutine(promptRoutine); promptRoutine = null; }
        TutorialPrompt.I.Hide();
        if (quest != null) quest.Complete();

        // Control off before the Timeline is evaluated: the CharacterController
        // writes to the same transform the animation is about to place at the log,
        // and whichever runs second wins.
        SetPlayerControl(false);

        if (stageTalk == null) { SetPlayerControl(true); return; }
        stageTalk.onFinished += OnTalkFinished;
        stageTalk.Play();
    }

    void OnTalkFinished()
    {
        if (stageTalk != null) stageTalk.onFinished -= OnTalkFinished;

        // Same handover, same reason: nothing after this lifts the black.
        if (ScreenFader.I != null) ScreenFader.I.FadeIn(handoverFade);

        Debug.Log("[Chapter1] Stage 3b จบ - ส่งคืนให้ผู้เล่น");
        SetPlayerControl(true);
        if (quest != null) quest.Clear();
    }

    void SetPlayerControl(bool on)
    {
        if (playerControl != null)
            foreach (var b in playerControl)
                if (b != null) b.enabled = on;

        if (playerBody != null) playerBody.enabled = on;
        if (followCamera != null) followCamera.enabled = on;

        // The brain is the other half of the same switch - during the walk it has
        // to let go, or it keeps steering the camera back to the last shot.
        if (cinemachineBrain != null) cinemachineBrain.enabled = !on;
    }

    void OnDestroy()
    {
        if (stage1 != null) stage1.onFinished -= OnStage1Finished;
        if (stageWake != null) stageWake.onFinished -= BeginWalk;
        if (stageTalk != null) stageTalk.onFinished -= OnTalkFinished;
        if (campfire != null) campfire.onUsed -= OnCampfireUsed;
    }
}
