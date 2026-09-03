using UnityEngine;

// Every tunable number and colour in the menu, in one asset.
//
// Create one via  Assets > Create > THE AFTER > Menu Theme  and save it at
// Assets/Resources/MenuTheme.asset - MenuTheme.Current picks it up
// automatically. Without an asset the field defaults below are used, so the
// game runs fine out of the box and the asset is purely an override layer.
//
// Values are read live, so editing the asset while in Play Mode updates the
// next panel that opens.
[CreateAssetMenu(menuName = "THE AFTER/Menu Theme", fileName = "MenuTheme")]
public class MenuThemeAsset : ScriptableObject
{
    // ================================================================ palette
    [Header("Parchment palette")]
    public Color parchment      = new Color(0.831f, 0.737f, 0.541f, 1f);
    public Color parchmentLight = new Color(0.871f, 0.792f, 0.616f, 1f);
    public Color parchmentDeep  = new Color(0.756f, 0.667f, 0.475f, 1f);
    public Color ink            = new Color(0.110f, 0.141f, 0.200f, 1f);
    public Color inkSoft        = new Color(0.110f, 0.141f, 0.200f, 0.70f);

    [Header("Accents")]
    public Color accent     = new Color(0.573f, 0.286f, 0.153f, 1f);
    public Color accentSoft = new Color(0.729f, 0.435f, 0.271f, 1f);

    [Header("Danger (exit, delete, discard)")]
    public Color danger         = new Color(0.353f, 0.110f, 0.071f, 1f);
    public Color dangerFill     = new Color(0.784f, 0.616f, 0.549f, 1f);

    [Header("Positive (save, confirm)")]
    public Color positive       = new Color(0.129f, 0.310f, 0.180f, 1f);   // deep moss text/border
    public Color positiveFill   = new Color(0.639f, 0.757f, 0.573f, 1f);   // sage fill
    public Color closeBadge     = new Color(0.847f, 0.357f, 0.227f, 1f);
    public Color closeBadgeText = new Color(0.290f, 0.106f, 0.047f, 1f);

    [Header("Backdrop")]
    public Color backdropTop    = new Color(0.129f, 0.114f, 0.106f, 1f);
    public Color backdropBottom = new Color(0.055f, 0.051f, 0.055f, 1f);
    public Color vignetteColor  = new Color(0f, 0f, 0f, 0.90f);
    public Color dustTint       = new Color(0.945f, 0.878f, 0.729f, 1f);
    public Color dimColor       = new Color(0.040f, 0.035f, 0.030f, 0.72f);
    public Color shadow         = new Color(0f, 0f, 0f, 0.30f);
    public Color nightBG        = new Color(0.050f, 0.045f, 0.050f, 1f);
    public Color subtitleColor  = new Color(0.784f, 0.729f, 0.639f, 1f);

