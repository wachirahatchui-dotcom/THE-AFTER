using System;
using UnityEngine;

// What a page needs from whatever menu is hosting it.
//
// OptionsPage and ConfirmDialog are used by both the main menu and the pause
// menu. Without this they would have to be written twice, or the pause menu
// would have to pretend to be a MainMenuUI. Each host implements the handful
// of operations below and the pages neither know nor care which one they are
// living inside.
public interface IMenuHost
{
    // Where pages parent their widgets.
    Transform CanvasRoot { get; }

    // Names of the transitions the host currently uses, from UIAnimLibrary.
    string PanelIn { get; }
    string PanelOut { get; }

    void OpenPanel(UIPanel panel);
    void CloseTop();

    // The plain destructive question: CONFIRM / CANCEL.
    void ShowConfirm(string title, string message, Action onAccept);

    // The same dialog, for pages that need to choose the wording, the colours
    // or what the left button does. See ConfirmDialog.Show.
    ConfirmDialog Confirm { get; }

    // Applied when the player picks a different menu animation.
    void SetPanelTransition(string enter);

    // Rebuilds the options page after a settings reset - its rows are built
    // from the values that were current, so they cannot simply be re-read.
    void RebuildOptionsPage();
}
