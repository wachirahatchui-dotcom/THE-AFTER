using UnityEngine;

// The single entry point for every menu tunable.
//
// Everything in the UI reads MenuTheme.Current.<field>. The value comes from
// an optional MenuThemeAsset, resolved in this order:
//
//   1. an asset assigned on MainMenuUI in the Inspector  (MenuTheme.Use)
//   2. Assets/Resources/MenuTheme.asset                  (auto-loaded)
//   3. the field defaults declared on MenuThemeAsset     (always works)
//
// Step 3 means the game never depends on an asset existing, so nothing breaks
// if the file is deleted or has not been created yet.
public static class MenuTheme
{
    static MenuThemeAsset current;

    public const string ResourcePath = "MenuTheme";

    public static MenuThemeAsset Current
    {
        get
        {
            if (current == null)
            {
                current = Resources.Load<MenuThemeAsset>(ResourcePath);

                if (current == null)
                {
                    // No asset in the project: fall back to a throwaway instance
                    // that carries the declared defaults.
                    current = ScriptableObject.CreateInstance<MenuThemeAsset>();
                    current.name = "MenuTheme (built-in defaults)";
                }
            }
            return current;
        }
    }

    // Called by MainMenuUI when a theme asset is wired up in the Inspector.
    public static void Use(MenuThemeAsset asset)
    {
        if (asset != null) current = asset;
    }

    // Forces the next read to resolve again. Useful after creating the asset.
    public static void Reset()
    {
        current = null;
    }
}
