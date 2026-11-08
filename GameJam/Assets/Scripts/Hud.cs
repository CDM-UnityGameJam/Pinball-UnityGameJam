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

    // Mockup palette.
    public static readonly Color
        Background = Hex(0x090C12), PanelFill = Hex(0x111725), PanelEdge = Hex(0x2B3448), TextColor = Hex(0xE8ECF2), Dim = Hex(0x97A1B2),
        Green = Hex(0x47D6A3), Blue = Hex(0x5B8DF0), Gold = Hex(0xE9B949), Orange = Hex(0xF0A23A), Yellow = Hex(0xF2D33A),
        Danger = Hex(0xE0505E), EnemyRing = Hex(0xC9475A), EnemyFill = Hex(0x5A2632), SpecialFill = Hex(0x6E4E1C),
        BumperRing = Hex(0xA9ADB8), BumperFill = Hex(0x3A3F4C), BoardFloor = Hex(0x2D303C), ButtonColor = Hex(0x23304A), ButtonHoverColor = Hex(0x33466B);

    const float SlotWidth = 170f, SlotHeight = 115f, SlotSpacing = 18f, SlotBottom = 40f;
    const float PopupTime = 1f, PopupRise = 40f;

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
    readonly SlotView[] slots = new SlotView[GameManager.HandSize];
    readonly Dictionary<Enemy, EnemyLabel> labels = new Dictionary<Enemy, EnemyLabel>();
    readonly HashSet<Enemy> alive = new HashSet<Enemy>();
    readonly List<Enemy> gone = new List<Enemy>();
    readonly List<FloatingText> popups = new List<FloatingText>();

    int dragSlot = -1; // slot the dragged card currently owns; -1 = no drag

    // Result the dragged card would give. Display only: damage keeps using the current number until release.
    public int? PreviewNumber
    {
        get
        {
            if (dragSlot < 0) return null;
            var c = gm.GetCard(dragSlot);
            return c == null ? (int?)null : c.Value.Apply(Card.Lucky);
        }
    }

    void Awake()
    {
        Instance = this;
        gm = GameManager.Instance;
        cam = Camera.main;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        gm.SlotReplaced += OnSlotReplaced;
        Build();
    }

    void OnDestroy() { if (gm) gm.SlotReplaced -= OnSlotReplaced; }

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
            if (nearest != dragSlot) { gm.MoveCard(dragSlot, nearest); dragSlot = nearest; }
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
        return Mathf.Abs(local.x) <= 5f && local.z >= -11f && local.z <= 12f;
    }

    void HandleDialog(bool pressed, Vector2 pointer)
    {
        bool over = gm.IsGameOver, pause = gm.Paused && !over;
        dialog.gameObject.SetActive(over || pause);
        countdownText.gameObject.SetActive(gm.IsWaiting && !gm.Paused);
        countdownText.text = Mathf.CeilToInt(gm.Countdown).ToString();
        if (!over && !pause) return;

        dialogText.text = over
            ? "GAME OVER\n<size=28><color=#" + ColorUtility.ToHtmlStringRGB(Dim) + ">Score </color>" + gm.Score.ToString("N0")
              + "<color=#" + ColorUtility.ToHtmlStringRGB(Dim) + ">     Active time </color>" + FormatTime(gm.ActiveTime) + "</size>"
            : "Restart run?";
        buttons[1].gameObject.SetActive(pause);
        buttonTexts[0].text = over ? "Restart" : "Yes";
        buttons[0].anchoredPosition = new Vector2(over ? 0f : -110f, -75f);
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
        previewText.text = PreviewNumber == null ? "" : "PREVIEW: " + Expression(gm.GetCard(dragSlot).Value, true);
        energyText.text = "ENERGY " + gm.Energy + " / " + GameManager.EnergyPerCard;
        energyFill.anchorMax = new Vector2(Mathf.Clamp01(gm.Energy / (float)GameManager.EnergyPerCard), 1f);
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
            s.card.fontSize = reserved ? 26 : 44;
            s.card.text = reserved ? "RESERVED" : c == null ? "-" : c.Value.ToString();
            s.result.text = reserved || c == null ? "" : Expression(c.Value, false);
        }

        ghost.gameObject.SetActive(dragSlot >= 0);
        if (dragSlot < 0) return;
        var card = gm.GetCard(dragSlot).Value;
        ghost.anchoredPosition = pointer / canvas.scaleFactor;
        ghostCard.text = card.ToString();
        ghostResult.text = "= " + card.Apply(Card.Lucky);
    }

    // Board callouts from the mockup, pinned to board positions.
    void RefreshBoardLabels()
    {
        spawnLabel.rectTransform.anchoredPosition = ToCanvas(gm.board.TransformPoint(new Vector3(0f, 0f, 11.2f)));
        // Outside the left wall: inside the board, enemies about to cross would cover it.
        dangerLabel.rectTransform.anchoredPosition = ToCanvas(gm.board.TransformPoint(new Vector3(-5.6f, 0f, Spawner.LineZ)));
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
            var forward = e.transform.parent.forward * Enemy.Radius;
            var top = e.transform.position + up;
            l.number.rectTransform.anchoredPosition = ToCanvas(top);
            l.below.rectTransform.anchoredPosition = ToCanvas(e.transform.position - up - forward) + new Vector2(0f, -6f);
            l.above.rectTransform.anchoredPosition = ToCanvas(top + forward) + new Vector2(0f, 4f);

            bool visible = e.Visible;
            l.number.gameObject.SetActive(e.kind != Enemy.Kind.Bumper);
            l.above.gameObject.SetActive(e.kind == Enemy.Kind.Special);
            l.number.text = visible ? e.target.ToString() : "?";
            l.number.fontSize = e.target >= 100 && visible ? 24 : 30; // 3 digits stay inside the disc
            l.number.color = !visible ? TextColor : e.target == current ? Green : preview == e.target ? Blue : TextColor;

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
            number = Free(MakeText(labelLayer, "", 30, TextColor, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(120f, 40f))),
            below = Free(MakeText(labelLayer, "", 15, Dim, TextAnchor.UpperCenter, Vector2.zero, new Vector2(200f, 20f))),
            above = Free(MakeText(labelLayer, "", 15, Gold, TextAnchor.LowerCenter, Vector2.zero, new Vector2(200f, 20f))),
        };
        l.number.fontStyle = FontStyle.Bold;
        l.above.fontStyle = FontStyle.Bold;
        return l;
    }

    public void Popup(Vector3 world, string text, Color color)
    {
        var t = Free(MakeText(popupLayer, text, 24, color, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(500f, 40f)));
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
            if (p.age >= PopupTime) { Destroy(p.text.gameObject); popups.RemoveAt(i); continue; }
            // Starts just above the disc's far edge so it never covers the number it is about.
            var start = ToCanvas(p.world + gm.board.up * 0.5f + gm.board.forward * Enemy.Radius);
            p.text.rectTransform.anchoredPosition = start + new Vector2(0f, 14f + PopupRise * p.age / PopupTime);
            var col = p.text.color; col.a = 1f - p.age / PopupTime; p.text.color = col;
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
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f; // the camera frames the board by height, so the HUD follows height too
        root = (RectTransform)go.transform;
        // Draw order: board labels, then feedback popups over them, then every panel (created after these).
        labelLayer = Layer("BoardLabels");
        popupLayer = Layer("Popups");

        // Title row above the stats bar, as in the mockup.
        Bold(Anchored(MakeText(root, "LUCKY 37", 40, TextColor, TextAnchor.UpperLeft, Vector2.zero, new Vector2(400f, 50f)), new Vector2(0f, 1f), new Vector2(60f, -26f)));
        Anchored(MakeText(root, "TEMPORARY UI / REDESIGN LATER", 22, Orange, TextAnchor.UpperRight, Vector2.zero, new Vector2(500f, 30f)), new Vector2(1f, 1f), new Vector2(-60f, -34f));

        // Top: score, lives, active time.
        topBar = Panel(root, "TopBar", new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1060f, 90f), PanelEdge);
        scoreText = Stat(topBar, "SCORE", -490f);
        livesText = Stat(topBar, "LIVES", -40f);
        timeText = Stat(topBar, "ACTIVE TIME", 280f);

        // Side panels.
        SidePanel(new Vector2(0f, 1f), new Vector2(70f, -230f), TextAnchor.UpperLeft, new[]
        {
            ("MATCH TO ATTACK", TextColor, new[] { ("Green: current number matches.", Green), ("Blue: matches the dragged card preview.", Blue) }),
            ("HIT TO REVEAL", TextColor, new[] { ("Hidden targets take no damage.", Dim), ("A hit reveals a target for 20s.", Dim), ("Every enemy hit gives +25 energy.", Dim), ("4 hits = 1 new card.", Dim) }),
            ("A / LEFT ARROW", Yellow, new[] { ("Left flipper", Dim) }),
            ("R", Yellow, new[] { ("Pause / restart", Dim) }),
        });
        SidePanel(new Vector2(1f, 1f), new Vector2(-70f, -230f), TextAnchor.UpperLeft, new[]
        {
            ("SPECIAL ENEMY", TextColor, new[] { ("5 HP / always visible", Gold), ("30s lifetime / stationary", Gold), ("111: score x10", Gold), ("222: score x2", Gold), ("333: score x3", Gold) }),
            ("HAND MANAGEMENT", TextColor, new[] { ("Drag cards to reorder.", Dim), ("Drop on the table to use.", Dim), ("Drop elsewhere to cancel.", Dim), ("Full hand: replace slot 5.", Dim) }),
            ("D / RIGHT ARROW", Yellow, new[] { ("Right flipper", Dim) }),
        });

        // Board callouts.
        spawnLabel = Free(MakeText(labelLayer, "ENEMY SPAWN AREA", 14, Dim, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300f, 24f)));
        dangerLabel = Free(MakeText(labelLayer, "DANGER LINE", 16, Danger, TextAnchor.MiddleRight, Vector2.zero, new Vector2(200f, 24f)));
        dangerLabel.rectTransform.pivot = new Vector2(1f, 0.5f);

        // Bottom: lucky number, current number, preview, energy bar.
        float handWidth = 5 * SlotWidth + 4 * SlotSpacing;
        bottomBar = Panel(root, "BottomBar", new Vector2(0.5f, 0f), new Vector2(0f, 210f), new Vector2(handWidth + 60f, 80f), PanelEdge);
        float left = -(handWidth + 60f) / 2f + 25f;
        LeftText(MakeText(bottomBar, "LUCKY 37", 20, Dim, TextAnchor.MiddleLeft, new Vector2(left, 17f), new Vector2(160f, 30f)));
        currentText = Bold(LeftText(MakeText(bottomBar, "", 34, Green, TextAnchor.MiddleLeft, new Vector2(left + 170f, 17f), new Vector2(300f, 40f))));
        previewText = LeftText(MakeText(bottomBar, "", 26, Blue, TextAnchor.MiddleLeft, new Vector2(left + 500f, 17f), new Vector2(400f, 36f)));
        energyText = LeftText(MakeText(bottomBar, "", 18, Dim, TextAnchor.MiddleLeft, new Vector2(left, -20f), new Vector2(200f, 26f)));
        var barBack = Box(bottomBar, "EnergyBar", new Vector2(0.5f, 0.5f), new Vector2(left + 170f + 360f, -20f), new Vector2(720f, 14f), Hex(0x1C2536));
        energyFill = Box(barBack, "Fill", Vector2.zero, Vector2.zero, Vector2.zero, Green);
        energyFill.anchorMin = Vector2.zero; energyFill.anchorMax = new Vector2(0f, 1f);
        energyFill.offsetMin = energyFill.offsetMax = Vector2.zero;

        // Hand: 5 slots; slot 5 is the one a full-hand draw replaces.
        handArea = Box(root, "Hand", new Vector2(0.5f, 0f), new Vector2(0f, SlotBottom + SlotHeight / 2f), new Vector2(handWidth + 60f, SlotHeight + 16f), Color.clear);
        for (int i = 0; i < slots.Length; i++)
        {
            float x = -handWidth / 2f + SlotWidth / 2f + i * (SlotWidth + SlotSpacing);
            var s = new SlotView();
            s.rect = Panel(handArea, "Slot" + (i + 1), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(SlotWidth, SlotHeight), PanelEdge);
            s.edge = s.rect.GetComponent<Image>();
            MakeText(s.rect, "SLOT " + (i + 1), 16, Dim, TextAnchor.MiddleCenter, new Vector2(0f, 38f), new Vector2(SlotWidth, 22f));
            s.card = Bold(MakeText(s.rect, "", 44, TextColor, TextAnchor.MiddleCenter, new Vector2(0f, 2f), new Vector2(SlotWidth, 52f)));
            s.result = MakeText(s.rect, "", 17, Dim, TextAnchor.MiddleCenter, new Vector2(0f, -36f), new Vector2(SlotWidth, 22f));
            slots[i] = s;
        }
        MakeText(slots[slots.Length - 1].rect, "REPLACED WHEN FULL", 14, Orange, TextAnchor.MiddleCenter, new Vector2(0f, -SlotHeight / 2f - 13f), new Vector2(SlotWidth + 40f, 20f));

        // Dragged card ghost.
        ghost = Panel(root, "Ghost", Vector2.zero, Vector2.zero, new Vector2(120f, 92f), Blue);
        ghostCard = Bold(MakeText(ghost, "", 38, Blue, TextAnchor.MiddleCenter, new Vector2(0f, 12f), new Vector2(120f, 46f)));
        ghostResult = MakeText(ghost, "", 20, Blue, TextAnchor.MiddleCenter, new Vector2(0f, -24f), new Vector2(120f, 26f));
        ghost.gameObject.SetActive(false);

        countdownText = Bold(MakeText(root, "", 120, TextColor, TextAnchor.MiddleCenter, new Vector2(0f, 40f), new Vector2(300f, 160f)));

        // Pause / game over dialog.
        dialog = Panel(root, "Dialog", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(640f, 270f), PanelEdge);
        dialogText = Bold(MakeText(dialog, "", 52, TextColor, TextAnchor.MiddleCenter, new Vector2(0f, 45f), new Vector2(620f, 140f)));
        for (int i = 0; i < 2; i++)
        {
            buttons[i] = Box(dialog, "Button" + i, new Vector2(0.5f, 0.5f), new Vector2(i == 0 ? -110f : 110f, -75f), new Vector2(190f, 60f), ButtonColor);
            buttonTexts[i] = MakeText(buttons[i], i == 0 ? "Yes" : "No", 28, TextColor, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(190f, 60f));
        }
        dialog.gameObject.SetActive(false);
    }

    Text Stat(RectTransform bar, string caption, float x)
    {
        LeftText(MakeText(bar, caption, 17, Dim, TextAnchor.MiddleLeft, new Vector2(x, 20f), new Vector2(260f, 24f)));
        return Bold(LeftText(MakeText(bar, "", 36, TextColor, TextAnchor.MiddleLeft, new Vector2(x, -16f), new Vector2(260f, 44f))));
    }

    void SidePanel(Vector2 anchor, Vector2 pos, TextAnchor align, (string title, Color color, (string text, Color color)[] lines)[] sections)
    {
        float y = 0f;
        var rt = Box(root, "Side", anchor, pos, new Vector2(420f, 10f), Color.clear);
        rt.pivot = new Vector2(anchor.x, 1f);
        rt.anchoredPosition = pos;
        foreach (var (title, color, lines) in sections)
        {
            Bold(TopLeft(MakeText(rt, title, 24, color, align, Vector2.zero, new Vector2(420f, 30f)), y));
            y -= 40f;
            foreach (var (text, c) in lines)
            {
                TopLeft(MakeText(rt, text, 20, c, align, Vector2.zero, new Vector2(420f, 26f)), y);
                y -= 31f;
            }
            y -= 32f;
        }
    }

    // ---- UI helpers ----

    RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color edge)
    {
        var outer = Box(parent, name, anchor, pos, size, edge);
        Box(outer, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(4f, 4f), PanelFill);
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
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
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

    // Slot form "37 +3 = 40"; preview form "37 + 3 = 40" as in the mockup.
    static string Expression(Card c, bool spaced) =>
        Card.Lucky + " " + (spaced ? c.op + " " + c.n : c.ToString()) + " = " + c.Apply(Card.Lucky);

    static string FormatTime(float t) => Mathf.FloorToInt(t / 60f).ToString("00") + ":" + Mathf.FloorToInt(t % 60f).ToString("00");

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);
}
