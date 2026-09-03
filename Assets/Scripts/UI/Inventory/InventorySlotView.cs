using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One cell of the inventory grid.
//
// Owns its own hover, focus and selection reactions rather than borrowing
// MenuButtonFX: that class is built around a button with a Label and an
// Outline child and animates a caret and an underline, none of which a slot
// has. What a slot needs is smaller and different - lift on hover, invert on
// select, flash on gain.
//
// Holds no item state. InventoryUI hands it a stack to display and it draws
// that; it never reads the Inventory itself.
public class InventorySlotView : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // The inventory slot this cell is currently showing, or -1 when empty.
    public int SlotIndex { get; private set; } = -1;

    public RectTransform Rect { get; private set; }
    public CanvasGroup Group { get; private set; }
    public Graphic Flash { get { return flash; } }

    public Action<InventorySlotView> onClick;

    Image background;
    Image outline;
    Image icon;
    Image flash;
    Text countLabel;

    bool hovered;
    bool selected;
    bool filled;

    Vector2 home;
    bool homeCached;

    // ------------------------------------------------------------------ build
    public static InventorySlotView Create(Transform parent, int cellIndex)
    {
        var theme = MenuTheme.Current;

        var bg = UIFactory.NewRounded("Slot_" + cellIndex, parent, theme.inventorySlotFill, 8);
        var view = bg.gameObject.AddComponent<InventorySlotView>();

        view.Rect = bg.rectTransform;
        view.background = bg;
        view.Group = bg.gameObject.AddComponent<CanvasGroup>();

        view.outline = UIFactory.AddOutline(bg, theme.inventorySlotBorder, 2, 8);

        // Under the icon, so a gain flash reads as the slot lighting up rather
        // than the item changing colour.
        view.flash = UIFactory.NewRounded("Flash", bg.transform, new Color(1f, 1f, 1f, 0f), 8);
        UIFactory.Stretch(view.flash.rectTransform, 2f);
        view.flash.raycastTarget = false;

        view.icon = UIFactory.NewImage("Icon", bg.transform, Color.white);
        view.icon.preserveAspect = true;
        view.icon.raycastTarget = false;
        var iconRect = view.icon.rectTransform;
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = new Vector2(14f, 14f);
        iconRect.offsetMax = new Vector2(-14f, -14f);

        view.countLabel = UIFactory.NewBodyText("Count", bg.transform, "", theme.inventoryCountFontSize,
            FontStyle.Bold, TextAnchor.LowerRight, theme.inventoryCountText);
        view.countLabel.raycastTarget = false;
        var countRect = view.countLabel.rectTransform;
        countRect.anchorMin = Vector2.zero;
        countRect.anchorMax = Vector2.one;
        countRect.offsetMin = new Vector2(6f, 6f);
        countRect.offsetMax = new Vector2(-9f, -6f);

        view.SetEmpty();
        return view;
    }

    // ------------------------------------------------------------------- draw
    public void Show(int slotIndex, ItemStack stack)
    {
        SlotIndex = slotIndex;

        var def = stack != null ? stack.Definition : null;
        if (def == null) { SetEmpty(); return; }

        filled = true;
        icon.enabled = true;
        icon.sprite = ItemIcon.Get(def.shape);
        icon.color = def.tint;

        countLabel.text = stack.count > 1 ? "x" + stack.count : "";
        ApplyTone(true);
    }

    public void SetEmpty()
    {
        SlotIndex = -1;
        filled = false;
        selected = false;

        icon.enabled = false;
        countLabel.text = "";
        ApplyTone(true);
    }

    public void SetSelected(bool value)
    {
        if (selected == value) return;
        selected = value;
        ApplyTone(false);

        if (value)
        {
            CacheHome();
            UITween.Punch(Rect, 0.10f, MenuTheme.Current.inventoryHoverDuration * 1.4f, Vector3.one);
        }
    }

    // Selection inverts the slot - parchment fill, ink glyph - because a
    // border alone disappears against a grid of bordered squares.
    void ApplyTone(bool instant)
    {
        var theme = MenuTheme.Current;
        float d = instant ? 0f : theme.inventoryHoverDuration;

        Color fill = selected ? theme.inventorySlotSelected
                   : hovered  ? theme.inventorySlotHover
                   : theme.inventorySlotFill;

        Color border = selected ? theme.inventorySlotSelectedBorder : theme.inventorySlotBorder;

        if (d <= 0f)
        {
            background.color = fill;
            outline.color = border;
        }
        else
        {
            UITween.ColorTo(background, fill, d, Ease.OutCubic);
            UITween.ColorTo(outline, border, d, Ease.OutCubic);
        }

        countLabel.color = selected ? theme.inventoryCountTextSelected : theme.inventoryCountText;
    }

    // ------------------------------------------------------------------ input
    public void OnPointerEnter(PointerEventData e)
    {
        hovered = true;
        ApplyTone(false);
        if (!filled) return;

        CacheHome();
        UITween.MoveAnchored(Rect, home + new Vector2(0f, MenuTheme.Current.inventoryHoverLift),
                             MenuTheme.Current.inventoryHoverDuration, Ease.OutCubic);
        UIAudio.PlayVaried(UISound.Hover, 0.5f);
    }

    public void OnPointerExit(PointerEventData e)
    {
        hovered = false;
        ApplyTone(false);
        if (!homeCached) return;

        UITween.MoveAnchored(Rect, home, MenuTheme.Current.inventoryHoverDuration, Ease.OutCubic);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (!filled) { UIAudio.PlayVaried(UISound.Tick, 0.4f); return; }

        UIAudio.PlayVaried(UISound.Click, 0.8f);
        onClick?.Invoke(this);
    }

    // The grid is laid out by a GridLayoutGroup, which positions children a
    // frame late - so the resting position cannot be read in Create.
    void CacheHome()
    {
        if (homeCached) return;
        home = Rect.anchoredPosition;
        homeCached = true;
    }

    // Called by InventoryUI after a relayout, since every cell has just moved.
    public void RefreshHome()
    {
        homeCached = false;
        CacheHome();
    }

    void OnDestroy()
    {
        UITween.Kill(Rect);
        UITween.Kill(Group);
    }
}
