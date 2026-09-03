using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public enum Ease
{
    Linear,
    InQuad, OutQuad, InOutQuad,
    InCubic, OutCubic, InOutCubic,
    OutQuint, OutExpo, InOutExpo,
    InBack, OutBack, InOutBack,
    OutElastic, OutBounce
}

// Minimal tween layer. The project has no DOTween, and pulling in a dependency
// just for a menu is overkill, so this covers the cases the UI actually needs:
// a driven 0..1 float, plus typed wrappers for the handful of properties the
// menu animates. Everything defaults to UNSCALED time because the pause menu
// runs at timeScale 0.
public static class UITween
{
    // ------------------------------------------------------------------ easing
    public static float Evaluate(Ease e, float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c2 = c1 * 1.525f;
        const float c3 = c1 + 1f;
        float c4 = 2f * Mathf.PI / 3f;

        switch (e)
        {
            case Ease.InQuad:    return t * t;
            case Ease.OutQuad:   return 1f - (1f - t) * (1f - t);
            case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

            case Ease.InCubic:   return t * t * t;
            case Ease.OutCubic:  return 1f - Mathf.Pow(1f - t, 3f);
            case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;

            case Ease.OutQuint:  return 1f - Mathf.Pow(1f - t, 5f);
            case Ease.OutExpo:   return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
            case Ease.InOutExpo:
                if (t <= 0f) return 0f;
                if (t >= 1f) return 1f;
                return t < 0.5f ? Mathf.Pow(2f, 20f * t - 10f) / 2f
                                : (2f - Mathf.Pow(2f, -20f * t + 10f)) / 2f;

            case Ease.InBack:    return c3 * t * t * t - c1 * t * t;
            case Ease.OutBack:   return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
            case Ease.InOutBack:
                return t < 0.5f
                    ? Mathf.Pow(2f * t, 2f) * ((c2 + 1f) * 2f * t - c2) / 2f
                    : (Mathf.Pow(2f * t - 2f, 2f) * ((c2 + 1f) * (t * 2f - 2f) + c2) + 2f) / 2f;

            case Ease.OutElastic:
                if (t <= 0f) return 0f;
                if (t >= 1f) return 1f;
                return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;

            case Ease.OutBounce:
            {
                const float n1 = 7.5625f, d1 = 2.75f;
                if (t < 1f / d1) return n1 * t * t;
                if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                t -= 2.625f / d1; return n1 * t * t + 0.984375f;
            }

            default: return t;
        }
    }

    // -------------------------------------------------------------- core tween
    public static Coroutine To(UnityEngine.Object owner, float duration, Ease ease,
                               Action<float> onUpdate, Action onComplete = null,
                               float delay = 0f, bool unscaled = true)
    {
        return UITweenRunner.I.Run(owner, Drive(duration, ease, onUpdate, onComplete, delay, unscaled));
    }

