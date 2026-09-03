using System.Collections.Generic;
using UnityEngine;

// The dialogue box's own entrance / exit presets, plus the rule for which one
// plays when.
//
// They are registered into UIAnimLibrary rather than hard-coded here, so the
// box can be handed any of the menu's existing transitions instead just by
// changing a name on MenuTheme.asset - and anything registered later from
// elsewhere is equally available to it.
//
// Variety here is deliberate rather than random noise: the animation carries
// information. A first meeting unrolls, a familiar face slides back in, a
// finished conversation folds shut, a skipped one just evaporates.
public static class DialogueAnimations
{
    // ---- entrances
    public const string Unfurl   = "DialogueUnfurl";     // paper unrolling - first meeting
    public const string InkBleed = "DialogueInkBleed";   // ink soaking through the page
    public const string RiseTilt = "DialogueRiseTilt";   // tossed down onto the table
    public const string SwipeIn  = "DialogueSwipeIn";    // slid in from the side

    // ---- exits
    public const string FoldAway = "DialogueFoldAway";   // conversation finished
    public const string SinkOut  = "DialogueSinkOut";    // walked away / interrupted
    public const string InkFade  = "DialogueInkFade";    // skipped, get out of the way

    // Why the box is closing. The exit animation is the answer.
    public enum Exit { Finished, Skipped, Interrupted }

    static bool registered;
    static int cycle;

    // ------------------------------------------------------------- selection
    public static string PickOpen(bool firstMeeting)
    {
        EnsureRegistered();

        var theme = MenuTheme.Current;
        if (firstMeeting)
            return Named(theme.dialogueFirstMeetingAnim, Unfurl);

        // Every hello after the first rotates through the remaining entrances,
        // so a long session never sees the same one twice in a row and the
        // ceremonial first-meeting animation stays reserved for first meetings.
        string[] pool = theme.dialogueOpenAnims;
        if (pool == null || pool.Length == 0)
            pool = new[] { InkBleed, RiseTilt, SwipeIn };

        string pick = pool[cycle % pool.Length];
        cycle++;
        return Named(pick, InkBleed);
    }

    public static string PickClose(Exit reason)
    {
        EnsureRegistered();

        var theme = MenuTheme.Current;
        switch (reason)
        {
            case Exit.Skipped:     return Named(theme.dialogueSkipAnim, InkFade);
            case Exit.Interrupted: return Named(theme.dialogueInterruptAnim, SinkOut);
            default:               return Named(theme.dialogueFinishAnim, FoldAway);
        }
    }

    static string Named(string configured, string fallback)
    {
        return string.IsNullOrEmpty(configured) ? fallback : configured;
    }

    // ---------------------------------------------------- line-change accents
    // Small moves inside the box when the line changes. The panel itself stays
    // put here: it has already arrived, and re-animating the whole slab every
    // line would read as a stutter rather than a beat.
    public static void PlayLineChange(RectTransform textRect, CanvasGroup textGroup,
                                      int lineIndex, bool speakerChanged, float duration)
    {
        if (textRect == null) return;

        Vector2 home = Home(textRect);
        UITween.Kill(textRect);
        if (textGroup != null) UITween.Kill(textGroup);

        textRect.anchoredPosition = home;

        // A new speaker gets a bigger gesture than the same person continuing.
        if (speakerChanged)
        {
            UITween.MoveAnchoredFrom(textRect, home + new Vector2(0f, -18f), duration, Ease.OutBack);
            if (textGroup != null)
            {
                textGroup.alpha = 0.15f;
                UITween.Fade(textGroup, 1f, duration * 0.85f, Ease.OutCubic);
            }
            return;
        }

        switch (lineIndex % 3)
        {
            case 0:
                UITween.MoveAnchoredFrom(textRect, home + new Vector2(26f, 0f), duration, Ease.OutQuint);
                break;
            case 1:
                UITween.MoveAnchoredFrom(textRect, home + new Vector2(0f, -12f), duration, Ease.OutCubic);
                break;
            default:
                UITween.MoveAnchoredFrom(textRect, home + new Vector2(-18f, 0f), duration, Ease.OutQuint);
                break;
        }

        if (textGroup != null)
        {
            textGroup.alpha = 0.25f;
            UITween.Fade(textGroup, 1f, duration * 0.9f, Ease.OutCubic);
        }
    }

