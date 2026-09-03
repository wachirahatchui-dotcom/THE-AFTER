using UnityEngine;

// Persistent host for the UI audio sources.
//
// SFX go through a small round-robin pool so a fast burst of clicks does not
// cut itself off, and music gets its own dedicated source so it can crossfade
// independently of anything the menu is doing.
public class UIAudioPlayer : MonoBehaviour
{
    const int PoolSize = 8;

    static UIAudioPlayer instance;

    AudioSource[] pool;
    int next;
    AudioSource musicA, musicB;
    bool musicOnA = true;

    public static UIAudioPlayer I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~UIAudioPlayer");
                instance = go.AddComponent<UIAudioPlayer>();
                DontDestroyOnLoad(go);
                instance.Build();
            }
            return instance;
        }
    }

    void Build()
    {
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;          // pure 2D, never positioned in the world
            s.ignoreListenerPause = true; // keeps working while the game is paused
            s.ignoreListenerVolume = false;
            pool[i] = s;
        }

        musicA = NewMusicSource();
        musicB = NewMusicSource();
    }

    AudioSource NewMusicSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        s.volume = 0f;
        return s;
    }

    public void PlayOneShot(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) return;
        var s = pool[next];
        next = (next + 1) % PoolSize;
        s.pitch = pitch;
        s.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public AudioSource ActiveMusic { get { return musicOnA ? musicA : musicB; } }
    AudioSource IdleMusic { get { return musicOnA ? musicB : musicA; } }

    // Fades the current track down while bringing the new one up, so switching
    // menu themes never produces a hard cut.
    public void CrossfadeMusic(AudioClip clip, float targetVolume, float duration)
    {
        var from = ActiveMusic;
        var to = IdleMusic;

        if (clip == null)
        {
            UITween.To(this, duration, Ease.InOutQuad,
                k => { if (from != null) from.volume = Mathf.Lerp(from.volume, 0f, k); },
                () => { if (from != null) from.Stop(); });
            return;
        }

        to.clip = clip;
        to.volume = 0f;
        to.Play();

        float fromStart = from != null ? from.volume : 0f;
        UITween.To(this, duration, Ease.InOutQuad, k =>
        {
            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(0f, targetVolume, k);
        }, () =>
        {
            if (from != null) from.Stop();
        });

        musicOnA = !musicOnA;
    }

    public void SetMusicVolume(float v)
    {
        if (ActiveMusic != null) ActiveMusic.volume = Mathf.Clamp01(v);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
