using System;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Resolves every new ball contact with an enemy. Port of the v0.6 GameManager.RegisterBallHit:
    // - A contact that passes the enemy's damage cooldown is a hit: it counts for the combo, deals the ball's
    //   damage (empowered damage if an empowered hit is stocked, using one), then runs every held element's
    //   enchantment in GameConfig order. The hit and its enchantments share one AttackRecord (so two kills
    //   between them are a multi-kill), including later burn ticks they cause.
    // - Every new contact, hit or not, then earns contact charge if a card is missing, so cards drawn by it
    //   never take part in that contact.
    public sealed class CombatResolver
    {
        readonly GameContext context;

        // (enemy hit, contact point, source) after the hit and its enchantments resolved.
        public event Action<Enemy, Vector3, AttackRecord> HitResolved;

        public CombatResolver(GameContext context)
        {
            this.context = context;
            context.Enemies.BallContact += OnContact;
        }

        void OnContact(Enemy direct, Ball ball, Vector3 point, bool damageAllowed)
        {
            var hand = context.Hand;
            if (!context.Run.IsPlaying || hand.Resolving) return;
            bool missing = !hand.IsFull;
            context.Stats.ballContacts++;
            hand.Resolving = true;
            try
            {
                if (damageAllowed)
                {
                    context.Score.CountComboHit();
                    var source = new AttackRecord("Ball hit");
                    bool empowered = hand.ConsumeEmpoweredHit();
                    int damage = empowered ? context.config.empoweredHitDamage : context.config.ballHitDamage;
                    direct.Health.TakeDamage(damage, source);
                    context.Stats.validBallHits++;
                    foreach (var element in context.config.elements)
                    {
                        if (!element || element.passive == null) continue;
                        int held = hand.HeldCount(element);
                        if (held > 0) element.passive.Apply(new PassiveHit(context, element, direct, point, held, source));
                    }
                    HitResolved?.Invoke(direct, point, source);
                }
                else context.Stats.cooldownContacts++;
            }
            finally { hand.Resolving = false; }
            if (missing) hand.EarnContactCharge(point);
        }
    }
}
