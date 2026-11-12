using System;
using System.Collections.Generic;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Resolves played cards and runs active-skill waves on the combat clock. Port of the old UseCard outcome
    // handling and SkillCast:
    // - Q < 0 short-circuits the card's element for a while (type-wide: every number of that element).
    // - Q = 0 misfires. Q > 0 casts: targets are taken from the front-first snapshot made before the card
    //   left the hand, the first wave fires immediately, and later waves follow at the skill's interval.
    // - Enemies that spawn later never join a cast's locked targets.
    public sealed class CastScheduler : ICombatTimer
    {
        readonly GameContext context;
        readonly List<CastResult> active = new List<CastResult>();
        readonly Dictionary<ElementDefinition, double> shortCircuitUntil = new Dictionary<ElementDefinition, double>();

        public CastScheduler(GameContext context) => this.context = context;

        public int PendingCasts => active.Count;

        public event Action<CastResult> CastResolved;
        public event Action<CastResult> WaveFired;

        public float ShortCircuitRemaining(ElementDefinition element) =>
            element && shortCircuitUntil.TryGetValue(element, out var until) ? (float)Math.Max(0, until - context.Clock.Now) : 0f;

        public bool IsShortCircuited(ElementDefinition element) => ShortCircuitRemaining(element) > 0f;

        internal CastResult Resolve(CardSpec card, int quality, bool lucky, int luckyNumber, List<Enemy> snapshot)
        {
            var outcome = QualityRoller.Classify(quality);
            var luck = context.config.luck;
            var plan = outcome == CastOutcome.Cast ? card.element.Plan(luck, quality) : default;
            var targets = new List<Enemy>();
            if (outcome == CastOutcome.Cast)
                for (int i = 0; i < snapshot.Count; i++)
                    if (plan.allTargets || i < plan.targetCount) targets.Add(snapshot[i]);

            var result = new CastResult(card, quality, outcome, lucky, luckyNumber, plan, targets);
            if (outcome == CastOutcome.ShortCircuit)
            {
                double until = context.Clock.Now + QualityRoller.ShortCircuitSeconds(luck.bands, quality, luck.shortCircuitMinSeconds, luck.shortCircuitMaxSeconds);
                shortCircuitUntil[card.element] = shortCircuitUntil.TryGetValue(card.element, out var current) ? Math.Max(current, until) : until;
            }
            else if (outcome == CastOutcome.Cast)
            {
                Fire(result);
                if (!result.Complete) active.Add(result);
            }
            CastResolved?.Invoke(result);
            return result;
        }

        void Fire(CastResult cast)
        {
            if (cast.Complete || cast.quality <= 0) return;
            var skill = cast.card.element.active;
            skill?.FireWave(new SkillWave(context, cast.card.element, cast.targets, cast.plan, cast.WavesFired == 0, cast.quality, cast));
            cast.WavesFired++;
            cast.NextWaveAt = cast.Complete ? double.PositiveInfinity
                : context.Clock.Now + Mathf.Max(0.01f, skill != null ? skill.WaveInterval : 0.01f);
            WaveFired?.Invoke(cast);
        }

        public double NextEventAt
        {
            get
            {
                double next = double.PositiveInfinity;
                foreach (var cast in active) next = Math.Min(next, cast.NextWaveAt);
                return next;
            }
        }

        // Old order: newest cast first when several waves are due at the same moment.
        public void OnTime(double now)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var cast = active[i];
                if (cast.NextWaveAt <= now + RuleMath.TimeEpsilon) Fire(cast);
                if (cast.Complete) active.RemoveAt(i);
            }
        }
    }
}
