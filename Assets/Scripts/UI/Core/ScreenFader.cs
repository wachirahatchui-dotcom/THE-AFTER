using System;
using UnityEngine;
using UnityEngine.UI;

// Full-screen fade used for scene transitions.
//
// Lives on its own DontDestroyOnLoad canvas at a very high sorting order so it
// covers the menu, the loading gap and the first frames of the game scene -
// which is the whole point: without it, LoadScene shows a hard cut.
public class ScreenFader : MonoBehaviour
{
    static ScreenFader instance;

    Image sheet;
    CanvasGroup group;

    public static ScreenFader I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~ScreenFader");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<ScreenFader>();
                instance.Build();
            }
            return instance;
        }
    }

    void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        gameObject.AddComponent<CanvasScaler>();

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var go = new GameObject("Sheet", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        sheet = go.GetComponent<Image>();
        sheet.color = Color.black;
        sheet.raycastTarget = true;

        var rt = sheet.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    public void SetColor(Color c)
    {
        if (sheet != null) sheet.color = new Color(c.r, c.g, c.b, sheet.color.a);
    }

    // Fades to opaque, runs `midpoint` (typically SceneManager.LoadScene), then
    // fades back in. The blocker is on for the whole thing so nothing under it
    // can be clicked mid-transition.
    public void Transition(Action midpoint, float outDuration = 0.55f, float holdSeconds = 0.1f, float inDuration = 0.7f)
    {
        group.blocksRaycasts = true;
        UITween.Kill(group);

        UITween.Fade(group, 1f, outDuration, Ease.InQuad, () =>
        {
            midpoint?.Invoke();
            UITween.Delay(group, holdSeconds, () =>
            {
                UITween.Fade(group, 0f, inDuration, Ease.OutQuad, () =>
                {
                    if (group != null) group.blocksRaycasts = false;
                });
            });
        });
    }

    public void FadeOut(float duration = 0.5f, Action onComplete = null)
    {
        group.blocksRaycasts = true;
        UITween.Kill(group);
        UITween.Fade(group, 1f, duration, Ease.InQuad, onComplete);
    }

    public void FadeIn(float duration = 0.7f, Action onComplete = null)
    {
        UITween.Kill(group);
        UITween.Fade(group, 0f, duration, Ease.OutQuad, () =>
        {
            if (group != null) group.blocksRaycasts = false;
            onComplete?.Invoke();
        });
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
