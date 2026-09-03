using System.Collections;
using UnityEngine;

// A doorway that moves the player to its opposite number.
//
// Two of these make a pair: walk into one and you arrive standing in front of
// the other, facing into the room rather than back at the door you came out of.
//
// The arrival spot is a child marker rather than a position typed in here. A
// gate is a slab with width to it, so "in front of" is a place in the level that
// has to be looked at in the Scene view and nudged until a person standing there
// is clear of the frame and the wall - not a number that can be worked out.
[RequireComponent(typeof(Collider))]
public class TeleportGate : MonoBehaviour
{
    [Header("The pair")]
    [Tooltip("The gate on the other side. Walking into this one puts the player at that one's arrival marker.")]
    public TeleportGate destination;

    [Tooltip("Where somebody arriving AT this gate is put down. Should stand clear of the slab and face away from it.")]
    public Transform arrivalPoint;

    [Header("Look")]
    [Tooltip("Off for a gate that is only a trigger - no mesh, nothing to bump into, just a line to cross.")]
    public bool visible = true;

    [Header("Feel")]
    [Tooltip("Seconds of black either side of the move. 0 teleports with no fade.")]
    public float fadeSeconds = 0.35f;

    [Tooltip("How long this gate ignores the player after they arrive, so stepping out of one does not immediately fall back into it.")]
    public float reentryGuard = 1.0f;

    // Guards are per-gate rather than global: two gates far apart should not have
    // to share a lock, and the one being arrived at is the only one that needs it.
    float ignoreUntil;

    static bool moving;   // one teleport at a time, whatever is touching what

    void Reset()
    {
        var c = GetComponent<Collider>();
        if (c != null) c.isTrigger = true;
    }

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        // A gate that is meant to be felt and not seen keeps its collider - that
        // is the whole of it - and loses only the drawing.
        if (!visible)
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                r.enabled = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (moving || Time.time < ignoreUntil) return;
        if (destination == null || destination.arrivalPoint == null) return;

        // Only the player. Anything else that wanders in is left alone.
        var player = other.GetComponentInParent<PlayerMovement>();
        if (player == null) return;

        StartCoroutine(Move(player.gameObject, destination.arrivalPoint));
    }

    IEnumerator Move(GameObject player, Transform to)
    {
        moving = true;

        if (fadeSeconds > 0f && ScreenFader.I != null)
        {
            ScreenFader.I.FadeOut(fadeSeconds);
            float t = 0f;
            while (t < fadeSeconds) { t += Time.deltaTime; yield return null; }
        }

        // The controller writes its own position every frame and will drag him
        // straight back if it is left switched on across the move.
        var cc = player.GetComponent<CharacterController>();
        bool had = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        player.transform.SetPositionAndRotation(to.position, to.rotation);

        // The first-person camera keeps its own yaw, so putting the body down
        // facing the room is not enough - the head has to be told as well, or the
        // player arrives looking at a wall.
        var look = player.GetComponentInChildren<FirstPersonCamera>();
        if (look == null) look = Object.FindAnyObjectByType<FirstPersonCamera>();
        if (look != null) look.SetYaw(to.eulerAngles.y);

        if (cc != null) cc.enabled = had;

        // The gate being arrived at must not fire on the way out of it.
        if (destination != null) destination.ignoreUntil = Time.time + destination.reentryGuard;
        ignoreUntil = Time.time + reentryGuard;

        yield return null;

        if (fadeSeconds > 0f && ScreenFader.I != null)
            ScreenFader.I.FadeIn(fadeSeconds);

        moving = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        if (arrivalPoint != null)
        {
            Gizmos.DrawWireSphere(arrivalPoint.position, 0.4f);
            Gizmos.DrawRay(arrivalPoint.position, arrivalPoint.forward * 1.5f);
        }
        if (destination != null)
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.8f);
            Gizmos.DrawLine(transform.position, destination.transform.position);
        }
    }
}
