using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    [Serializable]
    public struct ComboTier
    {
        public int hits, points;
        public ComboTier(int hits, int points) { this.hits = hits; this.points = points; }
    }

    // Time and combo scoring (port of the v0.6 ScoreRules / ScoreAccount), on combat time only:
    // - Every `timeInterval` seconds of combat pays `timePoints`.
    // - Each physical ball hit that passes the enemy's damage cooldown adds one combo hit. `comboTimeout`
    //   seconds without one ends the combo and pays the highest tier reached, once. Game over pays it too.
    public sealed class ComboScore
    {
        double timeProgress, lastHit;

        public int Combo { get; private set; }

        public double Remaining(double now, float comboTimeout) => Combo == 0 ? 0 : Math.Max(0, comboTimeout - (now - lastHit));

        // Advance by one combat step ending at `now`. Returns time points; `comboReward` is a finished combo's pay.
        public long Tick(double elapsed, double now, float timeInterval, int timePoints, float comboTimeout, IReadOnlyList<ComboTier> tiers, out int comboReward)
        {
            comboReward = Combo > 0 && now - lastHit >= comboTimeout - 1e-7 ? Finish(tiers) : 0;
            if (!RuleMath.Positive(timeInterval)) return 0;
            timeProgress += Math.Max(0, elapsed);
            double count = Math.Floor((timeProgress + 1e-7) / timeInterval);
            timeProgress = Math.Max(0, timeProgress - count * timeInterval);
            if (timePoints <= 0) return 0;
            return count >= long.MaxValue / (double)timePoints ? long.MaxValue : (long)count * timePoints;
        }

        // A counted hit at `now`. Returns the pay of a combo that had already timed out, if any.
        public int Hit(double now, float comboTimeout, IReadOnlyList<ComboTier> tiers)
        {
            int reward = Combo > 0 && now - lastHit >= comboTimeout - 1e-7 ? Finish(tiers) : 0;
            if (Combo < int.MaxValue) Combo++;
            lastHit = now;
            return reward;
        }

        public int Finish(IReadOnlyList<ComboTier> tiers)
        {
            int reward = Reward(tiers, Combo);
            Combo = 0;
            return reward;
        }

        // Points for the highest tier `hits` reaches (tiers in increasing order).
        public static int Reward(IReadOnlyList<ComboTier> tiers, int hits)
        {
            int points = 0;
            if (tiers == null) return 0;
            foreach (var t in tiers)
            {
                if (hits < t.hits) break;
                points = t.points;
            }
            return points;
        }

        // Returns null when valid, otherwise what's wrong.
        public static string Check(IReadOnlyList<ComboTier> tiers)
        {
            if (tiers == null || tiers.Count == 0) return "Add at least one combo tier.";
            int previous = 0;
            foreach (var t in tiers)
            {
                if (t.hits <= previous) return "Combo hit thresholds must be above zero and increasing.";
                if (t.points < 0) return "Combo points can't be negative.";
                previous = t.hits;
            }
            return null;
        }
    }
}
