using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A menu page that knows how to animate itself on and off screen.
//
// The transition is looked up by name from UIAnimLibrary, so swapping how a
// page enters is a string change, and a page can be given a bespoke animation
// without touching this class.
[RequireComponent(typeof(CanvasGroup))]
public class UIPanel : MonoBehaviour
{
    public string inAnim = UIAnimLibrary.DefaultIn;
    public string outAnim = UIAnimLibrary.DefaultOut;
    public float duration = 0.3f;

    [Tooltip("Control focused first when this panel opens (keyboard / gamepad).")]
    public GameObject firstSelected;

    [Tooltip("Invoked when BACK / Escape is pressed while this panel is on top.")]
    public Action onBack;

    public bool IsOpen { get; private set; }
    public bool IsAnimating { get; private set; }

    CanvasGroup cg;
    RectTransform rt;

    public CanvasGroup Group
    {
        get
        {
            if (cg == null) cg = GetComponent<CanvasGroup>();
            return cg;
        }
    }

    public RectTransform Rect
    {
        get
        {
            if (rt == null) rt = (RectTransform)transform;
            return rt;
        }
    }

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
        rt = (RectTransform)transform;
    }

    public UIPanel SetTransition(string enter, string exit, float seconds = -1f)
    {
        inAnim = enter;
        outAnim = exit;
        if (seconds > 0f) duration = seconds;
        return this;
    }

    // ------------------------------------------------------------------ show
    public void Show(Action onComplete = null)
    {
        if (IsOpen && !IsAnimating) { onComplete?.Invoke(); return; }

        gameObject.SetActive(true);
        IsOpen = true;
        IsAnimating = true;

        Group.blocksRaycasts = true;
        Group.interactable = true;

        UIAnimLibrary.PlayIn(inAnim, Rect, Group, duration, () =>
        {
            IsAnimating = false;
            FocusFirst();
            onComplete?.Invoke();
        });
    }

    public void ShowInstant()
    {
        gameObject.SetActive(true);
        IsOpen = true;
        IsAnimating = false;
        Group.alpha = 1f;
        Group.blocksRaycasts = true;
        Group.interactable = true;
        FocusFirst();
    }

    // ------------------------------------------------------------------ hide
    public void Hide(Action onComplete = null)
    {
        if (!IsOpen) { onComplete?.Invoke(); return; }

        IsOpen = false;
        IsAnimating = true;

        // Stop taking input the moment the exit starts, so a double-click
        // during the fade cannot re-trigger whatever is underneath.
        Group.blocksRaycasts = false;
        Group.interactable = false;

        UIAnimLibrary.PlayOut(outAnim, Rect, Group, duration, () =>
        {
            IsAnimating = false;
            if (this != null) gameObject.SetActive(false);
            onComplete?.Invoke();
        });
    }

    public void HideInstant()
    {
        IsOpen = false;
        IsAnimating = false;
        Group.alpha = 0f;
        Group.blocksRaycasts = false;
        Group.interactable = false;
        gameObject.SetActive(false);
    }

    void FocusFirst()
    {
        if (firstSelected == null || EventSystem.current == null) return;
        if (!firstSelected.activeInHierarchy) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(firstSelected);
    }

    // Re-caches every button's resting position after the layout has settled.
    // VerticalLayoutGroup positions children a frame late, and MenuButtonFX
    // captures its home in Awake, so without this the slide animation would
    // pull buttons back to (0,0).
    public void RefreshButtonHomes()
    {
        foreach (var fx in GetComponentsInChildren<MenuButtonFX>(true))
            fx.RefreshHome();
    }

    void OnDestroy()
    {
        UIAnimLibrary.ForgetHome(Rect);
        UITween.Kill(this);
    }
}