    // ---------------------------------------------------------- registration
    public static void EnsureRegistered()
    {
        if (registered) return;
        registered = true;

        // ---- IN ----------------------------------------------------------
        // Unrolled from a tight scroll. The overshoot coming out of the roll is
        // what sells it as paper rather than a growing rectangle.
        UIAnimLibrary.RegisterIn(Unfurl, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            UITween.Fade(cg, 1f, d * 0.35f, Ease.OutCubic);
            UITween.ScaleFrom(rt, new Vector3(0.86f, 0.03f, 1f), d, Ease.OutBack, () =>
            {
                if (rt != null) rt.localScale = Vector3.one;
                done?.Invoke();
            });
        });

        // Ink soaking outward through the page: no travel, just weight.
        UIAnimLibrary.RegisterIn(InkBleed, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            UITween.ScaleFrom(rt, Vector3.one * 1.05f, d, Ease.OutExpo);
            UITween.Fade(cg, 1f, d * 0.7f, Ease.OutExpo, () =>
            {
                UITween.Punch(rt, 0.02f, d * 0.4f, Vector3.one);
                done?.Invoke();
            });
        });

        // Tossed down onto the table - rises from below with the tilt righting
        // itself as it lands.
        UIAnimLibrary.RegisterIn(RiseTilt, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, -1.8f);
            UITween.RotateZ(rt, 0f, d, Ease.OutBack);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(0f, -90f), d, Ease.OutQuint);
            UITween.Fade(cg, 1f, d * 0.6f, Ease.OutCubic, done);
        });

        UIAnimLibrary.RegisterIn(SwipeIn, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(-140f, -24f), d, Ease.OutQuint);
            UITween.Fade(cg, 1f, d * 0.55f, Ease.OutCubic, done);
        });

        // ---- OUT ---------------------------------------------------------
        // The mirror of Unfurl: the page rolls itself back up.
        UIAnimLibrary.RegisterOut(FoldAway, (rt, cg, d, done) =>
        {
            UITween.Fade(cg, 0f, d, Ease.InQuad);
            UITween.ScaleTo(rt, new Vector3(0.9f, 0.03f, 1f), d, Ease.InCubic, () =>
            {
                Restore(rt);
                done?.Invoke();
            });
        });

        UIAnimLibrary.RegisterOut(SinkOut, (rt, cg, d, done) =>
        {
            UITween.MoveAnchored(rt, Home(rt) + new Vector2(0f, -70f), d, Ease.InCubic);
            UITween.Fade(cg, 0f, d, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        });

        // Skipping is an impatient action, so the box does not linger.
        UIAnimLibrary.RegisterOut(InkFade, (rt, cg, d, done) =>
        {
            UITween.ScaleTo(rt, Vector3.one * 0.98f, d * 0.6f, Ease.InQuad);
            UITween.Fade(cg, 0f, d * 0.6f, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        });
    }

    // ------------------------------------------------------------------ utils
    // The same trick UIAnimLibrary uses internally: the resting position is
    // stashed on first touch, because every one of these animations moves the
    // rect away from it and needs somewhere to put it back.
    static readonly Dictionary<RectTransform, Vector2> homes = new Dictionary<RectTransform, Vector2>();

    static Vector2 Home(RectTransform rt)
    {
        if (rt == null) return Vector2.zero;
        if (!homes.TryGetValue(rt, out var h)) homes[rt] = h = rt.anchoredPosition;
        return h;
    }

    static void Settle(RectTransform rt, CanvasGroup cg, float alpha)
    {
        if (rt != null)
        {
            Vector2 home = Home(rt);
            UITween.Kill(rt);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = home;
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

    public static void Forget(RectTransform rt)
    {
        if (rt != null) homes.Remove(rt);
    }
}
