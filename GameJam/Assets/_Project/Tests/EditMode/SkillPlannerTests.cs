using System;
using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    public class SkillPlannerTests
    {
        [Test]
        public void EveryPlanMatchesOldPrototype()
        {
            var bands = LegacyV03.Bands();
            foreach (var row in Golden.Load().plans)
            {
                var element = (LegacyV03.Element)Enum.Parse(typeof(LegacyV03.Element), row.element);
                var plan = SkillPlanner.Plan(bands, LegacyV03.Scaling(element), row.q);
                string at = $"{row.element} Q{row.q}";
                Assert.That(plan.allTargets, Is.EqualTo(row.all), at + " all targets");
                Assert.That(plan.targetCount, Is.EqualTo(row.targets), at + " targets");
                Assert.That(plan.waves, Is.EqualTo(row.waves), at + " waves");
                Assert.That(plan.burnSeconds, Is.EqualTo(row.burn).Within(1e-5f), at + " burn");
                Assert.That(plan.stunSeconds, Is.EqualTo(row.stun).Within(1e-5f), at + " stun");
            }
        }

        // The headline cases from LuckPrototypeCheck.CheckCards.
        [Test]
        public void Q5Q40Q90Coverage()
        {
            var bands = LegacyV03.Bands();
            foreach (LegacyV03.Element element in Enum.GetValues(typeof(LegacyV03.Element)))
            {
                var scaling = LegacyV03.Scaling(element);
                var low = SkillPlanner.Plan(bands, scaling, 5);
                var mid = SkillPlanner.Plan(bands, scaling, 40);
                var high = SkillPlanner.Plan(bands, scaling, 90);
                Assert.That(low.targetCount == 2 && mid.targetCount == 6 && high.allTargets, element.ToString());
                if (element == LegacyV03.Element.Arc) Assert.That(new[] { low.waves, mid.waves, high.waves }, Is.EqualTo(new[] { 1, 2, 4 }));
                if (element == LegacyV03.Element.Spark) Assert.That(new[] { low.burnSeconds, mid.burnSeconds, high.burnSeconds }, Is.EqualTo(new[] { 1f, 4f, 7.5f }));
                if (element == LegacyV03.Element.Shockwave)
                {
                    Assert.That(new[] { low.waves, mid.waves, high.waves }, Is.EqualTo(new[] { 1, 2, 3 }));
                    Assert.That(high.stunSeconds, Is.EqualTo(2.8f).Within(0.001f));
                }
            }
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void NonPositiveQHasNoPlan(int q)
        {
            var plan = SkillPlanner.Plan(LegacyV03.Bands(), LegacyV03.Scaling(LegacyV03.Element.Arc), q);
            Assert.That(plan.waves, Is.Zero);
            Assert.That(plan.allTargets, Is.False);
        }

        [Test]
        public void MissingScalingForBandHasNoPlan()
        {
            var scaling = LegacyV03.Scaling(LegacyV03.Element.Arc);
            scaling.RemoveAll(s => s.bandId == "Medium");
            Assert.That(SkillPlanner.Plan(LegacyV03.Bands(), scaling, 40).waves, Is.Zero);
        }

        [TestCase(2, 10, false, 2)]
        [TestCase(6, 3, false, 3)]
        [TestCase(0, 12, true, 12)]
        public void TargetCountTakesTheFrontOfTheSnapshot(int planned, int available, bool all, int expected) =>
            Assert.That(SkillPlanner.TargetCount(new SkillPlan { targetCount = planned, allTargets = all }, available), Is.EqualTo(expected));

        [Test]
        public void PassiveStackCapsMatchOldPrototype()
        {
            var golden = Golden.Load();
            for (int cards = 0; cards <= 6; cards++)
            {
                Assert.That(LegacyV03.ArcTargets.Evaluate(cards), Is.EqualTo(golden.arcTargets[cards]), $"arc targets, {cards} cards");
                Assert.That(LegacyV03.ExtraSparkTargets.Evaluate(cards), Is.EqualTo(golden.extraSparkTargets[cards]), $"spark targets, {cards} cards");
                Assert.That(LegacyV03.ShockRadius.Evaluate(cards), Is.EqualTo(golden.shockRadius[cards]).Within(1e-5f), $"shock radius, {cards} cards");
            }
        }
    }
}
