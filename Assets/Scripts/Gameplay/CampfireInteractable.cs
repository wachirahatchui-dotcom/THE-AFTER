using System;
using UnityEngine;
using UnityEngine.InputSystem;

// The place at the fire that starts the rest of the conversation.
//
// Walk up, get a prompt, press E when you are ready. Coming close is the offer;
// pressing the key is the answer, and nothing happens without it. There is a
// second, tighter ring available that starts the scene on its own, but it is off
// by default: taking the scene off a player who merely walked a step too close
// costs more than the stuck-with-nothing-happening case it guards against.
//
// Unlike NPCInteractable this starts something rather than saying something, and
// what it starts is up to whatever is listening on onUsed.
public class CampfireInteractable : MonoBehaviour
{
    [Tooltip("Close enough to be offered the scene. E starts it.")]
    public float interactRange = 3.2f;

    [Tooltip("Close enough that it starts on its own, without a key press. Off by default - set above 0 only if being unable to miss it matters more than choosing it.")]
    public float autoRange = 0f;

    [Tooltip("Who has to be in range. Left empty, it finds the player by tag.")]
    public Transform player;

    [Tooltip("Shown while the player is close enough. English, like the rest of the on-screen text.")]
    public string prompt = "Press  E  to sit with Logan";

    [Tooltip("Off until something switches it on - the walk tutorial does.")]
    public bool armed;

    [Tooltip("Only usable once.")]
    public bool oneShot = true;

    public event Action onUsed;

    bool used;
    bool showingPrompt;

    // Whether the player has stood clear of the inner ring since this was armed.
    //
    // Without it the safety net fires itself. The waking scene leaves Asher about
    // a metre and a half from the fire, which is already inside autoRange - so the
    // instant the walk is armed the sit-down triggers, on the first frame, and the
    // walk tutorial never happens at all. A catch-all for a player who never
    // arrives has no business firing for one who never left.
    bool everLeft;
    bool wasArmed;

    void Update()
    {
        if (armed != wasArmed)
        {
            wasArmed = armed;
            everLeft = false;
        }

        if (!armed || (oneShot && used)) return;

        if (player == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
            if (player == null) return;
        }

        // Measured on the ground plane: standing on a log or crouching should not
        // change whether you are close enough.
        float distance = Vector3.ProjectOnPlane(player.position - transform.position, Vector3.up).magnitude;

        // A margin, so standing exactly on the edge cannot flicker in and out and
        // arm the catch-all by accident.
        if (distance > autoRange + 0.6f) everLeft = true;

        bool near = distance <= interactRange;
        if (near != showingPrompt)
        {
            showingPrompt = near;
            if (near) TutorialPrompt.I.Show(prompt);
            else TutorialPrompt.I.Hide();
        }

        if (!near) return;

        // Keyboard.current is null in a build with no keyboard attached, and for
        // a frame or two while the Input System wakes up.
        var keyboard = Keyboard.current;
        bool pressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
        bool walkedIn = autoRange > 0f && everLeft && distance <= autoRange;

        if (!pressed && !walkedIn) return;

        used = true;
        showingPrompt = false;
        TutorialPrompt.I.Hide();
        onUsed?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRange);

        if (autoRange <= 0f) return;
        Gizmos.color = new Color(1f, 0.35f, 0.25f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, autoRange);
    }
}
