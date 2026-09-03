using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The inventory's own motion vocabulary.
//
// Two layers, because they answer different questions:
//   - panel presets, registered into UIAnimLibrary, for "the bag opened"
//   - slot cascades, run here, for "and here is what is in it"
//
// The cascade direction and the open preset both rotate on every open. Opening
// a bag is something a player does hundreds of times, and the same 0.3s
// animation every single time is what makes an inventory feel like a menu
// rather than a thing you are carrying.
public static class InventoryAnimations
{
    // ---- panel entrances
    public const string BagOpen    = "BagOpen";      // lifts and settles
    public const string BagUnclasp = "BagUnclasp";   // a flap folding open
    public const string BagSwing   = "BagSwing";     // swings in from the left
    public const string BagDrop    = "BagDrop";      // dropped onto the table

    // ---- panel exits
    public const string BagClose = "BagClose";
    public const string BagSink  = "BagSink";
    public const string BagSnap  = "BagSnap";        // used when Escape closes it

    // How the grid fills in.
    public enum Cascade { RowMajor, ColumnMajor, Diagonal, CentreOut }

    static bool registered;
    static int openCycle;
    static int cascadeCycle;

    // -------------------------------------------------------------- selection
    public static string NextOpen()
    {
        EnsureRegistered();

        var pool = MenuTheme.Current.inventoryOpenAnims;
        if (pool == null || pool.Length == 0)
            pool = new[] { BagOpen, BagUnclasp, BagSwing, BagDrop };

        string pick = pool[openCycle % pool.Length];
        openCycle++;
        return string.IsNullOrEmpty(pick) ? BagOpen : pick;
    }

    public static string CloseFor(bool cancelled)
    {
        EnsureRegistered();

        // Escape is an impatient close, so it gets the short one.
        var theme = MenuTheme.Current;
        string pick = cancelled ? theme.inventoryCancelAnim : theme.inventoryCloseAnim;
        return string.IsNullOrEmpty(pick) ? (cancelled ? BagSnap : BagClose) : pick;
    }

    public static Cascade NextCascade()
    {
        var pick = (Cascade)(cascadeCycle % 4);
        cascadeCycle++;
        return pick;
    }

    // ---------------------------------------------------------------- cascade
    // Fades and lifts every slot into place, ordered by the chosen direction.
    // columns is needed because the grid is a flat list; the order is the only
    // thing that knows it is a grid.
    public static void PlayCascade(IList<RectTransform> slots, IList<CanvasGroup> groups,
                                   int columns, Cascade cascade)
    {
        if (slots == null || slots.Count == 0) return;

        var theme = MenuTheme.Current;
        float duration = theme.inventorySlotDuration;
        float step = theme.inventorySlotStagger;

        for (int i = 0; i < slots.Count; i++)
        {
            var rt = slots[i];
            var cg = i < groups.Count ? groups[i] : null;
            if (rt == null) continue;

            float delay = Order(i, columns, slots.Count, cascade) * step;

            UITween.Kill(rt);
            if (cg != null) { UITween.Kill(cg); cg.alpha = 0f; }

            rt.localScale = Vector3.one * 0.72f;
            UITween.ScaleTo(rt, Vector3.one, duration, Ease.OutBack, null, delay);
            if (cg != null) UITween.Fade(cg, 1f, duration * 0.8f, Ease.OutCubic, null, delay);
        }
    }

    // Position of a slot in the cascade, in "steps from the start".
    static int Order(int index, int columns, int count, Cascade cascade)
    {
        if (columns <= 0) columns = 1;

        int row = index / columns;
        int col = index % columns;
        int rows = Mathf.CeilToInt(count / (float)columns);

        switch (cascade)
        {
            case Cascade.ColumnMajor:
                return col * rows + row;

            case Cascade.Diagonal:
                return row + col;

            case Cascade.CentreOut:
                // Chebyshev distance from the middle of the grid: rings, not rows.
                float cr = (rows - 1) * 0.5f;
                float cc = (columns - 1) * 0.5f;
                return Mathf.RoundToInt(Mathf.Max(Mathf.Abs(row - cr), Mathf.Abs(col - cc)));

            default:
                return index;
        }
    }

