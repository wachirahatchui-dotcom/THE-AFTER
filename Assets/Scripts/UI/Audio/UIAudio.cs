using System;
using System.Collections.Generic;
using UnityEngine;

public enum UISound
{
    Hover, Click, Back, Confirm, Cancel, Error,
    Whoosh, Tick, SlotSelect, Toggle, SliderStep,
    PanelOpen, PanelClose, GameStart
}

// Every UI sound in the game, synthesised at runtime.
//
// The project ships with no SFX files, so each cue is generated from a tiny
// oscillator + envelope the first time it is asked for and cached. Dropping a
// real .wav / .mp3 at Assets/Resources/Audio/UI/<Name> (for example
// "Assets/Resources/Audio/UI/Click.wav") transparently overrides the synth
// version with no code change - the loader checks Resources first.
//
// Music works the same way: Assets/Resources/Audio/Music/MainTheme is used if
// it exists, otherwise a soft generated drone plays underneath the menu.
public static class UIAudio
{
    public const string SfxResourceFolder = "Audio/UI/";
    public const string MusicResourceFolder = "Audio/Music/";
    public const string MainThemeName = "MainTheme";

    public const string K_Master = "MasterVolume";
    public const string K_Music = "MusicVolume";
    public const string K_Sfx = "SfxVolume";

    const int Rate = 44100;

    static readonly Dictionary<UISound, AudioClip> cache = new Dictionary<UISound, AudioClip>();
    static AudioClip musicClip;
    static bool musicResolved;

    // ---------------------------------------------------------------- volumes
    // Read straight from GameSettings, which follows the draft while a volume
    // slider is being dragged so the preview is audible. The K_* keys survive
    // only so SettingsFile can migrate a pre-JSON install; nothing writes them.
    public static float Master { get { return GameSettings.MasterVolume; } }
    public static float MusicVolume { get { return GameSettings.MusicVolume; } }
    public static float SfxVolume { get { return GameSettings.SfxVolume; } }
    public static float VoiceVolume { get { return GameSettings.VoiceVolume; } }
    public static float AmbienceVolume { get { return GameSettings.AmbienceVolume; } }

    // Called by SettingsApplier whenever any audio value changes.
    public static void RefreshVolumes()
    {
        UIAudioPlayer.I.SetMusicVolume(MusicVolume);
    }

    public static void ApplySavedVolumes()
    {
        AudioListener.volume = Master;
        RefreshVolumes();
    }

    // ------------------------------------------------------------------ play
    public static void Play(UISound sound, float volumeScale = 1f, float pitch = 1f)
    {
        var clip = Get(sound);
        if (clip == null) return;
        UIAudioPlayer.I.PlayOneShot(clip, SfxVolume * volumeScale, pitch);
    }

    // Slight random detune keeps rapid repeats (hovering down a button list)
    // from sounding like a machine gun.
    public static void PlayVaried(UISound sound, float volumeScale = 1f, float pitchSpread = 0.06f)
    {
        Play(sound, volumeScale, 1f + UnityEngine.Random.Range(-pitchSpread, pitchSpread));
    }

    public static AudioClip Get(UISound sound)
    {
        if (cache.TryGetValue(sound, out var c) && c != null) return c;

        // A real file always wins over the synthesised fallback.
        c = Resources.Load<AudioClip>(SfxResourceFolder + sound);
        if (c == null) c = Synthesise(sound);

        cache[sound] = c;
        return c;
    }

    // ----------------------------------------------------------------- music
    public static void StartMenuMusic(float fadeDuration = 2f)
    {
        if (!musicResolved)
        {
            musicResolved = true;
            musicClip = Resources.Load<AudioClip>(MusicResourceFolder + MainThemeName);
            if (musicClip == null) musicClip = BuildAmbientDrone();
        }
        UIAudioPlayer.I.CrossfadeMusic(musicClip, MusicVolume, fadeDuration);
    }

    public static void StopMusic(float fadeDuration = 1f)
    {
        UIAudioPlayer.I.CrossfadeMusic(null, 0f, fadeDuration);
    }

    // ------------------------------------------------------------- synthesis
    enum Wave { Sine, Triangle, Square, Saw, Noise }

