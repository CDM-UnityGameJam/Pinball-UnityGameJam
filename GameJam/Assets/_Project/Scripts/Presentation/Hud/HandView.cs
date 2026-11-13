using System.Collections.Generic;
using Pinball.Gameplay;
using UnityEngine;

namespace Pinball.Presentation
{
    // The hand and drag-to-play. Port of the old Hud drag rules:
    // - Press a card to pick it up (only while playing); releasing over the playfield plays it, releasing
    //   anywhere else (including over HUD panels) puts it back.
    // - If the dragged card shifts left because another card was played, the drag follows it.
    // - If a refill replaces the dragged slot, the drag is dropped, so a release can never play a different card.
    // - While dragging, enemies with the card's number are highlighted (passive preview).
    // - A press released where it started is a click: the first click selects the card (raised, hint shown,
    //   its enemies previewed), a second click on the same card within selectSeconds casts it.
    public sealed class HandView : HudPresenter
    {
        public List<CardView> slots = new List<CardView>();
        public DragGhostView ghost;
        [Tooltip("Releasing over these never plays a card.")]
        public List<RectTransform> dropBlockers = new List<RectTransform>();
        [Min(0), Tooltip("A press released within this many pixels of where it started counts as a click.")]
        public float clickSlop = 20f;
        [Min(0.1f), Tooltip("Seconds (unscaled) a selected card waits for the second click.")]
        public float selectSeconds = 3f;

        readonly List<Enemy> enemies = new List<Enemy>();
        readonly HashSet<EnemyView> previewed = new HashSet<EnemyView>();
        readonly Dictionary<Enemy, EnemyView> views = new Dictionary<Enemy, EnemyView>();
        Vector2 pointer, pressedAt;
        float selectedUntil;

        public int DragSlot { get; private set; } = -1;
        public int SelectedSlot { get; private set; } = -1;
        int PreviewSlot => DragSlot >= 0 ? DragSlot : SelectedSlot;
        public int? PreviewNumber => PreviewSlot >= 0 && Context.Hand[PreviewSlot].HasValue ? Context.Hand[PreviewSlot].Value.number : (int?)null;
        public int PreviewCount { get; private set; }

        protected override void OnBind()
        {
            for (int i = 0; i < slots.Count; i++) slots[i].Bind(this, i);
            Context.Hand.CardShifted += (from, to) =>
            {
                if (DragSlot == from) DragSlot = to;
                if (SelectedSlot == from) SelectedSlot = to;
            };
            Context.Hand.SlotReplaced += slot =>
            {
                if (slot == DragSlot) DragSlot = -1;
                if (slot == SelectedSlot) SelectedSlot = -1;
            };
        }

        public void Press(int slot, Vector2 screen)
        {
            if (!Ready || !Context.Run.IsPlaying || !Context.Hand[slot].HasValue) return;
            DragSlot = slot;
            pointer = pressedAt = screen;
        }

        public void Move(Vector2 screen) => pointer = screen;

        // Returns true if the card was played.
        public bool Release(Vector2 screen)
        {
            if (DragSlot < 0) return false;
            int slot = DragSlot;
            DragSlot = -1;
            if (!Context.Run.IsPlaying || !Context.Hand[slot].HasValue) return false;
            if (Vector2.Distance(screen, pressedAt) <= clickSlop) return Click(slot);
            SelectedSlot = -1;
            return OverPlayfield(screen) && Context.Hand.TryPlay(slot);
        }

        // First click selects, a second click on the selected card casts it.
        bool Click(int slot)
        {
            if (SelectedSlot != slot)
            {
                SelectedSlot = slot;
                selectedUntil = Time.unscaledTime + selectSeconds;
                return false;
            }
            SelectedSlot = -1;
            return Context.Hand.TryPlay(slot);
        }

        public bool OverPlayfield(Vector2 screen)
        {
            foreach (var blocker in dropBlockers)
                if (blocker && blocker.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(blocker, screen, null))
                    return false;
            return Context.Board.playfieldArea.Contains(Hud.WorldCamera.ScreenPointToRay(screen));
        }

        void Update()
        {
            if (!Ready) return;
            var hand = Context.Hand;
            if (!Context.Run.IsPlaying || (DragSlot >= 0 && !hand[DragSlot].HasValue)) DragSlot = -1;
            if (!Context.Run.IsPlaying || Time.unscaledTime > selectedUntil || (SelectedSlot >= 0 && !hand[SelectedSlot].HasValue)) SelectedSlot = -1;

            int lucky = Context.Luck.Current;
            for (int i = 0; i < slots.Count; i++)
            {
                var card = hand[i];
                int stack = card.HasValue ? hand.HeldCount(card.Value.element, card.Value.number) : 0;
                slots[i].Show(card, stack, card.HasValue && card.Value.number == lucky, i == DragSlot || i == SelectedSlot,
                    i == SelectedSlot, Theme, Context.config.luck.minNumber);
            }

            UpdatePreview();
            if (DragSlot >= 0) ghost.Show(hand[DragSlot].Value, Hud.ScreenToCanvas(pointer), PreviewCount);
            else ghost.Hide();
        }

        void UpdatePreview()
        {
            foreach (var view in previewed) if (view) view.Previewed = false;
            previewed.Clear();
            PreviewCount = 0;
            var number = PreviewNumber;
            if (!number.HasValue || !Context.Run.IsPlaying) return;
            foreach (var enemy in Context.Enemies.FrontFirst(enemies))
            {
                if (enemy.Number != number.Value) continue;
                PreviewCount++;
                if (!views.TryGetValue(enemy, out var view)) views[enemy] = view = enemy.GetComponent<EnemyView>();
                if (!view) continue;
                view.Previewed = true;
                previewed.Add(view);
            }
        }

        public bool IsPreviewed(Enemy enemy) => PreviewNumber.HasValue && Context.Run.IsPlaying && enemy.Number == PreviewNumber.Value;
    }
}
