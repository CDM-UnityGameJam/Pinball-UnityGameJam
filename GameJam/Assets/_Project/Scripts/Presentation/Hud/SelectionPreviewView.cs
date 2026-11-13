using System.Collections.Generic;
using System.Text;
using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // What casting the current selection would do: upgrade chance, targets, each element's plan per reachable
    // tier, the enchantment left behind, empowered hits gained and the charge cost.
    public sealed class SelectionPreviewView : HudPresenter
    {
        [Required] public HandView hand;
        public RectTransform panel;
        public TMP_Text title, details;

        readonly StringBuilder text = new StringBuilder();
        readonly List<string> plans = new List<string>();

        void Update()
        {
            if (!Ready) return;
            var selection = hand.Selection;
            bool show = selection.Count > 0;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) return;
            title.text = $"{selection.Count} selected · #{hand.SelectedNumber}";
            details.text = Describe(selection);
        }

        string Describe(IReadOnlyList<HandCard> selection)
        {
            var cards = Context.Hand;
            var config = Context.config;
            text.Clear();
            text.Append($"Upgrade chance: {DiceRules.UpgradeChance(selection.Count) * 100:0.0}%");
            text.Append($"\nPossible targets: {hand.PreviewCount} · Power +{(long)selection.Count * config.empoweredHitsPerCard}");
            var tiers = DiceRules.PossibleTiers(selection.Count);
            foreach (var element in config.elements)
            {
                int spent = 0;
                foreach (var c in selection) if (c.Element == element) spent++;
                if (spent == 0) continue;
                plans.Clear();
                foreach (var tier in tiers)
                {
                    var p = element.Plan(tier, spent);
                    string scope = p.allTargets ? "All" : p.targetCount.ToString();
                    string strength = element.active != null && !element.active.ShowsWaveProgress && p.burnSeconds > 0f ? $"{p.burnSeconds:0.##}s" : $"{p.waves}w";
                    plans.Add($"{DiceRules.Label(tier)}:{scope}/{strength}");
                }
                int held = cards.HeldCount(element), after = held - spent;
                text.Append($"\n{element.displayName} {string.Join(" · ", plans)}");
                text.Append($"\n{element.displayName}: held {held} → {after}");
                if (element.passive != null) text.Append("\n" + element.passive.Summary(after));
            }
            double before = cards.IsFull ? config.deck.baseCost : cards.ChargeRequired;
            text.Append($"\nCharge required: {before:0.#} → {cards.ChargeAfterBatch(selection.Count):0.#}");
            return text.ToString();
        }
    }
}
