using System.Collections;
using UnityEngine;

// A doorway that moves the player to its opposite number.
//
// Two of these make a pair: walk up to one and you arrive standing in front of
// the other, facing into the room rather than back at the door you came out of.
//
// It watches the distance to the player rather than waiting on a trigger.
//
// The trigger version did not survive contact with the level. The black door in
// the bunker sits on a wall with seven centimetres between the two, so the
// player is stopped by the wall while barely grazing the trigger volume - close
// enough to look like they walked into the door, not close enough for
// OnTriggerEnter to fire. Measuring a distance has none of that: no volume to
// clip through, nothing for a wall to get in front of, and it reads the same
// way the campfire does a few metres away, which is a thing the player has
// already learned by the time they get here.
//
// The arrival spot is a marker in the scene rather than a position typed in
// here. "In front of" is a place that has to be looked at in the Scene view and
// nudged until a person standing there is clear of the frame - not a number that
// can be worked out.
public class TeleportGate : MonoBehaviour
{
    [Header("The pair")]
    [Tooltip("The gate on the other side. Coming to this one puts the player at that one's arrival marker.")]
    public TeleportGate destination;

    [Tooltip("Where somebody arriving AT this gate is put down. Should stand clear of the doorway and face away from it.")]
    public Transform arrivalPoint;

    [Header("Range")]
    [Tooltip("How close the player has to get, measured along the ground. Generous on purpose: a doorway you have to hit exactly is a doorway that feels broken.")]
    public float range = 2.6f;

    [Tooltip("Measured to this instead of the gate's own origin, for a gate whose pivot is off in a corner. Left empty the gate itself is used.")]
    public Transform rangeFrom;

    [Header("Look")]
    [Tooltip("Off for a gate that is felt and not seen - no mesh, just a place that moves you.")]
    public bool visible = true;

    [Header("Feel")]
    [Tooltip("Seconds of black either side of the move. 0 teleports with no fade.")]
    public float fadeSeconds = 0.35f;

    [Tooltip("How long this gate ignores the player after they arrive, so stepping out of one does not immediately fall back into it.")]
    public float reentryGuard = 1.2f;

    [Header("The far side")]
    // Switched on by the gate being left rather than the gate being arrived at,
    // because the arriving gate cannot switch itself on: it lives inside the set,
    // so while the set is off that gate is off with it. The departing gate is the
    // only one of the pair still running at the moment the far side needs waking.
    //
    // This is what lets a stage stay switched off until somebody actually walks
    // into it. Chapter 1 holds the whole story in one scene, and a stage nobody
    // has reached yet should not cost anything to stand there.
    [Tooltip("Switched on just before the player is moved: the far side's scenery and cast. Anything already live is left alone.")]
    public GameObject[] activateOnUse;

    [Header("Quest")]
    [Tooltip("Objective put up when the player arrives through THIS gate. Left empty the tracker is untouched, which is what a gate the player is only passing back through wants.")]
    public string objectiveOnArrival;

    [Tooltip("Tick to tick off the standing objective as they arrive, before the new one goes up.")]
    public bool completesObjective;

    // Guards are per-gate rather than global: two gates far apart should not have
    // to share a lock, and the one being arrived at is the only one that needs it.
    float ignoreUntil;

    // Only the first arrival is a story beat; walking back through later should
    // not re-announce an errand the player is already on.
    bool announced;

    // Whether the player has stood clear of this gate since it last put them
    // down nearby.
    //
    // A timer is not enough on its own. Both arrival marks sit a couple of
    // metres from their own gate - which is what "in front of the door" means -
    // and that is inside the range that opens it. On a timer alone the player
    // arrives, waits a second and a half, and is sent straight back, for ever.
    // The campfire has the same shape of problem and solves it the same way: the
    // door will not open again until they have actually walked away from it.
    bool mustLeaveFirst;

    static bool moving;   // one teleport at a time, whatever is near what

    Transform player;

    Vector3 Centre { get { return rangeFrom != null ? rangeFrom.position : transform.position; } }

