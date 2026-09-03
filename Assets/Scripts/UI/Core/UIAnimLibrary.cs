using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Named panel transitions, stored in a registry rather than a switch statement
// so a new one can be dropped in from anywhere:
//
//     UIAnimLibrary.RegisterIn("MyWipe", (rt, cg, dur, done) => { ... });
//     panel.SetTransition("MyWipe", "FadeOut");
//
// Every preset receives the RectTransform and CanvasGroup already wired up by
// UIPanel, and is responsible for leaving the panel at its resting state
// (anchoredPosition restored, scale 1, alpha 1) when it finishes.
public delegate void UIAnimAction(RectTransform rt, CanvasGroup cg, float duration, Action onComplete);

public static class UIAnimLibrary
{
    static readonly Dictionary<string, UIAnimAction> ins = new Dictionary<string, UIAnimAction>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, UIAnimAction> outs = new Dictionary<string, UIAnimAction>(StringComparer.OrdinalIgnoreCase);
    static bool built;

    public const string DefaultIn = "RiseFade";
    public const string DefaultOut = "SinkFade";

    // ------------------------------------------------------------------- API
    public static void RegisterIn(string name, UIAnimAction action) { EnsureBuilt(); ins[name] = action; }
    public static void RegisterOut(string name, UIAnimAction action) { EnsureBuilt(); outs[name] = action; }

    public static IEnumerable<string> InNames { get { EnsureBuilt(); return ins.Keys; } }
    public static IEnumerable<string> OutNames { get { EnsureBuilt(); return outs.Keys; } }

    public static void PlayIn(string name, RectTransform rt, CanvasGroup cg, float duration, Action onComplete = null)
    {
        EnsureBuilt();
        if (rt == null) { onComplete?.Invoke(); return; }
        if (!ins.TryGetValue(name ?? DefaultIn, out var a)) a = ins[DefaultIn];
        a(rt, cg, duration, onComplete);
    }

    public static void PlayOut(string name, RectTransform rt, CanvasGroup cg, float duration, Action onComplete = null)
    {
        EnsureBuilt();
        if (rt == null) { onComplete?.Invoke(); return; }
        if (!outs.TryGetValue(name ?? DefaultOut, out var a)) a = outs[DefaultOut];
        a(rt, cg, duration, onComplete);
    }

    // --------------------------------------------------------------- built-ins
    static void EnsureBuilt()
    {
        if (built) return;
        built = true;

        // ---- IN ------------------------------------------------------------
        ins["Fade"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.Fade(cg, 1f, d, Ease.OutCubic, done);
        };

        ins["RiseFade"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(0, -46f), d, Ease.OutCubic);
            UITween.Fade(cg, 1f, d * 0.85f, Ease.OutCubic, done);
        };

