using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The bag, on screen.
//
// Opened with N. Same materials as the dialogue box - a translucent ink slab
// edged in parchment - because like the dialogue box it sits over live
// gameplay rather than replacing it.
//
// Layout is a grid on the left and a detail panel on the right. The grid shows
// cells, not slots: with a category tab active the matching stacks are packed
// together, so cell 0 is whatever the first match happens to be. cellToSlot
// maps back to the real inventory index, and everything downstream works in
// inventory indices.
//
// Reads Inventory but never writes to it except through Use and Drop.
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    static readonly ItemCategory[] TabOrder =
    {
        ItemCategory.Consumable, ItemCategory.Tool, ItemCategory.Material, ItemCategory.Key
    };

    Inventory inventory;

    Canvas canvas;
    CanvasGroup dimGroup;
    RectTransform panelRect;
    CanvasGroup panelGroup;
    UIPanel panel;

    RectTransform gridHost;
    GridLayoutGroup grid;
    readonly List<InventorySlotView> cells = new List<InventorySlotView>();
    readonly List<RectTransform> cellRects = new List<RectTransform>();
    readonly List<CanvasGroup> cellGroups = new List<CanvasGroup>();
    readonly List<int> cellToSlot = new List<int>();

    readonly List<Button> tabButtons = new List<Button>();
    int activeTab = -1;                     // -1 is the ALL tab

    // Detail panel
    Text detailName, detailMeta, detailBody, emptyHint;
    RectTransform actionRow, confirmRow;
    Button useButton, dropButton;
    Text useLabel, confirmText;

    int selectedSlot = -1;
    bool pendingDropConfirm;
    int columns = 5;

    bool built;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        IsOpen = false;

        EnsureBuilt();

        if (inventory != null)
        {
            inventory.Changed += OnInventoryChanged;
            inventory.SlotGained += OnSlotGained;
        }
    }

    // Idempotent, and does its own wiring, so the panel can be constructed by
    // something other than Awake - an editor preview, or a scene that spawns
    // the HUD late.
    public void EnsureBuilt()
    {
        if (built) return;
        built = true;

        InventoryAnimations.EnsureRegistered();

        inventory = Inventory.Instance != null
            ? Inventory.Instance
            : Object.FindAnyObjectByType<Inventory>();

        Build();
    }

    // Redraw from the model. Called automatically while the bag is open; public
    // so anything that changes the inventory outside the normal events can ask
    // for a repaint.
    public void Refresh() { Redraw(); }

    void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.Changed -= OnInventoryChanged;
            inventory.SlotGained -= OnSlotGained;
        }
        if (Instance == this) Instance = null;
        IsOpen = false;
        InventoryAnimations.Forget(panelRect);
    }

    // ------------------------------------------------------------------ input
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        // Escape belongs to the pause menu, which closes this first - see
        // PauseMenuUI.Update. N is the only key this class owns.
        if (!kb.nKey.wasPressedThisFrame) return;

        if (IsOpen) Close(false);
        else Open();
    }

    public void Toggle() { if (IsOpen) Close(false); else Open(); }

    public void Open()
    {
        if (IsOpen || inventory == null) return;

        // Not while something else already owns the screen.
        if (DialogueManager.IsActive) return;
        if (PauseMenuUI.IsPaused) return;
        if (LoadingScreen.IsLoading) return;

        IsOpen = true;
        canvas.enabled = true;

        SelectSlot(-1);
        SetTab(activeTab, false);

        panel.inAnim = InventoryAnimations.NextOpen();
        panel.duration = MenuTheme.Current.inventoryPanelDuration;
        panel.Show(() => RefreshCellHomes());

        UITween.Kill(dimGroup);
        dimGroup.alpha = 0f;
        dimGroup.blocksRaycasts = true;
        UITween.Fade(dimGroup, 1f, MenuTheme.Current.dimFadeDuration, Ease.OutCubic);

        PlayCascade();
        UIAudio.PlayVaried(UISound.PanelOpen, 0.9f);
    }

    // cancelled: closed by Escape rather than by N or the close button. It only
    // changes which exit animation plays.
    public void Close(bool cancelled)
    {
        if (!IsOpen) return;
        IsOpen = false;

        ClearDropConfirm();

        panel.outAnim = InventoryAnimations.CloseFor(cancelled);
        panel.duration = MenuTheme.Current.inventoryPanelDuration;
        panel.Hide(() => { if (canvas != null) canvas.enabled = false; });

        UITween.Kill(dimGroup);
        dimGroup.blocksRaycasts = false;
        UITween.Fade(dimGroup, 0f, MenuTheme.Current.dimFadeDuration, Ease.InQuad);

        UIAudio.PlayVaried(cancelled ? UISound.Back : UISound.PanelClose, 0.9f);
    }

    // ------------------------------------------------------------------ build
    void Build()
    {
        var theme = MenuTheme.Current;

        var canvasGo = new GameObject("InventoryCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;            // under the pause menu and dialogue

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = theme.referenceResolution;
        scaler.matchWidthOrHeight = theme.scalerMatch;

        // ---- dim
        var dim = UIFactory.NewImage("Dim", canvasGo.transform, theme.dimColor);
        UIFactory.Stretch(dim.rectTransform);
        dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
        dimGroup.alpha = 0f;

        // ---- panel shell
        panelRect = UIFactory.NewRect("InventoryPanel", canvasGo.transform);
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = theme.inventoryPanelSize;

        panelGroup = panelRect.gameObject.AddComponent<CanvasGroup>();
        panelGroup.alpha = 0f;
        panel = panelRect.gameObject.AddComponent<UIPanel>();
        panel.firstSelected = null;

        var shadow = UIFactory.NewImage("Shadow", panelRect, UIFactory.Shadow);
        shadow.sprite = UIGfx.RoundedRect(theme.cornerRadius, 0);
        shadow.type = Image.Type.Sliced;
        shadow.raycastTarget = false;
        UIFactory.Stretch(shadow.rectTransform, -10f);
        shadow.rectTransform.anchoredPosition = new Vector2(0f, -12f);

        var fill = UIFactory.NewRounded("Fill", panelRect, theme.dialogueFill);
        UIFactory.Stretch(fill.rectTransform);

        var grain = UIFactory.NewImage("Grain", fill.transform,
            new Color(1f, 1f, 1f, theme.dialogueGrainOpacity));
        grain.sprite = UIGfx.PaperFibre(256);
        grain.type = Image.Type.Tiled;
        grain.raycastTarget = false;
        UIFactory.Stretch(grain.rectTransform, 4f);

        UIFactory.AddOutline(fill, theme.dialogueBorder, 3);

        BuildHeader(theme);
        BuildTabs(theme);
        BuildGrid(theme);
        BuildDetail(theme);

        panelRect.gameObject.SetActive(false);
        canvas.enabled = false;
    }

    void BuildHeader(MenuThemeAsset theme)
    {
        var title = UIFactory.NewText("Title", panelRect, "INVENTORY", theme.inventoryTitleFontSize,
            FontStyle.Bold, TextAnchor.UpperLeft, theme.dialogueText);
        var titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(46f, -26f);
        titleRect.sizeDelta = new Vector2(500f, theme.inventoryTitleFontSize + 12f);

        var close = UIFactory.SmallButton(panelRect, "CLOSE", () => Close(false), new Vector2(120f, 44f));
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-40f, -26f);
    }

    void BuildTabs(MenuThemeAsset theme)
    {
        float x = 46f;
        const float width = 168f;
        const float gap = 10f;

        AddTab(theme, "ALL", -1, ref x, width, gap);
        for (int i = 0; i < TabOrder.Length; i++)
            AddTab(theme, TabOrder[i].ToString().ToUpperInvariant(), i, ref x, width, gap);
    }

    void AddTab(MenuThemeAsset theme, string label, int index, ref float x, float width, float gap)
    {
        var button = UIFactory.SmallButton(panelRect, label, () => SetTab(index, true), new Vector2(width, 42f));
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -theme.inventoryTitleFontSize - 44f);

        tabButtons.Add(button);
        x += width + gap;
    }

    void BuildGrid(MenuThemeAsset theme)
    {
        gridHost = UIFactory.NewRect("Grid", panelRect);
        gridHost.anchorMin = gridHost.anchorMax = new Vector2(0f, 1f);
        gridHost.pivot = new Vector2(0f, 1f);
        gridHost.anchoredPosition = new Vector2(46f, -theme.inventoryTitleFontSize - 100f);

        columns = Mathf.Max(1, theme.inventoryColumns);
        float cell = theme.inventoryCellSize;
        float spacing = theme.inventoryCellSpacing;

        int slots = inventory != null ? inventory.SlotCount : 20;
        int rows = Mathf.CeilToInt(slots / (float)columns);

        gridHost.sizeDelta = new Vector2(columns * cell + (columns - 1) * spacing,
                                         rows * cell + (rows - 1) * spacing);

        grid = gridHost.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(spacing, spacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        for (int i = 0; i < slots; i++)
        {
            var view = InventorySlotView.Create(gridHost, i);
            view.onClick = OnSlotClicked;

            cells.Add(view);
            cellRects.Add(view.Rect);
            cellGroups.Add(view.Group);
            cellToSlot.Add(-1);
        }
    }

    void BuildDetail(MenuThemeAsset theme)
    {
        var host = UIFactory.NewRect("Detail", panelRect);
        host.anchorMin = new Vector2(1f, 1f);
        host.anchorMax = new Vector2(1f, 1f);
        host.pivot = new Vector2(1f, 1f);
        host.anchoredPosition = new Vector2(-40f, -theme.inventoryTitleFontSize - 100f);
        host.sizeDelta = theme.inventoryDetailSize;

        var card = UIFactory.NewRounded("DetailFill", host, theme.inventoryDetailFill, 10);
        UIFactory.Stretch(card.rectTransform);
        UIFactory.AddOutline(card, theme.inventorySlotBorder, 2, 10);

        detailName = UIFactory.NewText("Name", host, "", theme.inventoryNameFontSize,
            FontStyle.Bold, TextAnchor.UpperLeft, theme.dialogueText);
        Place(detailName.rectTransform, 26f, -24f, theme.inventoryDetailSize.x - 52f, theme.inventoryNameFontSize + 10f);

        detailMeta = UIFactory.NewBodyText("Meta", host, "", theme.inventoryMetaFontSize,
            FontStyle.Normal, TextAnchor.UpperLeft, theme.accentSoft);
        Place(detailMeta.rectTransform, 26f, -24f - theme.inventoryNameFontSize - 14f,
              theme.inventoryDetailSize.x - 52f, theme.inventoryMetaFontSize + 8f);

        detailBody = UIFactory.NewBodyText("Body", host, "", theme.inventoryBodyFontSize,
            FontStyle.Normal, TextAnchor.UpperLeft, theme.dialogueText);
        detailBody.horizontalOverflow = HorizontalWrapMode.Wrap;
        detailBody.lineSpacing = 1.2f;
        var bodyRect = detailBody.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(26f, 92f);
        bodyRect.offsetMax = new Vector2(-26f, -(24f + theme.inventoryNameFontSize + theme.inventoryMetaFontSize + 34f));

        emptyHint = UIFactory.NewBodyText("EmptyHint", host, "Select an item.", theme.inventoryBodyFontSize,
            FontStyle.Italic, TextAnchor.MiddleCenter, theme.inventoryCountText);
        UIFactory.Stretch(emptyHint.rectTransform, 26f);

        // ---- action row
        actionRow = UIFactory.NewRect("Actions", host);
        actionRow.anchorMin = new Vector2(0f, 0f);
        actionRow.anchorMax = new Vector2(1f, 0f);
        actionRow.pivot = new Vector2(0.5f, 0f);
        actionRow.offsetMin = new Vector2(26f, 22f);
        actionRow.offsetMax = new Vector2(-26f, 22f + 52f);

        useButton = UIFactory.SmallButton(actionRow, "USE", OnUseClicked, new Vector2(180f, 52f));
        var useRect = (RectTransform)useButton.transform;
        useRect.anchorMin = useRect.anchorMax = new Vector2(0f, 0.5f);
        useRect.pivot = new Vector2(0f, 0.5f);
        useRect.anchoredPosition = Vector2.zero;
        useLabel = useButton.transform.Find("Label").GetComponent<Text>();

        dropButton = UIFactory.SmallButton(actionRow, "DROP", OnDropClicked, new Vector2(180f, 52f), MenuFxStyle.Soft, true);
        var dropRect = (RectTransform)dropButton.transform;
        dropRect.anchorMin = dropRect.anchorMax = new Vector2(1f, 0.5f);
        dropRect.pivot = new Vector2(1f, 0.5f);
        dropRect.anchoredPosition = Vector2.zero;

        // ---- confirm row, shown in place of the actions
        confirmRow = UIFactory.NewRect("Confirm", host);
        confirmRow.anchorMin = new Vector2(0f, 0f);
        confirmRow.anchorMax = new Vector2(1f, 0f);
        confirmRow.pivot = new Vector2(0.5f, 0f);
        confirmRow.offsetMin = new Vector2(26f, 22f);
        confirmRow.offsetMax = new Vector2(-26f, 22f + 96f);

        confirmText = UIFactory.NewBodyText("Question", confirmRow, "", theme.inventoryMetaFontSize,
            FontStyle.Bold, TextAnchor.UpperCenter, theme.dangerFill);
        var questionRect = confirmText.rectTransform;
        questionRect.anchorMin = new Vector2(0f, 1f);
        questionRect.anchorMax = new Vector2(1f, 1f);
        questionRect.pivot = new Vector2(0.5f, 1f);
        questionRect.offsetMin = new Vector2(0f, -34f);
        questionRect.offsetMax = new Vector2(0f, 0f);

        var yes = UIFactory.SmallButton(confirmRow, "THROW AWAY", ConfirmDrop, new Vector2(210f, 50f), MenuFxStyle.Soft, true);
        var yesRect = (RectTransform)yes.transform;
        yesRect.anchorMin = yesRect.anchorMax = new Vector2(0f, 0f);
        yesRect.pivot = new Vector2(0f, 0f);
        yesRect.anchoredPosition = Vector2.zero;

        var no = UIFactory.SmallButton(confirmRow, "KEEP IT", ClearDropConfirm, new Vector2(180f, 50f));
        var noRect = (RectTransform)no.transform;
        noRect.anchorMin = noRect.anchorMax = new Vector2(1f, 0f);
        noRect.pivot = new Vector2(1f, 0f);
        noRect.anchoredPosition = Vector2.zero;

        confirmRow.gameObject.SetActive(false);
    }

    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // ------------------------------------------------------------------- tabs
    void SetTab(int index, bool animate)
    {
        activeTab = index;

        for (int i = 0; i < tabButtons.Count; i++)
        {
            bool active = (i - 1) == index;      // button 0 is ALL, which is -1
            UIFactory.ApplyButtonTone(tabButtons[i], active ? UIFactory.MenuTone.Positive : UIFactory.MenuTone.Neutral);
        }

        Redraw();
        ClearDropConfirm();

        // A tab change swaps out the whole grid, so it earns a cascade of its
        // own - without one the contents simply teleport.
        if (animate) { PlayCascade(); UIAudio.PlayVaried(UISound.Tick, 0.6f); }
    }

    bool Matches(ItemDefinition def)
    {
        if (def == null) return false;
        if (activeTab < 0) return true;
        return def.category == TabOrder[activeTab];
    }

    // ----------------------------------------------------------------- redraw
    void Redraw()
    {
        if (inventory == null) return;

        for (int i = 0; i < cellToSlot.Count; i++) cellToSlot[i] = -1;

        int cell = 0;

        if (activeTab < 0)
        {
            // ALL shows the bag as it really is, empty slots included.
            for (int slot = 0; slot < inventory.SlotCount && cell < cells.Count; slot++, cell++)
            {
                var stack = inventory.At(slot);
                cellToSlot[cell] = slot;
                cells[cell].gameObject.SetActive(true);
                cells[cell].Show(slot, stack);
                if (stack == null || stack.IsEmpty) cells[cell].SetEmpty();
            }
        }
        else
        {
            for (int slot = 0; slot < inventory.SlotCount && cell < cells.Count; slot++)
            {
                var stack = inventory.At(slot);
                if (stack == null || stack.IsEmpty || !Matches(stack.Definition)) continue;

                cellToSlot[cell] = slot;
                cells[cell].gameObject.SetActive(true);
                cells[cell].Show(slot, stack);
                cell++;
            }
        }

        for (int i = cell; i < cells.Count; i++)
        {
            cells[i].SetEmpty();
            cells[i].gameObject.SetActive(activeTab < 0);
        }

        // The selection may have been filtered away or thrown away.
        if (selectedSlot >= 0 && !cellToSlot.Contains(selectedSlot)) SelectSlot(-1);
        else SyncSelectionTone();

        DrawDetail();
    }

    void SyncSelectionTone()
    {
        for (int i = 0; i < cells.Count; i++)
            cells[i].SetSelected(cellToSlot[i] >= 0 && cellToSlot[i] == selectedSlot);
    }

    void DrawDetail()
    {
        var stack = selectedSlot >= 0 && inventory != null ? inventory.At(selectedSlot) : null;
        var def = stack != null ? stack.Definition : null;

        bool has = def != null;
        emptyHint.gameObject.SetActive(!has);
        detailName.gameObject.SetActive(has);
        detailMeta.gameObject.SetActive(has);
        detailBody.gameObject.SetActive(has);
        actionRow.gameObject.SetActive(has && !pendingDropConfirm);
        confirmRow.gameObject.SetActive(has && pendingDropConfirm);

        if (!has) return;

        detailName.text = def.displayName;

        string meta = def.category.ToString();
        if (stack.count > 1) meta += "   ~   " + stack.count + " of " + def.maxStack;
        else if (def.Stacks) meta += "   ~   stacks to " + def.maxStack;
        detailMeta.text = meta;

        detailBody.text = def.description;

        useButton.gameObject.SetActive(def.CanUse);
        if (def.CanUse) useLabel.text = def.useVerb;

        dropButton.gameObject.SetActive(def.canDrop);
    }

    // ----------------------------------------------------------------- events
    void OnSlotClicked(InventorySlotView view)
    {
        SelectSlot(view.SlotIndex);
    }

    // Public so anything that wants to point the player at a specific slot -
    // a tutorial, a quest hand-in, a future hotbar - can do it without
    // pretending to be a click.
    public void SelectSlot(int slot)
    {
        selectedSlot = slot;
        pendingDropConfirm = false;
        SyncSelectionTone();
        DrawDetail();
    }

    void OnUseClicked()
    {
        if (selectedSlot < 0 || inventory == null) return;

        int cell = cellToSlot.IndexOf(selectedSlot);
        if (cell >= 0) InventoryAnimations.PlayGain(cells[cell].Rect, cells[cell].Flash);

        inventory.Use(selectedSlot);
        UIAudio.PlayVaried(UISound.Confirm, 0.85f);
    }

    void OnDropClicked()
    {
        if (selectedSlot < 0) return;

        var stack = inventory.At(selectedSlot);
        var def = stack != null ? stack.Definition : null;
        if (def == null || !def.canDrop) return;

        pendingDropConfirm = true;
        confirmText.text = "Throw away " + def.displayName
                         + (stack.count > 1 ? " (x" + stack.count + ")" : "") + "?";
        DrawDetail();
        UIAudio.PlayVaried(UISound.Cancel, 0.7f);
    }

    void ConfirmDrop()
    {
        if (selectedSlot < 0 || inventory == null) return;

        int cell = cellToSlot.IndexOf(selectedSlot);
        if (cell >= 0) InventoryAnimations.PlayDrop(cells[cell].Rect, cells[cell].Group);

        int slot = selectedSlot;
        pendingDropConfirm = false;

        // Let the slot fall away before the grid redraws under it.
        UITween.Delay(this, MenuTheme.Current.inventoryGainDuration, () =>
        {
            if (inventory == null) return;
            inventory.Drop(slot);
            RefreshCellHomes();
        });

        SelectSlot(-1);
        UIAudio.PlayVaried(UISound.Back, 0.85f);
    }

    void ClearDropConfirm()
    {
        if (!pendingDropConfirm) return;
        pendingDropConfirm = false;
        DrawDetail();
    }

    void OnInventoryChanged()
    {
        if (!IsOpen) return;
        Redraw();
    }

    void OnSlotGained(int slot)
    {
        if (!IsOpen) return;

        int cell = cellToSlot.IndexOf(slot);
        if (cell < 0) return;

        InventoryAnimations.PlayGain(cells[cell].Rect, cells[cell].Flash);
    }

    // ------------------------------------------------------------------ anim
    void PlayCascade()
    {
        var rects = new List<RectTransform>();
        var groups = new List<CanvasGroup>();

        for (int i = 0; i < cells.Count; i++)
        {
            if (!cells[i].gameObject.activeSelf) continue;
            rects.Add(cellRects[i]);
            groups.Add(cellGroups[i]);
        }

        InventoryAnimations.PlayCascade(rects, groups, columns, InventoryAnimations.NextCascade());
    }

    // GridLayoutGroup positions its children a frame after the panel opens, so
    // the hover lift would otherwise pull every cell back to (0,0).
    void RefreshCellHomes()
    {
        UITween.Delay(this, 0f, () =>
        {
            foreach (var cell in cells) cell.RefreshHome();
        });
    }
}
