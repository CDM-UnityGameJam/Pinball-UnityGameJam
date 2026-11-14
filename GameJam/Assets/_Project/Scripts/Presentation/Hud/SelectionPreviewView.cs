using System.Collections.Generic;
using System.Text;
using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // What casting the selected group would do: Normal or Lucky, targets, each element's plan, the enchantment
    // left behind and the new charge requirement.
    public sealed class SelectionPreviewView : HudPresenter
    {
        [Required] public HandView hand;
        public RectTransform panel;
        public TMP_Text title, details;

        readonly StringBuilder text = new StringBuilder();

        void Update()
        {
            if (!Ready) return;
            var selection = hand.Selection;
            bool show = selection.Count > 0;
            if (panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
            if (!show) return;
            var tier = hand.SelectedNumber == Context.Hand.LuckyNumber ? CastTier.Lucky : CastTier.Normal;
            title.text = $"{GroupRules.Label(tier)} · Group #{hand.SelectedNumber} · {selection.Count} card{(selection.Count == 1 ? "" : "s")}";
            details.text = Describe(selection, tier);
        }

        string Describe(IReadOnlyList<HandCard> selection, CastTier tier)
        {
            var cards = Context.Hand;
            text.Clear();
            text.Append($"Targets: {hand.PreviewCount}");
            foreach (var element in Context.config.elements)
            {
                int spent = 0;
                foreach (var c in selection) if (c.Element == element) spent++;
                if (spent == 0) continue;
                var p = element.Plan(tier, spent);
                string scope = p.allTargets ? "All" : p.targetCount.ToString();
                string summary = element.active != null ? element.active.Summary(p) : "";
                text.Append($"\n{element.displayName}: {scope} · {summary}");
                int held = cards.HeldCount(element), after = held - spent;
                int luckyAfter = tier == CastTier.Lucky ? 0 : cards.LuckyHeldCount(element);
                text.Append($"\n{element.displayName}: held {held} → {after}");
                if (element.passive != null) text.Append("\n" + element.passive.Summary(after, luckyAfter));
            }
            text.Append($"\nCharge required: {(cards.IsFull ? 0 : cards.ChargeRequired):0.#} → {cards.ChargeAfterCast(selection.Count):0.#}");
            return text.ToString();
        }
    }
}
