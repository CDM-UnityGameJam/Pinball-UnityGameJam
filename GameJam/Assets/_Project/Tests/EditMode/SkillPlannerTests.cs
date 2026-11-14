using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    // Whole-number groups, lucky draws, number keys and skill plans (port of the v0.7 GroupRulesRunner,
    // HandInteractionRunner and LuckTuning.Plan checks).
    public class SkillPlannerTests
    {
        sealed class Card { public int number; public Card(int n) => number = n; }

        // Replays a fixed list of rolls.
        sealed class ScriptedRandom : IRandom
        {
            readonly Queue<float> values;
            public int Calls { get; private set; }
            public ScriptedRandom(params float[] values) => this.values = new Queue<float>(values);
            public float Value() { Calls++; return values.Dequeue(); }
            public int Range(int minInclusive, int maxExclusive) => minInclusive;
        }

        static int Number(Card x) => x.number;

        [Test]
        public void GroupMustBeEveryHeldCardOfOneNumber()
        {
            Card a = new Card(2), b = new Card(2), c = new Card(3), stranger = new Card(2);
            var hand = new List<Card> { a, b, c, null, null };
            Assert.That(GroupRules.ValidGroup(new[] { a, b }, hand, Number));
            Assert.That(GroupRules.ValidGroup(new[] { c }, hand, Number));
            Assert.That(GroupRules.ValidGroup(new[] { a }, hand, Number), Is.False, "partial group");
            Assert.That(GroupRules.ValidGroup(new[] { a, c }, hand, Number), Is.False, "mixed numbers");
            Assert.That(GroupRules.ValidGroup(new[] { a, a }, hand, Number), Is.False, "same card twice");
            Assert.That(GroupRules.ValidGroup(new[] { stranger, b }, hand, Number), Is.False, "not in hand");
            Assert.That(GroupRules.ValidGroup(new Card[0], hand, Number), Is.False, "empty");
        }

        [Test]
        public void LuckyChanceGrowsWithDraws()
        {
            Assert.That(GroupRules.LuckyChance(0.2, 1), Is.EqualTo(0.2).Within(1e-12));
            Assert.That(GroupRules.LuckyChance(0.2, 5), Is.EqualTo(0.67232).Within(1e-12));
            Assert.That(GroupRules.LuckyChance(0.2, 0), Is.EqualTo(0));
        }

        [Test]
        public void PickLuckyUsesOneRollPerCardAndReservoirPicks()
        {
            var none = new ScriptedRandom(0.5f, 0.9f, 0.3f);
            Assert.That(GroupRules.PickLucky(new[] { 1, 2, 3 }, 0.2, none), Is.Zero, "no candidate, no lucky number");
            Assert.That(none.Calls, Is.EqualTo(3));

            var first = new ScriptedRandom(0.5f, 0.1f, 0.9f);
            Assert.That(GroupRules.PickLucky(new[] { 1, 2, 3 }, 0.2, first), Is.EqualTo(2), "the only candidate wins");

            // Second success: the pick roll 0.4 < 1/2 replaces the first candidate.
            var second = new ScriptedRandom(0.1f, 0.15f, 0.4f, 0.9f);
            Assert.That(GroupRules.PickLucky(new[] { 1, 3, 2 }, 0.2, second), Is.EqualTo(3));
            Assert.That(second.Calls, Is.EqualTo(4));

            var keep = new ScriptedRandom(0.1f, 0.15f, 0.6f);
            Assert.That(GroupRules.PickLucky(new[] { 1, 3 }, 0.2, keep), Is.EqualTo(1), "0.6 >= 1/2 keeps the first");

            Assert.That(GroupRules.PickLucky(new[] { 2 }, 1.0, new ScriptedRandom(0.99f)), Is.EqualTo(2), "p = 1 always succeeds");
        }

        [Test]
        public void WeightedPicksByWeight()
        {
            var w = new[] { 1f, 0f, 3f };
            Assert.That(GroupRules.Weighted(w, 0.0), Is.EqualTo(0));
            Assert.That(GroupRules.Weighted(w, 0.24), Is.EqualTo(0));
            Assert.That(GroupRules.Weighted(w, 0.26), Is.EqualTo(2));
            Assert.That(GroupRules.Weighted(w, 1.0), Is.EqualTo(2), "a roll of 1 picks the last positive weight");
        }

        [Test]
        public void RefillCostFollowsEmptySlots()
        {
            var costs = new[] { 100f, 250f, 450f, 700f, 1000f };
            Assert.That(Enumerable.Range(0, 7).Select(n => GroupRules.RefillCost(costs, n)),
                Is.EqualTo(new double[] { 0, 100, 250, 450, 700, 1000, 1000 }));
        }

        [Test]
        public void SortIsStableByNumberWithEmptySlotsLast()
        {
            Card a = new Card(3), b = new Card(1), c = new Card(3), d = new Card(2);
            var hand = new[] { a, null, b, c, d };
            GroupRules.SortByNumber(hand, Number);
            Assert.That(hand, Is.EqualTo(new[] { b, d, a, c, null }));
        }

        [Test]
        public void NumberKeysSelectThenDoubleTapToCast()
        {
            var keys = new GroupKeyInput(0.3);
            Assert.That(keys.Press(2, null, 0.0), Is.EqualTo(GroupKeyAction.Select));
            Assert.That(keys.Press(2, 2, 0.5), Is.EqualTo(GroupKeyAction.AwaitSecondTap), "the selecting press is not a first tap");
            Assert.That(keys.Press(2, 2, 0.7), Is.EqualTo(GroupKeyAction.Cast));

            Assert.That(keys.Press(2, 2, 1.0), Is.EqualTo(GroupKeyAction.AwaitSecondTap));
            Assert.That(keys.Advance(1.2, 2), Is.EqualTo(GroupKeyAction.None));
            Assert.That(keys.Advance(1.31, 2), Is.EqualTo(GroupKeyAction.Cancel), "a single tap cancels after the window");

            Assert.That(keys.Press(1, 2, 2.0), Is.EqualTo(GroupKeyAction.Select), "another number switches groups");
            keys.Press(1, 1, 2.1);
            Assert.That(keys.Advance(2.15, 3), Is.EqualTo(GroupKeyAction.None), "selection changed: the pending tap is dropped");
            Assert.That(keys.HasPendingTap, Is.False);
        }

        static readonly ActiveTier Arc = new ActiveTier(true, 4, 2, 40, 0f, 6f, 0f);

        [Test]
        public void PlanUsesTheRowPlusExtraCopies()
        {
            var plan = SkillPlanner.Plan(Arc, 1, 1, 2f);
            Assert.That((plan.allTargets, plan.targetCount, plan.waves, plan.damage), Is.EqualTo((true, 4, 2, 40)));
            plan = SkillPlanner.Plan(Arc, 3, 1, 2f);
            Assert.That((plan.waves, plan.burnSeconds, plan.burnDps), Is.EqualTo((4, 4f, 6f)));
            Assert.That(SkillPlanner.Plan(Arc, 0, 1, 2f).waves, Is.EqualTo(0), "no cards, no plan");
        }

        [Test]
        public void TargetCountRespectsTheLimit()
        {
            var limited = SkillPlanner.Plan(new ActiveTier(false, 4, 1, 12, 0, 0, 0), 1, 0, 0);
            var all = SkillPlanner.Plan(Arc, 1, 0, 0);
            Assert.That(SkillPlanner.TargetCount(limited, 10), Is.EqualTo(4));
            Assert.That(SkillPlanner.TargetCount(limited, 2), Is.EqualTo(2));
            Assert.That(SkillPlanner.TargetCount(all, 10), Is.EqualTo(10));
        }

        [Test]
        public void HeldTablesClampToTheLastRow()
        {
            var t = new HeldIntTable(3, 4, 6, 8, 13);
            Assert.That(new[] { 0, 1, 2, 5, 9 }.Select(t.Evaluate), Is.EqualTo(new[] { 0, 3, 4, 13, 13 }));
            Assert.That(new HeldFloatTable(0.5f, 0.75f).Evaluate(4), Is.EqualTo(0.75f));
        }
    }
}
