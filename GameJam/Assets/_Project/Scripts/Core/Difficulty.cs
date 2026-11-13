using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    // One row of the difficulty table: multipliers reached at `time` seconds of combat.
    [Serializable]
    public struct DifficultyStage
    {
        public float time, spawnMultiplier, moveMultiplier, healthMultiplier;

        public DifficultyStage(float time, float spawnMultiplier, float moveMultiplier, float healthMultiplier)
        {
            this.time = time; this.spawnMultiplier = spawnMultiplier;
            this.moveMultiplier = moveMultiplier; this.healthMultiplier = healthMultiplier;
        }
    }

    public struct DifficultySample
    {
        public double spawnMultiplier, moveMultiplier, healthMultiplier;
    }

    // Combat-time difficulty (port of the v0.6 DifficultyRules):
    // - Multipliers are linearly interpolated between stages and hold at the last stage.
    // - Spawn interval = base / spawn multiplier; march speed = distance / base descent × move multiplier.
    // - New enemies get round-half-up(base health × health multiplier), at least 1; living enemies never change.
    public static class DifficultyCurve
    {
        public static DifficultySample Sample(IReadOnlyList<DifficultyStage> stages, double time)
        {
            if (stages == null || stages.Count == 0) return new DifficultySample { spawnMultiplier = 1, moveMultiplier = 1, healthMultiplier = 1 };
            time = double.IsNaN(time) || double.IsInfinity(time) ? 0 : Math.Max(0, time);
            var a = stages[0];
            var b = a;
            double t = 0;
            for (int i = 1; i < stages.Count; i++)
            {
                b = stages[i];
                if (time <= b.time)
                {
                    t = b.time > a.time ? Math.Max(0, Math.Min(1, (time - a.time) / (b.time - a.time))) : 1;
                    break;
                }
                a = b;
            }
            if (time >= stages[stages.Count - 1].time) { a = b = stages[stages.Count - 1]; t = 0; }
            return new DifficultySample
            {
                spawnMultiplier = Clamp(a.spawnMultiplier + (b.spawnMultiplier - a.spawnMultiplier) * t),
                moveMultiplier = Clamp(a.moveMultiplier + (b.moveMultiplier - a.moveMultiplier) * t),
                healthMultiplier = Clamp(a.healthMultiplier + (b.healthMultiplier - a.healthMultiplier) * t),
            };
        }

        public static int Health(int baseHealth, double multiplier) =>
            (int)Math.Max(1, Math.Min(int.MaxValue, Math.Floor(Math.Max(1, baseHealth) * (RuleMath.Positive(multiplier) ? multiplier : 1) + 0.5)));

        // Returns null when valid, otherwise what's wrong.
        public static string Check(IReadOnlyList<DifficultyStage> stages)
        {
            if (stages == null || stages.Count == 0) return "Add at least one stage.";
            for (int i = 0; i < stages.Count; i++)
            {
                var s = stages[i];
                if (!RuleMath.NonNegative(s.time)) return $"Stage {i + 1}: time must be zero or more.";
                if (i > 0 && s.time <= stages[i - 1].time) return $"Stage {i + 1}: time must be later than the stage before.";
                if (!RuleMath.Positive(s.spawnMultiplier) || !RuleMath.Positive(s.moveMultiplier) || !RuleMath.Positive(s.healthMultiplier))
                    return $"Stage {i + 1}: multipliers must be above zero.";
            }
            return null;
        }

        static double Clamp(double value) => Math.Max(1e-6, Math.Min(1e6, value));
    }

    // Spawn cadence on the combat clock: progress += elapsed / interval, one attempt per whole unit.
    // A failed or blocked attempt still uses its unit, so nothing backs up.
    public sealed class SpawnPhase
    {
        public double Value { get; private set; }

        public int Advance(double elapsed, double interval)
        {
            if (!RuleMath.Positive(elapsed) || !RuleMath.Positive(interval)) return 0;
            Value += elapsed / interval;
            int attempts = 0;
            while (Value >= 1 - 1e-10)
            {
                Value = Math.Max(0, Value - 1);
                attempts++;
            }
            return attempts;
        }
    }
}
