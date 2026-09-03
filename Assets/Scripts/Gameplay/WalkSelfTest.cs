#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// A scripted run of the movement controls, so "WASD works" is something measured
// rather than assumed.
//
// It puts itself into play mode without being placed in the scene, holds each key
// in turn through the Input System, and reports how far Asher actually travelled
// and what the Animator was doing while he did. Editor-only, and it does nothing
// unless RunOnPlay is switched on.
public class WalkSelfTest : MonoBehaviour
{
    /// Flipped on by the editor script that wants a test, and off again after.
    public const string Flag = "THEAFTER_WALK_SELFTEST";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!UnityEditor.EditorPrefs.GetBool(Flag, false)) return;
        UnityEditor.EditorPrefs.SetBool(Flag, false);

        var go = new GameObject("~walk self test");
        DontDestroyOnLoad(go);
        go.AddComponent<WalkSelfTest>();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.6f);   // let everything wake up

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
        {
            // Say what the scene does contain rather than only that the lookup
            // failed - the difference between "wrong tag" and "wrong scene" is
            // the whole diagnosis.
            Report("!! ไม่เจออ็อบเจกต์ที่ tag เป็น Player");
            Report("   ซีนที่กำลังเล่น: "
                 + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
                 + "   (โหลดอยู่ " + UnityEngine.SceneManagement.SceneManager.sceneCount + " ซีน)");

            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                Report("   ราก: " + go.name.PadRight(22) + " tag=" + go.tag
                     + "  active=" + go.activeInHierarchy);

            var byName = GameObject.Find("Asher");
            Report("   หาโดยชื่อ 'Asher': " + (byName != null
                ? "เจอ ที่ " + byName.transform.position.ToString("F2") + " tag=" + byName.tag
                : "ไม่เจอ"));
            yield break;
        }

        var cc = player.GetComponent<CharacterController>();
        var move = player.GetComponent<PlayerMovement>();
        var anim = player.GetComponentInChildren<Animator>();

        Report("เริ่มเทส  ตัวละคร=" + player.name
             + "  CharacterController=" + (cc != null && cc.enabled)
             + "  PlayerMovement=" + (move != null && move.enabled)
             + "  Animator=" + (anim != null ? anim.runtimeAnimatorController?.name : "ไม่มี"));

        var kb = InputSystem.GetDevice<Keyboard>() ?? InputSystem.AddDevice<Keyboard>();

        // Driven from a script rather than a person, the game view never takes
        // focus, and by default the Input System throws away input for an
        // unfocused player. Without this the keys are queued and ignored.
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode =
            InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

        // Settle on the floor before measuring, so gravity is not counted as travel.
        yield return new WaitForSeconds(0.8f);
        Report("ยืนนิ่งที่ " + player.transform.position.ToString("F2")
             + "  ติดพื้น=" + (cc != null && cc.isGrounded));

        yield return Hold(kb, Key.W, 1.4f, "W เดินหน้า", player, anim);
        yield return Hold(kb, Key.S, 1.0f, "S ถอยหลัง", player, anim);
        yield return Hold(kb, Key.A, 1.0f, "A ซ้าย", player, anim);
        yield return Hold(kb, Key.D, 1.0f, "D ขวา", player, anim);

        // Sprint is W with shift down, so both keys go in the same state.
        yield return HoldTwo(kb, Key.W, Key.LeftShift, 1.2f, "W+Shift วิ่ง", player, anim);

        yield return Jump(kb, player, cc, anim);

        Report("เทสจบ");
        UnityEditor.EditorApplication.isPlaying = false;
    }

    // Queued and left for the engine's own input update to process at the start
    // of the next frame. Calling InputSystem.Update() by hand here looks like it
    // helps, and it does for keys read with isPressed - but it advances the input
    // frame early, so the press-down edge is spent before the game's Update runs
    // and wasPressedThisFrame never sees it. Jump is read on that edge.
    static void Press(Keyboard kb, params Key[] keys)
    {
        var state = new KeyboardState();
        foreach (var k in keys) state.Set(k, true);
        InputSystem.QueueStateEvent(kb, state);
    }

    static void Release(Keyboard kb) => InputSystem.QueueStateEvent(kb, new KeyboardState());

    IEnumerator Hold(Keyboard kb, Key key, float seconds, string label,
                     GameObject player, Animator anim)
    {
        yield return HoldKeys(kb, new[] { key }, seconds, label, player, anim);
    }

    IEnumerator HoldTwo(Keyboard kb, Key a, Key b, float seconds, string label,
                        GameObject player, Animator anim)
    {
        yield return HoldKeys(kb, new[] { a, b }, seconds, label, player, anim);
    }

    IEnumerator HoldKeys(Keyboard kb, Key[] keys, float seconds, string label,
                         GameObject player, Animator anim)
    {
        Vector3 from = player.transform.position;
        float peakSpeed = 0f;
        string state = "?";
        int framesKeyRead = 0, framesTotal = 0;

        float t = 0f;
        while (t < seconds)
        {
            framesTotal++;
            // Re-queued every frame: a state event is a snapshot, so a key that is
            // not re-sent reads as released on the very next frame.
            Press(kb, keys);
            yield return null;
            t += Time.deltaTime;

            // Read the key back through the same API the game uses, so a failure
            // says whether the press never arrived or arrived and did nothing.
            var live = Keyboard.current;
            if (live != null && live[keys[0]].isPressed) framesKeyRead++;

            if (anim != null)
            {
                float s = anim.GetFloat("Speed");
                if (s > peakSpeed) peakSpeed = s;
                var info = anim.GetCurrentAnimatorStateInfo(0);
                if (info.IsName("Locomotion")) state = "Locomotion";
                else if (info.IsName("Jump")) state = "Jump";
            }
        }

        Release(kb);
        yield return null;

        Vector3 to = player.transform.position;
        float flat = Vector3.ProjectOnPlane(to - from, Vector3.up).magnitude;
        Report(label.PadRight(14) + " ไป " + flat.ToString("F2") + " ม. ใน " + seconds.ToString("F1")
             + " วิ  (" + (flat / seconds).ToString("F2") + " ม./วิ)"
             + "   Speed สูงสุด " + peakSpeed.ToString("F2") + "  state=" + state
             + "   เกมอ่านปุ่มติด " + framesKeyRead + "/" + framesTotal + " เฟรม");

        yield return new WaitForSeconds(0.35f);
    }

    IEnumerator Jump(Keyboard kb, GameObject player, CharacterController cc, Animator anim)
    {
        float startY = player.transform.position.y;
        float peakY = startY;
        bool sawJumpState = false;

        // Held for a few frames rather than tapped: one queued event can land on
        // the same input update as its own release and cancel itself out.
        for (int i = 0; i < 4; i++) { Press(kb, Key.Space); yield return null; }
        Release(kb);

        float t = 0f;
        while (t < 1.8f)
        {
            yield return null;
            t += Time.deltaTime;
            peakY = Mathf.Max(peakY, player.transform.position.y);
            if (anim != null && anim.GetCurrentAnimatorStateInfo(0).IsName("Jump")) sawJumpState = true;
        }

        Report("Space กระโดด".PadRight(14) + " สูงขึ้น " + (peakY - startY).ToString("F2")
             + " ม.  เข้า state Jump=" + sawJumpState
             + "  ลงพื้นแล้ว=" + (cc != null && cc.isGrounded));
    }

    // Written to a file as well as the console: entering play mode clears the
    // console, and the results are wanted after play mode has ended.
    public const string LogPath =
        @"C:\Users\omgpo\AppData\Local\Temp\claude\C--Users-omgpo-OneDrive--------Claude-AI-BLENDER\ba02a62f-d576-4d8a-bcbf-55cdce3e56c6\scratchpad\walktest.txt";

    static void Report(string line)
    {
        Debug.Log("[WALKTEST] " + line);
        try { System.IO.File.AppendAllText(LogPath, line + "\n"); }
        catch (System.Exception e) { Debug.LogWarning("[WALKTEST] เขียนไฟล์ไม่ได้: " + e.Message); }
    }
}
#endif
