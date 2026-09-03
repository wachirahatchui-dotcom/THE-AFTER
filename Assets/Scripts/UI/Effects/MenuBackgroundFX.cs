using UnityEngine;
using UnityEngine.UI;

// The atmosphere layer behind the menu: warm gradient, drifting dust, an
// animated film-grain plate and a corner vignette, with the whole stack
// parallaxing gently against the pointer.
//
// Everything is plain uGUI Images fed by UIGfx, so it costs no post-processing
// volume and works identically in the editor and a build.
public class MenuBackgroundFX : MonoBehaviour
{
    [Header("Layers")]
    public bool dust = true;
    public bool grain = true;
    public bool vignette = true;
    public bool parallax = true;

    [Header("Tuning")]
    public int dustCount = 44;
    public float parallaxStrength = 18f;
    public float grainOpacity = 0.018f;
    public float vignetteOpacity = 0.55f;

    RectTransform root;
    RectTransform dustLayer;
    RectTransform parallaxLayer;
    Image grainImage;
    Sprite[] grainFrames;
    int grainFrame;
    float grainTimer;

    Mote[] motes;
    Vector2 parallaxOffset;

    struct Mote
    {
        public RectTransform rt;
        public float speed;      // upward drift, px/sec
        public float swayAmp;
        public float swayHz;
        public float phase;
        public float baseAlpha;
        public Image img;
        public float depth;      // 0 far .. 1 near, scales parallax
    }

