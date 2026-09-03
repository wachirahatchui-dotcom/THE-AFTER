using UnityEngine;
using UnityEngine.InputSystem;

// 3rd-person ORBIT camera for Asher.
//   • Hold the RIGHT mouse button and move the mouse to rotate around him.
//   • Scroll the mouse wheel to zoom in / out.
// Put this on the Main Camera. It finds Asher automatically (the object with
// PlayerMovement), so no manual wiring is needed.
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    [Header("Orbit (hold right mouse)")]
    public float yaw = 0f;
    public float pitch = 32f;
    public float mouseSensitivity = 0.28f;
    public float minPitch = 5f;
    public float maxPitch = 75f;

    [Header("Zoom (scroll wheel)")]
    public float distance = 8f;
    public float minDistance = 3f;
    public float maxDistance = 16f;
    public float zoomStep = 1f;        // units of zoom per scroll notch
    public float zoomSmooth = 10f;     // how fast the zoom eases

    private float targetDistance;

    [Header("Framing")]
    public float lookAtHeight = 1.6f;  // ~neck/head height of Asher
    public float followSmooth = 12f;

    [Header("Dialogue focus")]
    public float dialogueDistance = 2.6f;
    public float dialogueHeight = 1.6f;
    public float dialogueSideOffset = 1.4f; // shift camera to the side so Asher's head doesn't block
    private bool inDialogue;
    private Transform dialogueNpc;

    public void EnterDialogue(Transform npc) { dialogueNpc = npc; inDialogue = true; }
    public void ExitDialogue() { inDialogue = false; dialogueNpc = null; }

    void Start()
    {
        if (target == null)
        {
            var pm = Object.FindAnyObjectByType<PlayerMovement>();
            if (pm != null)
                target = pm.transform;
        }
        targetDistance = distance;
    }

    // Used when loading a save: jump straight to the stored zoom instead of
    // easing to it from the default, which would read as a stray dolly shot.
    public void SetDistanceImmediate(float value)
    {
        distance = Mathf.Clamp(value, minDistance, maxDistance);
        targetDistance = distance;
    }

    void Update()
    {
        if (inDialogue) return;   // no orbit/zoom while talking

        var mouse = Mouse.current;
        if (mouse == null)
            return;

        // Read live rather than caching in Start, so dragging the sensitivity
        // slider in the in-game options screen takes effect immediately.
        mouseSensitivity = GameSettings.MouseSensitivity;

        // Rotate while the right mouse button is held.
        if (mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            float vertical = GameSettings.InvertY ? -delta.y : delta.y;

            yaw += delta.x * mouseSensitivity;
            pitch -= vertical * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // Zoom with the scroll wheel. Use the sign so it behaves the same no
        // matter whether the platform reports the wheel as 1 or 120 per notch.
        float scroll = mouse.scroll.ReadValue().y;
        if (GameSettings.InvertZoom) scroll = -scroll;

        float step = zoomStep * GameSettings.ZoomSpeed;
        if (scroll > 0.01f)
            targetDistance -= step;       // wheel up = zoom in
        else if (scroll < -0.01f)
            targetDistance += step;       // wheel down = zoom out
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);

        distance = Mathf.Lerp(distance, targetDistance, zoomSmooth * Time.deltaTime);
    }

    void LateUpdate()
    {
        // While talking, smoothly move in front of the NPC and lock on.
        if (inDialogue && dialogueNpc != null)
        {
            Vector3 f = dialogueNpc.position + Vector3.up * dialogueHeight;
            Vector3 dp = f + dialogueNpc.forward * dialogueDistance
                           + dialogueNpc.right * dialogueSideOffset
                           + Vector3.up * 0.2f;
            transform.position = Vector3.Lerp(transform.position, dp,
                                              followSmooth * Time.deltaTime);
            transform.LookAt(f);
            return;
        }

        if (target == null)
            return;

        Vector3 focus = target.position + Vector3.up * lookAtHeight;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus + rot * new Vector3(0f, 0f, -distance);

        transform.position = Vector3.Lerp(transform.position, desiredPos,
                                          followSmooth * Time.deltaTime);
        transform.LookAt(focus);
    }
}
