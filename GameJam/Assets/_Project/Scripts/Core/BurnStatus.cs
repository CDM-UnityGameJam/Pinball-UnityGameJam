using UnityEngine;

namespace Pinball.Core
{
    // Burn on one enemy. Port of the old Enemy.Ignite / TickStatus rules:
    // - A passive (ball hit) burn and an active (skill) burn run side by side, each with its own expiry.
    // - Re-igniting extends that kind's expiry only if the new one ends later.
    // - Ticks keep their phase: re-igniting never resets the next tick time.
    // - Each tick deals the higher DPS of the burns still running at that tick, never the sum.
    public sealed class BurnStatus<TSource> where TSource : class
    {
        double passiveUntil = double.NegativeInfinity, activeUntil = double.NegativeInfinity;
        TSource passiveSource, activeSource;

        public double NextTickAt { get; private set; } = double.PositiveInfinity;

        public bool IsBurning(double now) => passiveUntil > now || activeUntil > now;
        public float PassiveRemaining(double now) => (float)System.Math.Max(0, passiveUntil - now);
        public float ActiveRemaining(double now) => (float)System.Math.Max(0, activeUntil - now);

        public void Ignite(double now, bool active, float seconds, float tickInterval, TSource source)
        {
            if (seconds <= 0f) return;
            if (!IsBurning(now)) NextTickAt = now + Mathf.Max(0.01f, tickInterval);
            double until = now + seconds;
            if (active)
            {
                if (until >= activeUntil) { activeUntil = until; activeSource = source; }
            }
            else if (until >= passiveUntil) { passiveUntil = until; passiveSource = source; }
        }

        // Call when the combat clock reaches NextTickAt. Returns true, with the damage to apply and who
        // caused it, if a tick landed. Always schedules the following tick, or none once both burns expired.
        public bool Tick(double now, float tickInterval, int passiveDps, int activeDps, out int damage, out TSource source) =>
            Tick(now, tickInterval, passiveDps, activeDps, out damage, out source, out _);

        // Same, also reporting whether the tick used the active (skill) burn.
        public bool Tick(double now, float tickInterval, int passiveDps, int activeDps, out int damage, out TSource source, out bool usedActive)
        {
            damage = 0;
            source = null;
            usedActive = false;
            if (NextTickAt > now + RuleMath.TimeEpsilon) return false;

            bool strong = activeUntil >= NextTickAt - RuleMath.TimeEpsilon;
            bool weak = passiveUntil >= NextTickAt - RuleMath.TimeEpsilon;
            bool landed = strong || weak;
            if (landed)
            {
                bool useActive = strong && (!weak || activeDps >= passiveDps);
                usedActive = useActive;
                int dps = useActive ? activeDps : passiveDps;
                source = useActive ? activeSource : passiveSource;
                damage = RuleMath.RoundHalfUp(dps * tickInterval);
            }

            double following = NextTickAt + Mathf.Max(0.01f, tickInterval);
            NextTickAt = System.Math.Max(passiveUntil, activeUntil) > now + RuleMath.TimeEpsilon ? following : double.PositiveInfinity;
            return landed;
        }

        // The owner died or was despawned: no more ticks.
        public void Clear()
        {
            passiveUntil = activeUntil = double.NegativeInfinity;
            passiveSource = activeSource = null;
            NextTickAt = double.PositiveInfinity;
        }
    }
}
