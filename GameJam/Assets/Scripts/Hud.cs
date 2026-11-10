using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Temporary HUD and card hand (spec section 8: redesign later), laid out after the functional mockup and
// built in code so the scene stays untouched. No EventSystem: the mouse is read directly, so dragging
// works during the launch wait when timeScale is 0.
public class Hud : MonoBehaviour
{
    public static Hud Instance;

    HudTuning H => gm.hud;
    public static Color Background => GameManager.Instance ? GameManager.Instance.hud.background : new HudTuning().background;
    public static Color PanelFill => GameManager.Instance ? GameManager.Instance.hud.panelFill : new HudTuning().panelFill;
    public static Color PanelEdge => GameManager.Instance ? GameManager.Instance.hud.panelEdge : new HudTuning().panelEdge;
    public static Color TextColor => GameManager.Instance ? GameManager.Instance.hud.textColor : new HudTuning().textColor;
    public static Color Dim => GameManager.Instance ? GameManager.Instance.hud.dim : new HudTuning().dim;
    public static Color Green => GameManager.Instance ? GameManager.Instance.hud.green : new HudTuning().green;
    public static Color Blue => GameManager.Instance ? GameManager.Instance.hud.blue : new HudTuning().blue;
    public static Color Gold => GameManager.Instance ? GameManager.Instance.hud.gold : new HudTuning().gold;
    public static Color Orange => GameManager.Instance ? GameManager.Instance.hud.orange : new HudTuning().orange;
    public static Color Yellow => GameManager.Instance ? GameManager.Instance.hud.yellow : new HudTuning().yellow;
    public static Color Danger => GameManager.Instance ? GameManager.Instance.hud.danger : new HudTuning().danger;
    public static Color BumperRing => GameManager.Instance ? GameManager.Instance.hud.bumperRing : new HudTuning().bumperRing;
    public static Color BoardFloor => GameManager.Instance ? GameManager.Instance.hud.boardFloor : new HudTuning().boardFloor;
    public static Color ButtonColor => GameManager.Instance ? GameManager.Instance.hud.buttonColor : new HudTuning().buttonColor;
    public static Color ButtonHoverColor => GameManager.Instance ? GameManager.Instance.hud.buttonHoverColor : new HudTuning().buttonHoverColor;

    class SlotView { public RectTransform rect; public Image edge; public Text card, result; }
    class EnemyLabel { public Text number, below, above; }
    class FloatingText { public Text text; public Vector3 world; public float age; }

    GameManager gm;
    Camera cam;
    Canvas canvas;
    Font font;
    RectTransform root, labelLayer, popupLayer, topBar, bottomBar, handArea, energyFill, ghost, dialog;
    Text scoreText, livesText, timeText, currentText, previewText, energyText, countdownText, ghostCard, ghostResult, dialogText, spawnLabel, dangerLabel;
    readonly RectTransform[] buttons = new RectTransform[2];
    readonly Text[] buttonTexts = new Text[2];
    SlotView[] slots;
    readonly Dictionary<Enemy, EnemyLabel> labels = new Dictionary<Enemy, EnemyLabel>();
    readonly HashSet<Enemy> alive = new HashSet<Enemy>();
    readonly List<Enemy> gone = new List<Enemy>();
    readonly List<FloatingText> popups = new List<FloatingText>();

    int dragSlot = -1; // slot the dragged card currently owns; -1 = no drag
    Text effectText;

    // Result the dragged card would give. Display only: damage keeps using the current number until release.
    public int? PreviewNumber
    {
        get
        {
            if (dragSlot < 0) return null;
            var c = gm.GetCard(dragSlot);
            return c == null || !c.Value.IsNumber ? (int?)null : c.Value.Apply(gm.cards.baseNumber);
        }
    }

    void Awake()
    {
        Instance = this;
        gm = GameManager.Instance;
        slots = new SlotView[gm.HandSize];
        cam = Camera.main;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gm.SlotReplaced += OnSlotReplaced;
        gm.CardShifted += OnCardShifted;
        Build();
    }

    void OnDestroy()
    {
        if (!gm) return;
        gm.SlotReplaced -= OnSlotReplaced;
        gm.CardShifted -= OnCardShifted;
    }

    void OnCardShifted(int from, int to) { if (dragSlot == from) dragSlot = to; }