    void Awake()
    {
        // A gate meant to be felt and not seen keeps whatever collider it has -
        // it is not used for the teleport either way - and loses only the drawing.
        if (!visible)
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;

        // Nobody is sent anywhere until they have been seen standing clear of the
        // door at least once.
        //
        // A save restores the player wherever they were standing, and one taken
        // near a doorway puts them inside its range on the first frame of the
        // load - so the game opens by teleporting them somewhere they did not
        // walk to, before they have touched the controls. Walking up to a gate
        // normally clears this on the way in, because approaching from outside
        // the range is what the check is looking for.
        mustLeaveFirst = true;
    }

    void Update()
    {
        if (moving || Time.time < ignoreUntil) return;
        if (destination == null || destination.arrivalPoint == null) return;

        if (player == null)
        {
            var pm = Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null) player = pm.transform;
            if (player == null) return;
        }

        // On the ground plane: standing on a step or crouching in a doorway
        // should not change whether you are close enough to go through it.
        float d = Vector3.ProjectOnPlane(player.position - Centre, Vector3.up).magnitude;

        // A margin past the edge, so somebody standing exactly on the boundary
        // cannot flicker in and out of range and re-open the door by shuffling.
        if (d > range + 0.8f) mustLeaveFirst = false;

        if (mustLeaveFirst || d > range) return;

        StartCoroutine(Move(player.gameObject, destination.arrivalPoint));
    }

    IEnumerator Move(GameObject who, Transform to)
    {
        moving = true;

        if (fadeSeconds > 0f && ScreenFader.I != null)
        {
            ScreenFader.I.FadeOut(fadeSeconds);
            float t = 0f;
            while (t < fadeSeconds) { t += Time.deltaTime; yield return null; }
        }

        // Under the black, not before it. Waking a stage costs a frame or two
        // while its meshes and skins come up, and those are frames the player
        // should be spending looking at nothing.
        if (activateOnUse != null)
            foreach (var go in activateOnUse)
                if (go != null && !go.activeSelf) go.SetActive(true);

        // The controller writes its own position every frame and will drag him
        // straight back if it is left switched on across the move.
        var cc = who.GetComponent<CharacterController>();
        bool had = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        who.transform.SetPositionAndRotation(to.position, to.rotation);

        // The first-person camera keeps its own yaw, so putting the body down
        // facing the room is not enough - the head has to be told as well, or the
        // player arrives looking at the wall they just came through.
        var look = Object.FindAnyObjectByType<FirstPersonCamera>();
        if (look != null) look.SetYaw(to.eulerAngles.y);

        if (cc != null) cc.enabled = had;

        // Both ends stand down: the gate arrived at holds until the player has
        // walked away from it, and this one holds for a moment in case they turn
        // straight round.
        if (destination != null)
        {
            destination.ignoreUntil = Time.time + destination.reentryGuard;
            destination.mustLeaveFirst = true;
        }
        ignoreUntil = Time.time + reentryGuard;
        mustLeaveFirst = true;

        if (destination != null) destination.Announce();

        yield return null;

        if (fadeSeconds > 0f && ScreenFader.I != null)
            ScreenFader.I.FadeIn(fadeSeconds);

        moving = false;
    }

    /// Puts this gate's objective up, once, the first time somebody arrives here.
    void Announce()
    {
        if (announced || string.IsNullOrEmpty(objectiveOnArrival)) return;
        announced = true;

        var quest = QuestUI.I;
        if (quest == null) return;

        if (completesObjective) quest.Complete(0.9f);

        // After the tick, so the player sees the old task close before the new
        // one arrives rather than the panel swapping text under them.
        StartCoroutine(ShowAfter(completesObjective ? 1.1f : 0f));
    }

    IEnumerator ShowAfter(float wait)
    {
        float t = 0f;
        while (t < wait) { t += Time.unscaledDeltaTime; yield return null; }
        QuestUI.I.Show(objectiveOnArrival);
    }

    void OnDrawGizmosSelected()
    {
        // The range is the whole of how this works, so it is the thing worth
        // seeing in the Scene view.
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(Centre, range);

        if (arrivalPoint != null)
        {
            Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
            Gizmos.DrawWireSphere(arrivalPoint.position, 0.4f);
            Gizmos.DrawRay(arrivalPoint.position, arrivalPoint.forward * 1.5f);
        }

        if (destination != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawLine(Centre, destination.Centre);
        }
    }
}
