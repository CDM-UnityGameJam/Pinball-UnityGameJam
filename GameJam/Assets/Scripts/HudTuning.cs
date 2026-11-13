using System;
using UnityEngine;

[Serializable]
public class HudTuning
{
    public Color background = new Color32(9, 12, 18, 255);
    public Color panelFill = new Color32(17, 23, 37, 255);
    public Color panelEdge = new Color32(43, 52, 72, 255);
    public Color textColor = new Color32(232, 236, 242, 255);
    public Color dim = new Color32(151, 161, 178, 255);
    public Color green = new Color32(71, 214, 163, 255);
    public Color blue = new Color32(91, 141, 240, 255);
    public Color gold = new Color32(233, 185, 73, 255);
    public Color orange = new Color32(240, 162, 58, 255);
    public Color yellow = new Color32(242, 211, 58, 255);
    public Color danger = new Color32(224, 80, 94, 255);
    public Color bumperRing = new Color32(169, 173, 184, 255);
    public Color boardFloor = new Color32(45, 48, 60, 255);
    public Color buttonColor = new Color32(35, 48, 74, 255);
    public Color buttonHoverColor = new Color32(51, 70, 107, 255);
    [Min(1f)] public float slotWidth = 170f;
    [Min(1f)] public float slotHeight = 115f;
    [Min(0f)] public float slotSpacing = 128f;
    public float slotBottom = 40f;
    [Min(0.01f)] public float popupTime = 1f;
    public float popupRise = 40f;
    public Color energyBackground = new Color32(28, 37, 54, 255);
    [Range(0f, 1f)] public float canvasHeightMatch = 1f;
    [Min(1)] public int gameOverDetailsFontSize = 28;
    public float popupWorldForwardOffset = 0.75f;
    public HudOverPlayfieldTuning overPlayfield = new HudOverPlayfieldTuning();
    public HudHandleDialogTuning handleDialog = new HudHandleDialogTuning();
    public HudRefreshSlotsTuning refreshSlots = new HudRefreshSlotsTuning();
    public HudRefreshBoardLabelsTuning refreshBoardLabels = new HudRefreshBoardLabelsTuning();
    public HudRefreshEnemyLabelsTuning refreshEnemyLabels = new HudRefreshEnemyLabelsTuning();
    public HudMakeEnemyLabelTuning makeEnemyLabel = new HudMakeEnemyLabelTuning();
    public HudRefreshPopupsTuning refreshPopups = new HudRefreshPopupsTuning();
    public HudBuildTuning build = new HudBuildTuning();
    public HudStatTuning scoreStat = new HudStatTuning();
    public HudStatTuning livesStat = new HudStatTuning();
    public HudStatTuning timeStat = new HudStatTuning();
    public HudSidePanelTuning leftHelp = new HudSidePanelTuning();
    public HudSidePanelTuning rightHelp = new HudSidePanelTuning();
    public HudPanelTuning panel = new HudPanelTuning();
    public HudMakeTextTuning makeText = new HudMakeTextTuning();
}

[Serializable]
public class HudOverPlayfieldTuning
{
    public float halfWidth = 5f;
    public float minZ = -11f;
    public float maxZ = 12f;
}

[Serializable]
public class HudHandleDialogTuning
{
    public float confirmButtonX = -110f;
    public float confirmButtonY = -75f;
}

[Serializable]
public class HudRefreshSlotsTuning
{
    public int reservedFontSize = 26;
    public int functionalFontSize = 28;
    public int numberFontSize = 44;
    public int ghostNumberFontSize = 38;
    public int ghostFunctionalFontSize = 22;
}

[Serializable]
public class HudRefreshBoardLabelsTuning
{
    public Vector3 spawnLabelPosition = new Vector3(0f, 0f, 11.2f);
    public float dangerLabelX = -5.6f;
}

[Serializable]
public class HudRefreshEnemyLabelsTuning
{
    public Vector2 belowOffset = new Vector2(0f, -6f);
    public Vector2 aboveOffset = new Vector2(0f, 4f);
    public int compactNumberThreshold = 100;
    public int compactFontSize = 24;
    public int regularFontSize = 30;
}

[Serializable]
public class HudMakeEnemyLabelTuning
{
    public Vector2 numberSize = new Vector2(120f, 40f);
    public Vector2 belowSize = new Vector2(200f, 20f);
    public int belowFontSize = 15;
    public Vector2 aboveSize = new Vector2(200f, 20f);
    public int aboveFontSize = 15;
    public Vector2 popupSize = new Vector2(500f, 40f);
    public int popupFontSize = 24;
}

[Serializable]
public class HudRefreshPopupsTuning
{
    public float worldHeight = 0.5f;
    public float initialRise = 14f;
}

