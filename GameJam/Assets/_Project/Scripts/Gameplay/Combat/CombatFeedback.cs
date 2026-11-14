using System;
using UnityEngine;

namespace Pinball.Gameplay
{
    public enum FeedbackKind
    {
        PassiveHit,   // an enchantment touched one enemy (arc jump, spark ignite...)
        PassivePulse, // an enchantment went off as an area (shockwave), at least one enemy affected
        ActiveHit,    // one wave of an active skill hit one enemy
        Ignite,       // an enemy started or refreshed burning
        BurnTick,     // an enemy took burn damage
        Stun,         // a stun landed on an enemy (new or extended)
    }

    // What happened, where. Presentation turns these into VFX and SFX; gameplay never plays effects itself.
    public readonly struct CombatFeedbackEvent
    {
        public readonly FeedbackKind kind;
        public readonly ElementDefinition element; // the status's element for Ignite, BurnTick and Stun
        public readonly Enemy target;              // null for PassivePulse
        public readonly Vector3 position;          // the target, or the pulse centre
        public readonly Vector3? from;             // where the effect came from (the ball contact for passives)
        public readonly float radius;              // PassivePulse radius
        public readonly bool strong;               // active ignite/burn/stun, or a Lucky cast's hit

        public CombatFeedbackEvent(FeedbackKind kind, ElementDefinition element, Enemy target, Vector3 position,
            Vector3? from = null, float radius = 0f, bool strong = false)
        {
            this.kind = kind; this.element = element; this.target = target; this.position = position;
            this.from = from; this.radius = radius; this.strong = strong;
        }
    }

    // Hub for combat feedback events (owned by GameContext).
    public sealed class CombatFeedback
    {
        public event Action<CombatFeedbackEvent> Raised;

        internal void Raise(in CombatFeedbackEvent e) => Raised?.Invoke(e);
    }
}
