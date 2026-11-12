using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    // How strongly one element's active skill scales inside one quality band.
    // Each range is interpolated by where Q sits in the band (see QualityBand.CastPosition).
    [Serializable]
    public class BandScaling
    {
        public string bandId;
        public bool allTargets;
        public IntRange targets;
        public IntRange waves;
        public SteppedRange burnSeconds;
        public SteppedRange stunSeconds;
    }

    // The resolved strength of one cast.
    public struct SkillPlan
    {
        public bool allTargets;
        public int targetCount, waves;
        public float burnSeconds, stunSeconds;
    }

    // Port of the old LuckTuning.Plan: turns a positive Q into targets, waves and durations.
    public static class SkillPlanner
    {
        public static SkillPlan Plan(IReadOnlyList<QualityBand> bands, IReadOnlyList<BandScaling> scaling, int q)
        {
            if (q <= 0) return default;
            int index = QualityRoller.BandOf(bands, q);
            if (index < 0) return default;
            var band = bands[index];
            var rule = Find(scaling, band.id);
            if (rule == null) return default;

            float t = band.CastPosition(q);
            return new SkillPlan
            {
                allTargets = rule.allTargets,
                targetCount = rule.targets.Evaluate(t),
                waves = Math.Max(1, rule.waves.Evaluate(t)),
                burnSeconds = rule.burnSeconds.Evaluate(t),
                stunSeconds = rule.stunSeconds.Evaluate(t),
            };
        }

        // How many targets a plan hits from a front-first snapshot taken before the card left the hand.
        public static int TargetCount(SkillPlan plan, int available) =>
            plan.allTargets ? available : Math.Min(plan.targetCount, available);

        static BandScaling Find(IReadOnlyList<BandScaling> scaling, string bandId)
        {
            foreach (var rule in scaling) if (rule != null && rule.bandId == bandId) return rule;
            return null;
        }
    }
}
