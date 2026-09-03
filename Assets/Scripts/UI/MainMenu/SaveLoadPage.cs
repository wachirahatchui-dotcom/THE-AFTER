using UnityEngine;
using UnityEngine.UI;

// The SAVE / LOAD page: one row per save slot, plus LOAD / DELETE / BACK.
//
// A plain class rather than a MonoBehaviour - it builds its widgets once and
// then just owns them, so there is nothing for Unity to tick.
//
// Saving is deliberately not offered here: the main menu has no running game
// to capture. Writing a slot is the pause menu's job, via
// SaveSystem.Write(slot, SaveSystem.CaptureFromScene(chapter)).
public class SaveLoadPage
{
    public UIPanel Panel { get; private set; }

    readonly MainMenuUI menu;
    readonly SlotView[] slots = new SlotView[SaveSystem.SlotCount];
    Button loadButton, deleteButton;
    int selected;

    class SlotView
    {
        public int index;
        public Image card;
        public Image ring;
        public Image shot;
        public Text heading;
        public Text meta;
        public Button button;
        public SaveSlotData data;
    }

    public SaveLoadPage(MainMenuUI menu)
    {
        this.menu = menu;
        Build();
    }

    // ================================================================== build
    void Build()
    {
        var theme = MenuTheme.Current;

        var card = UIFactory.Card("SavePanel", menu.CanvasRoot, theme.savePanelSize, Vector2.zero);
        Panel = UIFactory.MakePanel(card.gameObject, menu.PanelIn, menu.PanelOut, theme.panelDuration);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(44f, 40f);
        body.offsetMax = new Vector2(-44f, -34f);
        UIFactory.VStack(body, 14f);

        UIFactory.Header(body, "SAVE  /  LOAD");
        UIFactory.Divider(body);

        for (int i = 0; i < SaveSystem.SlotCount; i++)
            slots[i] = BuildSlotRow(body, i);

        UIFactory.Spacer(body, 6f);

        var actions = UIFactory.NewRect("Actions", body);
        UIFactory.SetHeight(actions.gameObject, 62f);
        UIFactory.HStack(actions, 16f);

        loadButton = UIFactory.SmallButton(actions, "LOAD", LoadSelected, new Vector2(0f, 58f));
        deleteButton = UIFactory.SmallButton(actions, "DELETE", DeleteSelected,
                                             new Vector2(0f, 58f), MenuFxStyle.Soft, true);
        UIFactory.SmallButton(actions, "BACK", menu.CloseTop, new Vector2(0f, 58f));

        Panel.firstSelected = slots[0].button.gameObject;
        Panel.onBack = menu.CloseTop;
    }

    SlotView BuildSlotRow(Transform parent, int index)
    {
        var view = new SlotView { index = index };

        var img = UIFactory.NewRounded("Slot" + index, parent, GameUITheme.ParchmentLight, 10);
        UIFactory.SetHeight(img.gameObject, MenuTheme.Current.slotRowHeight);
        view.card = img;
        view.ring = UIFactory.AddOutline(img, GameUITheme.Ink, 2, 10);

        // Thumbnail captured when the slot was written; falls back to the slot
        // number for empty slots and for saves made before thumbnails existed.
        var shotFrame = UIFactory.NewRounded("Shot", img.transform, GameUITheme.ParchmentDeep, 6);
        var fRt = shotFrame.rectTransform;
        fRt.anchorMin = fRt.anchorMax = new Vector2(0f, 0.5f);
        fRt.pivot = new Vector2(0f, 0.5f);
        fRt.sizeDelta = new Vector2(160f, 90f);
        fRt.anchoredPosition = new Vector2(12f, 0f);
        view.shot = shotFrame;
        UIFactory.AddOutline(shotFrame, GameUITheme.Ink, 2, 6);

        var number = UIFactory.NewText("Num", shotFrame.transform, (index + 1).ToString(), 44,
                                       FontStyle.Bold, TextAnchor.MiddleCenter, GameUITheme.InkSoft);
        UIFactory.Stretch(number.rectTransform);

        view.heading = UIFactory.NewText("Heading", img.transform, "", 26, FontStyle.Bold,
                                         TextAnchor.LowerLeft, GameUITheme.Ink);
        view.heading.horizontalOverflow = HorizontalWrapMode.Overflow;
        var hRt = view.heading.rectTransform;
        hRt.anchorMin = new Vector2(0f, 0.5f);
        hRt.anchorMax = new Vector2(1f, 1f);
        hRt.offsetMin = new Vector2(190f, 0f);
        hRt.offsetMax = new Vector2(-20f, -18f);

        view.meta = UIFactory.NewText("Meta", img.transform, "", 19, FontStyle.Normal,
                                      TextAnchor.UpperLeft, GameUITheme.InkSoft);
        view.meta.horizontalOverflow = HorizontalWrapMode.Overflow;
        var mRt = view.meta.rectTransform;
        mRt.anchorMin = new Vector2(0f, 0f);
        mRt.anchorMax = new Vector2(1f, 0.5f);
        mRt.offsetMin = new Vector2(190f, 16f);
        mRt.offsetMax = new Vector2(-20f, -2f);

        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        int captured = index;
        btn.onClick.AddListener(() => Select(captured, true));
        view.button = btn;

        // Soft style only: this row lives in a layout group, and the Slide
        // reaction would be overwritten by the next layout pass.
        var fx = img.gameObject.AddComponent<MenuButtonFX>();
        fx.style = MenuFxStyle.Scale | MenuFxStyle.Glow;
        fx.scaleAmount = 1.012f;
        fx.idleFill = GameUITheme.ParchmentLight;
        fx.focusFill = GameUITheme.ParchmentLight;
        fx.idleText = GameUITheme.Ink;
        fx.focusText = GameUITheme.Ink;
        fx.accent = UIFactory.Accent;
        fx.hoverSound = UISound.SlotSelect;

        return view;
    }