    static IEnumerator Drive(float duration, Ease ease, Action<float> onUpdate,
                             Action onComplete, float delay, bool unscaled)
    {
        if (delay > 0f)
        {
            float d = 0f;
            while (d < delay)
            {
                d += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
        }

        if (duration <= 0f)
        {
            onUpdate?.Invoke(1f);
            onComplete?.Invoke();
            yield break;
        }

        float t = 0f;
        onUpdate?.Invoke(Evaluate(ease, 0f));
        while (t < duration)
        {
            t += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            onUpdate?.Invoke(Evaluate(ease, t / duration));
            yield return null;
        }
        onUpdate?.Invoke(1f);
        onComplete?.Invoke();
    }

    // Deliberately goes through Existing, not I: Kill is called from OnDestroy,
    // which also runs during scene teardown, and creating the runner there
    // would leave a stray GameObject behind in the closing scene.
    public static void Kill(UnityEngine.Object owner)
    {
        var runner = UITweenRunner.Existing;
        if (runner != null) runner.Kill(owner);
    }

    public static Coroutine Delay(UnityEngine.Object owner, float seconds, Action action)
    {
        return To(owner, 0f, Ease.Linear, null, action, seconds);
    }

    // ------------------------------------------------------------- convenience
    public static Coroutine Fade(CanvasGroup cg, float to, float duration,
                                 Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
    {
        if (cg == null) return null;
        float from = cg.alpha;
        return To(cg, duration, ease,
                  k => { if (cg != null) cg.alpha = Mathf.LerpUnclamped(from, to, k); },
                  onComplete, delay);
    }

    public static Coroutine FadeGraphic(Graphic g, float to, float duration,
                                        Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
    {
        if (g == null) return null;
        float from = g.color.a;
        return To(g, duration, ease, k =>
        {
            if (g == null) return;
            var c = g.color;
            c.a = Mathf.LerpUnclamped(from, to, k);
            g.color = c;
        }, onComplete, delay);
    }

    public static Coroutine ColorTo(Graphic g, Color to, float duration,
                                    Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
    {
        if (g == null) return null;
        Color from = g.color;
        return To(g, duration, ease,
                  k => { if (g != null) g.color = Color.LerpUnclamped(from, to, k); },
                  onComplete, delay);
    }

    public static Coroutine MoveAnchored(RectTransform rt, Vector2 to, float duration,
                                         Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
    {
        if (rt == null) return null;
        Vector2 from = rt.anchoredPosition;
        return To(rt, duration, ease,
                  k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); },
                  onComplete, delay);
    }

    // Snaps to `from` immediately, then travels back to wherever the rect sits
    // now. This is the one used for every entrance animation.
    public static Coroutine MoveAnchoredFrom(RectTransform rt, Vector2 from, float duration,
                                             Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
    {
        if (rt == null) return null;
        Vector2 to = rt.anchoredPosition;
        rt.anchoredPosition = from;
        return To(rt, duration, ease,
                  k => { if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(from, to, k); },
                  onComplete, delay);
    }

    public static Coroutine ScaleTo(Transform tr, Vector3 to, float duration,
                                    Ease ease = Ease.OutBack, Action onComplete = null, float delay = 0f)
    {
        if (tr == null) return null;
        Vector3 from = tr.localScale;
        return To(tr, duration, ease,
                  k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, k); },
                  onComplete, delay);
    }

    public static Coroutine ScaleFrom(Transform tr, Vector3 from, float duration,
                                      Ease ease = Ease.OutBack, Action onComplete = null, float delay = 0f)
    {
        if (tr == null) return null;
        Vector3 to = tr.localScale;
        tr.localScale = from;
        return To(tr, duration, ease,
                  k => { if (tr != null) tr.localScale = Vector3.LerpUnclamped(from, to, k); },
                  onComplete, delay);
    }

    public static Coroutine RotateZ(Transform tr, float toDegrees, float duration,
                                    Ease ease = Ease.OutCubic, Action onComplete = null, float delay = 0f)
    {
        if (tr == null) return null;
        float from = tr.localEulerAngles.z;
        if (from > 180f) from -= 360f;
        return To(tr, duration, ease, k =>
        {
            if (tr != null)
                tr.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(from, toDegrees, k));
        }, onComplete, delay);
    }

    // Positional shake that always returns the rect to where it started.
    public static Coroutine Shake(RectTransform rt, float strength, float duration, Action onComplete = null)
    {
        if (rt == null) return null;
        Vector2 home = rt.anchoredPosition;
        return To(rt, duration, Ease.Linear, k =>
        {
            if (rt == null) return;
            float damp = 1f - k;
            rt.anchoredPosition = home + new Vector2(
                Mathf.PerlinNoise(Time.unscaledTime * 45f, 0f) - 0.5f,
                Mathf.PerlinNoise(0f, Time.unscaledTime * 45f) - 0.5f) * (2f * strength * damp);
        }, () =>
        {
            if (rt != null) rt.anchoredPosition = home;
            onComplete?.Invoke();
        });
    }

    // One-shot pop that overshoots and settles back onto baseScale.
    public static Coroutine Punch(Transform tr, float amount, float duration, Vector3 baseScale)
    {
        if (tr == null) return null;
        return To(tr, duration, Ease.Linear, k =>
        {
            if (tr == null) return;
            float s = 1f + Mathf.Sin(k * Mathf.PI) * amount * (1f - k);
            tr.localScale = baseScale * s;
        }, () => { if (tr != null) tr.localScale = baseScale; });
    }

    // Letter-by-letter reveal. perChar is seconds between characters.
    public static Coroutine Typewriter(Text label, string content, float perChar,
                                       Action onComplete = null, float delay = 0f)
    {
        if (label == null) return null;
        return To(label, Mathf.Max(0.01f, content.Length * perChar), Ease.Linear, k =>
        {
            if (label == null) return;
            int n = Mathf.RoundToInt(content.Length * k);
            label.text = content.Substring(0, Mathf.Clamp(n, 0, content.Length));
        }, onComplete, delay);
    }

    public static Coroutine NumberTo(UnityEngine.Object owner, float from, float to, float duration,
                                     Action<float> onValue, Ease ease = Ease.OutCubic)
    {
        return To(owner, duration, ease, k => onValue(Mathf.LerpUnclamped(from, to, k)));
    }
}