    // ================================================================= layout
    [Header("Canvas")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float scalerMatch = 0.5f;
    public int cornerRadius = 14;

    [Header("Title block")]
    public float titleX = 190f;
    public float titleY = -60f;
    public int titleFontSize = 96;
    public int subtitleFontSize = 30;
    public float titleRuleWidth = 620f;

    [Header("Cutscene captions")]
    [Tooltip("Size of the subtitle line during cutscenes. Kept apart from subtitleFontSize, which is the tagline under the game's title - captions are read once, at speed, and want to be bigger than a piece of menu dressing.")]
    public int captionFontSize = 44;

    [Header("Tutorial prompt")]
    [Tooltip("Size of the on-screen hint text (\"Press E to sit\", \"WASD to move\", ...).")]
    public int tutorialFontSize = 38;

    [Header("Quest tracker")]
    [Tooltip("The \"Main Objectives\" label above the task.")]
    public int questHeadingFontSize = 22;
    [Tooltip("The task itself. This is the line the player actually reads.")]
    public int questObjectiveFontSize = 26;
    [Tooltip("Slab behind the tracker. Alpha below 1 keeps the camp readable through it.")]
    public Color questPanelFill = new Color(0.055f, 0.063f, 0.075f, 0.80f);
    [Tooltip("Side of the empty tick box, in pixels.")]
    public float questBoxSize = 20f;

    [Header("Quest marker (the ! over the target)")]
    [Tooltip("How tall the mark is as a share of screen height. 0.05 is one twentieth of the screen - raise it to make the mark bigger, lower it to make it smaller. Size on screen stays the same however far away the target is.")]
    [Range(0.01f, 0.5f)] public float questMarkerScreenHeight = 0.26f;

    [Tooltip("Inside this many metres the mark fades out and leaves the screen to the interact prompt.")]
    public float questMarkerHideWithin = 4.5f;

    // Used only when Assets/Resources/Logo/TheAfterLogo.png exists; with no
    // file there the typed title is drawn instead and these do nothing.
    [Header("Main menu logo")]
    [Tooltip("On-screen height of the logo. Width follows the image aspect.")]
    public float titleLogoHeight = 360f;
    public Vector2 titleLogoOffset = new Vector2(-33.7f, 0f);
    [Tooltip("Seconds the logo takes to appear, in place of the typewriter.")]
    public float titleLogoReveal = 1.0f;
    [Tooltip("Multiplied over the logo. Parchment pulls white line art into the " +
             "menu palette; set it to white to keep the file's own colours.")]
    public Color titleLogoTint = new Color(0.871f, 0.792f, 0.616f, 1f);

    [Header("Button column")]
    public float columnX = 190f;
    [Tooltip("Distance from the top of the screen to the first button. Treated " +
             "as a minimum: a tall logo pushes the column further down.")]
    public float columnTop = -370f;
    [Tooltip("Clearance kept between the bottom of the title block and the first button.")]
    public float columnGapUnderTitle = 28f;
    public float buttonWidth = 430f;
    public float buttonHeight = 68f;
    public float buttonSpacing = 80f;
    public int buttonFontSize = 30;

    [Header("Panel sizes")]
    public Vector2 savePanelSize    = new Vector2(980f, 720f);
    public Vector2 optionsPanelSize = new Vector2(940f, 780f);
    public Vector2 creditsPanelSize = new Vector2(860f, 760f);
    public Vector2 confirmPanelSize = new Vector2(720f, 340f);
    public float slotRowHeight = 116f;

    // ================================================================= motion
    [Header("Panel transitions")]
    public string defaultPanelIn = "RiseFade";
    public float panelDuration = 0.32f;
    public float dimFadeDuration = 0.28f;

    [Header("Button reactions")]
    public float buttonFxDuration = 0.18f;
    public float buttonSlideDistance = 14f;
    public float buttonScaleAmount = 1.045f;
    public float buttonTiltDegrees = -1.2f;

    [Header("Intro sequence")]
    public float introFadeIn = 1.1f;
    public float titleDelay = 0.30f;
    public float titlePerChar = 0.075f;
    public float subtitleDelay = 1.25f;
    public float buttonStaggerStart = 1.35f;
    public float buttonStagger = 0.075f;
    public float musicFadeIn = 3f;

    [Header("Scene transition")]
    public float fadeOutDuration = 0.70f;
    public float fadeHold = 0.15f;
    public float fadeInDuration = 0.90f;

    // ============================================================= atmosphere
    [Header("Background effects")]
    public bool enableDust = true;
    public bool enableGrain = true;
    public bool enableVignette = true;
    public bool enableParallax = true;
    [Range(0, 200)] public int dustCount = 44;
    [Range(0f, 0.2f)] public float grainOpacity = 0.018f;
    [Range(0f, 1f)] public float vignetteOpacity = 0.55f;
    public float parallaxStrength = 18f;

    // =============================================================== dialogue
    // The dialogue box wears the menu palette inverted: an ink slab edged in
    // parchment, so it can sit over live gameplay without hiding it.
    [Header("Dialogue box")]
    public Vector2 dialoguePanelSize = new Vector2(1520f, 250f);
    public float dialogueBottomMargin = 48f;
    [Tooltip("Alpha below 1 keeps the scene readable behind the box.")]
    public Color dialogueFill     = new Color(0.075f, 0.094f, 0.129f, 0.88f);
    public Color dialogueBorder   = new Color(0.756f, 0.667f, 0.475f, 0.85f);
    public Color dialogueText     = new Color(0.925f, 0.878f, 0.792f, 1f);
    public Color dialogueNameFill = new Color(0.831f, 0.737f, 0.541f, 1f);
    public Color dialogueNameText = new Color(0.110f, 0.141f, 0.200f, 1f);
    public int dialogueNameFontSize = 32;
    public int dialogueLineFontSize = 31;
    [Range(0f, 0.3f)] public float dialogueGrainOpacity = 0.05f;

    [Header("Dialogue motion")]
    public float dialogueOpenDuration = 0.38f;
    public float dialogueCloseDuration = 0.28f;
    [Tooltip("Seconds one freshly typed character takes to fade in. 0 = no fade.")]
    public float dialogueCharFade = 0.16f;
    [Tooltip("Pixels a freshly typed character rises through as it fades in.")]
    public float dialogueCharRise = 9f;
    [Tooltip("Beat held after . ! ? - half of it after , ; :")]
    public float dialoguePunctuationPause = 0.16f;
    public float dialogueArrowPulseSpeed = 3.2f;
    public float dialogueLineChangeDuration = 0.22f;

    // Any name registered in UIAnimLibrary works here, including the menu's own
    // transitions (RiseFade, Elastic, InkStamp, ...), not just the dialogue set.
    [Header("Dialogue transitions (UIAnimLibrary names)")]
    public string dialogueFirstMeetingAnim = "DialogueUnfurl";
    [Tooltip("Cycled through for every conversation after the first hello.")]
    public string[] dialogueOpenAnims = { "DialogueInkBleed", "DialogueRiseTilt", "DialogueSwipeIn" };
    public string dialogueFinishAnim = "DialogueFoldAway";
    public string dialogueSkipAnim = "DialogueInkFade";
    public string dialogueInterruptAnim = "DialogueSinkOut";

    [Header("Interact prompt (Press E)")]
    public float dialoguePromptY = 340f;
    public int dialoguePromptFontSize = 22;
    public float dialoguePromptBob = 5f;

    // ============================================================== inventory
    // Same slab and border as the dialogue box - the bag also opens over live
    // gameplay, so it uses the same "ink over the world" language.
    [Header("Inventory layout")]
    public Vector2 inventoryPanelSize  = new Vector2(1280f, 700f);
    public Vector2 inventoryDetailSize = new Vector2(520f, 470f);
    public int inventoryColumns = 5;
    public float inventoryCellSize = 110f;
    public float inventoryCellSpacing = 14f;
    public int inventoryTitleFontSize = 46;
    public int inventoryNameFontSize = 34;
    public int inventoryMetaFontSize = 20;
    public int inventoryBodyFontSize = 22;
    public int inventoryCountFontSize = 20;

    [Header("Inventory colours")]
    public Color inventorySlotFill           = new Color(0.130f, 0.150f, 0.190f, 0.85f);
    public Color inventorySlotHover          = new Color(0.200f, 0.220f, 0.260f, 0.92f);
    // Selection tints the slot rather than inverting it: item glyphs are pale,
    // and a parchment fill behind a pale glyph erases the item.
    public Color inventorySlotSelected       = new Color(0.300f, 0.180f, 0.110f, 0.95f);
    public Color inventorySlotBorder         = new Color(0.756f, 0.667f, 0.475f, 0.50f);
    public Color inventorySlotSelectedBorder = new Color(0.729f, 0.435f, 0.271f, 1f);
    public Color inventoryDetailFill         = new Color(0.100f, 0.120f, 0.155f, 0.75f);
    public Color inventoryCountText          = new Color(0.925f, 0.878f, 0.792f, 0.85f);
    public Color inventoryCountTextSelected  = new Color(0.980f, 0.920f, 0.840f, 1f);

    [Header("Inventory motion")]
    public float inventoryPanelDuration = 0.34f;
    [Tooltip("How long one slot takes to arrive during the opening cascade.")]
    public float inventorySlotDuration = 0.28f;
    [Tooltip("Delay added per step of the cascade. 0 makes every slot arrive at once.")]
    public float inventorySlotStagger = 0.028f;
    public float inventoryHoverDuration = 0.14f;
    [Tooltip("Pixels a slot lifts under the pointer.")]
    public float inventoryHoverLift = 6f;
    [Tooltip("Length of the gain flash and the drop-away.")]
    public float inventoryGainDuration = 0.26f;

    [Tooltip("Cycled through on every open, so the bag never opens the same way twice in a row.")]
    public string[] inventoryOpenAnims = { "BagOpen", "BagUnclasp", "BagSwing", "BagDrop" };
    public string inventoryCloseAnim = "BagClose";
    [Tooltip("Used when Escape closes the bag rather than N or the close button.")]
    public string inventoryCancelAnim = "BagSnap";

    // ================================================================== audio
    [Header("Default volumes (first run only)")]
    [Range(0f, 1f)] public float defaultMaster = 1f;
    [Range(0f, 1f)] public float defaultMusic = 0.55f;
    [Range(0f, 1f)] public float defaultSfx = 0.80f;

    [Header("Credits auto-scroll")]
    public float creditsScrollSpeed = 0.035f;
}
