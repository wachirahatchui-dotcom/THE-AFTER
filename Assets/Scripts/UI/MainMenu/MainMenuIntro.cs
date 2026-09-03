using UnityEngine;
using UnityEngine.EventSystems;

// The opening sequence, kept apart from MainMenuUI so the timing can be
// reworked without touching the menu's structure.
//
// Order: fade up from black, type the title, draw the rule under it, fade the
// subtitle in, then slide the buttons in one at a time and hand focus to the
// first one. Every duration comes from MenuTheme.Current.
//
// Nothing here is on a coroutine of its own - each step is a delayed UITween,
// so the whole sequence is declared in one readable block.
public static class MainMenuIntro
{
    public static void Play(MainMenuUI menu)
    {
        var theme = MenuTheme.Current;

        // Belt and braces: every button has certainly run its Start by now, but
        // re-reading the resting positions costs nothing and guards against a
        // layout pass having moved something.
        menu.MainPanel.RefreshButtonHomes();

        ScreenFader.I.FadeIn(theme.introFadeIn);
        UIAudio.StartMenuMusic(theme.musicFadeIn);

        // 1. Title. Typed one letter at a time - unless a logo has replaced it,
        //    which cannot be typed, so it settles into place instead.
        float titleTime;

        if (menu.TitleLogo != null)
        {
            titleTime = theme.titleLogoReveal;
            var logo = menu.TitleLogo;

            UITween.Delay(menu, theme.titleDelay, () =>
            {
                UIAudio.Play(UISound.Whoosh, 0.55f);

                logo.transform.localScale = Vector3.one * 0.94f;
                UITween.ScaleTo(logo.transform, Vector3.one, titleTime, Ease.OutBack);
                UITween.FadeGraphic(logo, 1f, titleTime * 0.85f, Ease.OutCubic, () =>
                {
                    UIAudio.Play(UISound.Confirm, 0.5f);
                    UITween.Punch(logo.transform, 0.035f, 0.5f, Vector3.one);
                });
            });
        }
        else
        {
            titleTime = menu.titleText.Length * theme.titlePerChar;

            UITween.Delay(menu, theme.titleDelay, () =>
            {
                UIAudio.Play(UISound.Whoosh, 0.55f);
                UITween.Typewriter(menu.TitleLabel, menu.titleText, theme.titlePerChar, () =>
                {
                    UIAudio.Play(UISound.Confirm, 0.5f);
                    UITween.Punch(menu.TitleLabel.transform, 0.045f, 0.5f, Vector3.one);
                });
            });
        }

        // 2. Rule draws itself out from the left, once the title has landed.
        var rule = menu.TitleRule;
        UITween.To(rule, 0.7f, Ease.OutQuint, k =>
        {
            if (rule == null) return;
            var size = rule.rectTransform.sizeDelta;
            size.x = Mathf.Lerp(0f, theme.titleRuleWidth, k);
            rule.rectTransform.sizeDelta = size;
        }, null, theme.titleDelay + titleTime);

        // 3. Subtitle.
        UITween.Fade(menu.SubtitleGroup, 1f, 0.9f, Ease.OutCubic, null, theme.subtitleDelay);

        // 4. Buttons, staggered.
        var groups = menu.MainButtonGroups;
        for (int i = 0; i < groups.Count; i++)
        {
            var cg = groups[i];
            if (cg == null) continue;

            float delay = theme.buttonStaggerStart + i * theme.buttonStagger;
            var rt = (RectTransform)cg.transform;

            UITween.MoveAnchoredFrom(rt, rt.anchoredPosition + new Vector2(-80f, 0f),
                                     0.55f, Ease.OutQuint, null, delay);
            UITween.Fade(cg, 1f, 0.45f, Ease.OutCubic, null, delay);
        }

        // 5. Hand keyboard/gamepad focus to the first row once it has settled.
        float focusDelay = theme.buttonStaggerStart + groups.Count * theme.buttonStagger + 0.3f;
        UITween.Delay(menu, focusDelay, () =>
        {
            if (EventSystem.current != null && menu.MainPanel.firstSelected != null)
                EventSystem.current.SetSelectedGameObject(menu.MainPanel.firstSelected);
        });
    }
}
