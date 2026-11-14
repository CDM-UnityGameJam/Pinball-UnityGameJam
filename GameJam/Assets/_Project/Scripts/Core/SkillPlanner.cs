using System;

namespace Pinball.Core
{
    // How a played group casts: the lucky number's group casts Lucky skills, every other group Normal ones.
    public enum CastTier
    {
        Normal,
        Lucky,
    }

    // How strongly one element's active skill fires at one tier.
    [Serializable]
    public struct ActiveTier
    {
        public bool allTargets;
        public int targetCount;
        public int waves;
        public int damage;
        public float burnSeconds;
        public float burnDps;
        public float stunSeconds;

        public ActiveTier(bool allTargets, int targetCount, int waves, int damage, float burnSeconds, float burnDps, float stunSeconds)
        {
            this.allTargets = allTargets; this.targetCount = targetCount; this.waves = waves; this.damage = damage;
            this.burnSeconds = burnSeconds; this.burnDps = burnDps; this.stunSeconds = stunSeconds;
        }
    }

    // The resolved strength of one element's cast in a group.
    public struct SkillPlan
    {
        public bool allTargets;
        public int targetCount, waves, damage;
        public float burnSeconds, burnDps, stunSeconds;
    }

    // Port of the v0.7 LuckTuning.Plan: the tier's row, plus a bonus for every extra card of the same element
    // in the group (more waves, longer burn).
    public static class SkillPlanner
    {
        public static SkillPlan Plan(ActiveTier row, int copies, int extraWavesPerCopy, float extraBurnSecondsPerCopy)
        {
            if (copies < 1) return default;
            int extra = copies - 1;
            return new SkillPlan
            {
                allTargets = row.allTargets,
                targetCount = Math.Max(1, row.targetCount),
                waves = (int)Math.Min(int.MaxValue, Math.Max(1L, row.waves + (long)extra * Math.Max(0, extraWavesPerCopy))),
                damage = Math.Max(0, row.damage),
                burnSeconds = row.burnSeconds + extra * Math.Max(0f, extraBurnSecondsPerCopy),
                burnDps = Math.Max(0f, row.burnDps),
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
