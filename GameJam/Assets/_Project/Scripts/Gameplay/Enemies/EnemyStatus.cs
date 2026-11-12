using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Burn and stun on one enemy, on the combat clock (rules in Pinball.Core.BurnStatus / StunStatus).
    // Burn ticks are driven by EnemyRegistry so every enemy ticks front first, before other timers.
    public sealed class EnemyStatus : MonoBehaviour
    {
        readonly BurnStatus<AttackRecord> burn = new BurnStatus<AttackRecord>();
        readonly StunStatus stun = new StunStatus();
        int passiveDps, activeDps;
        Enemy enemy;

        GameContext Context => enemy ? enemy.Context : null;
        double Now => Context != null ? Context.Clock.Now : 0;
        bool CanApply => enemy && enemy.Alive && Context != null && Context.Run.IsPlaying;
        StatusRules Rules => Context.config.status;

        public bool IsBurning => enemy && enemy.Alive && burn.IsBurning(Now);
        public bool IsStunned => enemy && enemy.Alive && stun.IsStunned(Now);
        public float StunRemaining => stun.Remaining(Now);
        public float PassiveImmunityRemaining => stun.PassiveImmunityRemaining(Now);
        public float PassiveBurnRemaining => burn.PassiveRemaining(Now);
        public float ActiveBurnRemaining => burn.ActiveRemaining(Now);

        internal double NextTickAt => enemy && enemy.Alive ? burn.NextTickAt : double.PositiveInfinity;

        internal void ResetFor(Enemy owner)
        {
            enemy = owner;
            burn.Clear();
            stun.Clear();
            passiveDps = activeDps = 0;
        }

        // Burn for `seconds` at `dps`. Passive (ball hit) and active (skill) burns run side by side;
        // each tick deals the higher DPS of the two that are still running.
        public void Ignite(bool active, float seconds, int dps, AttackRecord source)
        {
            if (!CanApply || seconds <= 0f) return;
            if (active) activeDps = dps; else passiveDps = dps;
            enemy.Context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.Ignite, null, enemy, transform.position, strong: active));
            burn.Ignite(Now, active, seconds, Rules.burnTickInterval, source);
        }

        // Returns true if the stun landed (passive stuns can be refused, see StunStatus).
        public bool Stun(float seconds, bool active) =>
            CanApply && stun.TryStun(Now, seconds, active, Rules.passiveStunImmunity);

        internal void Tick(double now)
        {
            if (!enemy || !enemy.Alive) return;
            if (!burn.Tick(now, Rules.burnTickInterval, passiveDps, activeDps, out int damage, out var source, out bool usedActive)) return;
            enemy.Context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.BurnTick, null, enemy, transform.position, strong: usedActive));
            enemy.Health.TakeDamage(damage, source);
        }
    }
}
