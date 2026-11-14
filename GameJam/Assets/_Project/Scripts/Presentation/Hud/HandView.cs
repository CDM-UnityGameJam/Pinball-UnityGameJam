using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // The hand and how groups are cast. Port of the v0.7 Hud selection rules:
    // - A cast is always a whole group: every held card with one number.
    // - Mouse: pressing a card picks up its group (highlighted). Dragging moves the whole group; releasing outside
    //   the hand area casts it, releasing inside puts it back. A click without a drag never casts.
    // - Keys (groupKeys[i] = number i + 1): one press selects that group and shows the hint; a double-tap of the
    //   same key within doubleTapSeconds casts it. A single tap on the selected group cancels once the window
    //   runs out. Another number switches groups; holding a key never repeats.
    // - Picking up a group with the mouse drops pending taps; pausing or losing focus drops everything.
    // - Selected and dragged cards stay in the hand (they still enchant the ball) until cast.
    // - Enemies the selected group would target are highlighted.
    public sealed class HandView : HudPresenter
    {
        public List<CardView> slots = new List<CardView>();
        [Required, Tooltip("Releasing a dragged group outside this rectangle casts it.")]
        public RectTransform handArea;
        [Tooltip("Tinted while a group is dragged (optional).")]
        public Graphic handAreaTint;
        [Tooltip("Shown while a group is selected by key: \"Double-tap N to cast\" (optional).")]
        public GameObject keyHint;
        public TMP_Text keyHintText;

        [Title("Input")]
        [Tooltip("Element i selects the group with number i + 1.")]
        public List<InputActionReference> groupKeys = new List<InputActionReference>();
        [Min(0.05f), SuffixLabel("s")] public float doubleTapSeconds = 0.3f;
        [Min(0), Tooltip("Pixels the pointer must move before a press becomes a drag.")]
        public float dragThreshold = 8f;

        [Title("Dragged group")]
        [Range(0, 1), Tooltip("How far each dragged card moves toward the group's centre (1 = all on one spot).")]
        public float gatherAmount = 0.55f;
        [Tooltip("Degrees each card is rolled per slot away from the group's centre, so the group fans like a held hand.")]
        public float gatherFan = 5f;
        [Tooltip("Canvas units the outer cards of a dragged group drop per slot from the centre.")]
        public float gatherDrop = 8f;

        readonly List<HandCard> selection = new List<HandCard>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly HashSet<Enemy> targets = new HashSet<Enemy>();
        readonly HashSet<EnemyView> previewed = new HashSet<EnemyView>();
        readonly Dictionary<Enemy, EnemyView> views = new Dictionary<Enemy, EnemyView>();
        GroupKeyInput keys;
        Vector2 pressedAt, pressedLocal;

        public IReadOnlyList<HandCard> Selection => selection;
        public int? SelectedNumber => selection.Count == 0 ? (int?)null : selection[0].Number;
        public int PreviewCount => targets.Count;
        public bool PointerHeld { get; private set; }
        public bool Dragging { get; private set; }
        public bool KeySelection { get; private set; }
        // Canvas-space offset of the dragged group from where it was picked up.
        public Vector2 DragOffset { get; private set; }

        protected override void OnBind()
        {
            keys = new GroupKeyInput(doubleTapSeconds);
            for (int i = 0; i < slots.Count; i++) slots[i].Bind(this, i);
            Context.Hand.Changed += KeepHeldOnly;
            Context.Hand.BatchPlayed += _ => ClearAll();
            Context.Run.PauseChanged += _ => ClearAll();
        }

        void OnEnable()
        {
            foreach (var key in groupKeys) if (key && key.action != null) key.action.Enable();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && Hud != null) ClearAll();
        }

        bool CanAct => Ready && Context.Hand.CanPlay;

        // Select every held card with this number. False if none is held.
        public bool SelectNumber(int number)
        {
            if (!Ready) return false;
            Context.Hand.Group(number, selection);
            return selection.Count > 0;
        }

        public bool IsSelected(HandCard card) => card != null && selection.Contains(card);

        // Cast the selected group. True if it was accepted.
        public bool Cast()
        {
            if (!CanAct || selection.Count == 0) return false;
            KeepHeldOnly();
            return Context.Hand.TryPlay(new List<HandCard>(selection));
        }

        // Mouse ----------------------------------------------------------------------------------------------

        public void Press(int slot, Vector2 screen)
        {
            if (!CanAct || Context.Hand[slot] == null || !SelectNumber(Context.Hand[slot].Number)) return;
            keys.Reset();
            KeySelection = false;
            PointerHeld = true;
            Dragging = false;
            DragOffset = Vector2.zero;
            pressedAt = screen;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handArea, screen, null, out pressedLocal);
        }

        public void Move(Vector2 screen)
        {
            if (!PointerHeld) return;
            if ((screen - pressedAt).sqrMagnitude >= dragThreshold * dragThreshold) Dragging = true;
            if (Dragging && RectTransformUtility.ScreenPointToLocalPointInRectangle(handArea, screen, null, out var local))
                DragOffset = local - pressedLocal;
        }

        // Returns true if the group was cast.
        public bool Release(Vector2 screen)
        {
            if (!PointerHeld) return false;
            Move(screen);
            bool cast = Dragging && !RectTransformUtility.RectangleContainsScreenPoint(handArea, screen, null);
            bool played = cast && Cast();
            ClearAll();
            return played;
        }

        // Keys -----------------------------------------------------------------------------------------------

        void ReadKeys()
        {
            double now = Time.unscaledTimeAsDouble;
            if (keys.Advance(now, SelectedNumber) == GroupKeyAction.Cancel) ClearAll();
            for (int i = 0; i < groupKeys.Count; i++)
            {
                var key = groupKeys[i];
                if (!key || key.action == null || !key.action.WasPressedThisFrame()) continue;
                PressKey(i + 1, now);
            }
        }

        // One press of the number key for `number` (public so tests can drive it).
        public GroupKeyAction PressKey(int number, double now)
        {
            if (!CanAct || PointerHeld) return GroupKeyAction.None;
            var action = keys.Press(number, SelectedNumber, now);
            if (action == GroupKeyAction.Select)
            {
                KeySelection = SelectNumber(number);
                if (!KeySelection) { selection.Clear(); keys.Reset(); }
            }
            else if (action == GroupKeyAction.Cast) Cast();
            return action;
        }

        public GroupKeyAction AdvanceKeys(double now)
        {
            var action = keys.Advance(now, SelectedNumber);
            if (action == GroupKeyAction.Cancel) ClearAll();
            return action;
        }

        // ----------------------------------------------------------------------------------------------------

        void ClearAll()
        {
            selection.Clear();
            keys?.Reset();
            KeySelection = false;
            PointerHeld = false;
            Dragging = false;
            DragOffset = Vector2.zero;
        }

        // A refill or cast changed the hand: a selected group follows its number (new cards of it join).
        void KeepHeldOnly()
        {
            if (selection.Count == 0) return;
            int number = selection[0].Number;
            Context.Hand.Group(number, selection);
            if (selection.Count == 0) ClearAll();
        }

        void Update()
        {
            if (!Ready) return;
            if (!Context.Hand.CanPlay && (selection.Count > 0 || PointerHeld)) ClearAll();
            if (!PointerHeld) ReadKeys();

            var hand = Context.Hand;
            // Centre of the dragged group, in slot order, so its cards can close up around it.
            float centreX = 0f, centreIndex = 0f;
            int picked = 0;
            if (Dragging)
                for (int i = 0; i < slots.Count; i++)
                {
                    if (!IsSelected(hand[i])) continue;
                    centreX += ((RectTransform)slots[i].transform).anchoredPosition.x;
                    centreIndex += i;
                    picked++;
                }
            if (picked > 0) { centreX /= picked; centreIndex /= picked; }
            for (int i = 0; i < slots.Count; i++)
            {
                var card = hand[i];
                bool isPicked = IsSelected(card);
                var gather = Vector2.zero;
                float roll = 0f;
                if (isPicked && Dragging && picked > 1)
                {
                    float x = ((RectTransform)slots[i].transform).anchoredPosition.x;
                    float side = i - centreIndex;
                    gather = new Vector2((centreX - x) * gatherAmount, -Mathf.Abs(side) * gatherDrop);
                    roll = -side * gatherFan;
                }
                slots[i].Show(card, card != null ? hand.HeldCount(card.Element) : 0, card != null ? hand.LuckyHeldCount(card.Element) : 0,
                    hand.IsLucky(card), isPicked, isPicked && Dragging ? DragOffset : Vector2.zero, gather, roll, Theme);
            }
            if (handAreaTint) handAreaTint.enabled = Dragging;
            bool hint = KeySelection && selection.Count > 0 && hand.CanPlay;
            if (keyHint && keyHint.activeSelf != hint) keyHint.SetActive(hint);
            if (hint && keyHintText) keyHintText.text = $"Double-tap {SelectedNumber} to cast";

            UpdatePreview();
        }

        void UpdatePreview()
        {
            foreach (var view in previewed) if (view) view.Previewed = false;
            previewed.Clear();
            targets.Clear();
            if (selection.Count == 0 || !Context.Run.IsPlaying) return;
            Context.Enemies.FrontFirst(enemies);
            var tier = selection[0].Number == Context.Hand.LuckyNumber ? CastTier.Lucky : CastTier.Normal;
            foreach (var element in Context.config.elements)
            {
                int copies = 0;
                foreach (var c in selection) if (c.Element == element) copies++;
                if (copies == 0) continue;
                var plan = element.Plan(tier, copies);
                int count = 0;
                foreach (var enemy in enemies)
                {
                    if (!enemy.CanReceive(element)) continue;
                    if (!plan.allTargets && count >= plan.targetCount) break;
                    count++;
                    targets.Add(enemy);
                }
            }
            foreach (var enemy in targets)
            {
                if (!views.TryGetValue(enemy, out var view)) views[enemy] = view = enemy.GetComponent<EnemyView>();
                if (!view) continue;
                view.Previewed = true;
                previewed.Add(view);
            }
        }

        public bool IsPreviewed(Enemy enemy) => targets.Contains(enemy);
    }
}
