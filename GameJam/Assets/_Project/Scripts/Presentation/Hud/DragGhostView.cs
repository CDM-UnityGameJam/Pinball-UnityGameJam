using System.Collections.Generic;
using System.Text;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The card being dragged, with what playing it now would do: odds, targets per band, the enchantment
    // left behind and the refill cost.
    public sealed class DragGhostView : HudPresenter
    {
        public RectTransform panel;
        public TMP_Text title, details;
        [Tooltip("Offset from the pointer, in canvas units.")]
        public Vector2 pointerOffset = new Vector2(265f, 0f);
        [Tooltip("Keeps the panel this far inside the screen edges.")]
        public Vector2 edgeMargin = new Vector2(260f, 165f);

        readonly StringBuilder text = new StringBuilder();
        readonly List<Enemy> enemies = new List<Enemy>();

        public void Hide()
        {
            if (panel.gameObject.activeSelf) panel.gameObject.SetActive(false);
        }

        public void Show(CardSpec card, Vector2 canvasPointer, int passiveMatches)
        {
            if (!panel.gameObject.activeSelf) panel.gameObject.SetActive(true);
            var size = Hud.CanvasSize;
            var p = canvasPointer + pointerOffset;
            panel.anchoredPosition = new Vector2(Mathf.Clamp(p.x, edgeMargin.x, size.x - edgeMargin.x), Mathf.Clamp(p.y, edgeMargin.y, size.y - edgeMargin.y));
            title.text = CardView.CardText(card);
            details.text = Describe(card, passiveMatches);
        }

        string Describe(CardSpec card, int passiveMatches)
        {
            var luck = Context.config.luck;
            var hand = Context.Hand;
            bool lucky = card.number == Context.Luck.Current;
            var bands = luck.bands;
            int foes = Context.Enemies.FrontFirst(enemies).Count;

            text.Clear();
            text.Append(lucky ? "Lucky match" : "Normal cast");
            text.Append($" · {bands[0].id} {QualityRoller.Chance(bands, 0, lucky):P0} · High power {QualityRoller.Chance(bands, bands.Count - 1, lucky):P0}");
            text.Append($"\nPassive #{card.number} matches: {passiveMatches} foes (gold)");
            foreach (var band in bands)
            {
                if (band.maxQ <= 0) continue;
                var low = card.element.Plan(luck, Mathf.Max(band.minQ, 1));
                var high = card.element.Plan(luck, band.maxQ);
                if (high.allTargets) text.Append($"\n{band.id}: all foes at cast time (now {foes} foes)");
                else
                {
                    var scaling = card.element.bandScaling.Find(s => s.bandId == band.id);
                    string range = scaling != null ? $"{scaling.targets.min}-{scaling.targets.max}" : "?";
                    text.Append($"\n{band.id}: nearest {range}, any number (now {SkillPlanner.TargetCount(low, foes)}-{SkillPlanner.TargetCount(high, foes)} foes)");
                }
            }
            int held = hand.HeldCount(card.element, card.number), after = held - 1;
            text.Append($"\nAfter cast: #{card.number} {card.element.displayName} {held} → {after} cards");
            if (card.element.passive != null) text.Append("\n" + card.element.passive.Summary(card.number, after));
            float before = hand.IsFull ? Context.config.deck.refillInterval : hand.RefillRemaining;
            text.Append($"\nNext refill: {before:0.0} → {hand.RefillAfterPlay:0.0}s");
            return text.ToString();
        }
    }
}
