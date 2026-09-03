using UnityEngine;
using UnityEngine.SceneManagement;

// Counts how long the player has actually been playing, so save slots can show
// a play time instead of just a timestamp.
//
// Bootstraps itself the same way PauseMenu does, so no scene needs a manually
// placed object. Time only accrues in gameplay scenes: the menu scene is
// excluded, and paused time is excluded because it ticks on scaled deltaTime.
public class PlayTimeTracker : MonoBehaviour
{
    public static string MenuSceneName = "MainMenu";

    static PlayTimeTracker instance;
    static float total;

    public static float TotalSeconds { get { return total; } }
    public static void SetTotal(float seconds) { total = Mathf.Max(0f, seconds); }
    public static void Reset() { total = 0f; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        Ensure();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void Ensure()
    {
        if (instance != null) return;
        var go = new GameObject("~PlayTimeTracker");
        instance = go.AddComponent<PlayTimeTracker>();
        DontDestroyOnLoad(go);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Ensure();

        if (scene.name == MenuSceneName)
        {
            // Back at the menu - the previous run is over, so its story state
            // must not leak into whatever is started next.
            total = 0f;
            DialogueProgress.Clear();
            return;
        }

        // A slot was queued by the menu; apply it now that the scene exists.
        SaveSystem.ConsumePendingLoad();
    }

    void Update()
    {
        if (SceneManager.GetActiveScene().name == MenuSceneName) return;
        total += Time.deltaTime;   // scaled, so a paused game does not accrue time
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
