using System;
using UnityEngine;
using UnityEngine.UI;

// The shared yes/no dialog, used by EXIT, NEW GAME, DELETE SAVE, RESET and
// the settings save flow.
//
// Built once and reused: Show() swaps the text, the colours, the button
// wording and both callbacks rather than creating a new panel each time. That
// also means one question can replace another in place - which is how REVERT
// on the save dialog turns into its own confirmation without a second panel.
//
// The left button is the initially focused one, so a reflexive Enter or Space
// is always the cautious answer.
public class ConfirmDialog
{
    public UIPanel Panel { get; private set; }

    readonly IMenuHost menu;
    Text titleLabel;
    Text bodyLabel;
    Text confirmLabel;
    Text cancelLabel;
    Button confirmButton;
    Button cancelButton;
    Action onAccept;
    Action onCancel;
    bool closeOnConfirm = true;

    public ConfirmDialog(IMenuHost menu)
    {
        this.menu = menu;
        Build();
    }

    void Build()
    {
        var theme = MenuTheme.Current;

        var card = UIFactory.Card("ConfirmPanel", menu.CanvasRoot, theme.confirmPanelSize, Vector2.zero);
        Panel = UIFactory.MakePanel(card.gameObject, "ScalePop", "ScaleShrink", 0.24f);

        var body = UIFactory.NewRect("Body", card.transform);
        UIFactory.Stretch(body);
        body.offsetMin = new Vector2(36f, 30f);
        body.offsetMax = new Vector2(-36f, -28f);

        titleLabel = UIFactory.NewText("Title", body, "", 34, FontStyle.Bold,
                                       TextAnchor.UpperCenter, GameUITheme.Ink);
        var tRt = titleLabel.rectTransform;
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.sizeDelta = new Vector2(0f, 46f);

        // Body face, not the display serif: this is where the change list and
        // the consequences are spelled out, so it has to be easy to read.
        var soft = GameUITheme.Ink;
        soft.a = 0.92f;
        bodyLabel = UIFactory.NewBodyText("Message", body, "", 21, FontStyle.Normal,
                                          TextAnchor.UpperCenter, soft);
        var bRt = bodyLabel.rectTransform;
        bRt.anchorMin = new Vector2(0f, 0f);
        bRt.anchorMax = new Vector2(1f, 1f);
        bRt.offsetMin = new Vector2(0f, 76f);
        bRt.offsetMax = new Vector2(0f, -58f);

        var actions = UIFactory.NewRect("Actions", body);
        actions.anchorMin = new Vector2(0f, 0f);
        actions.anchorMax = new Vector2(1f, 0f);
        actions.pivot = new Vector2(0.5f, 0f);
        actions.sizeDelta = new Vector2(0f, 58f);
        UIFactory.HStack(actions, 18f);

        cancelButton = UIFactory.SmallButton(actions, "CANCEL", CancelPressed, new Vector2(0f, 58f));
        cancelLabel = cancelButton.transform.Find("Label").GetComponent<Text>();

        confirmButton = UIFactory.SmallButton(actions, "CONFIRM", ConfirmPressed,
                                              new Vector2(0f, 58f), MenuFxStyle.Soft, true);
        confirmLabel = confirmButton.transform.Find("Label").GetComponent<Text>();

        Panel.firstSelected = cancelButton.gameObject;

        // Escape always abandons the whole question, however many steps deep it
        // has gone, and never performs an action. Without this a multi-step
        // dialog can leave the player with no way back to what they were doing.
        Panel.onBack = Dismiss;
    }

    void Dismiss()
    {
        onAccept = null;
        onCancel = null;
        menu.CloseTop();
    }

    void ConfirmPressed()
    {
        var action = onAccept;
        onAccept = null;
        onCancel = null;

        // A confirm that only leads to another question keeps the panel open,
        // so the dialog changes in place instead of closing and reopening.
        if (closeOnConfirm) menu.CloseTop();

        if (action != null) action();
    }

    // The left button normally just closes. A caller can hand it a different
    // job - REVERT on the save dialog replaces the question rather than
    // dismissing it - in which case that action owns what happens next.
    void CancelPressed()
    {
        var action = onCancel;

        if (action == null) { onAccept = null; menu.CloseTop(); return; }

        onAccept = null;
        onCancel = null;
        action();
    }

    // Everything about the dialog is per-call: wording, colour and both
    // outcomes. Defaults give the plain destructive question the rest of the
    // game asks.
    public void Show(string title, string message, Action accept,
                     string confirmText = "CONFIRM", string cancelText = "CANCEL",
                     UIFactory.MenuTone confirmTone = UIFactory.MenuTone.Danger,
                     UIFactory.MenuTone cancelTone = UIFactory.MenuTone.Neutral,
                     Action cancel = null,
                     bool closeOnConfirm = true)
    {
        titleLabel.text = title;
        bodyLabel.text = message;
        onAccept = accept;
        onCancel = cancel;
        this.closeOnConfirm = closeOnConfirm;

        if (confirmLabel != null) confirmLabel.text = confirmText;
        if (cancelLabel != null) cancelLabel.text = cancelText;

        UIFactory.ApplyButtonTone(confirmButton, confirmTone);
        UIFactory.ApplyButtonTone(cancelButton, cancelTone);

        // No-op when the panel is already open, which is what lets one question
        // replace another in place.
        menu.OpenPanel(Panel);
    }
}
