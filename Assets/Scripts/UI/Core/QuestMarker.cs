using UnityEngine;
using UnityEngine.UI;

// The mark that floats over whoever the player is meant to go and see.
//
// Built in code and reached through a lazy singleton, the same way ScreenFader
// and QuestUI are, so nothing has to be wired in the Inspector.
//
// It answers a question the objective text in the corner cannot: reading "go to
// the campfire" tells the player what to do, but not which way to walk. The mark
// is the direction.
//
// It gets out of the way on arrival. Standing at the fire, the campfire's own
// "press E" prompt is the thing to read, and a marker still bouncing over
// Logan's head at two metres is just noise on top of it.
public class QuestMarker : MonoBehaviour
{
    static QuestMarker instance;

    Canvas canvas;
    CanvasGroup group;
    RectTransform holder;
    Text glyph;
    Outline edge;

    Transform target;
    float height;
    float wanted;

    [Tooltip("How high above the target's own origin the mark sits.")]
    public float defaultHeight = 2.1f;

    [Tooltip("Within this many metres the mark fades out and leaves the field to the interact prompt.")]
    public float hideWithin = 4.5f;

    [Tooltip("How far it rises and falls.")]
    public float bobHeight = 0.13f;
    public float bobSeconds = 1.9f;

    public float fadeSpeed = 3.2f;

    [Tooltip("On-screen size of the mark, in metres at the target's distance.")]
    public float scale = 0.0055f;

    Transform player;

    public static QuestMarker I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~QuestMarker");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<QuestMarker>();
                instance.Build();
            }
            return instance;
        }
    }

    Color Ink => MenuTheme.Current != null ? MenuTheme.Current.accentSoft : new Color(0.729f, 0.435f, 0.271f);

    void Build()
    {
        // World space, not an overlay: the mark belongs to a place in the camp,
        // so it has to be occluded by distance and framed by the shot like
        // anything else standing there.
        var go = new GameObject("Marker", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        group = go.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        holder = go.GetComponent<RectTransform>();
        holder.sizeDelta = new Vector2(120f, 160f);

        var textGo = new GameObject("Glyph", typeof(RectTransform), typeof(Outline), typeof(Text));
        textGo.transform.SetParent(holder, false);

        glyph = textGo.GetComponent<Text>();

        // The game's display face, the one the menus and captions are set in - a
        // marker in a different typeface reads as a stray debug gizmo.
        glyph.font = GameFont.Get();
        glyph.fontSize = 96;
        glyph.text = "!";
        glyph.alignment = TextAnchor.MiddleCenter;
        glyph.color = Ink;
        glyph.raycastTarget = false;
        glyph.horizontalOverflow = HorizontalWrapMode.Overflow;
        glyph.verticalOverflow = VerticalWrapMode.Overflow;

        // The camp is lit by one fire: bright on one side of a letter and black
        // on the other. An outline holds the shape against both.
        edge = textGo.GetComponent<Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
        edge.effectDistance = new Vector2(4f, -4f);
        edge.useGraphicAlpha = true;

        var rt = glyph.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        canvas.enabled = false;
    }

    /// Puts the mark over something. Pass the player so it knows to stand down
    /// on arrival; without one it simply stays up until it is told otherwise.
    public static void Show(Transform over, Transform walker = null, float heightOverride = -1f)
    {
        var m = I;
        m.target = over;
        m.player = walker;
        m.height = heightOverride > 0f ? heightOverride : m.defaultHeight;
        m.wanted = 1f;
        if (m.canvas != null) m.canvas.enabled = true;
    }

    public static void Hide()
    {
        if (instance == null) return;
        instance.wanted = 0f;
    }

    void LateUpdate()
    {
        if (canvas == null) return;

        var cam = Camera.main;
        if (target == null || cam == null)
        {
            Fade(0f);
            return;
        }

        // Close enough to interact means the interact prompt is on screen, and
        // two instructions at once is one too many.
        float want = wanted;
        if (want > 0f && player != null && hideWithin > 0f)
        {
            float d = Vector3.Distance(player.position, target.position);
            if (d <= hideWithin) want = 0f;
        }

        Fade(want);

        if (group.alpha <= 0.001f)
        {
            canvas.enabled = false;
            return;
        }
        canvas.enabled = true;

        float bob = Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(0.1f, bobSeconds)) * bobHeight;
        holder.position = target.position + Vector3.up * (height + bob);

        // Face the camera squarely, and keep a constant size on screen rather
        // than shrinking into the distance - it is a signpost, not scenery.
        holder.rotation = cam.transform.rotation;

        float dist = Vector3.Distance(cam.transform.position, holder.position);
        holder.localScale = Vector3.one * Mathf.Max(0.0005f, scale * dist);
    }

    void Fade(float want)
    {
        group.alpha = Mathf.MoveTowards(group.alpha, want, fadeSpeed * Time.deltaTime);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
