using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Runtime HUD. Drag-to-submit only; hand positions cannot be reordered.
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

    class SlotView { public RectTransform rect; public Image edge; public Text card, result, cooldown; }
    class EnemyLabel { public Text number, below, status; public RectTransform healthBack, healthFill; }
    class FloatingText { public Text text; public Vector3 world; public float age; public AttackRecord source; }

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
    Text effectText, metricsText;
    FloatingText castToast;

    readonly HashSet<Enemy> previewTargets = new HashSet<Enemy>();
    public int PreviewTargetCount => previewTargets.Count;

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
        RefreshPreview();
        RefreshBars();
        RefreshSlots(pointer);
        RefreshBoardLabels();
        RefreshEnemyLabels();
        RefreshPopups();
    }

    // ---- Input ----

    void HandleDrag(bool hasMouse, bool pressed, bool released, Vector2 pointer)
    {
        bool inputAllowed = gm.IsPlaying;
        if (!inputAllowed || !hasMouse) { dragSlot = -1; return; }

        if (dragSlot < 0)
        {
            if (!pressed) return;
            for (int i = 0; i < slots.Length; i++)
                if (gm.GetCard(i) != null && Contains(slots[i].rect, pointer)) { dragSlot = i; break; }
            return;
        }

        if (gm.GetCard(dragSlot) == null) { dragSlot = -1; return; }

        if (released)
        {
            if (OverPlayfield(pointer)) gm.UseCard(dragSlot); // Outside the table: cancel without consuming.
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
            : "PAUSED\nRestart this run?";
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

    void RefreshPreview()
    {
        foreach (var e in previewTargets) if (e) e.PreviewTarget = false;
        previewTargets.Clear();
        var card = gm.GetCard(dragSlot);
        if (!gm.IsPlaying || card == null) return;
        foreach (var e in EquipmentResolver.Targets(gm)) {
            if(e.Number!=card.number) continue;
            previewTargets.Add(e);
            e.PreviewTarget = true;
        }
    }
    public void ShowCast(SkillCast cast,bool lucky)
    {
        if(castToast!=null) {
            if(castToast.text) Destroy(castToast.text.gameObject);
            popups.Remove(castToast); castToast=null;
        }
        string result=cast.Quality<0 ? cast.Card.Name+" short circuit" : cast.Quality==0 ? "Misfire" : cast.Card.SkillName+" · "+cast.Plan.Summary(cast.Card.equipment);
        Popup(gm.board.TransformPoint(new Vector3(0,0,2)),result+"  Q="+cast.Quality,cast.Quality<0 ? Danger : cast.Quality==0 ? Dim : Orange);
        castToast=popups[popups.Count-1];
    }
    public void ShowMultiKill(AttackRecord source)
    {
        foreach(var p in popups) if(ReferenceEquals(p.source,source)) {
            p.text.text=source.Label+" ×"+source.Kills+" MULTIKILL";p.age=0;return;
        }
        Popup(gm.board.TransformPoint(new Vector3(0,0,1)),source.Label+" ×"+source.Kills+" MULTIKILL",Gold);
        popups[popups.Count-1].source=source;
    }
    string ElementStatus(Card.Equipment type)
    {
        string name=type==Card.Equipment.Arc?"Arc":type==Card.Equipment.Spark?"Spark":"Shockwave";
        float left=gm.ShortCircuitRemaining(type);
        var groups=new List<string>();
        for(int number=1;number<=6;number++) if(gm.HeldCount(type,number)>0) groups.Add("#"+number+"×"+gm.HeldCount(type,number));
        return name+" ["+(groups.Count==0?"none":string.Join(", ",groups))+"]"+(left>0?" [OFF "+left.ToString("0.0")+"s]":"");
    }
    void RefreshBars()
    {
        scoreText.text=gm.Score.ToString("N0"); livesText.text=gm.Lives+" / "+gm.maxLives; timeText.text=FormatTime(gm.ActiveTime);
        currentText.text=gm.CurrentNumber+"  Next "+gm.LuckyRemaining.ToString("0.0")+"s"; currentText.fontSize=25;
        var cast=gm.LastCast;
        if(cast==null) previewText.text="Active skill: not used";
        else if(cast.Quality<0) previewText.text=cast.Card.Name+" Q="+cast.Quality+" · OFF "+gm.ShortCircuitRemaining(cast.Card.equipment).ToString("0.0")+"s";
        else if(cast.Quality==0) previewText.text=cast.Card.SkillName+" Q=0 · Misfire";
        else previewText.text=cast.Card.SkillName+" Q="+cast.Quality+" · "+(cast.Card.equipment==Card.Equipment.Spark ? cast.Plan.Summary(cast.Card.equipment) : "Wave "+cast.WavesFired+"/"+cast.Plan.waves)+" · Kills "+cast.Kills;
        previewText.fontSize=16; previewText.color=cast!=null && cast.Quality<0?Danger:Orange;
        effectText.text=ElementStatus(Card.Equipment.Arc)+" · "+ElementStatus(Card.Equipment.Spark)+" · "+ElementStatus(Card.Equipment.Shockwave);
        effectText.fontSize=19;
        energyText.text=gm.IsHandFull?"Equipment full":"Refill "+gm.RefillRemaining.ToString("0.0")+"s";
        energyFill.anchorMax=new Vector2(gm.IsHandFull?0f:Mathf.Clamp01(1-gm.RefillRemaining/(gm.luck.refillInterval+gm.luck.playRefillDelay)),1f);
        if(metricsText) metricsText.text="RUN STATS\nPlays: "+gm.Metrics.plays+" · Average hand: "+gm.Metrics.AverageHand.ToString("0.00")+"\nLow gear (0-1 cards): "+gm.Metrics.lowEquipmentSeconds.ToString("0.0")+"s\nDeath cause: "+gm.Metrics.deathCause;
    }
    void RefreshSlots(Vector2 pointer)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            var c = gm.GetCard(i);
            bool reserved = i == dragSlot;
            s.edge.color = reserved ? Blue : c != null && c.number == gm.CurrentNumber ? Gold : PanelEdge;
            s.card.color = reserved ? Blue : TextColor;
            s.card.fontSize = c != null && c.equipment == Card.Equipment.Shockwave ? 23 : 30;
            s.card.rectTransform.anchoredPosition = new Vector2(0, 12);
            s.card.text = c == null ? "Empty" : CardText(c);
            s.result.text = c == null ? "Awaiting refill" : c.Description(gm.luck);
            s.cooldown.text = c == null ? "" : "#" + c.number + " stack: " + gm.HeldCount(c.equipment,c.number);
            s.cooldown.color = c != null && c.number == gm.CurrentNumber ? Gold : Dim;
            s.result.fontSize = 15;
            s.result.rectTransform.sizeDelta = new Vector2(H.slotWidth - 6, 58);
            s.result.rectTransform.anchoredPosition = new Vector2(0, -34);
        }

        ghost.gameObject.SetActive(dragSlot >= 0);
        if (dragSlot < 0) return;
        var card = gm.GetCard(dragSlot);
        Vector2 pos=pointer/canvas.scaleFactor;
        ghost.anchoredPosition=new Vector2(Mathf.Clamp(pos.x+265,260,root.rect.width-260),Mathf.Clamp(pos.y,165,root.rect.height-165));
        ghostCard.text = CardText(card);
        ghostCard.fontSize = 27;
        bool lucky = card.number == gm.CurrentNumber;
        var weights = gm.luck.Weights(lucky);
        int low0=EquipmentResolver.ActiveTargets(gm,gm.luck.Plan(card.equipment,1)).Count;
        int low1=EquipmentResolver.ActiveTargets(gm,gm.luck.Plan(card.equipment,19)).Count;
        int mid0=EquipmentResolver.ActiveTargets(gm,gm.luck.Plan(card.equipment,20)).Count;
        int mid1=EquipmentResolver.ActiveTargets(gm,gm.luck.Plan(card.equipment,59)).Count;
        int after=gm.HeldCount(card.equipment,card.number)-1;
        float before=gm.IsHandFull?gm.luck.refillInterval:gm.RefillRemaining;
        ghostResult.text=(lucky?"Lucky match":"Normal cast")+" · Backlash "+weights.Chance(0).ToString("P0")+" · High power "+weights.Chance(3).ToString("P0")+
            "\nPassive #"+card.number+" matches: "+previewTargets.Count+" foes (gold)"+
            "\nLow: nearest 2-3, any number (now "+low0+"-"+low1+" foes)"+
            "\nMid: nearest 4-8, any number (now "+mid0+"-"+mid1+" foes)"+
            "\nHigh: all foes at cast time (now "+EquipmentResolver.Targets(gm).Count+" foes)"+
            "\nAfter cast: #"+card.number+" "+card.Name+" "+gm.HeldCount(card.equipment,card.number)+" → "+after+" cards"+
            "\n"+(after==0 ? "#"+card.number+" enchantment lost" : gm.EnchantmentSummary(card.equipment,after))+
            "\nNext refill: "+before.ToString("0.0")+" → "+gm.RefillAfterPlay.ToString("0.0")+"s";
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
        foreach (var e in EquipmentResolver.Targets(gm)) {
            alive.Add(e);
            if (!labels.TryGetValue(e, out var l)) labels[e] = l = MakeEnemyLabel();
            var up = e.transform.up * e.transform.lossyScale.y;
            var forward = e.transform.parent.forward * e.Radius;
            l.number.rectTransform.anchoredPosition = ToCanvas(e.transform.position + up);
            Vector2 barPosition = ToCanvas(e.transform.position - up - forward) + H.refreshEnemyLabels.belowOffset;
            l.healthBack.anchoredPosition = barPosition;
            l.healthFill.sizeDelta = new Vector2(56 * Mathf.Clamp01(e.hp / (float)e.maxHp), 6);
            l.healthFill.GetComponent<Image>().color = e.PreviewTarget ? Gold : Green;
            l.below.rectTransform.anchoredPosition = barPosition + new Vector2(0, -12);
            l.number.text = e.Number.ToString();
            l.number.fontSize = 30;
            l.number.color = e.PreviewTarget ? Gold : TextColor;
            l.below.text = e.hp + "/" + e.maxHp;
            l.status.rectTransform.anchoredPosition=ToCanvas(e.transform.position+up+forward)+new Vector2(0,12);
            l.status.text=(e.IsBurning?"Burning ":"")+(e.IsStunned?"Stunned":e.PassiveImmunityRemaining>0?"Stun immune":"");
        }
        gone.Clear();
        foreach (var kv in labels) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var e in gone) {
            var l = labels[e];
            Destroy(l.number.gameObject); Destroy(l.below.gameObject); Destroy(l.status.gameObject); Destroy(l.healthBack.gameObject);
            labels.Remove(e);
        }
    }
    EnemyLabel MakeEnemyLabel()
    {
        var l = new EnemyLabel {
            number = Free(MakeText(labelLayer, "", 30, TextColor, TextAnchor.MiddleCenter, Vector2.zero, H.makeEnemyLabel.numberSize)),
            below = Free(MakeText(labelLayer, "", 12, Dim, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(80, 18))),
            status = Free(MakeText(labelLayer,"",12,Orange,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(90,18))),
            healthBack = Box(labelLayer, "HealthBar", Vector2.zero, Vector2.zero, new Vector2(60, 10), PanelEdge)
        };
        l.healthFill = Box(l.healthBack, "Health", new Vector2(0, .5f), new Vector2(2, 0), new Vector2(56, 6), Green);
        l.healthFill.pivot = new Vector2(0, .5f);
        l.number.fontStyle = FontStyle.Bold;
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
        Bold(Anchored(MakeText(root, "LUCK NUMBER", H.build.titleFontSize, TextColor, TextAnchor.UpperLeft, Vector2.zero, H.build.titleSize), H.build.titleAnchor, H.build.titlePosition));
        effectText = Anchored(MakeText(root, "", H.build.effectsFontSize, Orange, TextAnchor.UpperRight, Vector2.zero, new Vector2(920,35)), H.build.effectsAnchor, H.build.effectsPosition);

        // Top: score, lives, active time.
        topBar = Panel(root, "TopBar", H.build.topBarAnchor, H.build.topBarPosition, H.build.topBarSize, PanelEdge);
        scoreText = Stat(topBar, "SCORE", H.build.scoreX, H.scoreStat);
        livesText = Stat(topBar, "LIVES", H.build.livesX, H.livesStat);
        timeText = Stat(topBar, "ACTIVE TIME", H.build.timeX, H.timeStat);

        SidePanel(H.build.leftHelpAnchor,H.build.leftHelpPosition,H.leftHelp,TextAnchor.UpperLeft,new[] {
            ("HOLD = BALL ENCHANTMENTS",TextColor,new[] {("Any valid hit triggers held enchantments",Green),("Arc: 4 damage to matching foes in 3m",Dim),("Spark: matching foes burn for 3s",Dim),("Shockwave: 0.35s stun on matching foes",Dim),("Stacks combine by element + number",Dim)}),
            ("CONTROLS",Yellow,new[] {("A / Left: left flipper; D / Right: right",Dim),("R: pause / restart",Dim),("Enemy number = passive target group",Dim)})
        });
        SidePanel(H.build.rightHelpAnchor,H.build.rightHelpPosition,H.rightHelp,TextAnchor.UpperLeft,new[] {
            ("CAST = RANDOM SKILL POWER",TextColor,new[] {("Arc: Thunderstorm / Spark: Inferno",Blue),("Shockwave: Impact",Blue),("Low: small groups, brief effects",Dim),("Mid: more foes, waves or duration",Dim),("High: all current foes, stronger effects",Orange),("Active skills ignore enemy numbers",Dim)}),
            ("EVERY CAST HAS A COST",Gold,new[] {("Refill: 8s base, +4s for every cast",Gold),("Play 3: refill at 20 / 28 / 36 seconds",Dim),("Q < 0: that element is offline for 2-4s",Danger),("Q = 0: misfire. All casts pay full cost.",Dim)})
        });
        metricsText=Anchored(MakeText(root,"",17,Dim,TextAnchor.UpperLeft,Vector2.zero,new Vector2(390,140)),new Vector2(0,1),new Vector2(70,-650));

        // Board callouts.
        spawnLabel = Free(MakeText(labelLayer, "ENEMY SPAWN AREA", H.build.spawnLabelFontSize, Dim, TextAnchor.MiddleCenter, Vector2.zero, H.build.spawnLabelSize));
        dangerLabel = Free(MakeText(labelLayer, "DANGER LINE", H.build.dangerLabelFontSize, Danger, TextAnchor.MiddleRight, Vector2.zero, H.build.dangerLabelSize));
        dangerLabel.rectTransform.pivot = H.build.dangerLabelPivot;

        // Bottom: lucky number, current number, preview, energy bar.
        float handWidth = slots.Length * H.slotWidth + (slots.Length - 1) * H.slotSpacing;
        bottomBar = Panel(root, "BottomBar", H.build.bottomBarAnchor, H.build.bottomBarPosition, new Vector2(handWidth + H.build.bottomBarWidthPadding, H.build.bottomBarHeight), PanelEdge);
        float left = -(handWidth + H.build.bottomContentWidthPadding) / 2f + H.build.bottomContentInset;
        LeftText(MakeText(bottomBar, "LUCKY", H.build.luckyFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(left, H.build.luckyY), H.build.luckyLabelSize));
        currentText = Bold(LeftText(MakeText(bottomBar, "", H.build.currentFontSize, Green, TextAnchor.MiddleLeft, new Vector2(left + H.build.currentX, H.build.currentY), H.build.currentLabelSize)));
        previewText = LeftText(MakeText(bottomBar, "", H.build.previewFontSize, Blue, TextAnchor.MiddleLeft, new Vector2(left + H.build.previewX, H.build.previewY), H.build.previewLabelSize));
        energyText = LeftText(MakeText(bottomBar, "", H.build.energyFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(left, H.build.energyY), H.build.energyLabelSize));
        var barBack = Box(bottomBar, "EnergyBar", H.build.energyBarAnchor, new Vector2(left + H.build.energyBarX + H.build.energyBarExtraX, H.build.energyBarY), H.build.energyBarSize, H.energyBackground);
        energyFill = Box(barBack, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Green);
        energyFill.anchorMin = Vector2.zero; energyFill.anchorMax = H.build.energyFillAnchor;
        energyFill.offsetMin = energyFill.offsetMax = Vector2.zero;

        // Five fixed slots. Compaction is visual only; cooldown follows Card identity.
        handArea = Box(root, "Hand", H.build.handAnchor, new Vector2(0f, H.slotBottom + H.slotHeight / 2f), new Vector2(handWidth + H.build.handWidthPadding, H.slotHeight + H.build.handHeightPadding), Color.clear);
        for (int i = 0; i < slots.Length; i++)
        {
            float x = -handWidth / 2f + H.slotWidth / 2f + i * (H.slotWidth + H.slotSpacing);
            var s = new SlotView();
            s.rect = Panel(handArea, "Slot" + (i + 1), H.build.slotAnchor, new Vector2(x, 0f), new Vector2(H.slotWidth, H.slotHeight), PanelEdge);
            s.edge = s.rect.GetComponent<Image>();
            s.cooldown = MakeText(s.rect, "", H.build.slotCaptionFontSize, Dim, TextAnchor.MiddleCenter, H.build.slotCaptionPosition, new Vector2(H.slotWidth, H.build.slotCaptionHeight));
            s.card = Bold(MakeText(s.rect, "", H.refreshSlots.numberFontSize, TextColor, TextAnchor.MiddleCenter, H.build.slotCardPosition, new Vector2(H.slotWidth, H.build.slotCardHeight)));
            s.result = MakeText(s.rect, "", H.build.slotResultFontSize, Dim, TextAnchor.MiddleCenter, H.build.slotResultPosition, new Vector2(H.slotWidth, H.build.slotResultHeight));
            slots[i] = s;
        }

        // Dragged card ghost.
        ghost = Panel(root, "Ghost", Vector2.zero, Vector2.zero, new Vector2(500, 310), Blue);
        ghostCard = Bold(MakeText(ghost, "", H.refreshSlots.ghostNumberFontSize, Blue, TextAnchor.MiddleCenter, new Vector2(0, 128), new Vector2(480, 40)));
        ghostResult = MakeText(ghost, "", 17, Blue, TextAnchor.MiddleCenter, new Vector2(0, -8), new Vector2(475, 240));
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
            Bold(TopLeft(MakeText(rt, title, Mathf.Min(style.titleFontSize, 21), color, align, Vector2.zero, style.titleSize), y));
            y -= style.titleSpacing;
            foreach (var (text, c) in lines)
            {
                TopLeft(MakeText(rt, text, Mathf.Min(style.lineFontSize, 17), c, align, Vector2.zero, style.lineSize), y);
                y -= style.lineSpacing;
            }
            y -= style.sectionSpacing;
        }
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

    string CardText(Card c) => c.ToString();

    static string FormatTime(float t) => Mathf.FloorToInt(t / 60f).ToString("00") + ":" + Mathf.FloorToInt(t % 60f).ToString("00");

}
