using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    // Dice tiers and skill plans (port of the v0.6 DiceRuleChecks / LuckTuning.Plan checks).
    public class SkillPlannerTests
    {
        sealed class Card { public int number; public Card(int n) => number = n; }

        static IEnumerable<int[]> AllRolls(int count)
        {
            var faces = new int[count];
            for (int i = 0; i < count; i++) faces[i] = 1;
            while (true)
            {
                yield return (int[])faces.Clone();
                int k = 0;
                while (k < count && ++faces[k] > DiceRules.Faces) { faces[k] = 1; k++; }
                if (k == count) yield break;
            }
        }

        [Test]
        public void TwoDiceGive25Base10FullField1Jackpot()
        {
            var tiers = AllRolls(2).Select(f => DiceRules.Tier(4, f)).ToList();
            Assert.That(tiers.Count, Is.EqualTo(36));
            Assert.That(tiers.Count(t => t == CastTier.Base), Is.EqualTo(25));
            Assert.That(tiers.Count(t => t == CastTier.FullField), Is.EqualTo(10));
            Assert.That(tiers.Count(t => t == CastTier.Jackpot), Is.EqualTo(1));
        }

        [Test]
        public void EveryRollOfOneToFiveDiceMatchesTheTierRule()
        {
            for (int count = 1; count <= DiceRules.MaxCards; count++)
            {
                int upgraded = 0, total = 0;
                foreach (var faces in AllRolls(count))
                {
                    total++;
                    int matches = faces.Count(f => f == 3);
                    var expected = matches == 0 ? CastTier.Base : count == 1 ? CastTier.Hit : matches == 1 ? CastTier.FullField : CastTier.Jackpot;
                    Assert.That(DiceRules.Tier(3, faces), Is.EqualTo(expected), string.Join(",", faces));
                    if (matches > 0) upgraded++;
                }
                Assert.That(upgraded / (double)total, Is.EqualTo(DiceRules.UpgradeChance(count)).Within(1e-12), $"{count} dice");
                Assert.That(DiceRules.PossibleTiers(count), count == 1 ? Is.EqualTo(new[] { CastTier.Base, CastTier.Hit })
                    : Is.EqualTo(new[] { CastTier.Base, CastTier.FullField, CastTier.Jackpot }));
            }
        }

        [Test]
        public void InvalidFacesAreRejected()
        {
            Assert.That(DiceRules.ValidFaces(new[] { 1, 6 }, 2));
            Assert.That(DiceRules.ValidFaces(new[] { 0, 6 }, 2), Is.False);
            Assert.That(DiceRules.ValidFaces(new[] { 7 }, 1), Is.False);
            Assert.That(DiceRules.ValidFaces(new[] { 1 }, 2), Is.False, "one face per card");
            Assert.That(DiceRules.ValidFaces(new[] { 1, 1, 1, 1, 1, 1 }, 6), Is.False, "at most five cards");
            Assert.Throws<System.ArgumentException>(() => DiceRules.Tier(2, new[] { 9 }));
        }

        [Test]
        public void SelectionMustBeDistinctHeldSameNumberCards()
        {
            Card a = new Card(4), b = new Card(4), c = new Card(6), stranger = new Card(4);
            var hand = new List<Card> { a, b, c, null, null };
            int Number(Card x) => x.number;
            Assert.That(DiceRules.ValidSelection(new[] { a }, hand, Number));
            Assert.That(DiceRules.ValidSelection(new[] { a, b }, hand, Number));
            Assert.That(DiceRules.ValidSelection(new[] { a, c }, hand, Number), Is.False, "mixed numbers");
            Assert.That(DiceRules.ValidSelection(new[] { a, a }, hand, Number), Is.False, "same card twice");
            Assert.That(DiceRules.ValidSelection(new[] { stranger }, hand, Number), Is.False, "not in hand");
            Assert.That(DiceRules.ValidSelection(new Card[0], hand, Number), Is.False, "empty");
        }

        static readonly ActiveTier[] ArcTiers =
        {
            new ActiveTier(false, 4, 1, 0, 0), new ActiveTier(false, 6, 2, 0, 0),
            new ActiveTier(true, 4, 3, 0, 0), new ActiveTier(true, 4, 4, 0, 0),
        };

        [Test]
        public void PlanUsesTheTierRowPlusExtraCopies()
        {
            var plan = SkillPlanner.Plan(ArcTiers, CastTier.Hit, 1, 1, 2f);
            Assert.That((plan.allTargets, plan.targetCount, plan.waves), Is.EqualTo((false, 6, 2)));
            plan = SkillPlanner.Plan(ArcTiers, CastTier.Jackpot, 3, 1, 2f);
            Assert.That((plan.allTargets, plan.waves, plan.burnSeconds), Is.EqualTo((true, 6, 4f)));
            Assert.That(SkillPlanner.Plan(ArcTiers, CastTier.Base, 0, 1, 2f).waves, Is.EqualTo(0), "no cards, no plan");
        }

        [Test]
        public void TargetCountRespectsTheLimit()
        {
            var limited = SkillPlanner.Plan(ArcTiers, CastTier.Base, 1, 0, 0);
            var all = SkillPlanner.Plan(ArcTiers, CastTier.FullField, 1, 0, 0);
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