[Serializable]
public class HudBuildTuning
{
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public Vector2 titleSize = new Vector2(400f, 50f);
    public Vector2 titleAnchor = new Vector2(0f, 1f);
    public Vector2 titlePosition = new Vector2(60f, -26f);
    public int titleFontSize = 40;
    public Vector2 effectsSize = new Vector2(600f, 30f);
    public Vector2 effectsAnchor = new Vector2(1f, 1f);
    public Vector2 effectsPosition = new Vector2(-60f, -34f);
    public int effectsFontSize = 22;
    public Vector2 topBarAnchor = new Vector2(0.5f, 1f);
    public Vector2 topBarPosition = new Vector2(0f, -75f);
    public Vector2 topBarSize = new Vector2(1060f, 90f);
    public float scoreX = -130f;
    public float livesX = -490f;
    public float timeX = 280f;
    public Vector2 comboAnchor = new Vector2(0f, 1f);
    public Vector2 comboPosition = new Vector2(90f, -150f);
    public Vector2 groupPreviewAnchor = new Vector2(1f, .5f);
    public Vector2 groupPreviewPosition = new Vector2(-240f, 40f);
    public Vector2 castHintAnchor = new Vector2(.5f, 0f);
    public Vector2 castHintPosition = new Vector2(0f, 16f);
    public Vector2 keyboardHintAnchor = new Vector2(.5f, 0f);
    public Vector2 keyboardHintPosition = new Vector2(0f, 300f);
    public Vector2 leftHelpAnchor = new Vector2(0f, 1f);
    public Vector2 leftHelpPosition = new Vector2(70f, -230f);
    public Vector2 rightHelpAnchor = new Vector2(1f, 1f);
    public Vector2 rightHelpPosition = new Vector2(-70f, -230f);
    public Vector2 spawnLabelSize = new Vector2(300f, 24f);
    public int spawnLabelFontSize = 14;
    public Vector2 dangerLabelSize = new Vector2(200f, 24f);
    public int dangerLabelFontSize = 16;
    public Vector2 dangerLabelPivot = new Vector2(1f, 0.5f);
    public Vector2 bottomBarAnchor = new Vector2(0.5f, 0f);
    public Vector2 bottomBarPosition = new Vector2(0f, 210f);
    public float bottomBarWidthPadding = 60f;
    [Min(1f)] public float rechargePanelWidth = 982f;
    public float bottomBarHeight = 80f;
    public float bottomContentWidthPadding = 60f;
    public float bottomContentInset = 25f;
    public Vector2 luckyLabelSize = new Vector2(160f, 30f);
    public int luckyFontSize = 20;
    public float luckyY = 17f;
    public Vector2 currentLabelSize = new Vector2(300f, 40f);
    public int currentFontSize = 34;
    public float currentX = 170f;
    public float currentY = 17f;
    public Vector2 previewLabelSize = new Vector2(400f, 36f);
    public int previewFontSize = 26;
    public float previewX = 500f;
    public float previewY = 17f;
    public Vector2 energyLabelSize = new Vector2(200f, 26f);
    public int energyFontSize = 18;
    public float energyY = -20f;
    public Vector2 energyBarAnchor = new Vector2(0.5f, 0.5f);
    public Vector2 energyBarSize = new Vector2(720f, 14f);
    public float energyBarX = 170f;
    public float energyBarExtraX = 360f;
    public float energyBarY = -20f;
    public Vector2 energyFillAnchor = new Vector2(0f, 1f);
    public Vector2 handAnchor = new Vector2(0.5f, 0f);
    public float handWidthPadding = 60f;
    public float handHeightPadding = 16f;
    public Vector2 slotAnchor = new Vector2(0.5f, 0.5f);
    public Vector2 slotCaptionPosition = new Vector2(0f, 38f);
    public int slotCaptionFontSize = 16;
    public float slotCaptionHeight = 22f;
    public Vector2 slotCardPosition = new Vector2(0f, 2f);
    public float slotCardHeight = 52f;
    public Vector2 slotResultPosition = new Vector2(0f, -36f);
    public int slotResultFontSize = 17;
    public float slotResultHeight = 22f;
    public int replacementFontSize = 14;
    public float replacementY = 13f;
    public float replacementWidthPadding = 40f;
    public float replacementHeight = 20f;
    public Vector2 ghostSize = new Vector2(120f, 92f);
    public Vector2 ghostCardPosition = new Vector2(0f, 12f);
    public Vector2 ghostCardSize = new Vector2(120f, 46f);
    public Vector2 ghostResultPosition = new Vector2(0f, -24f);
    public Vector2 ghostResultSize = new Vector2(120f, 26f);
    public int ghostResultFontSize = 20;
    public Vector2 countdownPosition = new Vector2(0f, 40f);
    public Vector2 countdownSize = new Vector2(300f, 160f);
    public int countdownFontSize = 120;
    public Vector2 dialogAnchor = new Vector2(0.5f, 0.5f);
    public Vector2 dialogPosition = new Vector2(0f, 40f);
    public Vector2 dialogSize = new Vector2(640f, 270f);
    public Vector2 dialogTextPosition = new Vector2(0f, 45f);
    public Vector2 dialogTextSize = new Vector2(620f, 140f);
    public int dialogFontSize = 52;
    public Vector2 buttonAnchor = new Vector2(0.5f, 0.5f);
    public Vector2 buttonSize = new Vector2(190f, 60f);
    public float firstButtonX = -110f;
    public float secondButtonX = 110f;
    public float buttonY = -75f;
    public Vector2 buttonTextSize = new Vector2(190f, 60f);
    public int buttonFontSize = 28;
}

[Serializable]
public class HudStatTuning
{
    public Vector2 captionSize = new Vector2(260f, 24f);
    public int captionFontSize = 17;
    public float captionY = 20f;
    public Vector2 valueSize = new Vector2(260f, 44f);
    public int valueFontSize = 36;
    public float valueY = -16f;
}

[Serializable]
public class HudSidePanelTuning
{
    public Vector2 size = new Vector2(420f, 10f);
    public Vector2 titleSize = new Vector2(420f, 30f);
    public int titleFontSize = 24;
    public float titleSpacing = 40f;
    public Vector2 lineSize = new Vector2(420f, 26f);
    public int lineFontSize = 20;
    public float lineSpacing = 31f;
    public float sectionSpacing = 32f;
}

[Serializable]
public class HudPanelTuning
{
    public Vector2 fillAnchor = new Vector2(0.5f, 0.5f);
    public Vector2 borderInset = new Vector2(4f, 4f);
}

[Serializable]
public class HudMakeTextTuning
{
    public Vector2 defaultAnchor = new Vector2(0.5f, 0.5f);
}
