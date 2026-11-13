using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Group drag and number-key selection do not consume equipment until submission.
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

    class SlotView { public RectTransform rect; public Image edge; public Text card; }
    class EnemyLabel { public Text number, below; public RectTransform healthBack, healthFill; }
    class FloatingText { public Text text; public Vector3 world; public float age; }

    GameManager gm;
    Camera cam;
    Canvas canvas;
    Font font;
    RectTransform root, labelLayer, popupLayer, topBar, bottomBar, handArea, energyFill, dialog;
    Text scoreText, livesText, timeText, energyText, countdownText, dialogText;
    readonly RectTransform[] buttons = new RectTransform[2];
    readonly Text[] buttonTexts = new Text[2];
    SlotView[] slots;
    readonly Dictionary<Enemy, EnemyLabel> labels = new Dictionary<Enemy, EnemyLabel>();
    readonly HashSet<Enemy> alive = new HashSet<Enemy>();
    readonly List<Enemy> gone = new List<Enemy>();
    readonly List<FloatingText> popups = new List<FloatingText>();

    readonly CardSelection selection=new CardSelection();
    readonly GroupKeyInput groupKeys=new GroupKeyInput();
    bool pointerHeld,dragging,keyboardSelection;
    Vector2 dragStartScreen,dragStartLocal,dragOffset;
    Vector2[] slotHomes;
    RectTransform keyboardHint;
    Text keyboardHintText;
    public IList<Card> SelectedCards=>selection.Cards;
    public bool SelectCard(int slot)=>gm.CanSubmit && selection.ToggleGroup(gm.GetCard(slot),gm.HandSnapshot);
    public bool SubmitSelection(){selection.KeepOnly(gm.HandSnapshot);return gm.UseCards(new List<Card>(selection.Cards));}
    void OnHandChanged()=>selection.KeepOnly(gm.HandSnapshot);
    void OnBatchSubmitted(DiceBatch batch){selection.Clear();ResetPointer();groupKeys.Reset();keyboardSelection=false;}
    Text comboText;

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
        HandleSelection(mouse,pointer);
        HandleDialog(mouse != null && mouse.leftButton.wasPressedThisFrame, pointer);
        RefreshPreview();
        RefreshBars();
        RefreshSlots(pointer);
        RefreshEnemyLabels();
        RefreshPopups();
    }

    // ---- Input ----

    void ResetPointer(){pointerHeld=false;dragging=false;dragOffset=Vector2.zero;}
    void OnApplicationFocus(bool focused){if(!focused){ResetPointer();groupKeys.Reset();selection.Clear();keyboardSelection=false;}}
    void HandleSelection(Mouse mouse,Vector2 pointer)
    {
        var debugPanel=gm.GetComponent<DifficultyDebugPanel>();
        if(!gm.CanSubmit||(debugPanel&&debugPanel.Visible)){if(pointerHeld)selection.Clear();ResetPointer();groupKeys.Reset();return;}
        double now=Time.unscaledTimeAsDouble;
        if(!pointerHeld){
            if(groupKeys.Advance(now,selection.Number)==GroupKeyAction.Cancel){selection.Clear();keyboardSelection=false;}
            var kb=Keyboard.current;
            if(kb!=null)for(int n=1;n<=3;n++){
                bool pressed=n==1?(kb.digit1Key.wasPressedThisFrame||kb.numpad1Key.wasPressedThisFrame):n==2?(kb.digit2Key.wasPressedThisFrame||kb.numpad2Key.wasPressedThisFrame):(kb.digit3Key.wasPressedThisFrame||kb.numpad3Key.wasPressedThisFrame);
                if(!pressed)continue;
                var action=groupKeys.Press(n,selection.Number,now);
                if(action==GroupKeyAction.Select){keyboardSelection=selection.SelectNumber(n,gm.HandSnapshot);if(!keyboardSelection)groupKeys.Reset();}
                else if(action==GroupKeyAction.Cast)SubmitSelection();
                break;
            }
        }
        if(mouse==null){ResetPointer();return;}
        if(mouse.leftButton.wasPressedThisFrame){
            for(int i=0;i<slots.Length;i++)if(Contains(slots[i].rect,pointer)&&gm.GetCard(i)!=null){
                selection.SelectNumber(gm.GetCard(i).number,gm.HandSnapshot);groupKeys.Reset();keyboardSelection=false;
                pointerHeld=true;dragging=false;dragStartScreen=pointer;dragOffset=Vector2.zero;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(handArea,pointer,null,out dragStartLocal);break;
            }
        }
        if(!pointerHeld)return;
        if(mouse.leftButton.isPressed||mouse.leftButton.wasReleasedThisFrame){
            if((pointer-dragStartScreen).sqrMagnitude>=64)dragging=true;
            if(dragging&&RectTransformUtility.ScreenPointToLocalPointInRectangle(handArea,pointer,null,out var local))dragOffset=local-dragStartLocal;
        }
        if(mouse.leftButton.wasReleasedThisFrame){
            bool cast=dragging&&!Contains(handArea,pointer);ResetPointer();
            if(cast)SubmitSelection();selection.Clear();keyboardSelection=false;
        }else if(!mouse.leftButton.isPressed){ResetPointer();selection.Clear();}
    }

    void HandleDialog(bool pressed, Vector2 pointer)
    {
        bool over = gm.IsGameOver, pause = gm.Paused && !over;
        dialog.gameObject.SetActive(over || pause);
        countdownText.gameObject.SetActive((gm.IsWaiting || gm.IsRelaunching) && !gm.Paused);
        countdownText.text = (gm.IsRelaunching?"<size=24>RELAUNCH</size>\n":"")+Mathf.CeilToInt(gm.Countdown).ToString();
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
            int copies=0;foreach(var selected in selection.Cards)if(selected.equipment==card.equipment)copies++;
            int limit=0;bool all=false;
            foreach(var tier in new[]{selection.Number==gm.LuckyNumber?CastTier.Lucky:CastTier.Normal}){
                var plan=gm.luck.Plan(card.equipment,tier,copies);all|=plan.allTargets;limit=Mathf.Max(limit,plan.targetCount);
            }
            int count=0;
            foreach(var e in EquipmentResolver.Targets(gm)) {
                if(!e.CanReceive(card.equipment))continue;
                if(!all && count>=limit)break;
                count++;previewTargets.Add(e);e.PreviewTarget=true;
            }
        }
    }
    void RefreshBars()
    {
        scoreText.text=gm.Score.ToString("N0");livesText.text=gm.Lives+" / "+gm.maxLives;timeText.text=FormatTime(gm.ActiveTime);
        comboText.text=gm.ComboHits>0?gm.ComboHits.ToString():"";
        energyText.text=gm.RechargeRequired.ToString("0.#");
        energyFill.anchorMax=new Vector2(gm.RechargeRequired<=0?0:Mathf.Clamp01((float)(gm.RechargeProgress/gm.RechargeRequired)),1);
        keyboardHint.gameObject.SetActive(keyboardSelection&&selection.Count>0&&gm.CanSubmit&&!pointerHeld);
        keyboardHintText.text=$"Double-tap {selection.Number} to cast";
    }
    void RefreshSlots(Vector2 pointer)
    {
        handArea.GetComponent<Image>().color=dragging?new Color(Blue.r,Blue.g,Blue.b,.14f):Color.clear;
        for(int i=0;i<slots.Length;i++) {
            var v=slots[i];var c=gm.GetCard(i);bool picked=c!=null && selection.Contains(c);
            v.rect.anchoredPosition=slotHomes[i]+(dragging&&picked?dragOffset:Vector2.zero);
            bool blocked=false;
            v.edge.color=picked?Gold:blocked?PanelEdge:c==null?PanelEdge:Card.ColorFor(c.equipment);
            if(gm.IsLucky(c)&&gm.CombatTime-gm.LastDrawTime<.9)v.edge.color=Color.Lerp(Card.ColorFor(c.equipment),Gold,.6f+.4f*Mathf.Sin((float)(gm.CombatTime-gm.LastDrawTime)*20));
            v.card.color=c==null?Dim:gm.IsLucky(c)?Gold:Card.ColorFor(c.equipment);
            v.card.fontSize=29;
            v.card.rectTransform.anchoredPosition=Vector2.zero;
            v.card.text=c==null?"":c.number.ToString();
        }
        if(dragging)for(int i=0;i<slots.Length;i++)if(selection.Contains(gm.GetCard(i)))slots[i].rect.SetAsLastSibling();
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
        }
        gone.Clear();
        foreach (var kv in labels) if (!alive.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var e in gone) {
            var l = labels[e];
            Destroy(l.number.gameObject); Destroy(l.below.gameObject); Destroy(l.healthBack.gameObject);
            labels.Remove(e);
        }
    }
    EnemyLabel MakeEnemyLabel()
    {
        var l = new EnemyLabel {
            number = Free(MakeText(labelLayer, "", 30, TextColor, TextAnchor.MiddleCenter, Vector2.zero, H.makeEnemyLabel.numberSize)),
            below = Free(MakeText(labelLayer, "", 12, Dim, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(80, 18))),
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

        // Top: score, lives, active time.
        topBar = Panel(root, "TopBar", H.build.topBarAnchor, H.build.topBarPosition, H.build.topBarSize, PanelEdge);
        scoreText = Stat(topBar, "SCORE", H.build.scoreX, H.scoreStat,true);
        livesText = Stat(topBar, "LIVES", H.build.livesX, H.livesStat);
        timeText = Stat(topBar, "ACTIVE TIME", H.build.timeX, H.timeStat);
        comboText=Anchored(MakeText(root,"",18,Gold,TextAnchor.UpperLeft,Vector2.zero,new Vector2(430,28)),H.build.comboAnchor,H.build.comboPosition);



        // Bottom: recharge progress.
        float handWidth = slots.Length * H.slotWidth + (slots.Length - 1) * H.slotSpacing;
        bottomBar = Panel(root, "BottomBar", H.build.bottomBarAnchor, H.build.bottomBarPosition, new Vector2(H.build.rechargePanelWidth, H.build.bottomBarHeight), PanelEdge);
        float left = -H.build.rechargePanelWidth / 2f + H.build.bottomContentInset;
        var barBack = Box(bottomBar, "EnergyBar", H.build.energyBarAnchor, new Vector2(left + H.build.energyBarX + H.build.energyBarExtraX, -32), H.build.energyBarSize, H.energyBackground);
        energyFill = Box(barBack, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Green);
        energyFill.anchorMin = Vector2.zero; energyFill.anchorMax = H.build.energyFillAnchor;
        energyFill.offsetMin = energyFill.offsetMax = Vector2.zero;
        float requiredX=barBack.anchoredPosition.x+barBack.sizeDelta.x*.5f+12f;
        energyText=LeftText(MakeText(bottomBar,"",H.build.energyFontSize,Dim,TextAnchor.MiddleLeft,new Vector2(requiredX,barBack.anchoredPosition.y),H.build.energyLabelSize));

        // Fixed sorted slots; dragging moves every selected card by the same offset.
        slotHomes=new Vector2[slots.Length];
        handArea = Box(root, "Hand", H.build.handAnchor, new Vector2(0f, H.slotBottom + H.slotHeight / 2f), new Vector2(handWidth + H.build.handWidthPadding, H.slotHeight + H.build.handHeightPadding), Color.clear);
        for (int i = 0; i < slots.Length; i++)
        {
            float x = -handWidth / 2f + H.slotWidth / 2f + i * (H.slotWidth + H.slotSpacing);
            var s = new SlotView();
            s.rect = Panel(handArea, "Slot" + (i + 1), H.build.slotAnchor, new Vector2(x, 0f), new Vector2(H.slotWidth, H.slotHeight), PanelEdge);
            s.edge = s.rect.GetComponent<Image>();
            s.card = Bold(MakeText(s.rect, "", H.refreshSlots.numberFontSize, TextColor, TextAnchor.MiddleCenter, H.build.slotCardPosition, new Vector2(H.slotWidth, H.build.slotCardHeight)));
            slots[i] = s;
            slotHomes[i]=s.rect.anchoredPosition;
        }

        keyboardHint=Box(root,"KeyboardCastHint",H.build.castHintAnchor,H.build.castHintPosition,new Vector2(340,32),PanelEdge);
        keyboardHintText=MakeText(keyboardHint,"",17,Gold,TextAnchor.MiddleCenter,Vector2.zero,new Vector2(330,30));keyboardHint.gameObject.SetActive(false);

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

    Text Stat(RectTransform bar, string caption, float x, HudStatTuning style,bool centered=false)
    {
        var align=centered?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;
        LeftText(MakeText(bar, caption, style.captionFontSize, Dim, align, new Vector2(x, style.captionY), style.captionSize));
        return Bold(LeftText(MakeText(bar, "", style.valueFontSize, TextColor, align, new Vector2(x, style.valueY), style.valueSize)));
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

