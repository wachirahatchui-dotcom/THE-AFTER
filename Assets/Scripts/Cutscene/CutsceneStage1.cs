using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// Chapter 1, Stage 1 - the memory of the bedroom.
//
// The shot order, the camera moves and the acting all live in the Timeline asset,
// where they can be re-timed by dragging. This class owns only the three things
// Timeline does not do by itself: opening on black, fading up into the scene, and
// handing over to whatever runs next when the sequence ends.
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneStage1 : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [Header("Timing")]
    [Tooltip("How long the screen stays fully black before the fade begins.")]
    public float openingBlack = 1.2f;

    public float fadeInSeconds = 2f;
    public float fadeOutSeconds = 1.5f;

    [Header("Playback")]
    public bool playOnStart = true;

    [Tooltip("Switched off for the duration - player movement, interaction, the follow camera.")]
    public MonoBehaviour[] disableDuringCutscene;

    [Tooltip("Hidden for the duration - anything belonging to the play area rather than the memory.")]
    public GameObject[] hideDuringCutscene;

    // CutsceneChapter1 hooks in here and starts the waking scene.
    public event Action onFinished;

    PlayableDirector director;
    Coroutine routine;

    // Whether the timeline has actually reached its end.
    //
    // director.state cannot answer this. PlayState has only Playing and Paused, so
    // a timeline that has finished and a timeline the pause menu has frozen report
    // the same thing - and waiting on "state == Playing" therefore treats opening
    // the pause menu as the scene being over. That is what made a cutscene jump
    // ahead after pausing: the stage finished early and handed on to the next one
    // while the player was still in the menu. The stopped event fires when the
    // director really stops and does not fire for a pause, which is the difference
    // that matters.
    bool timelineDone;

    void OnDirectorStopped(PlayableDirector d) => timelineDone = true;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();

        // Timeline would otherwise start on its own and play the first frames
        // behind the black, before the fade has even begun.
        director.playOnAwake = false;
    }

    void Start()
    {
        if (playOnStart) Play();
    }

    public void Play()
    {
        if (IsPlaying) return;
        if (director.playableAsset == null)
        {
            Debug.LogError("[CutsceneStage1] no Timeline assigned to the director.");
            return;
        }

        IsPlaying = true;
        SetGameplayEnabled(false);
        SetHiddenObjects(false);
        routine = StartCoroutine(Run());
    }

    public void Skip()
    {
        if (!IsPlaying) return;

        if (routine != null) StopCoroutine(routine);
        routine = null;
        director.Stop();
        ScreenFader.I.FadeIn(0.3f);
        Finish();
    }

    IEnumerator Run()
    {
        // Black first, and instantly - the fade has to reveal the bedroom, not
        // whatever the camera was last pointing at.
        ScreenFader.I.FadeOut(0.01f);
        yield return null;

        // Held at frame zero so the opening shot is already framed behind the black.
        director.time = 0d;
        director.Evaluate();

        float t = 0f;
        while (t < openingBlack) { t += Time.deltaTime; yield return null; }

        ScreenFader.I.FadeIn(fadeInSeconds);

        timelineDone = false;
        director.stopped -= OnDirectorStopped;   // never subscribed twice, even after a Skip
        director.stopped += OnDirectorStopped;
        director.Play();

        // Fade out over the end of the timeline, not after it.
        //
        // When a Timeline's Cinemachine track finishes it lets go of the camera,
        // and the brain immediately picks whichever virtual camera in the scene is
        // next in line. The campfire cameras live in the same scene, so the shot
        // snapped to Asher and Logan sitting at the fire - in full view, because
        // the fade had not started yet. Starting the fade a few seconds early means
        // the handover happens behind black and nobody sees the room it lands in.
        double fadeAt = System.Math.Max(0d, director.duration - fadeOutSeconds);
        while (!timelineDone && director.time < fadeAt) yield return null;

        ScreenFader.I.FadeOut(fadeOutSeconds);
        t = 0f;
        while (t < fadeOutSeconds) { t += Time.deltaTime; yield return null; }

        // By now the timeline has normally finished behind the black; wait if it
        // has not, so the next stage never starts on top of this one.
        while (!timelineDone) yield return null;
        director.stopped -= OnDirectorStopped;

        routine = null;
        Finish();
    }

    void Finish()
    {
        IsPlaying = false;
        SetGameplayEnabled(true);
        SetHiddenObjects(true);

        Debug.Log("[CutsceneStage1] Stage 1 จบ");
        onFinished?.Invoke();
    }

    void SetGameplayEnabled(bool on)
    {
        if (disableDuringCutscene == null) return;
        foreach (var mb in disableDuringCutscene)
            if (mb != null) mb.enabled = on;
    }

    void SetHiddenObjects(bool visible)
    {
        if (hideDuringCutscene == null) return;
        foreach (var go in hideDuringCutscene)
            if (go != null) go.SetActive(visible);
    }

    void OnDestroy() => IsPlaying = false;
}
