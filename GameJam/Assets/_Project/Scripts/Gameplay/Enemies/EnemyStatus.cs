using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Burn and stun on one enemy, on the combat clock (rules in Pinball.Core.BurnStatus / StunStatus).
    // Burn ticks are driven by EnemyRegistry so every enemy ticks front first, before other timers.
    // Enemies of the burning or stunning element are immune to it.
    public sealed class EnemyStatus : MonoBehaviour
    {
        readonly BurnStatus<AttackRecord> burn = new BurnStatus<AttackRecord>();
        readonly StunStatus stun = new StunStatus();
        ElementDefinition burnElement;
        Enemy enemy;

        GameContext Context => enemy ? enemy.Context : null;
        double Now => Context != null ? Context.Clock.Now : 0;
        bool CanApply(ElementDefinition element) => enemy && enemy.CanReceive(element) && Context != null && Context.Run.IsPlaying;
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
            burnElement = null;
        }

        // Burn for `seconds` at `dps`. Passive (ball hit) and active (skill) burns run side by side;
        // each tick deals the higher DPS of the two that are still running.
        public void Ignite(ElementDefinition element, bool active, float seconds, float dps, AttackRecord source)
        {
            if (!CanApply(element) || seconds <= 0f) return;
            burnElement = element;
            enemy.Context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.Ignite, element, enemy, transform.position, strong: active));
            burn.Ignite(Now, active, seconds, dps, Rules.burnTickInterval, source);
        }

        // Returns true if the stun landed (passive stuns can be refused, see StunStatus).
        public bool Stun(ElementDefinition element, float seconds, bool active)
        {
            if (!CanApply(element) || !stun.TryStun(Now, seconds, active, Rules.passiveStunImmunity)) return false;
            enemy.Context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.Stun, element, enemy, transform.position, strong: active));
            return true;
        }

        internal void Tick(double now)
        {
            if (!enemy || !enemy.Alive) return;
            if (!burn.Tick(now, Rules.burnTickInterval, out int damage, out var source, out bool usedActive)) return;
            enemy.Context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.BurnTick, burnElement, enemy, transform.position, strong: usedActive));
            enemy.TakeElementDamage(damage, burnElement, source);
        }
    }
}
