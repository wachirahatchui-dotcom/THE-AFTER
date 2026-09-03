using UnityEngine;
using UnityEngine.SceneManagement;

// Applies the settings that need a live scene object, and owns the two global
// behaviours that have nowhere else to live: mute-when-minimised and the FPS
// counter.
//
// GameSettings can talk to QualitySettings and Screen through SettingsApplier,
// but camera field of view needs something that exists in
// the scene and survives a load. This bootstraps itself the same way PauseMenu
// does, so no scene has to place it by hand, and re-applies on every scene load
// because a new scene brings a new camera.
public class SettingsRuntime : MonoBehaviour
{
    static SettingsRuntime instance;

    Camera cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        // Reads settings.json and pushes it at the engine before the first
        // scene draws a frame.
        SettingsStore.Load();

        Ensure();
        SceneManager.sceneLoaded += (scene, mode) =>
        {
            Ensure();
            if (instance != null)
            {
                instance.cached = null;   // new scene, new camera
                instance.ApplyAll();
            }
        };
    }

    static void Ensure()
    {
        if (instance != null) return;

        var go = new GameObject("~SettingsRuntime");
        instance = go.AddComponent<SettingsRuntime>();
        DontDestroyOnLoad(go);
    }

    void OnEnable()
    {
        SettingsStore.Applied += ApplyAll;
        ApplyAll();
    }

    void OnDisable()
    {
        SettingsStore.Applied -= ApplyAll;
    }

    void ApplyAll()
    {
        ApplyFieldOfView();
        FpsCounter.SetVisible(GameSettings.ShowFps);
    }

    void ApplyFieldOfView()
    {
        var cam = ActiveCamera();
        if (cam != null && !cam.orthographic)
            cam.fieldOfView = GameSettings.FieldOfView;
    }

    Camera ActiveCamera()
    {
        if (cached != null && cached.isActiveAndEnabled) return cached;

        cached = Camera.main;
        if (cached == null) cached = FindAnyObjectByType<Camera>();
        return cached;
    }

    // The main camera can be swapped at runtime, so the FOV is re-asserted
    // rather than only pushed on change. Cheap: one float compare.
    void LateUpdate()
    {
        var cam = ActiveCamera();
        if (cam != null && !cam.orthographic
            && !Mathf.Approximately(cam.fieldOfView, GameSettings.FieldOfView))
        {
            cam.fieldOfView = GameSettings.FieldOfView;
        }
    }

    // Silences the game when another window takes focus, if asked to.
    void OnApplicationFocus(bool hasFocus)
    {
        if (!GameSettings.MuteWhenUnfocused) { AudioListener.volume = GameSettings.MasterVolume; return; }
        AudioListener.volume = hasFocus ? GameSettings.MasterVolume : 0f;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
