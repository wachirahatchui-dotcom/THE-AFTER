using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Hidden host that owns every UI coroutine. Tweens outlive the Graphic they
// animate (a panel can be torn down mid-fade), so they run here instead of on
// the target, and are tracked per-owner so UITween.Kill can cancel cleanly.
public class UITweenRunner : MonoBehaviour
{
    static UITweenRunner instance;

    readonly Dictionary<Object, List<Coroutine>> byOwner = new Dictionary<Object, List<Coroutine>>();

    public static UITweenRunner I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("~UITweenRunner");
                go.hideFlags = HideFlags.HideAndDontSave;
                instance = go.AddComponent<UITweenRunner>();

                // DontDestroyOnLoad throws outside play mode, and the throw
                // happens after the GameObject exists - so an editor tool that
                // touches any tweening code path used to both fail and leave a
                // ~UITweenRunner behind in the open scene. Coroutines do not
                // tick in edit mode anyway; the runner just sits there unused.
                if (Application.isPlaying) DontDestroyOnLoad(go);
            }
            return instance;
        }
    }

    // Returns the runner only if one already exists, never creating it.
    //
    // Panels call UITween.Kill from OnDestroy, which also runs while a scene is
    // being torn down. Going through I there would spawn a fresh GameObject
    // mid-teardown, which Unity reports as "Some objects were not cleaned up
    // when closing the scene".
    public static UITweenRunner Existing { get { return instance; } }

    public Coroutine Run(Object owner, IEnumerator routine)
    {
        var co = StartCoroutine(routine);
        if (owner != null)
        {

            if (!byOwner.TryGetValue(owner, out var list))
                byOwner[owner] = list = new List<Coroutine>();
            list.Add(co);
        }
        return co;
    }

    public void Kill(Object owner)
    {
        if (owner == null) return;

        if (!byOwner.TryGetValue(owner, out var list)) return;
        foreach (var co in list)
            if (co != null) StopCoroutine(co);
        list.Clear();
    }

    public void KillAll()
    {
        StopAllCoroutines();
        byOwner.Clear();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
