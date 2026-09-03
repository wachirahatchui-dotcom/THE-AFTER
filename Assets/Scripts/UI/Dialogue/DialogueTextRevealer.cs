using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Types one dialogue line out onto a Text, and nothing else.
//
// Splitting this off DialogueManager means the pacing rules - reading speed,
// the beat held after punctuation, whether subtitles are on at all - live in
// one small file that can be read end to end.
//
// The visual side of the reveal belongs to TypewriterFade: this class only
// decides which characters exist yet.
[RequireComponent(typeof(Text))]
[RequireComponent(typeof(TypewriterFade))]
public class DialogueTextRevealer : MonoBehaviour
{
    [Tooltip("Characters per second at Dialogue Speed = 1. 0 reveals instantly.")]
    public float charactersPerSecond = 35f;

    // Raised once the last character has been typed (not once it has faded in).
    public Action onRevealed;

    public bool IsRevealing { get; private set; }

    Text label;
    TypewriterFade fade;
    Coroutine routine;
    string current = "";

    void Awake()
    {
        label = GetComponent<Text>();
        fade = GetComponent<TypewriterFade>();
    }

    // instant: the player has pushed Dialogue Speed to its maximum.
    // subtitles: when off the line is never drawn, but the timing still runs so
    // voiced lines and auto-advance behave the same either way.
    public void Play(string line, bool instant, bool subtitles)
    {
        Stop();

        current = line ?? "";
        var theme = MenuTheme.Current;

        fade.charFade = instant ? 0f : theme.dialogueCharFade;
        fade.charRise = theme.dialogueCharRise;
        fade.Restart();

        if (!subtitles)
        {
            label.text = "";
            IsRevealing = false;
            onRevealed?.Invoke();
            return;
        }

        if (instant || charactersPerSecond <= 0f || current.Length == 0)
        {
            label.text = current;
            IsRevealing = false;
            onRevealed?.Invoke();
            return;
        }

        routine = StartCoroutine(Reveal());
    }

    // Jump to the finished line - the first advance press while typing.
    public void Complete()
    {
        Stop();
        label.text = GameSettings.Subtitles ? current : "";
        IsRevealing = false;
    }

    public void Clear()
    {
        Stop();
        current = "";
        label.text = "";
        fade.Restart();
        IsRevealing = false;
    }

    void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    IEnumerator Reveal()
    {
        IsRevealing = true;
        label.text = "";

        float shown = 0f;
        int typed = 0;

        while (typed < current.Length)
        {
            // Read live so dragging the Dialogue Speed slider re-paces the line
            // currently on screen.
            shown += Time.unscaledDeltaTime * charactersPerSecond
                   * Mathf.Max(0.05f, GameSettings.DialogueTextSpeed);

            int n = Mathf.Clamp(Mathf.FloorToInt(shown), 0, current.Length);
            if (n != typed)
            {
                label.text = current.Substring(0, n);
                typed = n;

                float pause = PauseAfter(typed);
                if (pause > 0f)
                {
                    float t = 0f;
                    while (t < pause) { t += Time.unscaledDeltaTime; yield return null; }
                    shown = typed;   // the pause must not bank up characters
                }
            }
            yield return null;
        }

        label.text = current;
        IsRevealing = false;
        routine = null;
        onRevealed?.Invoke();
    }

    // A beat after sentence-ending punctuation, half a beat after a comma -
    // but only when the mark actually ends a word, so "3.5" is not read aloud
    // as two sentences.
    float PauseAfter(int typed)
    {
        char c = current[typed - 1];
        bool endsWord = typed >= current.Length || current[typed] == ' ';
        if (!endsWord) return 0f;

        float beat = MenuTheme.Current.dialoguePunctuationPause;
        if (beat <= 0f) return 0f;

        if (c == '.' || c == '!' || c == '?' || c == '\u2026') return beat;
        if (c == ',' || c == ';' || c == ':') return beat * 0.5f;
        return 0f;
    }
}
