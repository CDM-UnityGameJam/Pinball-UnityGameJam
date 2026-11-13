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

    // One played batch: its cards, the dice rolled for them, and one cast per element in it.
    // It is the AttackRecord for every wave and burn tick of every element in the batch, so kills add up per batch.
    public sealed class DiceBatch : AttackRecord
    {
        readonly HandCard[] cards;
        readonly int[] dice;
        readonly List<SkillCast> casts = new List<SkillCast>();
        readonly HashSet<Enemy> covered = new HashSet<Enemy>();

        public readonly int number, matches;
        public readonly CastTier tier;
        public readonly double revealAt;

        public IReadOnlyList<HandCard> Cards => cards;
        public IReadOnlyList<int> Dice => dice;
        public IReadOnlyList<SkillCast> Casts => casts;
        public bool Revealed { get; internal set; }
        // Distinct enemies the batch's skills reached.
        public int Coverage => covered.Count;

        // `snapshot`: every living enemy, front first, taken before the cards left the hand.
        internal DiceBatch(HandCard[] cards, int[] dice, double revealAt, IReadOnlyList<Enemy> snapshot, IReadOnlyList<ElementDefinition> elementOrder)
            : base("Batch #" + cards[0].Number)
        {
            this.cards = (HandCard[])cards.Clone();
            this.dice = (int[])dice.Clone();
            number = cards[0].Number;
            matches = DiceRules.Matches(number, dice);
            tier = DiceRules.Tier(number, dice);
            this.revealAt = revealAt;

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

    // One element's active skill inside a batch: its plan, locked targets and wave progress.
    public sealed class SkillCast
    {
        public readonly DiceBatch batch;
        public readonly ElementDefinition element;
        public readonly int copies;
        public readonly SkillPlan plan;
        public readonly IReadOnlyList<Enemy> targets;

        public int WavesFired { get; internal set; }
        public double NextWaveAt { get; internal set; } = double.PositiveInfinity;
        public bool Complete => WavesFired >= Math.Max(1, plan.waves);

        internal SkillCast(DiceBatch batch, ElementDefinition element, int copies, SkillPlan plan, List<Enemy> targets)
        {
            this.batch = batch; this.element = element; this.copies = copies; this.plan = plan; this.targets = targets;
        }
    }
}