    static AudioClip Synthesise(UISound sound)
    {
        switch (sound)
        {
            case UISound.Hover:
                return Blend("ui_hover",
                    Tone(0.055f, 900f, 1180f, Wave.Sine, 0.004f, 3.2f, 0.22f),
                    Tone(0.020f, 2400f, 1800f, Wave.Noise, 0.001f, 9f, 0.05f));

            case UISound.Click:
                return Blend("ui_click",
                    Tone(0.100f, 640f, 300f, Wave.Triangle, 0.002f, 4.5f, 0.40f),
                    Tone(0.028f, 3000f, 1200f, Wave.Noise, 0.001f, 8f, 0.12f));

            case UISound.Back:
                return Blend("ui_back",
                    Tone(0.120f, 430f, 235f, Wave.Triangle, 0.004f, 4f, 0.35f),
                    Tone(0.030f, 1600f, 900f, Wave.Noise, 0.001f, 8f, 0.06f));

            case UISound.Confirm:
                return Blend("ui_confirm",
                    Tone(0.190f, 523f, 784f, Wave.Sine, 0.006f, 2.6f, 0.30f),
                    Tone(0.190f, 784f, 1046f, Wave.Sine, 0.030f, 2.8f, 0.16f));

            case UISound.Cancel:
                return Tone("ui_cancel", 0.160f, 360f, 200f, Wave.Triangle, 0.004f, 3.6f, 0.32f);

            case UISound.Error:
                return Blend("ui_error",
                    Tone(0.230f, 196f, 178f, Wave.Square, 0.005f, 2.2f, 0.24f),
                    Tone(0.230f, 294f, 262f, Wave.Square, 0.005f, 2.4f, 0.12f));

            case UISound.Whoosh:
                return Sweep("ui_whoosh", 0.34f, 380f, 2600f, 0.28f);

            case UISound.Tick:
                return Blend("ui_tick",
                    Tone(0.026f, 1800f, 1500f, Wave.Sine, 0.001f, 12f, 0.18f),
                    Tone(0.014f, 4000f, 3000f, Wave.Noise, 0.001f, 14f, 0.06f));

            case UISound.SlotSelect:
                return Blend("ui_slot",
                    Tone(0.090f, 700f, 700f, Wave.Sine, 0.004f, 4f, 0.24f),
                    Tone(0.090f, 1050f, 1050f, Wave.Sine, 0.012f, 4.4f, 0.14f));

            case UISound.Toggle:
                return Tone("ui_toggle", 0.070f, 820f, 1240f, Wave.Square, 0.002f, 6f, 0.16f);

            case UISound.SliderStep:
                return Tone("ui_sliderstep", 0.020f, 1500f, 1500f, Wave.Sine, 0.001f, 14f, 0.10f);

            case UISound.PanelOpen:
                return Blend("ui_open",
                    Sweep(null, 0.30f, 300f, 2200f, 0.16f),
                    Tone(0.300f, 220f, 440f, Wave.Sine, 0.020f, 2.2f, 0.16f));

            case UISound.PanelClose:
                return Blend("ui_close",
                    Sweep(null, 0.26f, 2000f, 320f, 0.14f),
                    Tone(0.260f, 400f, 190f, Wave.Sine, 0.010f, 2.6f, 0.16f));

            case UISound.GameStart:
                return Blend("ui_start",
                    Tone(0.900f, 96f, 58f, Wave.Sine, 0.010f, 1.4f, 0.45f),
                    Blend(null,
                        Tone(0.900f, 392f, 784f, Wave.Sine, 0.180f, 1.6f, 0.14f),
                        Sweep(null, 0.60f, 500f, 5000f, 0.10f)));

            default:
                return Tone("ui_default", 0.08f, 600f, 400f, Wave.Sine, 0.003f, 5f, 0.3f);
        }
    }