    // A draw is replacing the dragged card: drop the drag so its release can never use the new card.
    void OnSlotReplaced(int slot) { if (slot == dragSlot) dragSlot = -1; }

    void Update()
    {
        var mouse = Mouse.current;
        Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        HandleDrag(mouse != null, mouse != null && mouse.leftButton.wasPressedThisFrame, mouse != null && mouse.leftButton.wasReleasedThisFrame, pointer);
        HandleDialog(mouse != null && mouse.leftButton.wasPressedThisFrame, pointer);
        RefreshBars();
        RefreshSlots(pointer);
        RefreshBoardLabels();
        RefreshEnemyLabels();
        RefreshPopups();
    }

    // ---- Input ----

    void HandleDrag(bool hasMouse, bool pressed, bool released, Vector2 pointer)
    {
        bool inputAllowed = !gm.Paused && !gm.IsGameOver;
        if (!inputAllowed || !hasMouse) { dragSlot = -1; return; }

        if (dragSlot < 0)
        {
            if (!pressed) return;
            for (int i = 0; i < slots.Length; i++)
                if (gm.GetCard(i) != null && Contains(slots[i].rect, pointer)) { dragSlot = i; break; }
            return;
        }

        if (gm.GetCard(dragSlot) == null) { dragSlot = -1; return; }

        // Over the hand, the dragged card takes the nearest slot and the others shift by insertion.
        if (Contains(handArea, pointer))
        {
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < slots.Length; i++)
            {
                float d = Mathf.Abs(RectTransformUtility.WorldToScreenPoint(null, slots[i].rect.position).x - pointer.x);
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest != dragSlot) dragSlot = gm.MoveCard(dragSlot, nearest);
        }

