using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // The hand, card selection and the Cast button. Port of the v0.6 Hud selection rules:
    // - Click a card to select it, click again to unselect. The first card locks the number: cards with another
    //   number are greyed out and can't be selected. Up to five cards; elements can mix.
    // - Selected cards stay in the hand (they still enchant the ball) until the batch is cast.
    // - The Cast button plays the selection: one die per card. Nothing can be selected or cast while a batch
    //   waits for its dice.
    // - A card that leaves the hand drops out of the selection; casting clears it.
    // - Enemies the selection could target are highlighted (the widest tier it can reach).
    public sealed class HandView : HudPresenter
    {
        public List<CardView> slots = new List<CardView>();
        public Button castButton;
        public TMP_Text castLabel;
        public Image castButtonImage;
        public Color castReadyColor = new Color32(43, 52, 72, 255);

        readonly List<HandCard> selection = new List<HandCard>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly HashSet<Enemy> targets = new HashSet<Enemy>();
        readonly HashSet<EnemyView> previewed = new HashSet<EnemyView>();
        readonly Dictionary<Enemy, EnemyView> views = new Dictionary<Enemy, EnemyView>();

        public IReadOnlyList<HandCard> Selection => selection;
        public int? SelectedNumber => selection.Count == 0 ? (int?)null : selection[0].Number;
        public int PreviewCount => targets.Count;

        protected override void OnBind()
        {
            for (int i = 0; i < slots.Count; i++) slots[i].Bind(this, i);
            Context.Hand.Changed += () => selection.RemoveAll(c => !Context.Hand.Contains(c));
            Context.Hand.BatchPlayed += _ => selection.Clear();
            if (castButton) castButton.onClick.AddListener(() => Cast());
        }

        // Returns true if the card's selection changed.
        public bool Toggle(int slot)
        {
            if (!Ready || !Context.Hand.CanPlay) return false;
            var card = Context.Hand[slot];
            if (card == null) return false;
            if (selection.Remove(card)) return true;
            if (selection.Count >= DiceRules.MaxCards || (SelectedNumber.HasValue && card.Number != SelectedNumber.Value)) return false;
            selection.Add(card);
            return true;
        }

        // Play the selection. Returns true if the batch was accepted.
        public bool Cast() => Ready && selection.Count > 0 && Context.Hand.TryPlay(new List<HandCard>(selection));

        public bool IsSelected(HandCard card) => card != null && selection.Contains(card);

        void Update()
        {
            if (!Ready) return;
            var hand = Context.Hand;
            for (int i = 0; i < slots.Count; i++)
            {
                var card = hand[i];
                bool blocked = card != null && SelectedNumber.HasValue && card.Number != SelectedNumber.Value;
                slots[i].Show(card, card != null ? hand.HeldCount(card.Element) : 0, card != null && hand.HeldCountOfNumber(card.Number) >= 2,
                    IsSelected(card), blocked, Theme);
            }

            bool ready = selection.Count > 0 && hand.CanPlay;
            if (castLabel)
            {
                castLabel.text = $"Cast {selection.Count} · Roll {selection.Count} {(selection.Count == 1 ? "die" : "dice")}";
                castLabel.color = ready ? Theme.text : Theme.dim;
            }
            if (castButtonImage) castButtonImage.color = ready ? castReadyColor : Theme.panelEdge;
            if (castButton) castButton.interactable = ready;

            UpdatePreview();
        }

        void UpdatePreview()
        {
            foreach (var view in previewed) if (view) view.Previewed = false;
            previewed.Clear();
            targets.Clear();
            if (selection.Count == 0 || !Context.Run.IsPlaying) return;
            Context.Enemies.FrontFirst(enemies);
            var tiers = DiceRules.PossibleTiers(selection.Count);
            foreach (var element in Context.config.elements)
            {
                int copies = 0;
                foreach (var c in selection) if (c.Element == element) copies++;
                if (copies == 0) continue;
                bool all = false;
                int limit = 0;
                foreach (var tier in tiers)
                {
                    var plan = element.Plan(tier, copies);
                    all |= plan.allTargets;
                    limit = Mathf.Max(limit, plan.targetCount);
                }
                int count = 0;
                foreach (var enemy in enemies)
                {
                    if (!enemy.CanReceive(element)) continue;
                    if (!all && count >= limit) break;
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