    // --------------------------------------------------------------- accents
    // A slot that just received something.
    public static void PlayGain(RectTransform slot, Graphic flash)
    {
        if (slot == null) return;

        var theme = MenuTheme.Current;
        UITween.Punch(slot, 0.16f, theme.inventoryGainDuration, Vector3.one);

        if (flash == null) return;

        var colour = flash.color;
        colour.a = 0.85f;
        flash.color = colour;
        UITween.FadeGraphic(flash, 0f, theme.inventoryGainDuration * 1.6f, Ease.OutCubic);
    }

    // A slot whose contents were just thrown away.
    public static void PlayDrop(RectTransform slot, CanvasGroup group)
    {
        if (slot == null) return;

        float d = MenuTheme.Current.inventoryGainDuration;

        UITween.MoveAnchored(slot, slot.anchoredPosition + new Vector2(0f, -22f), d, Ease.InCubic);
        UITween.ScaleTo(slot, Vector3.one * 0.86f, d, Ease.InCubic);
        if (group != null) UITween.Fade(group, 0f, d, Ease.InQuad);
    }

    // ---------------------------------------------------------- registration
    public static void EnsureRegistered()
    {
        if (registered) return;
        registered = true;

        UIAnimLibrary.RegisterIn(BagOpen, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(0f, -54f), d, Ease.OutBack);
            UITween.ScaleFrom(rt, Vector3.one * 0.92f, d, Ease.OutBack);
            UITween.Fade(cg, 1f, d * 0.7f, Ease.OutCubic, done);
        });

        // Vertical unfold, like the flap of a satchel being turned back.
        UIAnimLibrary.RegisterIn(BagUnclasp, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            UITween.Fade(cg, 1f, d * 0.3f, Ease.OutCubic);
            UITween.ScaleFrom(rt, new Vector3(0.98f, 0.04f, 1f), d, Ease.OutCubic, () =>
            {
                if (rt != null) rt.localScale = Vector3.one;
                done?.Invoke();
            });
        });

        UIAnimLibrary.RegisterIn(BagSwing, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            if (rt != null) rt.localRotation = Quaternion.Euler(0f, 0f, -3.5f);
            UITween.RotateZ(rt, 0f, d, Ease.OutBack);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(-180f, -20f), d, Ease.OutQuint);
            UITween.Fade(cg, 1f, d * 0.55f, Ease.OutCubic, done);
        });

        UIAnimLibrary.RegisterIn(BagDrop, (rt, cg, d, done) =>
        {
            Settle(rt, cg, 0f);
            UITween.MoveAnchoredFrom(rt, Home(rt) + new Vector2(0f, 130f), d * 1.15f, Ease.OutBounce);
            UITween.ScaleFrom(rt, Vector3.one * 1.06f, d, Ease.OutCubic);
            UITween.Fade(cg, 1f, d * 0.4f, Ease.OutCubic, done);
        });

        UIAnimLibrary.RegisterOut(BagClose, (rt, cg, d, done) =>
        {
            UITween.Fade(cg, 0f, d, Ease.InQuad);
            UITween.ScaleTo(rt, new Vector3(0.98f, 0.04f, 1f), d, Ease.InCubic, () =>
            {
                Restore(rt);
                done?.Invoke();
            });
        });

        UIAnimLibrary.RegisterOut(BagSink, (rt, cg, d, done) =>
        {
            UITween.MoveAnchored(rt, Home(rt) + new Vector2(0f, -60f), d, Ease.InCubic);
            UITween.Fade(cg, 0f, d, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        });

        UIAnimLibrary.RegisterOut(BagSnap, (rt, cg, d, done) =>
        {
            UITween.ScaleTo(rt, Vector3.one * 0.94f, d * 0.55f, Ease.InQuad);
            UITween.Fade(cg, 0f, d * 0.55f, Ease.InQuad, () => { Restore(rt); done?.Invoke(); });
        });
    }

    // ------------------------------------------------------------------ utils
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
        if (cg != null) { UITween.Kill(cg); cg.alpha = alpha; }
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
