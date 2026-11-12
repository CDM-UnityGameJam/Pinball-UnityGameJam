using System;
using System.Collections.Generic;
using Pinball.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // Effect modules are small serializable classes picked per element in the Inspector
    // ([SerializeReference] + Odin's type picker). CombatResolver runs passives; CastScheduler runs actives.
    //
    // Passive (held) rules shared by all modules, from the v0.3 prototype:
    // - They trigger only on a valid ball hit, never from other damage, and never recurse.
    // - They only affect enemies whose number matches the card's number. "Stack" = how many cards of this
    //   element with that number are held; each element-number pair is resolved independently.
    // - A short-circuited element contributes nothing.
    // Active skills ignore numbers. They hit the targets locked in when the card was played (front first),
    // and their scale (targets, waves, burn, stun) comes from the element's Band Scaling for the rolled Q.

    // Everything a passive needs about one ball hit, for one element-number pair.
    public readonly struct PassiveHit
    {
        public readonly GameContext context;
        public readonly Enemy direct;      // the enemy the ball touched (may have died from the hit)
        public readonly Vector3 point;     // contact point
        public readonly int number;        // card number being resolved
        public readonly int stack;         // matching cards held (>= 1)
        public readonly AttackRecord source;
        public readonly ElementDefinition element;

        public PassiveHit(GameContext context, ElementDefinition element, Enemy direct, Vector3 point, int number, int stack, AttackRecord source)
        {
            this.context = context; this.element = element; this.direct = direct; this.point = point;
            this.number = number; this.stack = stack; this.source = source;
        }

        // Feedback for one enemy this enchantment touched.
        public void Touched(Enemy target) =>
            context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.PassiveHit, element, target, target.transform.position, point));
    }

    // One wave of an active skill.
    public readonly struct SkillWave
    {
        public readonly GameContext context;
        public readonly IReadOnlyList<Enemy> targets;
        public readonly SkillPlan plan;
        public readonly bool first;
        public readonly AttackRecord source;
        public readonly ElementDefinition element;
        public readonly int quality;

        public SkillWave(GameContext context, ElementDefinition element, IReadOnlyList<Enemy> targets, SkillPlan plan, bool first, int quality, AttackRecord source)
        {
            this.context = context; this.element = element; this.targets = targets; this.plan = plan;
            this.first = first; this.quality = quality; this.source = source;
        }

        // Feedback for one target of this wave. All-targets (high Q) plans play the strong version.
        public void Hit(Enemy target) =>
            context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.ActiveHit, element, target, target.transform.position, strong: plan.allTargets));
    }

    // An effect that runs on a valid ball hit while cards of its element are held.
    public interface IPassiveEffect
    {
        void Apply(in PassiveHit hit);

        // Card text: fills this module's tokens ({radius}, {damage}, {dps}, {seconds}...) in a template.
        string FillTokens(string template);

        // One line for "what holding `stack` matching cards does" (0 = enchantment lost).
        string Summary(int number, int stack);
    }

    // An effect that runs when a card of its element is played and casts (Q > 0), once per wave.
    public interface IActiveSkill
    {
        float WaveInterval { get; }
        void FireWave(in SkillWave wave);

        // True when the skill's progress reads as waves (Arc, Shockwave) rather than one effect (Spark).
        bool ShowsWaveProgress { get; }

        // Short description of a cast, e.g. "3 waves · Stun 2.8s".
        string Summary(SkillPlan plan);
    }

    static class EffectScratch
    {
        [ThreadStatic] static List<Enemy> list;
        public static List<Enemy> List => list ??= new List<Enemy>();
    }

    // Arc: damage jumps to nearby matching enemies (never the one the ball hit).
    [Serializable]
    public sealed class ChainDamagePassive : IPassiveEffect
    {
        [MinValue(0), SuffixLabel("m")] public float radius = 3f;
        [MinValue(0)] public int damage = 4;
        [Tooltip("Matching enemies hit, nearest first, by number of matching cards held.")]
        public StackedInt maxTargets = new StackedInt(2, 1, 0, 5);

        public void Apply(in PassiveHit hit)
        {
            var targets = hit.context.Enemies.Nearby(hit.point, radius, hit.number, hit.direct, EffectScratch.List);
            int count = Mathf.Min(maxTargets.Evaluate(hit.stack), targets.Count);
            for (int i = 0; i < count; i++)
            {
                hit.Touched(targets[i]);
                targets[i].Health.TakeDamage(damage, hit.source);
            }
        }

        public string FillTokens(string template) => template
            .Replace("{radius}", radius.ToString("0.#")).Replace("{damage}", damage.ToString());

        public string Summary(int number, int stack) => stack <= 0 ? $"#{number} enchantment lost"
            : $"{maxTargets.Evaluate(stack)} other matching foes in {radius:0.#}m: {damage} damage each";
    }

    // Spark: the hit enemy burns if it matches, plus extra nearby matching enemies.
    [Serializable]
    public sealed class IgnitePassive : IPassiveEffect
    {
        [Tooltip("Ignite the enemy the ball hit when its number matches.")]
        public bool igniteHitEnemy = true;
        [MinValue(0), SuffixLabel("m"), Tooltip("Search radius for extra targets.")]
        public float radius = 2.5f;
        [Tooltip("Extra matching enemies ignited, nearest first, by number of matching cards held.")]
        public StackedInt extraTargets = new StackedInt(0, 1, 0, 3);
        [MinValue(0), SuffixLabel("s")] public float burnSeconds = 3f;
        [MinValue(0)] public int burnDps = 4;

        public void Apply(in PassiveHit hit)
        {
            if (igniteHitEnemy && hit.direct && hit.direct.Alive && hit.direct.Number == hit.number)
            {
                hit.Touched(hit.direct);
                hit.direct.Status.Ignite(false, burnSeconds, burnDps, hit.source);
            }
            var targets = hit.context.Enemies.Nearby(hit.point, radius, hit.number, hit.direct, EffectScratch.List);
            int count = Mathf.Min(extraTargets.Evaluate(hit.stack), targets.Count);
            for (int i = 0; i < count; i++)
            {
                hit.Touched(targets[i]);
                targets[i].Status.Ignite(false, burnSeconds, burnDps, hit.source);
            }
        }

        public string FillTokens(string template) => template
            .Replace("{dps}", burnDps.ToString()).Replace("{seconds}", burnSeconds.ToString("0.#")).Replace("{radius}", radius.ToString("0.#"));

        public string Summary(int number, int stack) => stack <= 0 ? $"#{number} enchantment lost"
            : $"Ignite matching hit target + {extraTargets.Evaluate(stack)} nearby matches";
    }

    // Shockwave: stuns every matching enemy around the hit point, including the one hit.
    [Serializable]
    public sealed class StunPulsePassive : IPassiveEffect
    {
        [Tooltip("Pulse radius by number of matching cards held.")]
        public StackedFloat radius = new StackedFloat(1.2f, 0.25f);
        [MinValue(0), SuffixLabel("s")] public float stunSeconds = 0.35f;

        public void Apply(in PassiveHit hit)
        {
            float r = radius.Evaluate(hit.stack);
            bool affected = false;
            foreach (var e in hit.context.Enemies.Nearby(hit.point, r, hit.number, null, EffectScratch.List))
                affected |= e.Status.Stun(stunSeconds, false);
            if (affected)
                hit.context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.PassivePulse, hit.element, null, hit.point, radius: r));
        }

        public string FillTokens(string template) => template
            .Replace("{seconds}", stunSeconds.ToString("0.##")).Replace("{radius}", radius.baseValue.ToString("0.##"));

        public string Summary(int number, int stack) => stack <= 0 ? $"#{number} enchantment lost"
            : $"Matching foes in {radius.Evaluate(stack):0.##}m shockwave";
    }

    // Arc (Thunderstorm): damage every locked target once per wave.
    [Serializable]
    public sealed class WaveDamageSkill : IActiveSkill
    {
        [MinValue(0)] public int damagePerWave = 12;
        [MinValue(0.01), SuffixLabel("s")] public float waveInterval = 0.25f;

        public float WaveInterval => waveInterval;
        public bool ShowsWaveProgress => true;
        public string Summary(SkillPlan plan) => $"{plan.waves} waves";

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.targets)
            {
                if (!e || !e.Alive) continue;
                wave.Hit(e);
                e.Health.TakeDamage(damagePerWave, wave.source);
            }
        }
    }

    // Spark (Inferno): ignite every locked target; duration comes from Band Scaling's burn seconds.
    [Serializable]
    public sealed class BurnSkill : IActiveSkill
    {
        [MinValue(0)] public int burnDps = 6;

        public float WaveInterval => 0.01f;
        public bool ShowsWaveProgress => false;
        public string Summary(SkillPlan plan) => $"Burn {plan.burnSeconds:0.#}s";

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.targets) if (e && e.Alive) e.Status.Ignite(true, wave.plan.burnSeconds, burnDps, wave.source);
        }
    }

    // Shockwave (Impact): damage per wave; the first wave also stuns (Band Scaling's stun seconds).
    [Serializable]
    public sealed class StunWaveSkill : IActiveSkill
    {
        [MinValue(0)] public int damagePerWave = 8;
        [MinValue(0.01), SuffixLabel("s")] public float waveInterval = 0.35f;
        public bool stunOnFirstWave = true;

        public float WaveInterval => waveInterval;
        public bool ShowsWaveProgress => true;
        public string Summary(SkillPlan plan) => $"{plan.waves} waves" + (stunOnFirstWave ? $" · Stun {plan.stunSeconds:0.#}s" : "");

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.targets)
            {
                if (!e || !e.Alive) continue;
                if (stunOnFirstWave && wave.first) e.Status.Stun(wave.plan.stunSeconds, true);
                wave.Hit(e);
                e.Health.TakeDamage(damagePerWave, wave.source);
            }
        }
    }
}
