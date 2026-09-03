using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Per-character fade-in for a legacy UI Text.
//
// DialogueTextRevealer grows Text.text one character at a time; this effect
// stamps the moment each new glyph appeared and fades + lifts it into place,
// so a line arrives letter by letter instead of popping.
//
// It works on glyph quads rather than string indices on purpose. Quads are
// emitted in reading order and only for characters that actually draw, so
// "the newest quad" is always the newest glyph - no mapping between string
// index and mesh index is needed, and word wrap cannot desynchronise it.
//
// Set charFade to 0 for an instant reveal (the Instant dialogue-speed setting).
[RequireComponent(typeof(Text))]
public class TypewriterFade : BaseMeshEffect
{
    [Tooltip("Seconds one character takes to fade in. 0 disables the effect.")]
    public float charFade = 0.16f;

    [Tooltip("Pixels a character rises through while fading in.")]
    public float charRise = 9f;

    // One birth timestamp per glyph quad, in reading order.
    readonly List<float> born = new List<float>();
    bool animating;

    // True while at least one glyph is still fading. The revealer uses this to
    // know the line has fully settled, not just finished typing.
    public bool Animating { get { return animating; } }

    // Called before each new line so the next glyph counts as brand new.
    public void Restart()
    {
        born.Clear();
        animating = false;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    // The mesh only rebuilds when something marks it dirty, so drive it here
    // for as long as anything is mid-fade.
    void Update()
    {
        if (animating && graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive() || vh.currentVertCount == 0)
        {
            animating = false;
            return;
        }

        int quads = vh.currentVertCount / 4;
        float now = Time.unscaledTime;

        while (born.Count < quads) born.Add(now);
        if (born.Count > quads) born.RemoveRange(quads, born.Count - quads);

        if (charFade <= 0f)
        {
            animating = false;
            return;
        }

        bool any = false;
        var v = new UIVertex();

        for (int q = 0; q < quads; q++)
        {
            float k = Mathf.Clamp01((now - born[q]) / charFade);
            if (k >= 1f) continue;
            any = true;

            float e = 1f - Mathf.Pow(1f - k, 3f);      // OutCubic
            float lift = (1f - e) * charRise;

            for (int i = 0; i < 4; i++)
            {
                int idx = q * 4 + i;
                vh.PopulateUIVertex(ref v, idx);

                var c = v.color;
                c.a = (byte)Mathf.Clamp(c.a * e, 0f, 255f);
                v.color = c;
                v.position += new Vector3(0f, -lift, 0f);

                vh.SetUIVertex(v, idx);
            }
        }

        animating = any;
    }
}
