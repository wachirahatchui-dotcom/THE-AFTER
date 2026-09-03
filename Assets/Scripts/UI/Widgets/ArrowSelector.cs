using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A "< value >" stepper used instead of a Dropdown.
//
// uGUI's Dropdown needs a styled template prefab to not look like a Windows 95
// combo box, and its popup fights the panel animations. A stepper is fully
// themeable from code, reads well on a gamepad, and never opens an overlay.
//
// Not a MonoBehaviour - it just owns the widgets it built and the button
// callbacks close over it.
public class ArrowSelector
{
    public readonly GameObject Root;
    public readonly Button Left;
    public readonly Button Right;
    public readonly Text ValueLabel;

    readonly List<string> options;
    int index;
    readonly Action<int> onChange;

    public int Index { get { return index; } }
    public string Value { get { return options.Count == 0 ? "" : options[index]; } }
    public int Count { get { return options.Count; } }

    public ArrowSelector(GameObject root, Button left, Button right, Text valueLabel,
                         List<string> options, int startIndex, Action<int> onChange)
    {
        Root = root;
        Left = left;
        Right = right;
        ValueLabel = valueLabel;

        this.options = options ?? new List<string>();
        this.onChange = onChange;
        index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, this.options.Count - 1));

        left.onClick.AddListener(() => Step(-1));
        right.onClick.AddListener(() => Step(1));

        Refresh(false);
    }

    public void Step(int delta)
    {
        if (options.Count == 0) return;

        int next = index + delta;
        if (next < 0 || next >= options.Count)
        {
            // Hit the end of the list - nudge instead of wrapping, so the
            // player can feel where the boundary is.
            UIAudio.Play(UISound.Error, 0.35f);
            UITween.Shake((RectTransform)Root.transform, 4f, 0.18f);
            return;
        }

        index = next;
        Refresh(true);
        onChange?.Invoke(index);
    }

    public void SetIndex(int value, bool notify)
    {
        if (options.Count == 0) return;
        index = Mathf.Clamp(value, 0, options.Count - 1);
        Refresh(false);
        if (notify) onChange?.Invoke(index);
    }

    public void SetOptions(List<string> newOptions, int newIndex)
    {
        options.Clear();
        if (newOptions != null) options.AddRange(newOptions);
        index = Mathf.Clamp(newIndex, 0, Mathf.Max(0, options.Count - 1));
        Refresh(false);
    }

    void Refresh(bool animate)
    {
        if (ValueLabel != null)
        {
            ValueLabel.text = Value;
            if (animate)
            {
                UIAudio.PlayVaried(UISound.Tick, 0.8f);
                UITween.Punch(ValueLabel.transform, 0.10f, 0.20f, Vector3.one);
            }
        }

        // Grey out the arrow that cannot move any further.
        SetArrowEnabled(Left, index > 0);
        SetArrowEnabled(Right, index < options.Count - 1);
    }

    static void SetArrowEnabled(Button b, bool on)
    {
        if (b == null) return;
        var t = b.GetComponentInChildren<Text>(true);
        if (t == null) return;
        var c = t.color;
        c.a = on ? 1f : 0.28f;
        t.color = c;
    }
}
