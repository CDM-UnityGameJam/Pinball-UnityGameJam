using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // Lucky number tiles (current and next), the last cast, and the refill countdown and bar.
    // Every field except `lucky` is optional.
    public sealed class LuckPanelView : HudPresenter
    {
        public TMP_Text lucky, lastCast, refill;
        [Tooltip("The number that becomes lucky when the timer runs out.")]
        public TMP_Text next;
        [Tooltip("Seconds until the lucky number changes.")]
        public TMP_Text timer;
        [Tooltip("Fill whose right anchor tracks refill progress.")]
        public RectTransform refillFill;

        int shownCurrent = int.MinValue, shownNext = int.MinValue;

        void Update()
        {
            if (!Ready) return;
            var luck = Context.Luck;
            // Build the number strings only when they change; the timer uses TMP's allocation-free SetText.
            if (luck.Current != shownCurrent) { shownCurrent = luck.Current; lucky.text = shownCurrent.ToString(); }
            if (next && luck.Next != shownNext) { shownNext = luck.Next; next.text = shownNext.ToString(); }
            if (timer) timer.SetText("{0:1}s", luck.Remaining);
            if (lastCast)
            {
                lastCast.text = Describe(Context.Hand.LastPlay, out var color);
                lastCast.color = color;
            }

            var hand = Context.Hand;
            var deck = Context.config.deck;
            if (refill) refill.text = hand.IsFull ? "Equipment full" : $"Refill {hand.RefillRemaining:0.0}s";
            float progress = hand.IsFull ? 0f : Mathf.Clamp01(1f - hand.RefillRemaining / (deck.refillInterval + deck.playRefillDelay));
            if (refillFill) refillFill.anchorMax = new Vector2(progress, refillFill.anchorMax.y);
        }

        string Describe(CastResult cast, out Color color)
        {
            color = Theme.orange;
            if (cast == null) return "Active skill: not used";
            var element = cast.card.element;
            switch (cast.outcome)
            {
                case CastOutcome.ShortCircuit:
                    color = Theme.danger;
                    return $"{element.displayName} Q={cast.quality} · OFF {Context.Casts.ShortCircuitRemaining(element):0.0}s";
                case CastOutcome.Misfire:
                    return $"{element.skillName} Q=0 · Misfire";
                default:
                    var skill = element.active;
                    string progress = skill != null && !skill.ShowsWaveProgress ? skill.Summary(cast.plan) : $"Wave {cast.WavesFired}/{cast.plan.waves}";
                    return $"{element.skillName} Q={cast.quality} · {progress} · Kills {cast.Kills}";
            }
        }
    }
}