    // One oscillator with a pitch glide and an exponential decay envelope.
    static float[] Tone(float duration, float freqStart, float freqEnd, Wave wave,
                        float attack, float decayRate, float amplitude)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(duration * Rate));
        var data = new float[n];
        var rnd = new System.Random(unchecked((int)(freqStart * 131 + duration * 977)));
        double phase = 0;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float k = i / (float)n;
            float f = Mathf.Lerp(freqStart, freqEnd, k);
            phase += f / Rate;
            float p = (float)(phase - Math.Floor(phase));

            float s;
            switch (wave)
            {
                case Wave.Triangle: s = 4f * Mathf.Abs(p - 0.5f) - 1f; break;
                case Wave.Square:   s = p < 0.5f ? 1f : -1f; break;
                case Wave.Saw:      s = 2f * p - 1f; break;
                case Wave.Noise:    s = (float)(rnd.NextDouble() * 2.0 - 1.0); break;
                default:            s = Mathf.Sin(p * 2f * Mathf.PI); break;
            }

            float env = Mathf.Exp(-decayRate * t * 6f);
            if (attack > 0f && t < attack) env *= t / attack;
            // Short fade at the tail so the buffer never ends on a hard edge.
            float tail = Mathf.Clamp01((n - i) / 128f);

            data[i] = s * env * amplitude * tail;
        }
        return data;
    }

    static AudioClip Tone(string name, float duration, float f0, float f1, Wave wave,
                          float attack, float decay, float amp)
    {
        return ToClip(name, Tone(duration, f0, f1, wave, attack, decay, amp));
    }

    // Band-limited noise whose centre frequency glides - the classic UI whoosh.
    // Implemented as a one-pole lowpass with a moving coefficient, minus a
    // slower one-pole to fake the highpass side of a bandpass.
    static float[] Sweep(float duration, float fStart, float fEnd, float amplitude)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(duration * Rate));
        var data = new float[n];
        var rnd = new System.Random(4242);
        float lp = 0f, hp = 0f;

        for (int i = 0; i < n; i++)
        {
            float k = i / (float)n;
            float x = (float)(rnd.NextDouble() * 2.0 - 1.0);

            float cutoff = Mathf.Lerp(fStart, fEnd, k);
            float a = Mathf.Clamp01(2f * Mathf.PI * cutoff / Rate);
            lp += a * (x - lp);

            float aH = Mathf.Clamp01(2f * Mathf.PI * (cutoff * 0.35f) / Rate);
            hp += aH * (lp - hp);

            // Bell-shaped envelope so it swells and dies rather than clicking in.
            float env = Mathf.Sin(k * Mathf.PI);
            data[i] = (lp - hp) * env * amplitude * 3f;
        }
        return data;
    }

    static AudioClip Sweep(string name, float duration, float f0, float f1, float amp)
    {
        var d = Sweep(duration, f0, f1, amp);
        return name == null ? ToClip("tmp", d) : ToClip(name, d);
    }

    // ----------------------------------------------------------- mixing utils
    static float[] Mix(float[] a, float[] b)
    {
        int n = Mathf.Max(a.Length, b.Length);
        var outp = new float[n];
        for (int i = 0; i < n; i++)
        {
            float v = 0f;
            if (i < a.Length) v += a[i];
            if (i < b.Length) v += b[i];
            outp[i] = Mathf.Clamp(v, -1f, 1f);
        }
        return outp;
    }

    static AudioClip Blend(string name, float[] a, float[] b) { return ToClip(name ?? "tmp", Mix(a, b)); }
    static AudioClip Blend(string name, float[] a, AudioClip b) { return ToClip(name ?? "tmp", Mix(a, Samples(b))); }
    static AudioClip Blend(string name, AudioClip a, AudioClip b) { return ToClip(name ?? "tmp", Mix(Samples(a), Samples(b))); }
    static AudioClip Blend(string name, AudioClip a, float[] b) { return ToClip(name ?? "tmp", Mix(Samples(a), b)); }

    static float[] Samples(AudioClip c)
    {
        if (c == null) return new float[0];
        var d = new float[c.samples * c.channels];
        c.GetData(d, 0);
        return d;
    }

    static AudioClip ToClip(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // ------------------------------------------------------- ambient fallback
    // A 12 second seamless pad used only when no MainTheme file is present.
    // Every partial is an exact multiple of 1/12 Hz so the buffer loops without
    // a seam, and the noise layer is crossfaded into its own head.
    static AudioClip BuildAmbientDrone()
    {
        const float loop = 12f;
        int n = Mathf.CeilToInt(loop * Rate);
        var data = new float[n];
        var rnd = new System.Random(90210);

        float[] partials = { 55f, 82.5f, 110f, 164.5f, 220f };
        float[] gains = { 0.30f, 0.16f, 0.13f, 0.06f, 0.035f };
        float[] lfoHz = { 1f / 12f, 2f / 12f, 1f / 12f, 3f / 12f, 2f / 12f };

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float v = 0f;
            for (int p = 0; p < partials.Length; p++)
            {
                float lfo = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * lfoHz[p] * t);
                v += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * gains[p] * lfo;
            }
            data[i] = v;
        }

        // Soft wind layer: heavily lowpassed noise, then head/tail crossfaded.
        var wind = new float[n];
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float x = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += 0.0022f * (x - lp);
            wind[i] = lp * 6f;
        }
        int fade = Mathf.CeilToInt(1.5f * Rate);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            wind[i] = Mathf.Lerp(wind[n - fade + i], wind[i], k);
        }

        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(data[i] * 0.5f + wind[i] * 0.35f, -1f, 1f);

        var clip = AudioClip.Create("ui_ambient_drone", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
