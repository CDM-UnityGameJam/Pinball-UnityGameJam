namespace Pinball.Core
{
    // Stun on one enemy. Port of the old Enemy.Stun rules:
    // - A passive (ball hit) stun can't refresh an ongoing stun, and can't land during immunity.
    // - An active (skill) stun always lands and can extend the stun; repeats take the max, never the sum.
    // - Every stun ends with a passive-immunity window that starts when the stun ends.
    public sealed class StunStatus
    {
        double stunUntil = double.NegativeInfinity, immuneUntil = double.NegativeInfinity;

        public bool IsStunned(double now) => stunUntil > now + RuleMath.TimeEpsilon;
        public float Remaining(double now) => (float)System.Math.Max(0, stunUntil - now);
        public float PassiveImmunityRemaining(double now) => IsStunned(now) ? 0f : (float)System.Math.Max(0, immuneUntil - now);

        public bool TryStun(double now, float seconds, bool active, float passiveImmunitySeconds)
        {
            if (seconds <= 0f) return false;
            if (!active && (IsStunned(now) || now < immuneUntil - RuleMath.TimeEpsilon)) return false;
            stunUntil = System.Math.Max(stunUntil, now + seconds);
            immuneUntil = System.Math.Max(immuneUntil, stunUntil + passiveImmunitySeconds);
            return true;
        }

        public void Clear() => stunUntil = immuneUntil = double.NegativeInfinity;
    }
}