    // Builds the whole stack under `parent`, stretched to fill it.
    // Colours and tuning both come from the theme asset, so the caller only
    // has to say where it goes.
    public static MenuBackgroundFX Create(Transform parent)
    {
        var go = new GameObject("BackgroundFX", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.SetAsFirstSibling();

        var t = MenuTheme.Current;
        var fx = go.AddComponent<MenuBackgroundFX>();

        fx.dust = t.enableDust;
        fx.grain = t.enableGrain;
        fx.vignette = t.enableVignette;
        fx.parallax = t.enableParallax;
        fx.dustCount = t.dustCount;
        fx.grainOpacity = t.grainOpacity;
        fx.vignetteOpacity = t.vignetteOpacity;
        fx.parallaxStrength = t.parallaxStrength;

        fx.Build(t.backdropTop, t.backdropBottom, t.vignetteColor, t.dustTint);
        return fx;
    }

    void Build(Color top, Color bottom, Color vignetteColor, Color dustColor)
    {
        root = (RectTransform)transform;
        Fill(root, 0f);

        // Parallax container: everything inside drifts together.
        var pl = new GameObject("Parallax", typeof(RectTransform));
        pl.transform.SetParent(transform, false);
        parallaxLayer = (RectTransform)pl.transform;
        // Oversized so the parallax shift never exposes an edge.
        Fill(parallaxLayer, -parallaxStrength * 2.5f);

        // 1. Base gradient.
        var grad = NewImage("Gradient", parallaxLayer, Color.white);
        grad.sprite = UIGfx.VerticalGradient(top, bottom, 256);
        grad.type = Image.Type.Simple;
        Fill(grad.rectTransform, 0f);

        // 2. Paper grit, tiled, very faint.
        var fibre = NewImage("Fibre", parallaxLayer, new Color(1f, 1f, 1f, 0.30f));
        fibre.sprite = UIGfx.PaperFibre(256);
        fibre.type = Image.Type.Tiled;
        Fill(fibre.rectTransform, 0f);

        // 3. Dust motes.
        if (dust)
        {
            var dl = new GameObject("Dust", typeof(RectTransform));
            dl.transform.SetParent(parallaxLayer, false);
            dustLayer = (RectTransform)dl.transform;
            Fill(dustLayer, 0f);
            BuildDust(dustColor);
        }

        // 4. Vignette, outside the parallax so the frame stays put.
        if (vignette)
        {
            var v = NewImage("Vignette", transform, new Color(1f, 1f, 1f, vignetteOpacity));
            v.sprite = UIGfx.RadialFalloff(new Color(0f, 0f, 0f, 0f), vignetteColor, 2.4f, 256);
            v.type = Image.Type.Simple;
            Fill(v.rectTransform, 0f);
        }

        // 5. Film grain on top of everything.
        if (grain)
        {
            grainFrames = new Sprite[4];
            for (int i = 0; i < grainFrames.Length; i++)
                grainFrames[i] = UIGfx.Grain(1000 + i, 128, 0.7f);

            grainImage = NewImage("Grain", transform, new Color(1f, 1f, 1f, grainOpacity));
            grainImage.sprite = grainFrames[0];
            grainImage.type = Image.Type.Tiled;
            Fill(grainImage.rectTransform, 0f);
        }
    }

    void BuildDust(Color tint)
    {
        motes = new Mote[dustCount];
        var dot = UIGfx.SoftDot(32, 2.2f);

        for (int i = 0; i < dustCount; i++)
        {
            var img = NewImage("Mote", dustLayer, tint);
            img.sprite = dot;
            img.raycastTarget = false;

            float depth = Random.Range(0.15f, 1f);
            float size = Mathf.Lerp(2.5f, 9f, depth);

            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(Random.Range(-980f, 980f), Random.Range(-560f, 560f));

            float alpha = Mathf.Lerp(0.10f, 0.42f, depth);
            var c = tint; c.a = alpha;
            img.color = c;

            motes[i] = new Mote
            {
                rt = rt,
                img = img,
                speed = Mathf.Lerp(4f, 17f, depth),
                swayAmp = Random.Range(8f, 34f),
                swayHz = Random.Range(0.05f, 0.22f),
                phase = Random.Range(0f, 100f),
                baseAlpha = alpha,
                depth = depth
            };
        }
    }

    // ------------------------------------------------------------------ loop
    void Update()
    {
        float t = Time.unscaledTime;

        if (motes != null)
        {
            for (int i = 0; i < motes.Length; i++)
            {
                var m = motes[i];
                if (m.rt == null) continue;

                var p = m.rt.anchoredPosition;
                p.y += m.speed * Time.unscaledDeltaTime;
                float sway = Mathf.Sin((t + m.phase) * m.swayHz * Mathf.PI * 2f) * m.swayAmp;

                // Recycle at the top edge.
                if (p.y > 600f)
                {
                    p.y = -600f;
                    p.x = Random.Range(-980f, 980f);
                }

                m.rt.anchoredPosition = new Vector2(p.x + sway * Time.unscaledDeltaTime * 6f, p.y);

                // Slow twinkle so the field never looks like static confetti.
                if (m.img != null)
                {
                    var c = m.img.color;
                    c.a = m.baseAlpha * (0.55f + 0.45f * Mathf.Sin((t * 0.7f) + m.phase));
                    m.img.color = c;
                }
            }
        }

        if (grainImage != null && grainFrames != null)
        {
            grainTimer += Time.unscaledDeltaTime;
            if (grainTimer >= 1f / 18f)   // 18 fps looks like film, 60 looks like noise
            {
                grainTimer = 0f;
                grainFrame = (grainFrame + 1) % grainFrames.Length;
                grainImage.sprite = grainFrames[grainFrame];
            }
        }

        if (parallax && parallaxLayer != null)
        {
            Vector2 target = PointerBias() * parallaxStrength;
            parallaxOffset = Vector2.Lerp(parallaxOffset, target, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
            parallaxLayer.anchoredPosition = parallaxOffset;
        }
    }

    // Pointer position as -1..1 from screen centre, with a safe fallback when
    // there is no mouse (gamepad-only or headless).
    static Vector2 PointerBias()
    {
        Vector2 mouse;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current == null) return Vector2.zero;
        mouse = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#else
        mouse = Input.mousePosition;
#endif
        if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;
        return new Vector2(
            Mathf.Clamp(mouse.x / Screen.width - 0.5f, -0.5f, 0.5f) * -2f,
            Mathf.Clamp(mouse.y / Screen.height - 0.5f, -0.5f, 0.5f) * -2f);
    }

    // ----------------------------------------------------------------- utils
    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    static void Fill(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }
}
