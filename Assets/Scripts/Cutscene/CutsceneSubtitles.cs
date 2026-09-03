using UnityEngine;
using UnityEngine.Playables;

// Captions for a Timeline cutscene, driven off the director's own clock.
//
// The times live here as plain data rather than as Timeline markers, for two
// reasons: they can be re-timed in the Inspector without opening the Timeline
// window, and the build script that generates the scene can fill them in from
// the voice clips it already knows the length of.
//
// Reading director.time rather than counting seconds means the captions stay
// with the picture through a pause, a scrub in the editor, or a scene that is
// re-timed after the fact.
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneSubtitles : MonoBehaviour
{
    [System.Serializable]
    public class Caption
    {
        public float start;
        public float end;

        [TextArea(2, 4)]
        public string text;
    }

    [Tooltip("Shown while the director's playhead is inside each range.")]
    public Caption[] captions;

    PlayableDirector director;
    string showing;

    void Awake() => director = GetComponent<PlayableDirector>();

    void Update()
    {
        if (director == null || captions == null) return;

        // The playhead decides, not the play state.
        //
        // PlayState has only Playing and Paused, so a scene frozen by the pause
        // menu reports the same thing as one that has finished - and clearing on
        // "not Playing" wiped the caption the moment the game was paused, mid
        // sentence. Every caption carries its own window, so a playhead sitting
        // still keeps showing the line it is sitting inside, and one parked past
        // the end of the scene matches nothing and shows nothing.
        float t = (float)director.time;
        for (int i = 0; i < captions.Length; i++)
        {
            var c = captions[i];
            if (c != null && t >= c.start && t < c.end) { Put(c.text); return; }
        }
        Put(null);
    }

    void Put(string text)
    {
        if (showing == text) return;
        showing = text;

        if (string.IsNullOrEmpty(text)) SubtitleUI.I.Clear();
        else SubtitleUI.I.Show(text);
    }

    void OnDisable()
    {
        showing = null;
        if (SubtitleUI.I != null) SubtitleUI.I.Clear();
    }
}
