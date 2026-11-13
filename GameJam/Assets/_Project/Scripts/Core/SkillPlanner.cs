using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    // The dice result of one played batch, weakest first.
    public enum CastTier
    {
        Base,      // no die matched: the skills still fire, at their smallest
        Hit,       // one card, its die matched
        FullField, // two or more cards, exactly one die matched
        Jackpot,   // two or more dice matched
    }

    // How strongly one element's active skill fires at one tier.
    [Serializable]
    public struct ActiveTier
    {
        public bool allTargets;
        public int targetCount;
        public int waves;
        public float burnSeconds;
        public float stunSeconds;

        public ActiveTier(bool allTargets, int targetCount, int waves, float burnSeconds, float stunSeconds)
        {
            this.allTargets = allTargets; this.targetCount = targetCount; this.waves = waves;
            this.burnSeconds = burnSeconds; this.stunSeconds = stunSeconds;
        }
    }

    // The resolved strength of one element's cast in a batch.
    public struct SkillPlan
    {
        public bool allTargets;
        public int targetCount, waves;
        public float burnSeconds, stunSeconds;
    }

    // Port of the v0.6 LuckTuning.Plan: the tier row, plus a bonus for every extra card of the same element
    // in the batch (more waves, longer burn).
    public static class SkillPlanner
    {
        public const int TierCount = 4;

        public static SkillPlan Plan(IReadOnlyList<ActiveTier> tiers, CastTier tier, int copies, int extraWavesPerCopy, float extraBurnSecondsPerCopy)
        {
            if (copies < 1 || tiers == null || (int)tier >= tiers.Count) return default;
            var row = tiers[(int)tier];
            int extra = copies - 1;
            return new SkillPlan
            {
                allTargets = row.allTargets,
                targetCount = Math.Max(1, row.targetCount),
                waves = (int)Math.Min(int.MaxValue, Math.Max(1L, row.waves + (long)extra * Math.Max(0, extraWavesPerCopy))),
                burnSeconds = row.burnSeconds + extra * Math.Max(0f, extraBurnSecondsPerCopy),
                stunSeconds = row.stunSeconds,
            };
        }

        // How many targets a plan hits from `available` eligible enemies.
        public static int TargetCount(SkillPlan plan, int available) =>
            plan.allTargets ? available : Math.Min(plan.targetCount, available);
    }

    // A value looked up by how many cards of an element are held (row 1..N; more cards use the last row).
    [Serializable]
    public struct HeldIntTable
    {
        public int[] byHeld;
        public HeldIntTable(params int[] byHeld) => this.byHeld = byHeld;
        public int Evaluate(int held) =>
            held <= 0 || byHeld == null || byHeld.Length == 0 ? 0 : byHeld[Math.Min(held, byHeld.Length) - 1];
    }

    [Serializable]
    public struct HeldFloatTable
    {
        public float[] byHeld;
        public HeldFloatTable(params float[] byHeld) => this.byHeld = byHeld;
        public float Evaluate(int held) =>
            held <= 0 || byHeld == null || byHeld.Length == 0 ? 0f : byHeld[Math.Min(held, byHeld.Length) - 1];
    }
}
