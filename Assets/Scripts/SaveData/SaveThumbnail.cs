using System;
using System.IO;
using UnityEngine;

// The little picture on each save slot.
//
// Captured by rendering the active camera into a RenderTexture rather than
// with ScreenCapture: that is synchronous (so it can run inside the same call
// that writes the JSON) and it leaves the UI out, so the thumbnail shows the
// world rather than the pause menu that was covering it.
//
// Stored as a PNG next to the slot file - saves/slot_N.png - so deleting a
// slot deletes both.
public static class SaveThumbnail
{
    public const int Width = 320;
    public const int Height = 180;

    public static string PathFor(int slot)
    {
        return Path.Combine(SaveSystem.Dir, "slot_" + slot + ".png");
    }

    public static bool Exists(int slot)
    {
        return File.Exists(PathFor(slot));
    }

    // Renders the current view and writes it. Returns false if there was no
    // camera to render (the main menu, for instance).
    public static bool Capture(int slot)
    {
        var cam = Camera.main;
        if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
        if (cam == null) return false;

        RenderTexture rt = null;
        Texture2D tex = null;
        var previousTarget = cam.targetTexture;
        var previousActive = RenderTexture.active;

        try
        {
            rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32);

            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            Directory.CreateDirectory(SaveSystem.Dir);
            File.WriteAllBytes(PathFor(slot), tex.EncodeToPNG());
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveThumbnail] Could not capture slot " + slot + ": " + e.Message);
            return false;
        }
        finally
        {
            // Restore first, destroy second - leaving the camera pointed at a
            // released texture would blank the game view.
            cam.targetTexture = previousTarget;
            RenderTexture.active = previousActive;

            if (rt != null) RenderTexture.ReleaseTemporary(rt);
            if (tex != null) UnityEngine.Object.Destroy(tex);
        }
    }

    public static Sprite Load(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return null;

            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!tex.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveThumbnail] Could not load slot " + slot + ": " + e.Message);
            return null;
        }
    }

    public static void Delete(int slot)
    {
        try
        {
            string path = PathFor(slot);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[SaveThumbnail] Could not delete slot " + slot + ": " + e.Message);
        }
    }
}
