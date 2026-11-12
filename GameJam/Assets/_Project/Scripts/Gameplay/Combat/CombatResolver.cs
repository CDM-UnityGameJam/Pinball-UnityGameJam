using System;

namespace Pinball.Gameplay
{
    // Runs held-card enchantments on every valid ball hit. Port of the old EquipmentResolver.BallHit:
    // elements in GameConfig order, and for each, every number in the luck range with at least one enabled
    // card; each element-number pair is independent. One AttackRecord covers the whole hit.
    // Enchantments never trigger from other damage, so they can't recurse.
    public sealed class CombatResolver
    {
        readonly GameContext context;

        // (enemy hit, contact point, source) after all enchantments resolved.
        public event Action<Enemy, UnityEngine.Vector3, AttackRecord> HitResolved;

        public CombatResolver(GameContext context)
        {
            this.context = context;
            context.Enemies.BallHit += OnBallHit;
        }

        void OnBallHit(Enemy direct, Ball ball, UnityEngine.Vector3 point)
        {
            if (!context.Run.IsPlaying || context.Hand.Resolving) return;
            context.Stats.validBallHits++;
            var source = new AttackRecord("Ball enchantments");
            var luck = context.config.luck;
            foreach (var element in context.config.elements)
            {
                if (!element || element.passive == null) continue;
                for (int number = luck.minNumber; number <= luck.maxNumber; number++)
                {
                    int stack = context.Hand.EnabledCount(element, number);
                    if (stack > 0) element.passive.Apply(new PassiveHit(context, element, direct, point, number, stack, source));
                }
            }
            HitResolved?.Invoke(direct, point, source);
        }
    }
}
