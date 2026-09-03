using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

// The drawn images that take over the screen while somebody keeps talking.
//
// Driven off the director's own clock rather than a coroutine. That is the whole
// point: the pictures illustrate a speech, so they have to stay with it through a
// pause, a scrub in the editor, or a re-time after the fact. A coroutine counting
// its own seconds drifts away from the voice the first time either changes.
//
// Two layers, and they do different jobs. The black plate covers the 3D scene for
// the length of a block, so the bedroom is not showing behind a half-faded
// drawing. The two picture layers cross-fade into each other so no gap of black
// opens up between one image and the next.
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneStills : MonoBehaviour
{
    [System.Serializable]
    public class Still
    {
        public Sprite image;

        [Tooltip("When this picture is fully up.")]
        public float start;

        [Tooltip("When the next one starts taking over.")]
        public float end;
    }

    [System.Serializable]
    public class Block
    {
        [Tooltip("When the screen starts going black to hand over to pictures.")]
        public float start;

        [Tooltip("When the screen has finished coming back to the scene.")]
        public float end;

        [Tooltip("How long the black takes to arrive and to leave.")]
        public float fade = 0.9f;
    }

    [Tooltip("The stretches of the scene that belong to the pictures. Between them the camera is back in the room.")]
    public Block[] blocks;

    [Tooltip("Every picture, with the window it holds the screen for.")]
    public Still[] stills;

    [Tooltip("How long one picture takes to become the next.")]
    public float crossFade = 0.9f;

    [Tooltip("A slow push in across each picture. 1 is no movement.")]
    public float driftZoom = 1.05f;

    PlayableDirector director;
    Canvas canvas;
    Image black, back, front;
    RectTransform backRt, frontRt;

    void Awake()
    {
        director = GetComponent<PlayableDirector>();
        Build();
    }

    void Build()
    {
        var go = new GameObject("StillsCanvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Over the game, under the screen fader so a transition can still black
        // everything out, and well under the pause menu. sortingOrder is a signed
        // 16-bit value - everything here stays inside 32767.
        canvas.sortingOrder = 31700;
        canvas.enabled = false;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        black = Layer(go.transform, "Black", Color.black);
        back = Layer(go.transform, "Back", Color.white);
        front = Layer(go.transform, "Front", Color.white);
        backRt = back.rectTransform;
        frontRt = front.rectTransform;

        // Filled, not fitted.
        //
        // preserveAspect makes the picture fit inside the screen, which is the same
        // thing as putting bars down the sides of anything that is not exactly the
        // screen's shape. EnvelopeParent does the opposite - it grows the picture
        // until it covers the screen and lets the overflow be cropped. The drawings
        // are 1.79 against a 1.78 screen, so what gets cropped is a few pixels.
        Envelope(back);
        Envelope(front);
    }

    static void Envelope(Image img)
    {
        img.preserveAspect = false;

        var fitter = img.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
        fitter.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = 16f / 9f;   // corrected per sprite as each one goes up
    }

    static void Fit(Image img, Sprite sprite)
    {
        if (sprite == null) return;
        var fitter = img.GetComponent<UnityEngine.UI.AspectRatioFitter>();
        if (fitter == null) return;

        // Each drawing states its own shape, so a picture of a different size still
        // covers the screen rather than being stretched to the last one's ratio.
        var r = sprite.rect;
        if (r.height > 0f) fitter.aspectRatio = r.width / r.height;
    }

    static Image Layer(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.color = new Color(colour.r, colour.g, colour.b, 0f);
        img.raycastTarget = false;

        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return img;
    }

    static void Alpha(Image img, float a)
    {
        var c = img.color;
        c.a = a;
        img.color = c;
    }

    void LateUpdate()
    {
        if (director == null || canvas == null) return;

        // No state check here on purpose.
        //
        // PlayState has only Playing and Paused, so a paused scene is
        // indistinguishable from a finished one - and hiding on "not Playing"
        // meant that pausing during the drawings took them off screen and showed
        // the bedroom behind the pause menu. The playhead answers the question
        // properly: outside every block the cover is zero and nothing is drawn,
        // and a scene that has ended leaves the playhead outside them all.
        float t = (float)director.time;
        float cover = Cover(t);

        if (cover <= 0.001f) { canvas.enabled = false; return; }

        canvas.enabled = true;
        Alpha(black, cover);
        Paint(t, cover);
    }

    /// How much of the screen the pictures own at this moment.
    float Cover(float t)
    {
        if (blocks == null) return 0f;

        float most = 0f;
        foreach (var b in blocks)
        {
            if (b == null || t < b.start - b.fade || t > b.end + b.fade) continue;

            float fade = Mathf.Max(0.01f, b.fade);
            float rising = Mathf.Clamp01((t - (b.start - fade)) / fade);
            float falling = Mathf.Clamp01(((b.end + fade) - t) / fade);
            most = Mathf.Max(most, Mathf.Min(rising, falling));
        }
        return most;
    }

    /// Which picture is up, and how far the next one has come over it.
    void Paint(float t, float cover)
    {
        if (stills == null || stills.Length == 0) return;

        int current = -1;
        for (int i = 0; i < stills.Length; i++)
        {
            var s = stills[i];
            if (s == null || s.image == null) continue;
            if (t >= s.start - crossFade && t < s.end) { current = i; break; }
        }

        // Past the last one, or in a gap: hold whatever was showing so the block
        // ends on a picture rather than on bare black.
        if (current < 0)
        {
            for (int i = stills.Length - 1; i >= 0; i--)
                if (stills[i] != null && stills[i].image != null && t >= stills[i].start) { current = i; break; }
            if (current < 0) return;
        }

        var now = stills[current];
        float into = Mathf.Clamp01((t - (now.start - crossFade)) / Mathf.Max(0.01f, crossFade));

        // A dip through black between pictures, rather than one dissolving into
        // the other.
        //
        // The outgoing picture goes in the first half of the change and the
        // incoming one arrives in the second, so for a moment there is only the
        // black plate. Two drawings cross-fading directly overlay two sets of
        // pencil lines and read as a double exposure; a beat of black separates
        // them into two images instead of one muddle.
        float leaving = 1f - Mathf.Clamp01(into * 2f);
        float arriving = Mathf.Clamp01(into * 2f - 1f);

        // Only the picture immediately before this one in the same block. The last
        // picture of the first block sits at index 2 and the first of the second
        // block at index 3, and without this the second block would open by fading
        // out a drawing from twenty seconds and a scene-change ago.
        int previous = current - 1;
        bool joins = previous >= 0 && stills[previous] != null
                  && Mathf.Abs(stills[previous].end - now.start) < 0.01f;

        if (joins && stills[previous].image != null)
        {
            back.sprite = stills[previous].image;
            Fit(back, back.sprite);
            Alpha(back, cover * leaving);
        }
        else Alpha(back, 0f);

        front.sprite = now.image;
        Fit(front, front.sprite);
        Alpha(front, cover * arriving);

        float span = Mathf.Max(0.01f, now.end - (now.start - crossFade));
        float through = Mathf.Clamp01((t - (now.start - crossFade)) / span);
        frontRt.localScale = Vector3.one * Mathf.Lerp(1f, driftZoom, through);
        backRt.localScale = Vector3.one * driftZoom;
    }

    void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
    }
}
