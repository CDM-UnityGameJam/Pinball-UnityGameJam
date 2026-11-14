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
    // - Held cards of the lucky number add the module's lucky bonus once per such card ("lucky").
    // Active skills hit the targets locked when the group was played; their numbers come from the element's
    // Normal or Lucky row (SkillPlan).

    // Everything a passive needs about one ball hit, for one element.
    public readonly struct PassiveHit
    {
        public readonly GameContext context;
        public readonly Enemy direct;      // the enemy the ball touched (may have died from the hit)
        public readonly Vector3 point;     // contact point
        public readonly int held;          // cards of this element held (>= 1)
        public readonly int lucky;         // of those, cards with the lucky number
        public readonly AttackRecord source;
        public readonly ElementDefinition element;

        public PassiveHit(GameContext context, ElementDefinition element, Enemy direct, Vector3 point, int held, int lucky, AttackRecord source)
        {
            this.context = context; this.element = element; this.direct = direct; this.point = point;
            this.held = held; this.lucky = lucky; this.source = source;
        }

        // Feedback for one enemy this enchantment touched.
        public void Touched(Enemy target) =>
            context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.PassiveHit, element, target, target.transform.position, point));

        // Living enemies in `radius` that this element can affect, nearest first.
        public List<Enemy> Nearby(float radius, Enemy except) =>
            context.Enemies.Nearby(point, radius, element, except, EffectScratch.List);
    }

    // One wave of an element's active skill in a played group.
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

        // Count one target as reached by the group and play its hit feedback. Strong = a Lucky cast.
        public void Hit(Enemy target)
        {
            cast.batch.Cover(target);
            context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.ActiveHit, cast.element, target, target.transform.position,
                strong: cast.batch.Lucky));
        }
    }

    // An effect that runs on a ball hit while cards of its element are held.
    public interface IPassiveEffect
    {
        void Apply(in PassiveHit hit);

        // Card text: fills this module's tokens ({radius}, {damage}, {dps}, {seconds}...) in a template.
        string FillTokens(string template, int held, int lucky = 0);

        // One line for "what holding `held` cards of this element (`lucky` of them lucky) does" (0 = enchantment lost).
        string Summary(int held, int lucky = 0);
    }

    // An effect that runs when a group with cards of its element is cast, once per wave.
    public interface IActiveSkill
    {
        float WaveInterval { get; }
        void FireWave(in SkillWave wave);

        // True when the skill's progress reads as waves (Arc) rather than one effect (Spark, Shockwave).
        bool ShowsWaveProgress { get; }

        // Short description of a cast, e.g. "3 waves × 40".
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
        [MinValue(0), Tooltip("Extra damage for every held lucky-number card of this element.")]
        public int luckyDamageBonus = 2;
        [Tooltip("Also damage the enemy the ball hit.")]
        public bool damageHitEnemy = true;
        [Tooltip("Nearby enemies hit, nearest first, by how many cards of this element are held (row 1 = one card).")]
        public HeldIntTable nearbyTargets = new HeldIntTable(5, 7, 10, 15, 25);

        public int Damage(int lucky) => (int)Math.Min(int.MaxValue, damage + (long)Math.Max(0, lucky) * luckyDamageBonus);

        public void Apply(in PassiveHit hit)
        {
            int damage = Damage(hit.lucky);
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

        public string FillTokens(string template, int held, int lucky = 0) => template
            .Replace("{radius}", radius.ToString("0.#")).Replace("{damage}", Damage(lucky).ToString())
            .Replace("{targets}", nearbyTargets.Evaluate(Mathf.Max(1, held)).ToString());

        public string Summary(int held, int lucky = 0) => held <= 0 ? "Enchantment lost"
            : $"Hit foe + {nearbyTargets.Evaluate(held)} nearby · {radius:0.#}m · {Damage(lucky)} damage";
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
        public HeldIntTable extraTargets = new HeldIntTable(5, 7, 10, 15, 25);
        [MinValue(0), SuffixLabel("s")] public float burnSeconds = 6f;
        [Tooltip("Burn damage per second by cards held. Fractions add up across ticks.")]
        public HeldFloatTable burnDps = new HeldFloatTable(0.5f, 0.75f, 1.5f, 4f, 5f);
        [MinValue(0), SuffixLabel("/s"), Tooltip("Extra burn DPS for every held lucky-number card of this element.")]
        public float luckyDpsBonus = 1f;

        public float Dps(int held, int lucky) => burnDps.Evaluate(held) + Mathf.Max(0, lucky) * luckyDpsBonus;

        public void Apply(in PassiveHit hit)
        {
            float dps = Dps(hit.held, hit.lucky);
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

        public string FillTokens(string template, int held, int lucky = 0) => template
            .Replace("{dps}", Dps(Mathf.Max(1, held), lucky).ToString("0.###")).Replace("{seconds}", burnSeconds.ToString("0.#"))
            .Replace("{radius}", radius.ToString("0.#")).Replace("{targets}", extraTargets.Evaluate(Mathf.Max(1, held)).ToString());

        public string Summary(int held, int lucky = 0) => held <= 0 ? "Enchantment lost"
            : $"Hit foe + {extraTargets.Evaluate(held)} nearby · {radius:0.#}m · {Dps(held, lucky):0.###} DPS · {burnSeconds:0.#}s";
    }

    // Shockwave: stuns every enemy around the hit point, including the one hit. No damage.
    [Serializable]
    public sealed class StunPulsePassive : IPassiveEffect
    {
        [Tooltip("Pulse radius by cards held.")]
        public HeldFloatTable radius = new HeldFloatTable(4f, 5f, 5.5f, 6f, 7f);
        [MinValue(0), SuffixLabel("s")] public float stunSeconds = 1f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Extra stun seconds for every held lucky-number card of this element.")]
        public float luckyStunBonus = 0.25f;

        public float Seconds(int lucky) => stunSeconds + Mathf.Max(0, lucky) * luckyStunBonus;

        public void Apply(in PassiveHit hit)
        {
            float r = radius.Evaluate(hit.held);
            float seconds = Seconds(hit.lucky);
            bool affected = false;
            foreach (var e in hit.Nearby(r, null)) affected |= e.Status.Stun(hit.element, seconds, false);
            if (affected)
                hit.context.Feedback.Raise(new CombatFeedbackEvent(FeedbackKind.PassivePulse, hit.element, null, hit.point, radius: r));
        }

        public string FillTokens(string template, int held, int lucky = 0) => template
            .Replace("{seconds}", Seconds(lucky).ToString("0.##")).Replace("{radius}", radius.Evaluate(Mathf.Max(1, held)).ToString("0.##"));

        public string Summary(int held, int lucky = 0) => held <= 0 ? "Enchantment lost"
            : $"{radius.Evaluate(held):0.##}m · stun {Seconds(lucky):0.##}s · no damage";
    }

    // Arc (Thunderstorm): damage every locked target once per wave (the plan's damage).
    [Serializable]
    public sealed class WaveDamageSkill : IActiveSkill
    {
        [MinValue(0.01), SuffixLabel("s")] public float waveInterval = 0.25f;

        public float WaveInterval => waveInterval;
        public bool ShowsWaveProgress => true;
        public string Summary(SkillPlan plan) => $"{plan.waves} waves × {plan.damage}";

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.Targets)
            {
                wave.Hit(e);
                e.TakeElementDamage(wave.Plan.damage, wave.Element, wave.Source);
            }
        }
    }

    // Spark (Inferno): ignite every locked target for the plan's burn seconds at its DPS.
    [Serializable]
    public sealed class BurnSkill : IActiveSkill
    {
        public float WaveInterval => 0.01f;
        public bool ShowsWaveProgress => false;
        public string Summary(SkillPlan plan) => $"Burn {plan.burnSeconds:0.#}s · {plan.burnDps:0.##} DPS";

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.Targets)
            {
                wave.Hit(e);
                e.Status.Ignite(wave.Element, true, wave.Plan.burnSeconds, wave.Plan.burnDps, wave.Source);
            }
        }
    }

    // Shockwave (Impact): the plan's damage per wave; the first wave also stuns for the plan's stun seconds.
    [Serializable]
    public sealed class StunWaveSkill : IActiveSkill
    {
        [MinValue(0.01), SuffixLabel("s")] public float waveInterval = 0.35f;
        public bool stunOnFirstWave = true;

        public float WaveInterval => waveInterval;
        public bool ShowsWaveProgress => false;
        public string Summary(SkillPlan plan) => $"{plan.damage} damage" + (stunOnFirstWave ? $" · Stun {plan.stunSeconds:0.#}s" : "");

        public void FireWave(in SkillWave wave)
        {
            foreach (var e in wave.Targets)
            {
                if (stunOnFirstWave && wave.first) e.Status.Stun(wave.Element, wave.Plan.stunSeconds, true);
                wave.Hit(e);
                e.TakeElementDamage(wave.Plan.damage, wave.Element, wave.Source);
            }
        }
    }
}
