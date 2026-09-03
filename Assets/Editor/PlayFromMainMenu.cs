using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Makes the editor's Play button always boot the main menu, whatever scene is
// currently open.
//
// Without this, Play runs the open scene, so working in Chapter1 drops you
// straight into the level with no menu, no settings applied through the menu
// flow and no CONTINUE - which does not match how the built game starts.
// MainMenu is scene 0 in Build Settings, so this makes the editor agree with
// the build.
//
// Toggle it from the menu bar: THE AFTER > Always Play From Main Menu.
[InitializeOnLoad]
public static class PlayFromMainMenu
{
    const string MenuPath = "THE AFTER/Always Play From Main Menu";
    const string PrefKey = "TheAfter.PlayFromMainMenu";
    const string MenuScenePath = "Assets/Scenes/MainMenu.unity";

    static PlayFromMainMenu()
    {
        // Deferred: EditorBuildSettings and the asset database are not
        // guaranteed to be ready inside a static constructor.
        EditorApplication.delayCall += Apply;
    }

    static bool Enabled
    {
        get { return EditorPrefs.GetBool(PrefKey, true); }
        set { EditorPrefs.SetBool(PrefKey, value); }
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Apply();
        Debug.Log("[PlayFromMainMenu] " + (Enabled
            ? "Play now always starts at the main menu."
            : "Play now starts at whichever scene is open."));
    }

    [MenuItem(MenuPath, true)]
    static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    static void Apply()
    {
        if (!Enabled)
        {
            EditorSceneManager.playModeStartScene = null;
            return;
        }

        var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        if (scene == null)
        {
            Debug.LogWarning("[PlayFromMainMenu] " + MenuScenePath + " not found; Play will use the open scene.");
            return;
        }

        EditorSceneManager.playModeStartScene = scene;
    }
}
