using System;

namespace Pinball.Core
{
    // Burn on one enemy. Port of the v0.6 Enemy.Ignite / TickStatus rules:
    // - A passive (ball hit) burn and an active (skill) burn run side by side, each with its own expiry.
    // - Re-igniting extends that kind's expiry only if the new one ends later (and then takes its DPS).
    // - Ticks keep their phase: re-igniting never resets the next tick time.
    // - Each tick uses the higher DPS of the burns still running at that tick, never the sum.
    // - DPS can be fractional: each tick adds dps × interval to one accumulator per continuous burn and deals
    //   its whole points, carrying the remainder. A burn that fully goes out starts a fresh accumulator.
    public sealed class BurnStatus<TSource> where TSource : class
    {
        double passiveUntil = double.NegativeInfinity, activeUntil = double.NegativeInfinity;
        float passiveDps, activeDps;
        TSource passiveSource, activeSource;
        double remainder;

        public double NextTickAt { get; private set; } = double.PositiveInfinity;

        public bool IsBurning(double now) => passiveUntil > now || activeUntil > now;
        public float PassiveRemaining(double now) => (float)Math.Max(0, passiveUntil - now);
        public float ActiveRemaining(double now) => (float)Math.Max(0, activeUntil - now);

        public void Ignite(double now, bool active, float seconds, float dps, float tickInterval, TSource source)
        {
            if (seconds <= 0f) return;
            if (!IsBurning(now))
            {
                NextTickAt = now + Math.Max(0.000001, tickInterval);
                remainder = 0;
            }
            double until = now + seconds;
            if (active)
            {
                if (until >= activeUntil) { activeUntil = until; activeSource = source; activeDps = dps; }
            }
            else if (until >= passiveUntil) { passiveUntil = until; passiveSource = source; passiveDps = dps; }
        }

        // Call when the combat clock reaches NextTickAt. Returns true, with the whole damage to apply and who
        // caused it, if a tick dealt damage. Always schedules the following tick, or none once both burns ended.
        public bool Tick(double now, float tickInterval, out int damage, out TSource source, out bool usedActive)
        {
            damage = 0;
            source = null;
            usedActive = false;
            if (NextTickAt > now + RuleMath.TimeEpsilon) return false;

            bool strong = activeUntil >= NextTickAt - RuleMath.TimeEpsilon;
            bool weak = passiveUntil >= NextTickAt - RuleMath.TimeEpsilon;
            if (strong || weak)
            {
                usedActive = strong && (!weak || activeDps >= passiveDps);
                float dps = usedActive ? activeDps : passiveDps;
                source = usedActive ? activeSource : passiveSource;
                damage = Accumulate((double)dps * tickInterval);
            }

            double following = NextTickAt + Math.Max(0.000001, tickInterval);
            NextTickAt = Math.Max(passiveUntil, activeUntil) > now + RuleMath.TimeEpsilon ? following : double.PositiveInfinity;
            return damage > 0;
        }

        int Accumulate(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0) return 0;
            double total = remainder + amount;
            int whole = WholeDamage(total);
            remainder = Math.Max(0, total - whole);
            return whole;
        }

        public static int WholeDamage(double amount) => (int)Math.Max(0, Math.Min(int.MaxValue, Math.Floor(amount + 1e-7)));

        // The owner died or was despawned: no more ticks.
        public void Clear()
        {
            passiveUntil = activeUntil = double.NegativeInfinity;
            passiveSource = activeSource = null;
            passiveDps = activeDps = 0f;
            remainder = 0;
            NextTickAt = double.PositiveInfinity;
        }
    }
}
