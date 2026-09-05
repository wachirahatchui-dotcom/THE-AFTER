using UnityEngine;
using UnityEngine.UI;

// The STAGE SELECT page: start the game part-way through instead of at the top.
//
// Reads StageCatalog, which an editor tool fills in from the real scene, so the
// entries here are only ever labels and a button each - the knowledge of where
// Chapter 1's stages actually begin lives with the scene that has them.
//
// Each entry goes through the save system rather than round it: it builds the
// same SaveSlotData a real save would hold and hands it to the same queue, so
// starting at Stage 2 exercises the code a player's load does. That is worth
// more than a shortcut would be - it was loading a save that turned up the
// chapter replaying its own opening over the top of the restored position.
public class StageSelectPage
{
    public UIPanel Panel { get; private set; }

    readonly MainMenuUI menu;
    StageCatalog catalog;

    public StageSelectPage(MainMenuUI menu)
    {
        this.menu = menu;
        Build();
    }

    /// Whether there is anything to show. The menu hides its button otherwise,
    /// rather than opening a page with nothing on it.
    public bool HasStages
    {
        get { return catalog != null && catalog.stages != null && catalog.stages.Length > 0; }
    }

    void Build()
    {
        var theme = MenuTheme.Current;
        catalog = StageCatalog.Load();

        var card = UIFactory.Card("StageSelectPanel", menu.CanvasRoot,
                                  new Vector2(720f, 560f), Vector2.zero);
        Panel = UIFactory.MakePanel(card.gameObject, menu.PanelIn, menu.PanelOut, theme.panelDuration);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(40f, 36f);
        body.offsetMax = new Vector2(-40f, -32f);

        var header = UIFactory.NewText("Header", body, "STAGE SELECT", 44, FontStyle.Bold,
                                       TextAnchor.UpperCenter, GameUITheme.Ink);
        var hRt = header.rectTransform;
        hRt.anchorMin = new Vector2(0f, 1f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.pivot = new Vector2(0.5f, 1f);
        hRt.sizeDelta = new Vector2(0f, 60f);

        var sub = UIFactory.NewText("Sub", body,
            "Jump straight to a point in the story.", 20, FontStyle.Normal,
            TextAnchor.UpperCenter, GameUITheme.InkSoft);
        var sRt = sub.rectTransform;
        sRt.anchorMin = new Vector2(0f, 1f);
        sRt.anchorMax = new Vector2(1f, 1f);
        sRt.pivot = new Vector2(0.5f, 1f);
        sRt.sizeDelta = new Vector2(0f, 30f);
        sRt.anchoredPosition = new Vector2(0f, -58f);

        if (!HasStages)
        {
            var none = UIFactory.NewText("None", body,
                "No stages listed yet.\nRun  THE AFTER > Dev > Rebuild Stage Catalog",
                22, FontStyle.Normal, TextAnchor.MiddleCenter, GameUITheme.InkSoft);
            UIFactory.Stretch(none.rectTransform);
        }
        else
        {
            const float rowHeight = 74f;
            const float gap = 12f;

            for (int i = 0; i < catalog.stages.Length; i++)
            {
                var entry = catalog.stages[i];     // captured per row, not per loop
                var btn = UIFactory.MenuButton(body, entry.label, () => Start(entry),
                                               MenuFxStyle.Classic, rowHeight, 26, false);

                var rt = (RectTransform)btn.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0f, rowHeight);
                rt.anchoredPosition = new Vector2(0f, -(108f + i * (rowHeight + gap)));

                var labelT = btn.transform.Find("Label");
                if (labelT != null)
                {
                    var t = labelT.GetComponent<Text>();
                    t.alignment = TextAnchor.MiddleLeft;
                    t.rectTransform.offsetMin = new Vector2(28f, string.IsNullOrEmpty(entry.hint) ? 0f : 16f);
                    t.rectTransform.offsetMax = new Vector2(-18f, 0f);

                    if (!string.IsNullOrEmpty(entry.hint))
                    {
                        var hint = UIFactory.NewText("Hint", btn.transform, entry.hint, 17,
                                                     FontStyle.Normal, TextAnchor.MiddleLeft,
                                                     GameUITheme.InkSoft);
                        var qRt = hint.rectTransform;
                        UIFactory.Stretch(qRt);
                        qRt.offsetMin = new Vector2(28f, 0f);
                        qRt.offsetMax = new Vector2(-18f, -34f);
                        hint.raycastTarget = false;
                    }
                }

                if (i == 0) Panel.firstSelected = btn.gameObject;
            }
        }

        // Same button, same size, same handler as every other page's BACK - the
        // one on Credits and the one on Save/Load. A page that closes itself a
        // different way is a page that behaves differently for no reason.
        var back = UIFactory.SmallButton(body, "BACK", menu.CloseTop, new Vector2(240f, 56f));

        if (Panel.firstSelected == null && back != null)
            Panel.firstSelected = back.gameObject;
    }

    void Start(StageCatalog.Entry entry)
    {
        PlayTimeTracker.Reset();

        // Stage one is the beginning, so there is nothing to skip to and nothing
        // to restore - queueing a save that says "reached 0" would only make the
        // chapter do exactly what a new game does, through more machinery.
        if (entry.reached > 0)
            SaveSystem.QueueLoad(StageCatalog.ToSave(entry));

        PlayerPrefs.SetString(SettingsKeys.LastChapter, entry.chapterName);
        menu.StartGame(entry.sceneName, entry.chapterName);
    }
}
