using System;
using System.Collections.Generic;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // A card in the hand. A class so a selection can point at one card even while the hand shifts.
    public sealed class HandCard
    {
        public readonly CardSpec spec;
        public HandCard(CardSpec spec) => this.spec = spec;
        public ElementDefinition Element => spec.element;
        public int Number => spec.number;
        public override string ToString() => spec.ToString();
    }

    // One played group: its cards, whether it was the lucky number, and one cast per element in it.
    // It is the AttackRecord for every wave and burn tick of every element in the group, so kills add up per group.
    public sealed class CastBatch : AttackRecord
    {
        readonly HandCard[] cards;
        readonly List<SkillCast> casts = new List<SkillCast>();
        readonly HashSet<Enemy> covered = new HashSet<Enemy>();

        public readonly int number;
        public readonly CastTier tier;

        public IReadOnlyList<HandCard> Cards => cards;
        public IReadOnlyList<SkillCast> Casts => casts;
        public bool Lucky => tier == CastTier.Lucky;
        // Distinct enemies the group's skills reached.
        public int Coverage => covered.Count;

        // `snapshot`: every living enemy, front first, taken before the cards left the hand.
        internal CastBatch(HandCard[] cards, bool lucky, IReadOnlyList<Enemy> snapshot, IReadOnlyList<ElementDefinition> elementOrder)
            : base("Group #" + cards[0].Number)
        {
            this.cards = (HandCard[])cards.Clone();
            number = cards[0].Number;
            tier = lucky ? CastTier.Lucky : CastTier.Normal;

            // One cast per element present, in the game's element order. Each locks its own targets:
            // the snapshot minus enemies of its element, nearest the danger line first.
            foreach (var element in elementOrder)
            {
                int copies = 0;
                foreach (var c in cards) if (c.Element == element) copies++;
                if (copies == 0) continue;
                var plan = element.Plan(tier, copies);
                var targets = new List<Enemy>();
                foreach (var enemy in snapshot)
                {
                    if (enemy.Element == element) continue;
                    if (!plan.allTargets && targets.Count >= plan.targetCount) break;
                    targets.Add(enemy);
                }
                casts.Add(new SkillCast(this, element, copies, plan, targets));
            }
        }

        internal void Cover(Enemy enemy) => covered.Add(enemy);
    }

    // One element's active skill inside a group: its plan, locked targets and wave progress.
    public sealed class SkillCast
    {
        public readonly CastBatch batch;
        public readonly ElementDefinition element;
        public readonly int copies;
        public readonly SkillPlan plan;
        public readonly IReadOnlyList<Enemy> targets;

        public int WavesFired { get; internal set; }
        public double NextWaveAt { get; internal set; } = double.PositiveInfinity;
        public bool Complete => WavesFired >= Math.Max(1, plan.waves);

        internal SkillCast(CastBatch batch, ElementDefinition element, int copies, SkillPlan plan, List<Enemy> targets)
        {
            this.batch = batch; this.element = element; this.copies = copies; this.plan = plan; this.targets = targets;
        }
    }
}