        if (released)
        {
            if (OverPlayfield(pointer)) gm.UseCard(dragSlot); // anywhere else: stays in its latest slot, unused
            dragSlot = -1;
        }
    }

    bool OverPlayfield(Vector2 pointer)
    {
        if (Contains(topBar, pointer) || Contains(bottomBar, pointer) || Contains(handArea, pointer)) return false;
        var board = gm.board;
        var ray = cam.ScreenPointToRay(pointer);
        if (!new Plane(board.up, board.position).Raycast(ray, out float dist)) return false;
        var local = board.InverseTransformPoint(ray.GetPoint(dist));
        return Mathf.Abs(local.x) <= H.overPlayfield.halfWidth && local.z >= H.overPlayfield.minZ && local.z <= H.overPlayfield.maxZ;
    }

    void HandleDialog(bool pressed, Vector2 pointer)
    {
        bool over = gm.IsGameOver, pause = gm.Paused && !over;
        dialog.gameObject.SetActive(over || pause);
        countdownText.gameObject.SetActive(gm.IsWaiting && !gm.Paused);
        countdownText.text = Mathf.CeilToInt(gm.Countdown).ToString();
        if (!over && !pause) return;

        dialogText.text = over
            ? "GAME OVER\n<size=" + H.gameOverDetailsFontSize + "><color=#" + ColorUtility.ToHtmlStringRGB(Dim) + ">Score </color>" + gm.Score.ToString("N0")
              + "<color=#" + ColorUtility.ToHtmlStringRGB(Dim) + ">     Active time </color>" + FormatTime(gm.ActiveTime) + "</size>"
            : "Restart run?";
        buttons[1].gameObject.SetActive(pause);
        buttonTexts[0].text = over ? "Restart" : "Yes";
        buttons[0].anchoredPosition = new Vector2(over ? 0f : H.handleDialog.confirmButtonX, H.handleDialog.confirmButtonY);
        for (int i = 0; i < buttons.Length; i++)
            buttons[i].GetComponent<Image>().color = Contains(buttons[i], pointer) ? ButtonHoverColor : ButtonColor;

        if (!pressed) return;
        if (Contains(buttons[0], pointer)) gm.Restart();
        else if (pause && Contains(buttons[1], pointer)) gm.SetPaused(false);
    }

    // ---- Refresh ----

    void RefreshBars()
    {
        scoreText.text = gm.Score.ToString("N0");
        livesText.text = gm.Lives + " / " + gm.maxLives;
        timeText.text = FormatTime(gm.ActiveTime);
        currentText.text = "CURRENT " + gm.CurrentNumber;
        previewText.text = dragSlot < 0 ? "" : "PREVIEW: " + Expression(gm.GetCard(dragSlot).Value, true);
        int autoReturns = 0;
        foreach (var activeBall in gm.Balls)
            if (activeBall && activeBall.TryGetComponent<SideReturnBall>(out var returnState) && returnState.InTransit) autoReturns++;
        effectText.text = "BALLS " + gm.BallCount + "  |  RETURNING " + autoReturns + (gm.QueuedBalls > 0 ? "  (QUEUED " + gm.QueuedBalls + ")" : "") +
            (gm.HasMatchBoost ? "  |  MATCH " + gm.MatchBoostLeft.ToString("0.0") + "s" : "");
        energyText.text = "ENERGY " + gm.Energy + " / " + gm.EnergyPerCard;
        energyFill.anchorMax = new Vector2(Mathf.Clamp01(gm.Energy / (float)gm.EnergyPerCard), 1f);
    }

    void RefreshSlots(Vector2 pointer)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            var c = gm.GetCard(i);
            bool reserved = i == dragSlot;
            s.edge.color = reserved ? Blue : i == slots.Length - 1 ? Orange : PanelEdge;
            s.card.color = reserved ? Blue : TextColor;
            s.card.fontSize = reserved ? H.refreshSlots.reservedFontSize : c != null && !c.Value.IsNumber ? H.refreshSlots.functionalFontSize : H.refreshSlots.numberFontSize;
            s.card.text = reserved ? "RESERVED" : c == null ? "-" : CardText(c.Value);
            s.result.text = reserved || c == null ? "" : Expression(c.Value, false);
        }

        ghost.gameObject.SetActive(dragSlot >= 0);
        if (dragSlot < 0) return;
        var card = gm.GetCard(dragSlot).Value;
        ghost.anchoredPosition = pointer / canvas.scaleFactor;
        ghostCard.text = CardText(card);
        ghostCard.fontSize = card.IsNumber ? H.refreshSlots.ghostNumberFontSize : H.refreshSlots.ghostFunctionalFontSize;
        ghostResult.text = card.IsNumber ? "= " + card.Apply(gm.cards.baseNumber) : card.kind == Card.Kind.MatchBoost ? "+" + gm.matchBoostDuration + " seconds" : "+" + gm.extraBallsPerCard + " ball";
    }

    // Board callouts from the mockup, pinned to board positions.
    void RefreshBoardLabels()
    {
        spawnLabel.rectTransform.anchoredPosition = ToCanvas(gm.board.TransformPoint(H.refreshBoardLabels.spawnLabelPosition));
        // Outside the left wall: inside the board, enemies about to cross would cover it.
        dangerLabel.rectTransform.anchoredPosition = ToCanvas(gm.board.TransformPoint(new Vector3(H.refreshBoardLabels.dangerLabelX, 0f, gm.spawning.dangerZ)));
    }

    void RefreshEnemyLabels()
    {
        alive.Clear();
        int current = gm.CurrentNumber;
        var preview = PreviewNumber;
        foreach (var e in FindObjectsByType<Enemy>())
        {
            alive.Add(e);
            if (!labels.TryGetValue(e, out var l)) labels[e] = l = MakeEnemyLabel();

            // Hug the disc's projected outline at any resolution: the number on its top face, the info line
            // under its near (bottom) edge including the visible side band, the special line over its far edge.
            var up = e.transform.up * e.transform.lossyScale.y; // cylinder half-height = scale.y
            var forward = e.transform.parent.forward * e.Radius;
            var top = e.transform.position + up;
            l.number.rectTransform.anchoredPosition = ToCanvas(top);
            l.below.rectTransform.anchoredPosition = ToCanvas(e.transform.position - up - forward) + H.refreshEnemyLabels.belowOffset;
            l.above.rectTransform.anchoredPosition = ToCanvas(top + forward) + H.refreshEnemyLabels.aboveOffset;

            bool visible = e.Visible;
            l.number.gameObject.SetActive(e.kind != Enemy.Kind.Bumper);
            l.above.gameObject.SetActive(e.kind == Enemy.Kind.Special);
            l.number.text = visible ? e.target.ToString() : "?";
            l.number.fontSize = e.target >= H.refreshEnemyLabels.compactNumberThreshold && visible ? H.refreshEnemyLabels.compactFontSize : H.refreshEnemyLabels.regularFontSize; // 3 digits stay inside the disc
            l.number.color = !visible ? TextColor : gm.Matches(e) ? Green : preview == e.target ? Blue : TextColor;

            switch (e.kind)
            {
                case Enemy.Kind.Special:
                    l.above.text = "x" + e.Multiplier + "  ·  " + Mathf.CeilToInt(e.LifeLeft) + "s"; // kept short so passing descenders' labels clear it
                    l.below.text = "HP " + e.hp + " / " + e.maxHp;
                    break;
                case Enemy.Kind.Normal:
                    l.below.text = "HP " + e.hp + " / " + e.maxHp + (visible ? "  ·  " + Mathf.CeilToInt(e.VisibleLeft) + "s" : "");
                    break;
                default:
                    l.below.text = "BUMPER";
                    break;
            }
        }

        gone.Clear();
        foreach (var kv in labels) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var e in gone)
        {
            var l = labels[e];
            Destroy(l.number.gameObject);
            Destroy(l.below.gameObject);
            Destroy(l.above.gameObject);
            labels.Remove(e);
        }
    }

    EnemyLabel MakeEnemyLabel()
    {
        var l = new EnemyLabel
        {
            number = Free(MakeText(labelLayer, "", H.refreshEnemyLabels.regularFontSize, TextColor, TextAnchor.MiddleCenter, Vector2.zero, H.makeEnemyLabel.numberSize)),
            below = Free(MakeText(labelLayer, "", H.makeEnemyLabel.belowFontSize, Dim, TextAnchor.UpperCenter, Vector2.zero, H.makeEnemyLabel.belowSize)),
            above = Free(MakeText(labelLayer, "", H.makeEnemyLabel.aboveFontSize, Gold, TextAnchor.LowerCenter, Vector2.zero, H.makeEnemyLabel.aboveSize)),
        };
        l.number.fontStyle = FontStyle.Bold;
        l.above.fontStyle = FontStyle.Bold;
        return l;
    }

    public void Popup(Vector3 world, string text, Color color)
    {
        var t = Free(MakeText(popupLayer, text, H.makeEnemyLabel.popupFontSize, color, TextAnchor.MiddleCenter, Vector2.zero, H.makeEnemyLabel.popupSize));
        t.fontStyle = FontStyle.Bold;
        popups.Add(new FloatingText { text = t, world = world });
    }

    // Unscaled so feedback still finishes when a hit causes game over (timeScale 0).
    void RefreshPopups()
    {
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            var p = popups[i];
            p.age += Time.unscaledDeltaTime;
            if (p.age >= Mathf.Max(0.01f, H.popupTime)) { Destroy(p.text.gameObject); popups.RemoveAt(i); continue; }
            // Starts just above the disc's far edge so it never covers the number it is about.
            var start = ToCanvas(p.world + gm.board.up * H.refreshPopups.worldHeight + gm.board.forward * H.popupWorldForwardOffset);
            p.text.rectTransform.anchoredPosition = start + new Vector2(0f, H.refreshPopups.initialRise + H.popupRise * p.age / Mathf.Max(0.01f, H.popupTime));
            var col = p.text.color; col.a = 1f - p.age / Mathf.Max(0.01f, H.popupTime); p.text.color = col;
        }
    }

    // ---- Layout (reference 1920x1080, scaled by height) ----

    void Build()
    {
        var go = new GameObject("HudCanvas", typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = H.build.referenceResolution;
        scaler.matchWidthOrHeight = H.canvasHeightMatch; // the camera frames the board by height, so the HUD follows height too
        root = (RectTransform)go.transform;
        // Draw order: board labels, then feedback popups over them, then every panel (created after these).
        labelLayer = Layer("BoardLabels");
        popupLayer = Layer("Popups");

        // Title row above the stats bar, as in the mockup.
        Bold(Anchored(MakeText(root, "LUCKY " + gm.cards.baseNumber, H.build.titleFontSize, TextColor, TextAnchor.UpperLeft, Vector2.zero, H.build.titleSize), H.build.titleAnchor, H.build.titlePosition));
        effectText = Anchored(MakeText(root, "", H.build.effectsFontSize, Orange, TextAnchor.UpperRight, Vector2.zero, H.build.effectsSize), H.build.effectsAnchor, H.build.effectsPosition);

        // Top: score, lives, active time.
        topBar = Panel(root, "TopBar", H.build.topBarAnchor, H.build.topBarPosition, H.build.topBarSize, PanelEdge);
        scoreText = Stat(topBar, "SCORE", H.build.scoreX, H.scoreStat);
        livesText = Stat(topBar, "LIVES", H.build.livesX, H.livesStat);
        timeText = Stat(topBar, "ACTIVE TIME", H.build.timeX, H.timeStat);

        // Side panels.
        SidePanel(H.build.leftHelpAnchor, H.build.leftHelpPosition, H.leftHelp, TextAnchor.UpperLeft, new[]
        {
            ("MATCH TO INSTANT KILL", TextColor, new[] { ("Green: matches / kill reward x" + gm.MatchKillMultiplier + ".", Green), ("Match pulse: " + gm.normalEnemy.pulseDamage + " damage.", Dim), ("Reveals hidden targets for " + gm.normalEnemy.revealDuration + "s.", Dim), ("Blue: matches the dragged card preview.", Blue) }),
            (gm.normalEnemy.health + " HP / HIT TO REVEAL", TextColor, new[] { ("Normal hit: " + gm.normalEnemy.hitDamage + " damage.", Dim), ("Reveal duration: " + gm.normalEnemy.revealDuration + "s.", Dim), ("Normal hit energy: +" + gm.normalEnemy.hitEnergy + ".", Dim), (gm.EnergyPerCard + " energy = 1 new card.", Dim) }),
            ("A / LEFT ARROW", Yellow, new[] { ("Left flipper", Dim) }),
            ("R", Yellow, new[] { ("Pause / restart", Dim) }),
        });
        SidePanel(H.build.rightHelpAnchor, H.build.rightHelpPosition, H.rightHelp, TextAnchor.UpperLeft, new[]
        {
            ("SPECIAL ENEMY", TextColor, SpecialHelp()),
            ("HAND MANAGEMENT", TextColor, new[] { ("Start empty / cards shift left after use.", Dim), ("Drag cards to reorder.", Dim), ("Drop on the table to use.", Dim), ("Drop elsewhere to cancel.", Dim), ("Full hand: replace slot " + gm.HandSize + ".", Dim), ("D / Right arrow: right flipper.", Yellow) }),
            ("FUNCTION CARDS", Orange, new[] { ("MATCH: +" + gm.matchBoostDuration + "s auto-match.", Dim), ("BALL +" + gm.extraBallsPerCard + ": no ball limit.", Dim), ("Only losing the last ball costs a life.", Dim) }),
        });

        // Board callouts.
        spawnLabel = Free(MakeText(labelLayer, "ENEMY SPAWN AREA", H.build.spawnLabelFontSize, Dim, TextAnchor.MiddleCenter, Vector2.zero, H.build.spawnLabelSize));
        dangerLabel = Free(MakeText(labelLayer, "DANGER LINE", H.build.dangerLabelFontSize, Danger, TextAnchor.MiddleRight, Vector2.zero, H.build.dangerLabelSize));
        dangerLabel.rectTransform.pivot = H.build.dangerLabelPivot;

        // Bottom: lucky number, current number, preview, energy bar.
        float handWidth = slots.Length * H.slotWidth + (slots.Length - 1) * H.slotSpacing;
        bottomBar = Panel(root, "BottomBar", H.build.bottomBarAnchor, H.build.bottomBarPosition, new Vector2(handWidth + H.build.bottomBarWidthPadding, H.build.bottomBarHeight), PanelEdge);
        float left = -(handWidth + H.build.bottomContentWidthPadding) / 2f + H.build.bottomContentInset;
        LeftText(MakeText(bottomBar, "LUCKY " + gm.cards.baseNumber, H.build.luckyFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(left, H.build.luckyY), H.build.luckyLabelSize));
        currentText = Bold(LeftText(MakeText(bottomBar, "", H.build.currentFontSize, Green, TextAnchor.MiddleLeft, new Vector2(left + H.build.currentX, H.build.currentY), H.build.currentLabelSize)));
        previewText = LeftText(MakeText(bottomBar, "", H.build.previewFontSize, Blue, TextAnchor.MiddleLeft, new Vector2(left + H.build.previewX, H.build.previewY), H.build.previewLabelSize));
        energyText = LeftText(MakeText(bottomBar, "", H.build.energyFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(left, H.build.energyY), H.build.energyLabelSize));
        var barBack = Box(bottomBar, "EnergyBar", H.build.energyBarAnchor, new Vector2(left + H.build.energyBarX + H.build.energyBarExtraX, H.build.energyBarY), H.build.energyBarSize, H.energyBackground);
        energyFill = Box(barBack, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Green);
        energyFill.anchorMin = Vector2.zero; energyFill.anchorMax = H.build.energyFillAnchor;
        energyFill.offsetMin = energyFill.offsetMax = Vector2.zero;

        // Hand: 5 slots; slot 5 is the one a full-hand draw replaces.
        handArea = Box(root, "Hand", H.build.handAnchor, new Vector2(0f, H.slotBottom + H.slotHeight / 2f), new Vector2(handWidth + H.build.handWidthPadding, H.slotHeight + H.build.handHeightPadding), Color.clear);
        for (int i = 0; i < slots.Length; i++)
        {
            float x = -handWidth / 2f + H.slotWidth / 2f + i * (H.slotWidth + H.slotSpacing);
            var s = new SlotView();
            s.rect = Panel(handArea, "Slot" + (i + 1), H.build.slotAnchor, new Vector2(x, 0f), new Vector2(H.slotWidth, H.slotHeight), PanelEdge);
            s.edge = s.rect.GetComponent<Image>();
            MakeText(s.rect, "SLOT " + (i + 1), H.build.slotCaptionFontSize, Dim, TextAnchor.MiddleCenter, H.build.slotCaptionPosition, new Vector2(H.slotWidth, H.build.slotCaptionHeight));
            s.card = Bold(MakeText(s.rect, "", H.refreshSlots.numberFontSize, TextColor, TextAnchor.MiddleCenter, H.build.slotCardPosition, new Vector2(H.slotWidth, H.build.slotCardHeight)));
            s.result = MakeText(s.rect, "", H.build.slotResultFontSize, Dim, TextAnchor.MiddleCenter, H.build.slotResultPosition, new Vector2(H.slotWidth, H.build.slotResultHeight));
            slots[i] = s;
        }
        MakeText(slots[slots.Length - 1].rect, "REPLACED WHEN FULL", H.build.replacementFontSize, Orange, TextAnchor.MiddleCenter, new Vector2(0f, -H.slotHeight / 2f - H.build.replacementY), new Vector2(H.slotWidth + H.build.replacementWidthPadding, H.build.replacementHeight));

        // Dragged card ghost.
        ghost = Panel(root, "Ghost", Vector2.zero, Vector2.zero, H.build.ghostSize, Blue);
        ghostCard = Bold(MakeText(ghost, "", H.refreshSlots.ghostNumberFontSize, Blue, TextAnchor.MiddleCenter, H.build.ghostCardPosition, H.build.ghostCardSize));
        ghostResult = MakeText(ghost, "", H.build.ghostResultFontSize, Blue, TextAnchor.MiddleCenter, H.build.ghostResultPosition, H.build.ghostResultSize);
        ghost.gameObject.SetActive(false);

        countdownText = Bold(MakeText(root, "", H.build.countdownFontSize, TextColor, TextAnchor.MiddleCenter, H.build.countdownPosition, H.build.countdownSize));

        // Pause / game over dialog.
        dialog = Panel(root, "Dialog", H.build.dialogAnchor, H.build.dialogPosition, H.build.dialogSize, PanelEdge);
        dialogText = Bold(MakeText(dialog, "", H.build.dialogFontSize, TextColor, TextAnchor.MiddleCenter, H.build.dialogTextPosition, H.build.dialogTextSize));
        for (int i = 0; i < 2; i++)
        {
            buttons[i] = Box(dialog, "Button" + i, H.build.buttonAnchor, new Vector2(i == 0 ? H.build.firstButtonX : H.build.secondButtonX, H.build.buttonY), H.build.buttonSize, ButtonColor);
            buttonTexts[i] = MakeText(buttons[i], i == 0 ? "Yes" : "No", H.build.buttonFontSize, TextColor, TextAnchor.MiddleCenter, Vector2.zero, H.build.buttonTextSize);
        }
        dialog.gameObject.SetActive(false);
    }

    Text Stat(RectTransform bar, string caption, float x, HudStatTuning style)
    {
        LeftText(MakeText(bar, caption, style.captionFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(x, style.captionY), style.captionSize));
        return Bold(LeftText(MakeText(bar, "", style.valueFontSize, TextColor, TextAnchor.MiddleLeft, new Vector2(x, style.valueY), style.valueSize)));
    }

    void SidePanel(Vector2 anchor, Vector2 pos, HudSidePanelTuning style, TextAnchor align, (string title, Color color, (string text, Color color)[] lines)[] sections)
    {
        float y = 0f;
        var rt = Box(root, "Side", anchor, pos, style.size, Color.clear);
        rt.pivot = new Vector2(anchor.x, 1f);
        rt.anchoredPosition = pos;
        foreach (var (title, color, lines) in sections)
        {
            Bold(TopLeft(MakeText(rt, title, style.titleFontSize, color, align, Vector2.zero, style.titleSize), y));
            y -= style.titleSpacing;
            foreach (var (text, c) in lines)
            {
                TopLeft(MakeText(rt, text, style.lineFontSize, c, align, Vector2.zero, style.lineSize), y);
                y -= style.lineSpacing;
            }
            y -= style.sectionSpacing;
        }
    }

    (string text, Color color)[] SpecialHelp()
    {
        var lines = new List<(string text, Color color)> {
            ("Exact number match only / ignores pulse", Gold),
            ("Unmatched: balls pass through.", Gold),
            (gm.specialEnemy.lifetime + "s lifetime / stationary", Gold),
            ("Match kill reward x" + gm.specialMatchKillMultiplier, Gold)
        };
        foreach (var entry in gm.spawning.specialTargets)
            if (entry != null) lines.Add((entry.target + ": score x" + entry.scoreMultiplier, Gold));
        return lines.ToArray();
    }

    // ---- UI helpers ----

    RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color edge)
    {
        var outer = Box(parent, name, anchor, pos, size, edge);
        Box(outer, "Fill", H.panel.fillAnchor, Vector2.zero, size - H.panel.borderInset, PanelFill);
        return outer;
    }

    static RectTransform Box(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    // Text centered on `pos` inside its parent; the helpers below re-anchor it.
    Text MakeText(RectTransform parent, string text, int size, Color color, TextAnchor align, Vector2 pos, Vector2 box)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = H.makeText.defaultAnchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Text Bold(Text t) { t.fontStyle = FontStyle.Bold; return t; }

    static Text Anchored(Text t, Vector2 anchor, Vector2 pos)
    {
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        return t;
    }

    // Left edge sits at the given x (a pivot change, so `anchoredPosition.x` is the left edge).
    static Text LeftText(Text t) { t.rectTransform.pivot = new Vector2(0f, 0.5f); return t; }

    static Text TopLeft(Text t, float y)
    {
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        return t;
    }

    // Board-following text: bottom-left anchored so anchoredPosition is canvas space.
    static Text Free(Text t)
    {
        t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
        return t;
    }

    // Full-screen container whose children use canvas-space positions from the bottom-left corner.
    RectTransform Layer(string name)
    {
        var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    Vector2 ToCanvas(Vector3 world) => (Vector2)cam.WorldToScreenPoint(world) / canvas.scaleFactor;

    static bool Contains(RectTransform rt, Vector2 screen) =>
        rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rt, screen, null);

    string CardText(Card c) => c.kind == Card.Kind.ExtraBall ? "BALL +" + gm.extraBallsPerCard : c.ToString();

    // Slot form "37 +3 = 40"; preview form "37 + 3 = 40" as in the mockup.
    string Expression(Card c, bool spaced) => !c.IsNumber
        ? c.kind == Card.Kind.MatchBoost ? "+" + gm.matchBoostDuration + "s auto-match" : "+" + gm.extraBallsPerCard + " ball"
        : gm.cards.baseNumber + " " + (spaced ? c.op + " " + c.n : c.ToString()) + " = " + c.Apply(gm.cards.baseNumber);

    static string FormatTime(float t) => Mathf.FloorToInt(t / 60f).ToString("00") + ":" + Mathf.FloorToInt(t % 60f).ToString("00");

}
