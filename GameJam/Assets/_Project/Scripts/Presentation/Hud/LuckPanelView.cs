using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // Bottom bar: lucky number and its timer, the last cast, and the refill countdown and bar.
    public sealed class LuckPanelView : HudPresenter
    {
        public TMP_Text lucky, lastCast, refill;
        [Tooltip("Fill whose right anchor tracks refill progress.")]
        public RectTransform refillFill;

        void Update()
        {
            if (!Ready) return;
            lucky.text = $"{Context.Luck.Current}  Next {Context.Luck.Remaining:0.0}s";
            lastCast.text = Describe(Context.Hand.LastPlay, out var color);
            lastCast.color = color;

            var hand = Context.Hand;
            var deck = Context.config.deck;
            refill.text = hand.IsFull ? "Equipment full" : $"Refill {hand.RefillRemaining:0.0}s";
            float progress = hand.IsFull ? 0f : Mathf.Clamp01(1f - hand.RefillRemaining / (deck.refillInterval + deck.playRefillDelay));
            refillFill.anchorMax = new Vector2(progress, refillFill.anchorMax.y);
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
