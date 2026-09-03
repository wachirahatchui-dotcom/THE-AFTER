using UnityEngine;
using UnityEngine.UI;

// The CREDITS page.
//
// Lines come from MainMenuUI.creditsLines, editable in the Inspector: a line
// starting with # renders as an accent heading, an empty line becomes a gap,
// anything else is body text.
//
// The list crawls upward on its own and stops for good the moment the player
// scrolls, so manual reading is never fought by the animation.
public class CreditsPage
{
    public UIPanel Panel { get; private set; }

    readonly MainMenuUI menu;
    ScrollRect scroll;
    bool autoScroll;

    public CreditsPage(MainMenuUI menu)
    {
        this.menu = menu;
        Build();
    }

    void Build()
    {
        var theme = MenuTheme.Current;

        var card = UIFactory.Card("CreditsPanel", menu.CanvasRoot, theme.creditsPanelSize, Vector2.zero);
        Panel = UIFactory.MakePanel(card.gameObject, menu.PanelIn, menu.PanelOut, theme.panelDuration);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(40f, 36f);
        body.offsetMax = new Vector2(-40f, -32f);

        var header = UIFactory.NewText("Header", body, "CREDITS", 44, FontStyle.Bold,
                                       TextAnchor.UpperCenter, GameUITheme.Ink);
        var hRt = header.rectTransform;
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.sizeDelta = new Vector2(0f, 60f);

        var scrollHost = UIFactory.NewRect("ScrollHost", body);
        scrollHost.anchorMin = new Vector2(0f, 0f);
        scrollHost.anchorMax = new Vector2(1f, 1f);
        scrollHost.offsetMin = new Vector2(0f, 74f);
        scrollHost.offsetMax = new Vector2(0f, -70f);

        var content = UIFactory.ScrollArea(scrollHost, out scroll);
        UIFactory.Stretch((RectTransform)scroll.transform);

        foreach (var raw in menu.creditsLines)
        {
            bool heading = raw.StartsWith("#");
            string line = heading ? raw.Substring(1) : raw;

            if (string.IsNullOrEmpty(line))
            {
                UIFactory.Spacer(content, 14f);
                continue;
            }

            var t = UIFactory.NewText("Line", content, line,
                heading ? 28 : 22,
                heading ? FontStyle.Bold : FontStyle.Normal,
                TextAnchor.UpperCenter,
                heading ? UIFactory.Accent : GameUITheme.Ink);
            UIFactory.SetHeight(t.gameObject, heading ? 40f : 32f);
        }

        var back = UIFactory.SmallButton(body, "BACK", menu.CloseTop, new Vector2(240f, 56f));
        var bRt = (RectTransform)back.transform;
        bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 0f);
        bRt.pivot = new Vector2(0.5f, 0f);
        bRt.anchoredPosition = Vector2.zero;
        bRt.sizeDelta = new Vector2(240f, 56f);

        Panel.firstSelected = back.gameObject;
        Panel.onBack = menu.CloseTop;
    }

    public void RestartScroll()
    {
        if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        autoScroll = true;
    }

    // Driven from MainMenuUI.Update so this class needs no MonoBehaviour.
    public void Tick()
    {
        if (!autoScroll || Panel == null || !Panel.IsOpen || scroll == null) return;

        // Any real scrolling input hands control to the player permanently.
        if (scroll.velocity.sqrMagnitude > 1f)
        {
            autoScroll = false;
            return;
        }

        if (scroll.verticalNormalizedPosition > 0f)
        {
            scroll.verticalNormalizedPosition = Mathf.Max(0f,
                scroll.verticalNormalizedPosition
                - Time.unscaledDeltaTime * MenuTheme.Current.creditsScrollSpeed);
        }
    }
}
