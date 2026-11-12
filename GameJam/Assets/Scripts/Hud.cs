using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Click selection is independent of equipment ownership until the batch is accepted.
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

    readonly CardSelection selection=new CardSelection();
    RectTransform castButton,dicePanel;
    Text castButtonText,diceFaces,diceCards,diceResult;
    public IList<Card> SelectedCards=>selection.Cards;
    public bool SelectCard(int slot)=>gm.CanSubmit && selection.Toggle(gm.GetCard(slot));
    public bool SubmitSelection()=>gm.UseCards(new List<Card>(selection.Cards));
    void OnHandChanged()=>selection.KeepOnly(gm.HandSnapshot);
    void OnBatchSubmitted(DiceBatch batch)=>selection.Clear();
    Text effectText, metricsText, rechargeHintText;
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
        gm.HandChanged+=OnHandChanged;
        gm.BatchSubmitted+=OnBatchSubmitted;
        Build();
    }

    void OnDestroy()
    {
        if (!gm) return;
        gm.HandChanged-=OnHandChanged;
        gm.BatchSubmitted-=OnBatchSubmitted;
    }

    void Update()
    {
        var mouse = Mouse.current;
        Vector2 pointer = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        HandleSelection(mouse!=null && mouse.leftButton.wasPressedThisFrame,pointer);
        HandleDialog(mouse != null && mouse.leftButton.wasPressedThisFrame, pointer);
        RefreshPreview();
        RefreshBars();
        RefreshSlots(pointer);
        RefreshBoardLabels();
        RefreshEnemyLabels();
        RefreshPopups();
        RefreshDice();
    }

    // ---- Input ----

    void HandleSelection(bool pressed,Vector2 pointer)
    {
        if(!pressed || !gm.CanSubmit)return;
        if(Contains(castButton,pointer)){if(selection.Count>0)SubmitSelection();return;}
        for(int i=0;i<slots.Length;i++)if(Contains(slots[i].rect,pointer)){SelectCard(i);return;}
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
        foreach(var e in previewTargets)if(e)e.PreviewTarget=false;
        previewTargets.Clear();
        if(selection.Count==0)return;
        foreach(var card in selection.Cards) {
            int count=0;
            foreach(var e in EquipmentResolver.Targets(gm)) {
                if(!e.CanReceive(card.equipment))continue;
                if(selection.Count==1 && count>=gm.luck.standardTargets)break;
                count++;previewTargets.Add(e);e.PreviewTarget=true;
            }
        }
    }
    public void ShowBatch(DiceBatch batch)
    {
        if(castToast!=null){if(castToast.text)Destroy(castToast.text.gameObject);popups.Remove(castToast);}
        Popup(gm.board.TransformPoint(new Vector3(0,0,2)),DiceRules.Label(batch.Tier)+" · "+batch.CoverageCount+" targets",batch.Tier==CastTier.Base?Dim:batch.Tier==CastTier.Jackpot?Gold:Blue);
        castToast=popups[popups.Count-1];
    }
    void RefreshDice()
    {
        var batch=gm.LastBatch;
        dicePanel.gameObject.SetActive(batch!=null && (!batch.Revealed || gm.CombatTime-batch.RevealAt<2.5));
        if(batch==null)return;
        var names=new List<string>();foreach(var c in batch.Cards)names.Add(c.ToString());
        diceCards.text=string.Join("   |   ",names);
        diceCards.color=batch.Revealed && batch.Tier!=CastTier.Base?Gold:TextColor;
        var faces=new List<string>();
        foreach(int value in batch.Dice)faces.Add(!batch.Revealed?"[ ? ]":value==batch.Number?"<color=#FFD56A>[ "+value+" ]</color>":"[ "+value+" ]");
        diceFaces.text=string.Join("   ",faces);
        diceResult.text=!batch.Revealed?"Rolling to match #"+batch.Number:DiceRules.Label(batch.Tier)+" · "+batch.CoverageCount+" targets · "+batch.Kills+" kills";
        diceResult.color=batch.Revealed && batch.Tier==CastTier.Jackpot?Gold:TextColor;
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
    {return (type==Card.Equipment.Arc?"Arc":type==Card.Equipment.Spark?"Spark":"Shockwave")+" ×"+gm.HeldCount(type);}
    void RefreshBars()
    {
        scoreText.text=gm.Score.ToString("N0");livesText.text=gm.Lives+" / "+gm.maxLives;timeText.text=FormatTime(gm.ActiveTime);
        currentText.fontSize=22;currentText.text=gm.PendingBatch!=null?"Rolling "+gm.PendingBatch.Dice.Count+" dice":selection.Count==0?"Select cards":"Match #"+selection.Number+" · "+selection.Count+" selected";
        var batch=gm.LastBatch;
        previewText.text=batch==null?"No cast yet":!batch.Revealed?"Revealing dice...":DiceRules.Label(batch.Tier)+" · "+batch.CoverageCount+" targets · "+batch.Kills+" kills";
        previewText.fontSize=17;previewText.color=batch!=null && batch.Revealed && batch.Tier==CastTier.Jackpot?Gold:Blue;
        effectText.text="BALLS "+gm.BallCount+" · POWER "+gm.EmpoweredHits+" · "+ElementStatus(Card.Equipment.Arc)+" · "+ElementStatus(Card.Equipment.Spark)+" · "+ElementStatus(Card.Equipment.Shockwave);
        effectText.fontSize=19;
        energyText.text=gm.IsHandFull?"Equipment full":"Recharge: "+gm.RechargeProgress.ToString("0.#")+" / "+gm.RechargeRequired.ToString("0.#");
        rechargeHintText.text="Per card cast: required charge +"+gm.luck.rechargeCastCost.ToString("0.#");
        energyFill.anchorMax=new Vector2(gm.RechargeRequired<=0?0:Mathf.Clamp01((float)(gm.RechargeProgress/gm.RechargeRequired)),1);
        metricsText.text="RUN STATS\nBatches: "+gm.Metrics.batches+" · Cards: "+gm.Metrics.plays+" · Skills: "+gm.Metrics.skillReleases+" · Matches: "+gm.Metrics.diceMatches+"\nMean hand: "+gm.Metrics.AverageHand.ToString("0.00")+" · Low gear: "+gm.Metrics.lowEquipmentSeconds.ToString("0.0")+"s\nBase / Hit / Full / Jackpot: "+gm.Metrics.baseBatches+" / "+gm.Metrics.standardBatches+" / "+gm.Metrics.fullFieldBatches+" / "+gm.Metrics.jackpotBatches;
        castButtonText.text="Cast "+selection.Count+" · Roll "+selection.Count+(selection.Count==1?" die":" dice");
        castButton.GetComponent<Image>().color=selection.Count>0 && gm.CanSubmit?ButtonColor:PanelEdge;
        castButtonText.color=selection.Count>0 && gm.CanSubmit?TextColor:Dim;
    }
    void RefreshSlots(Vector2 pointer)
    {
        for(int i=0;i<slots.Length;i++) {
            var v=slots[i];var c=gm.GetCard(i);bool picked=c!=null && selection.Contains(c);
            bool blocked=c!=null && selection.Number.HasValue && c.number!=selection.Number.Value;
            v.edge.color=picked?Gold:blocked?PanelEdge:c==null?PanelEdge:Card.ColorFor(c.equipment);
            v.card.color=blocked?Dim:c==null?Dim:Card.ColorFor(c.equipment);
            v.card.fontSize=c!=null && c.equipment==Card.Equipment.Shockwave?22:29;
            v.card.rectTransform.anchoredPosition=new Vector2(0,12);
            v.card.text=c==null?"Empty":c.ToString();
            v.cooldown.text=c==null?"":(picked?"SELECTED · ":"")+Card.IconFor(c.equipment)+(gm.NumberCount(c.number)>=2?"  PAIR":"");
            v.cooldown.color=picked?Gold:blocked?Dim:TextColor;
            v.result.text=c==null?"Awaiting charge":c.Description(gm.luck);v.result.fontSize=14;v.result.color=blocked?Dim:TextColor;
            v.result.rectTransform.sizeDelta=new Vector2(H.slotWidth-6,58);v.result.rectTransform.anchoredPosition=new Vector2(0,-34);
        }
        ghost.gameObject.SetActive(selection.Count>0);
        if(selection.Count==0)return;
        ghostCard.text=selection.Count+" selected · #"+selection.Number;
        var lines=new List<string>();
        lines.Add("Upgrade chance: "+DiceRules.HitChance(selection.Count).ToString("P1"));
        lines.Add(selection.Count==1?"Base: 1 foe · Match: Hit (up to 4 foes)":"Base: 1 foe/type · 1 match: Full · 2+: Jackpot");
        lines.Add("Currently eligible: "+previewTargets.Count+" unique foes · Power +"+selection.Count);
        for(int type=0;type<3;type++) {
            int spent=0;foreach(var c in selection.Cards)if((int)c.equipment==type)spent++;
            if(spent==0)continue;
            var e=(Card.Equipment)type;int after=gm.HeldCount(e)-spent;
            lines.Add(e+": held "+gm.HeldCount(e)+" → "+after);
            lines.Add(gm.EnchantmentSummary(e,after));
        }
        lines.Add("Charge required: "+(gm.IsHandFull?gm.luck.rechargeBaseCost:gm.RechargeRequired).ToString("0.#")+" → "+gm.RechargeAfterBatch(selection.Count).ToString("0.#"));
        ghostResult.text=string.Join("\n",lines);ghostResult.fontSize=15;
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
            l.number.text = Card.IconFor(e.Element);
            l.number.fontSize = 30;
            l.number.color = e.PreviewTarget ? Gold : Card.ColorFor(e.Element);
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
            ("HOLD = BALL ENCHANTMENTS",TextColor,new[] {("Held cards combine by element",Green),("Enemy colors show their immunity",Dim),("Arc / Fire / Shock foes resist their own type",Dim),("Base ball hit: 4 neutral damage",Dim)}),
            ("PLAY ONE OR COMBINE",Gold,new[] {("Play one card or combine cards",Dim),("of the same number. Roll one die per card.",Dim),("A match upgrades them all.",Gold),("A / D: flippers · R: pause / restart",Dim)})
        });
        SidePanel(H.build.rightHelpAnchor,H.build.rightHelpPosition,H.rightHelp,TextAnchor.UpperLeft,new[] {
            ("DICE RESULTS",TextColor,new[] {("No matching die: Base (skill guaranteed)",Dim),("One card + match: Hit (local)",Blue),("2+ cards, one match: Full Field",Blue),("2+ matching dice: Jackpot",Gold),("Repeated elements add waves or burn time",Dim)}),
            ("RECHARGE",Gold,new[] {("100 base +50 required per card spent",Gold),("10/s natural · contacts / first burn tick +2.5",Dim),("Combat budget 6/s · minimum +0.25/event",Dim)})
        });
        metricsText=Anchored(MakeText(root,"",16,Dim,TextAnchor.UpperLeft,Vector2.zero,new Vector2(400,150)),new Vector2(0,1),new Vector2(70,-830));

        // Board callouts.
        spawnLabel = Free(MakeText(labelLayer, "ENEMY SPAWN AREA", H.build.spawnLabelFontSize, Dim, TextAnchor.MiddleCenter, Vector2.zero, H.build.spawnLabelSize));
        dangerLabel = Free(MakeText(labelLayer, "DANGER LINE", H.build.dangerLabelFontSize, Danger, TextAnchor.MiddleRight, Vector2.zero, H.build.dangerLabelSize));
        dangerLabel.rectTransform.pivot = H.build.dangerLabelPivot;

        // Bottom: lucky number, current number, preview, energy bar.
        float handWidth = slots.Length * H.slotWidth + (slots.Length - 1) * H.slotSpacing;
        bottomBar = Panel(root, "BottomBar", H.build.bottomBarAnchor, H.build.bottomBarPosition, new Vector2(handWidth + H.build.bottomBarWidthPadding, H.build.bottomBarHeight), PanelEdge);
        float left = -(handWidth + H.build.bottomContentWidthPadding) / 2f + H.build.bottomContentInset;
        LeftText(MakeText(bottomBar, "CAST", H.build.luckyFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(left, H.build.luckyY), H.build.luckyLabelSize));
        currentText = Bold(LeftText(MakeText(bottomBar, "", H.build.currentFontSize, Green, TextAnchor.MiddleLeft, new Vector2(left + H.build.currentX, H.build.currentY), H.build.currentLabelSize)));
        previewText = LeftText(MakeText(bottomBar, "", H.build.previewFontSize, Blue, TextAnchor.MiddleLeft, new Vector2(left + H.build.previewX, H.build.previewY), H.build.previewLabelSize));
        energyText = LeftText(MakeText(bottomBar, "", H.build.energyFontSize, Dim, TextAnchor.MiddleLeft, new Vector2(left, H.build.energyY), H.build.energyLabelSize));
        rechargeHintText = LeftText(MakeText(bottomBar,"",15,Dim,TextAnchor.MiddleLeft,new Vector2(left+230,-12),new Vector2(450,24)));
        var barBack = Box(bottomBar, "EnergyBar", H.build.energyBarAnchor, new Vector2(left + H.build.energyBarX + H.build.energyBarExtraX, -32), H.build.energyBarSize, H.energyBackground);
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

        castButton=Box(root,"CastBatch",new Vector2(.5f,0),new Vector2(0,16),new Vector2(340,32),ButtonColor);
        castButtonText=MakeText(castButton,"Cast 0 · Roll 0 dice",20,Dim,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(330,30));
        ghost=Panel(root,"GroupPreview",new Vector2(1,1),new Vector2(-280,-800),new Vector2(440,275),Blue);
        ghostCard=Bold(MakeText(ghost,"",23,Blue,TextAnchor.MiddleCenter,new Vector2(0,115),new Vector2(425,35)));
        ghostResult=MakeText(ghost,"",15,TextColor,TextAnchor.MiddleCenter,new Vector2(0,-10),new Vector2(425,230));ghost.gameObject.SetActive(false);
        dicePanel=Panel(root,"DiceReveal",new Vector2(.5f,1),new Vector2(0,-265),new Vector2(700,140),Gold);
        diceCards=MakeText(dicePanel,"",17,TextColor,TextAnchor.MiddleCenter,new Vector2(0,45),new Vector2(680,28));
        diceFaces=Bold(MakeText(dicePanel,"",31,TextColor,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(680,50)));
        diceResult=MakeText(dicePanel,"",21,Gold,TextAnchor.MiddleCenter,new Vector2(0,-45),new Vector2(680,30));dicePanel.gameObject.SetActive(false);

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

