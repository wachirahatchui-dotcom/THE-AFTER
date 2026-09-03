using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// Chapter 1, Stage 3 - Asher wakes at the camp and Logan talks him into going.
//
// The shot order, the camera moves, the acting and the voice lines all live in
// the Timeline asset, where they can be re-timed by dragging. This class owns
// only what Timeline does not do by itself: opening on black, fading up into
// Asher's eyes, and handing over to the tutorial when the sequence ends.
//
// CutsceneChapter1 calls Play() in order. Nothing else should.
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneStage3 : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }

    [Header("Timing")]
    [Tooltip("How long the screen stays fully black before the fade begins.")]
    public float openingBlack = 0.8f;

    [Tooltip("Slow, because it is a man opening his eyes rather than a scene change.")]
    public float fadeInSeconds = 2.4f;

    public float fadeOutSeconds = 1.5f;

    [Header("Playback")]
    [Tooltip("Off by default - CutsceneChapter1 decides when this one runs.")]
    public bool playOnStart = false;

    [Tooltip("Switched off for the duration - player movement, interaction, the follow camera.")]
    public MonoBehaviour[] disableDuringCutscene;

    [Tooltip("Hidden for the duration - anything belonging to the play area rather than the scene.")]
    public GameObject[] hideDuringCutscene;

    // The tutorial hooks in here.
    public event Action onFinished;

    PlayableDirector director;
    Coroutine routine;

    // Whether the timeline has actually reached its end.
    //
    // director.state cannot answer this: PlayState has only Playing and Paused, so
    // a finished timeline and one the pause menu has frozen look identical. Waiting
    // on "state == Playing" therefore reads opening the pause menu as the scene
    // being over, ends the stage early and hands on to the next one while the
    // player is still in the menu - which is the cutscene appearing to jump ahead
    // after a pause. The stopped event fires on a real stop and not on a pause.
    bool timelineDone;

    void OnDirectorStopped(PlayableDirector d) => timelineDone = true;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();

        // Timeline would otherwise start on its own and play the first seconds
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
            Debug.LogError("[CutsceneStage3] ยังไม่ได้ใส่ Timeline ให้ director");
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
        if (ScreenFader.I != null) ScreenFader.I.FadeIn(0.3f);
        Finish();
    }

    IEnumerator Run()
    {
        // Black first, and instantly - the fade has to reveal the camp, not
        // whatever the camera was last pointing at.
        if (ScreenFader.I != null) ScreenFader.I.FadeOut(0.01f);
        yield return null;

        // Held at frame zero so the opening shot is already framed behind the black.
        director.time = 0d;
        director.Evaluate();

        float t = 0f;
        while (t < openingBlack) { t += Time.deltaTime; yield return null; }

        if (ScreenFader.I != null) ScreenFader.I.FadeIn(fadeInSeconds);

        timelineDone = false;
        director.stopped -= OnDirectorStopped;   // never subscribed twice, even after a Skip
        director.stopped += OnDirectorStopped;
        director.Play();

        // Fade out over the end of the timeline, not after it.
        //
        // A Timeline that finishes lets go of the camera, and the brain picks up
        // whichever virtual camera in the scene comes next - which is how the
        // campfire ended up on screen for a moment between stages. Starting the
        // fade before the last frame puts that handover behind black.
        double fadeAt = System.Math.Max(0d, director.duration - fadeOutSeconds);
        while (!timelineDone && director.time < fadeAt) yield return null;

        if (ScreenFader.I != null) ScreenFader.I.FadeOut(fadeOutSeconds);
        t = 0f;
        while (t < fadeOutSeconds) { t += Time.deltaTime; yield return null; }

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

        Debug.Log("[CutsceneStage3] Stage 3 จบ - ต่อด้วย Tutorial");
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
