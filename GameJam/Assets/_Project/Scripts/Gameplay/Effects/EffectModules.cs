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
    // Passive (held) rules shared by all modules, from the v0.6 prototype:
    // - They trigger only on a ball hit that deals damage, never from other damage, and never recurse.
    // - Held cards combine by element whatever their number: "held" = how many cards of this element are held.
    // - Enemies of the element are immune; targets are picked after filtering them out.
    // Active skills hit the targets locked when the batch was played; their scale comes from the dice tier.

    // Everything a passive needs about one ball hit, for one element.
    public readonly struct PassiveHit
    {
        public readonly GameContext context;
        public readonly Enemy direct;      // the enemy the ball touched (may have died from the hit)
        public readonly Vector3 point;     // contact point
        public readonly int held;          // cards of this element held (>= 1)
        public readonly AttackRecord source;
        public readonly ElementDefinition element;

        public PassiveHit(GameContext context, ElementDefinition element, Enemy direct, Vector3 point, int held, AttackRecord source)
        {
            this.context = context; this.element = element; this.direct = direct; this.point = point;
            this.held = held; this.source = source;
        }

        // Feedback for one enemy this enchantment touched.
        public void Touched(Enemy target) =>
            context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.PassiveHit, element, target, target.transform.position, point));

        // Living enemies in `radius` that this element can affect, nearest first.
        public List<Enemy> Nearby(float radius, Enemy except) =>
            context.Enemies.Nearby(point, radius, element, except, EffectScratch.List);
    }

    // One wave of an element's active skill in a played batch.
    public readonly struct SkillWave
    {
        public readonly GameContext context;
        public readonly SkillCast cast;
        public readonly bool first;

        public SkillWave(GameContext context, SkillCast cast, bool first)
        {
            this.context = context; this.cast = cast; this.first = first;
        }

        public ElementDefinition Element => cast.element;
        public SkillPlan Plan => cast.plan;
        public AttackRecord Source => cast.batch;

        // Locked targets this element can still affect (alive, not of its element).
        public IEnumerable<Enemy> Targets
        {
            get { foreach (var e in cast.targets) if (e && e.CanReceive(cast.element)) yield return e; }
        }

        // Count one target as reached by the batch and play its hit feedback. Full Field and Jackpot play strong.
        public void Hit(Enemy target)
        {
            cast.batch.Cover(target);
            context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.ActiveHit, cast.element, target, target.transform.position,
                strong: cast.batch.tier >= CastTier.FullField));
        }
    }

    // An effect that runs on a ball hit while cards of its element are held.
    public interface IPassiveEffect
    {
        void Apply(in PassiveHit hit);

        // Card text: fills this module's tokens ({radius}, {damage}, {dps}, {seconds}...) in a template.
        string FillTokens(string template, int held);

        // One line for "what holding `held` cards of this element does" (0 = enchantment lost).
        string Summary(int held);
    }

    // An effect that runs when a batch with cards of its element reveals, once per wave.
    public interface IActiveSkill
    {
        float WaveInterval { get; }
        void FireWave(in SkillWave wave);

        // True when the skill's progress reads as waves (Arc) rather than one effect (Spark, Shockwave).
        bool ShowsWaveProgress { get; }

        // Short description of a cast, e.g. "3 waves × 20".
        string Summary(SkillPlan plan);
    }

    static class EffectScratch
    {
        [ThreadStatic] static List<Enemy> list;
        public static List<Enemy> List => list ??= new List<Enemy>();
    }

    // Arc: damages the hit enemy, then jumps to nearby enemies (the hit one doesn't use a jump).
    [Serializable]
    public sealed class ChainDamagePassive : IPassiveEffect
    {
        [MinValue(0), SuffixLabel("m")] public float radius = 5f;
        [MinValue(0)] public int damage = 4;
        [Tooltip("Also damage the enemy the ball hit.")]
        public bool damageHitEnemy = true;
        [Tooltip("Nearby enemies hit, nearest first, by how many cards of this element are held (row 1 = one card).")]
        public HeldIntTable nearbyTargets = new HeldIntTable(3, 4, 6, 8, 13);

        public void Apply(in PassiveHit hit)
        {
            if (damageHitEnemy && hit.direct && hit.direct.CanReceive(hit.element))
            {
                hit.Touched(hit.direct);
                hit.direct.TakeElementDamage(damage, hit.element, hit.source);
            }
            var targets = hit.Nearby(radius, hit.direct);
            int count = Mathf.Min(nearbyTargets.Evaluate(hit.held), targets.Count);
            for (int i = 0; i < count; i++)
            {
                hit.Touched(targets[i]);
                targets[i].TakeElementDamage(damage, hit.element, hit.source);
            }
        }

        public string FillTokens(string template, int held) => template
            .Replace("{radius}", radius.ToString("0.#")).Replace("{damage}", damage.ToString())
            .Replace("{targets}", nearbyTargets.Evaluate(Mathf.Max(1, held)).ToString());

        public string Summary(int held) => held <= 0 ? "Enchantment lost"
            : $"Hit foe + {nearbyTargets.Evaluate(held)} nearby · {radius:0.#}m · {damage} damage";
    }

    // Spark: the hit enemy burns, plus nearby enemies. Burn strength grows with cards held.
    [Serializable]
    public sealed class IgnitePassive : IPassiveEffect
    {
        [Tooltip("Ignite the enemy the ball hit.")]
        public bool igniteHitEnemy = true;
        [MinValue(0), SuffixLabel("m"), Tooltip("Search radius for extra targets.")]
        public float radius = 5f;
        [Tooltip("Extra nearby enemies ignited, nearest first, by cards held.")]
        public HeldIntTable extraTargets = new HeldIntTable(2, 3, 4, 5, 6);
        [MinValue(0), SuffixLabel("s")] public float burnSeconds = 6f;
        [Tooltip("Burn damage per second by cards held. Fractions add up across ticks.")]
        public HeldFloatTable burnDps = new HeldFloatTable(0.5f, 0.75f, 1.5f, 4f, 10f);

        public void Apply(in PassiveHit hit)
        {
            float dps = burnDps.Evaluate(hit.held);
            if (igniteHitEnemy && hit.direct && hit.direct.CanReceive(hit.element))
            {
                hit.Touched(hit.direct);
                hit.direct.Status.Ignite(hit.element, false, burnSeconds, dps, hit.source);
            }
            var targets = hit.Nearby(radius, hit.direct);
            int count = Mathf.Min(extraTargets.Evaluate(hit.held), targets.Count);
            for (int i = 0; i < count; i++)
            {
                hit.Touched(targets[i]);
                targets[i].Status.Ignite(hit.element, false, burnSeconds, dps, hit.source);
            }
        }

        public string FillTokens(string template, int held) => template
            .Replace("{dps}", burnDps.Evaluate(Mathf.Max(1, held)).ToString("0.###")).Replace("{seconds}", burnSeconds.ToString("0.#"))
            .Replace("{radius}", radius.ToString("0.#")).Replace("{targets}", extraTargets.Evaluate(Mathf.Max(1, held)).ToString());

        public string Summary(int held) => held <= 0 ? "Enchantment lost"
            : $"Hit foe + {extraTargets.Evaluate(held)} nearby · {radius:0.#}m · {burnDps.Evaluate(held):0.###} DPS · {burnSeconds:0.#}s";
    }

    // Shockwave: stuns every enemy around the hit point, including the one hit. No damage.
    [Serializable]
    public sealed class StunPulsePassive : IPassiveEffect
    {
        [Tooltip("Pulse radius by cards held.")]
        public HeldFloatTable radius = new HeldFloatTable(3f, 4f, 6f, 8f, 10f);
        [MinValue(0), SuffixLabel("s")] public float stunSeconds = 0.8f;

        public void Apply(in PassiveHit hit)
        {
            float r = radius.Evaluate(hit.held);
            bool affected = false;
            foreach (var e in hit.Nearby(r, null)) affected |= e.Status.Stun(hit.element, stunSeconds, false);
            if (affected)
                hit.context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.PassivePulse, hit.element, null, hit.point, radius: r));
        }

        public string FillTokens(string template, int held) => template
            .Replace("{seconds}", stunSeconds.ToString("0.##")).Replace("{radius}", radius.Evaluate(Mathf.Max(1, held)).ToString("0.##"));

        public string Summary(int held) => held <= 0 ? "Enchantment lost"
            : $"{radius.Evaluate(held):0.##}m · stun {stunSeconds:0.##}s · no damage";
    }

    // Arc (Thunderstorm): damage every locked target once per wave.
    [Serializable]
    public sealed class WaveDamageSkill : IActiveSkill
    {
        [MinValue(0)] public int damagePerWave = 20;
        [MinValue(0.01), SuffixLabel("s")] public float waveInterval = 0.25f;

        public float WaveInterval => waveInterval;
        public bool ShowsWaveProgress => true;
        public string Summary(SkillPlan plan) => $"{plan.waves} waves × {damagePerWave}";

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.Targets)
            {
                wave.Hit(e);
                e.TakeElementDamage(damagePerWave, wave.Element, wave.Source);
            }
        }
    }

    // Spark (Inferno): ignite every locked target for the tier's burn seconds.
    [Serializable]
    public sealed class BurnSkill : IActiveSkill
    {
        [MinValue(0)] public float burnDps = 4f;

        public float WaveInterval => 0.01f;
        public bool ShowsWaveProgress => false;
        public string Summary(SkillPlan plan) => $"Burn {plan.burnSeconds:0.#}s · {burnDps:0.##} DPS";

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.Targets)
            {
                wave.Hit(e);
                e.Status.Ignite(wave.Element, true, wave.Plan.burnSeconds, burnDps, wave.Source);
            }
        }
    }

    // Shockwave (Impact): damage per wave; the first wave also stuns for the tier's stun seconds.
    [Serializable]
    public sealed class StunWaveSkill : IActiveSkill
    {
        [MinValue(0)] public int damagePerWave = 15;
        [MinValue(0.01), SuffixLabel("s")] public float waveInterval = 0.35f;
        public bool stunOnFirstWave = true;

        public float WaveInterval => waveInterval;
        public bool ShowsWaveProgress => false;
        public string Summary(SkillPlan plan) => $"{damagePerWave} damage" + (stunOnFirstWave ? $" · Stun {plan.stunSeconds:0.#}s" : "");

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.Targets)
            {
                if (stunOnFirstWave && wave.first) e.Status.Stun(wave.Element, wave.Plan.stunSeconds, true);
                wave.Hit(e);
                e.TakeElementDamage(damagePerWave, wave.Element, wave.Source);
            }
        }
    }
}
