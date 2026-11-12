using NUnit.Framework;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    public class QualityRollerTests
    {
        Random.State savedState;

        [SetUp] public void SaveRandom() => savedState = Random.state;
        [TearDown] public void RestoreRandom() => Random.state = savedState;

        [Test]
        public void SeededRollsMatchOldPrototype()
        {
            var bands = LegacyV03.Bands();
            foreach (var set in Golden.Load().rolls)
            {
                Random.InitState(set.seed);
                for (int i = 0; i < set.q.Length; i++)
                    Assert.That(QualityRoller.Roll(bands, set.lucky, UnityRandom.Shared), Is.EqualTo(set.q[i]),
                        $"seed {set.seed}, lucky {set.lucky}, roll {i}");
            }
        }

        [Test]
        public void BandSelectionMatchesOldPrototype()
        {
            var bands = LegacyV03.Bands();
            var expected = Golden.Load().bandSelection;
            for (int i = 0; i <= 1000; i++)
            {
                Assert.That(QualityRoller.SelectBand(bands, false, i / 1000f), Is.EqualTo(expected[i]), $"normal sample {i}");
                Assert.That(QualityRoller.SelectBand(bands, true, i / 1000f), Is.EqualTo(expected[1001 + i]), $"lucky sample {i}");
            }
        }

        // Port of LuckPrototypeCheck.CheckCards: distribution and endpoints over 160k rolls each.
        [TestCase(false)]
        [TestCase(true)]
        public void DistributionMatchesWeightsAndReachesEndpoints(bool lucky)
        {
            var bands = LegacyV03.Bands();
            var random = new SeededRandom(303);
            var counts = new int[bands.Count];
            bool min = false, max = false;
            const int samples = 160000;
            for (int i = 0; i < samples; i++)
            {
                int q = QualityRoller.Roll(bands, lucky, random);
                counts[QualityRoller.BandOf(bands, q)]++;
                min |= q == -10;
                max |= q == 100;
            }
            Assert.That(min && max, "both endpoints reachable");
            for (int i = 0; i < bands.Count; i++)
                Assert.That(counts[i] / (float)samples, Is.EqualTo(QualityRoller.Chance(bands, i, lucky)).Within(0.006f), bands[i].id);
        }

        [Test]
        public void ChanceTablesAreTheV03Tables()
        {
            var bands = LegacyV03.Bands();
            Assert.That(QualityRoller.Chance(bands, 0, false), Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(QualityRoller.Chance(bands, 3, false), Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(QualityRoller.Chance(bands, 2, true), Is.EqualTo(0.40f).Within(1e-6f));
            Assert.That(QualityRoller.Chance(bands, 3, true), Is.EqualTo(0.25f).Within(1e-6f));
        }

        [Test]
        public void AllZeroWeightsFallBackToTheBandHoldingZero()
        {
            var bands = LegacyV03.Bands();
            foreach (var band in bands) band.normalWeight = 0;
            Assert.That(QualityRoller.SelectBand(bands, false, 0.7f), Is.EqualTo(1));
            Assert.That(QualityRoller.Chance(bands, 1, false), Is.EqualTo(1f));
        }

        [Test]
        public void ZeroWeightBandsAreNeverPicked()
        {
            var bands = LegacyV03.Bands();
            bands[3].normalWeight = 0;
            for (int i = 0; i <= 100; i++) Assert.That(QualityRoller.SelectBand(bands, false, i / 100f), Is.Not.EqualTo(3));
        }

        [TestCase(-10, CastOutcome.ShortCircuit)]
        [TestCase(-1, CastOutcome.ShortCircuit)]
        [TestCase(0, CastOutcome.Misfire)]
        [TestCase(1, CastOutcome.Cast)]
        [TestCase(100, CastOutcome.Cast)]
        public void ClassifyBySign(int q, CastOutcome expected) => Assert.That(QualityRoller.Classify(q), Is.EqualTo(expected));

        [Test]
        public void ShortCircuitDurationsMatchOldPrototype()
        {
            var bands = LegacyV03.Bands();
            foreach (var row in Golden.Load().shortCircuit)
                Assert.That(QualityRoller.ShortCircuitSeconds(bands, row.q, LegacyV03.ShortCircuitMin, LegacyV03.ShortCircuitMax),
                    Is.EqualTo(row.seconds).Within(1e-5f), $"Q {row.q}");
        }

        [Test]
        public void LuckyNumberSequenceMatchesOldPrototype()
        {
            var golden = Golden.Load().luckySequence;
            Random.InitState(golden.seed);
            int current = golden.start;
            foreach (int expected in golden.values)
            {
                int next = LuckyNumberRule.Next(current, LegacyV03.MinNumber, LegacyV03.MaxNumber, UnityRandom.Shared);
                Assert.That(next, Is.EqualTo(expected));
                current = next;
            }
        }

        [Test]
        public void LuckyNumberNeverRepeatsAndStaysInRange()
        {
            var random = new SeededRandom(5);
            int current = 1;
            for (int i = 0; i < 1000; i++)
            {
                int next = LuckyNumberRule.Next(current, 1, 6, random);
                Assert.That(next, Is.Not.EqualTo(current));
                Assert.That(next, Is.InRange(1, 6));
                current = next;
            }
        }
    }
}