        ins["SinkFade"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(0, 46f), d, Ease.OutCubic);
            UITween.Fade(cg, 1f, d * 0.85f, Ease.OutCubic, done);
        };

        ins["SlideLeft"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(160f, 0), d, Ease.OutQuint);
            UITween.Fade(cg, 1f, d * 0.7f, Ease.OutCubic, done);
        };

        ins["SlideRight"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(-160f, 0), d, Ease.OutQuint);
            UITween.Fade(cg, 1f, d * 0.7f, Ease.OutCubic, done);
        };

        ins["ScalePop"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.ScaleFrom(rt, Vector3.one * 0.82f, d, Ease.OutBack);
            UITween.Fade(cg, 1f, d * 0.6f, Ease.OutCubic, done);
        };

        ins["ScaleDrop"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.ScaleFrom(rt, Vector3.one * 1.25f, d, Ease.OutCubic);
            UITween.Fade(cg, 1f, d * 0.55f, Ease.OutCubic, done);
        };

        ins["Elastic"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.ScaleFrom(rt, Vector3.one * 0.6f, d * 1.4f, Ease.OutElastic);
            UITween.Fade(cg, 1f, d * 0.5f, Ease.OutCubic, done);
        };

        ins["Bounce"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(0, 120f), d * 1.2f, Ease.OutBounce);
            UITween.Fade(cg, 1f, d * 0.4f, Ease.OutCubic, done);
        };

        // Vertical unfold, like a sheet of paper being unrolled.
        ins["Unfold"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 1f);
            UITween.ScaleFrom(rt, new Vector3(1f, 0.02f, 1f), d, Ease.OutCubic, done);
        };

        ins["UnfoldWide"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 1f);
            UITween.ScaleFrom(rt, new Vector3(0.02f, 1f, 1f), d, Ease.OutCubic, done);
        };

        // Fake 3D card flip by squashing X through zero.
        ins["FlipX"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 1f);
            UITween.To(rt, d, Ease.OutCubic, k =>
            {
                if (rt == null) return;
                rt.localScale = new Vector3(Mathf.Max(0.001f, Mathf.Abs(Mathf.Sin(k * Mathf.PI * 0.5f))), 1f, 1f);
            }, () => { if (rt != null) rt.localScale = Vector3.one; done?.Invoke(); });
        };

        // Heavy, fast slam - reads as an ink stamp hitting parchment.
        ins["InkStamp"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            UITween.ScaleFrom(rt, Vector3.one * 1.6f, d * 0.55f, Ease.OutQuint);
            UITween.Fade(cg, 1f, d * 0.3f, Ease.OutExpo, () =>
            {
                UITween.Punch(rt, 0.05f, d * 0.45f, Vector3.one);
                done?.Invoke();
            });
        };

        ins["Tilt"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 0f);
            if (rt != null) rt.localRotation = Quaternion.Euler(0, 0, -6f);
            UITween.RotateZ(rt, 0f, d, Ease.OutBack);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(-40f, -30f), d, Ease.OutCubic);
            UITween.Fade(cg, 1f, d * 0.7f, Ease.OutCubic, done);
        };

        ins["Instant"] = (rt, cg, d, done) =>
        {
            Reset(rt, cg, 1f);
            done?.Invoke();
        };

        // ---- OUT -----------------------------------------------------------
        outs["Fade"] = (rt, cg, d, done) => UITween.Fade(cg, 0f, d, Ease.InQuad, done);

        outs["SinkFade"] = (rt, cg, d, done) =>
        {
            UITween.MoveAnchored(rt, Home(rt) + new Vector2(0, -40f), d, Ease.InQuad);
            UITween.Fade(cg, 0f, d, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        };

        outs["RiseFade"] = (rt, cg, d, done) =>
        {
            UITween.MoveAnchored(rt, Home(rt) + new Vector2(0, 40f), d, Ease.InQuad);
            UITween.Fade(cg, 0f, d, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        };

        outs["SlideLeft"] = (rt, cg, d, done) =>
        {
            UITween.MoveAnchored(rt, Home(rt) + new Vector2(-180f, 0), d, Ease.InCubic);
            UITween.Fade(cg, 0f, d * 0.8f, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        };

        outs["SlideRight"] = (rt, cg, d, done) =>
        {
            UITween.MoveAnchored(rt, Home(rt) + new Vector2(180f, 0), d, Ease.InCubic);
            UITween.Fade(cg, 0f, d * 0.8f, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        };

        outs["ScaleShrink"] = (rt, cg, d, done) =>
        {
            UITween.ScaleTo(rt, Vector3.one * 0.85f, d, Ease.InCubic);
            UITween.Fade(cg, 0f, d, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        };

        outs["ScaleBurst"] = (rt, cg, d, done) =>
        {
            UITween.ScaleTo(rt, Vector3.one * 1.15f, d, Ease.OutCubic);
            UITween.Fade(cg, 0f, d, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        };

        outs["Fold"] = (rt, cg, d, done) =>
        {
            UITween.ScaleTo(rt, new Vector3(1f, 0.02f, 1f), d, Ease.InCubic, () =>
            {
                if (cg != null) cg.alpha = 0f;
                Restore(rt);
                done?.Invoke();
            });
        };

        outs["Instant"] = (rt, cg, d, done) =>
        {
            if (cg != null) cg.alpha = 0f;
            Restore(rt);
            done?.Invoke();
        };
    }

    // ------------------------------------------------------------------ utils
    // Panels are laid out once and then animated around that layout position, so
    // the resting anchoredPosition is stashed on first touch and reused as the
    // origin for every subsequent transition.
    static readonly Dictionary<RectTransform, Vector2> homes = new Dictionary<RectTransform, Vector2>();

    static Vector2 Home(RectTransform rt)
    {
        if (rt == null) return Vector2.zero;

        if (!homes.TryGetValue(rt, out var h))
            homes[rt] = h = rt.anchoredPosition;
        return h;
    }

    static void Reset(RectTransform rt, CanvasGroup cg, float alpha)
    {
        if (rt != null)
        {
            Home(rt);
            UITween.Kill(rt);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = homes[rt];
        }
        if (cg != null)
        {
            UITween.Kill(cg);
            cg.alpha = alpha;
        }
    }

    static void Restore(RectTransform rt)
    {
        if (rt == null) return;
        rt.anchoredPosition = Home(rt);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    public static void ForgetHome(RectTransform rt)
    {
        if (rt != null) homes.Remove(rt);
    }
}