    // ================================================================== state
    public void Refresh()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var v = slots[i];
            v.data = SaveSystem.Read(i);

            var thumb = SaveThumbnail.Load(i);
            var num = v.shot.transform.Find("Num");
            if (thumb != null)
            {
                v.shot.sprite = thumb;
                v.shot.type = Image.Type.Simple;
                v.shot.color = Color.white;
                if (num != null) num.gameObject.SetActive(false);
            }
            else
            {
                v.shot.sprite = UIGfx.RoundedRect(6, 0);
                v.shot.type = Image.Type.Sliced;
                v.shot.color = GameUITheme.ParchmentDeep;
                if (num != null) num.gameObject.SetActive(true);
            }

            if (v.data == null)
            {
                var faded = GameUITheme.Ink;
                faded.a = 0.42f;
                v.heading.text = "EMPTY SLOT";
                v.heading.color = faded;
                v.meta.text = "No data";
            }
            else
            {
                v.heading.text = v.data.chapterName;
                v.heading.color = GameUITheme.Ink;
                v.meta.text = "Played " + v.data.PlayTimeText + "        " + v.data.SavedAtText;
            }
        }

        // Land on the most recent save, falling back to the first slot.
        int recent = SaveSystem.MostRecentSlot();
        Select(recent >= 0 ? recent : 0, false);
    }

    void Select(int index, bool audible)
    {
        selected = Mathf.Clamp(index, 0, slots.Length - 1);

        for (int i = 0; i < slots.Length; i++)
        {
            bool on = i == selected;
            var v = slots[i];

            UITween.ColorTo(v.ring, on ? UIFactory.Accent : GameUITheme.Ink, 0.18f);
            UITween.ColorTo(v.card, on ? GameUITheme.Parchment : GameUITheme.ParchmentLight, 0.18f);
            if (on && audible) UITween.Punch(v.card.transform, 0.02f, 0.22f, Vector3.one);
        }

        if (audible) UIAudio.Play(UISound.SlotSelect, 0.9f);

        bool hasData = slots[selected].data != null;
        SetInteractable(loadButton, hasData);
        SetInteractable(deleteButton, hasData);
    }

    static void SetInteractable(Button b, bool on)
    {
        if (b == null) return;
        b.interactable = on;

        var fx = b.GetComponent<MenuButtonFX>();
        if (fx != null) fx.interactableOverride = on;

        var label = b.transform.Find("Label");
        if (label != null)
        {
            var t = label.GetComponent<Text>();
            var c = t.color;
            c.a = on ? 1f : 0.32f;
            t.color = c;
        }
    }

    // =============================================================== handlers
    void LoadSelected()
    {
        if (slots[selected].data == null) { UIAudio.Play(UISound.Error); return; }
        menu.LoadSlot(selected);
    }

    void DeleteSelected()
    {
        var data = slots[selected].data;
        if (data == null) { UIAudio.Play(UISound.Error); return; }

        int target = selected;
        menu.ShowConfirm("DELETE SAVE",
            "Slot " + (target + 1) + " (" + data.chapterName + ", " + data.PlayTimeText + ")\n" +
            "will be permanently erased. This cannot be undone.",
            () =>
            {
                SaveSystem.Delete(target);
                Refresh();
                UITween.Shake(slots[target].card.rectTransform, 6f, 0.3f);
                UIAudio.Play(UISound.Cancel);
            });
    }
}
