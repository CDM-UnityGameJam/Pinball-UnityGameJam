using System.Collections.Generic;
using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    // Charge, difficulty and scoring rules (port of the v0.6 RechargeAccountChecks, DifficultyRuleChecks and
    // ScoreChecks).
    public class ChargeAndScoreTests
    {
        [Test]
        public void ChargeCostsBasePlusPerCardAndCarriesOverflow()
        {
            var a = new ChargeAccount();
            a.PlayBatch(handWasFull: true, count: 2, baseCost: 100, perCardCost: 50);
            Assert.That(a.Required, Is.EqualTo(200));
            a.Earn(150);
            Assert.That(a.TryConsume(100), Is.False);
            a.Earn(70);
            Assert.That(a.TryConsume(100), "220 covers 200");
            Assert.That(a.Progress, Is.EqualTo(20).Within(1e-9), "overflow carries over");
            Assert.That(a.Required, Is.EqualTo(100), "the next card costs base again");

            a.PlayBatch(handWasFull: false, count: 1, baseCost: 100, perCardCost: 50);
            Assert.That(a.Required, Is.EqualTo(150), "a partial hand keeps progress and adds the card cost");
            Assert.That(a.Progress, Is.EqualTo(20).Within(1e-9));
        }

        [Test]
        public void ChargeThresholdsMatchV05()
        {
            // v0.5 shared thresholds: from a full hand, one card then two more cards: 150 / 250 / 350.
            var a = new ChargeAccount();
            a.PlayBatch(true, 1, 100, 50);
            Assert.That(a.Required, Is.EqualTo(150));
            a.PlayBatch(false, 2, 100, 50);
            Assert.That(a.Required, Is.EqualTo(250));
            a.PlayBatch(false, 2, 100, 50);
            Assert.That(a.Required, Is.EqualTo(350));
            Assert.That(a.RequiredAfterBatch(false, 1, 100, 50), Is.EqualTo(400));
            Assert.That(a.RequiredAfterBatch(true, 1, 100, 50), Is.EqualTo(150));
        }

        [Test]
        public void FullHandClearsChargeAndIgnoresEarnings()
        {
            var a = new ChargeAccount();
            a.StartMissing(100);
            a.Earn(60);
            a.FullHand();
            Assert.That((a.Progress, a.Required), Is.EqualTo((0.0, 0.0)));
            Assert.That(a.Earn(50), Is.EqualTo(0), "nothing is missing");
            Assert.That(a.TryConsume(100), Is.False);
        }

        [Test]
        public void ContactChargeIsUncapped()
        {
            var a = new ChargeAccount();
            a.StartMissing(1e9);
            for (int i = 0; i < 30000; i++) Assert.That(a.Earn(13), Is.EqualTo(13));
            Assert.That(a.Progress, Is.EqualTo(390000).Within(1e-6));
        }

        static readonly List<DifficultyStage> Stages = new List<DifficultyStage>
        {
            new DifficultyStage(0, 1, 1, 1), new DifficultyStage(30, 1.2f, 1.25f, 2), new DifficultyStage(90, 1.5f, 1.3f, 4),
            new DifficultyStage(180, 2, 1.5f, 6), new DifficultyStage(300, 5, 1.7f, 8), new DifficultyStage(480, 10, 1.8f, 10),
        };

        [Test]
        public void DifficultyInterpolatesAndHoldsAtTheLastStage()
        {
            var s = DifficultyCurve.Sample(Stages, 15);
            Assert.That(s.spawnMultiplier, Is.EqualTo(1.1).Within(1e-6));
            Assert.That(s.moveMultiplier, Is.EqualTo(1.125).Within(1e-6));
            Assert.That(s.healthMultiplier, Is.EqualTo(1.5).Within(1e-6));
            s = DifficultyCurve.Sample(Stages, 1000);
            Assert.That(s.spawnMultiplier, Is.EqualTo(10).Within(1e-5));
            Assert.That(s.moveMultiplier, Is.EqualTo(1.8).Within(1e-5));
            Assert.That(s.healthMultiplier, Is.EqualTo(10).Within(1e-5));
            Assert.That(DifficultyCurve.Sample(Stages, -5).spawnMultiplier, Is.EqualTo(1));
        }

        [Test]
        public void SpawnHealthRoundsHalfUp()
        {
            Assert.That(DifficultyCurve.Health(9, DifficultyCurve.Sample(Stages, 0).healthMultiplier), Is.EqualTo(9));
            Assert.That(DifficultyCurve.Health(9, DifficultyCurve.Sample(Stages, 15).healthMultiplier), Is.EqualTo(14), "13.5 rounds up");
            Assert.That(DifficultyCurve.Health(9, DifficultyCurve.Sample(Stages, 300).healthMultiplier), Is.EqualTo(72));
            Assert.That(DifficultyCurve.Health(9, DifficultyCurve.Sample(Stages, 480).healthMultiplier), Is.EqualTo(90));
            Assert.That(DifficultyCurve.Health(1, 0.01), Is.EqualTo(1), "at least one");
        }

        [Test]
        public void InvalidStagesAreReported()
        {
            Assert.That(DifficultyCurve.Check(Stages), Is.Null);
            Assert.That(DifficultyCurve.Check(new List<DifficultyStage>()), Is.Not.Null);
            Assert.That(DifficultyCurve.Check(new List<DifficultyStage> { new DifficultyStage(10, 1, 1, 1), new DifficultyStage(10, 1, 1, 1) }), Does.Contain("later"));
            Assert.That(DifficultyCurve.Check(new List<DifficultyStage> { new DifficultyStage(0, 0, 1, 1) }), Does.Contain("above zero"));
        }

        [Test]
        public void SpawnPhaseConsumesEveryAttempt()
        {
            var phase = new SpawnPhase();
            Assert.That(phase.Advance(4.9, 5), Is.EqualTo(0));
            Assert.That(phase.Advance(0.1, 5), Is.EqualTo(1));
            Assert.That(phase.Advance(10, 5), Is.EqualTo(2));
            Assert.That(phase.Advance(1, 0.25), Is.EqualTo(4), "a faster interval keeps the fractional phase");
            Assert.That(phase.Advance(1, 0), Is.EqualTo(0));
        }

        static readonly List<ComboTier> Tiers = new List<ComboTier> { new ComboTier(5, 50), new ComboTier(10, 100) };

        [Test]
        public void TimePointsFollowCombatTime()
        {
            var score = new ComboScore();
            long total = 0;
            for (int i = 0; i < 1100; i++) total += score.Tick(0.01, (i + 1) * 0.01, 5f, 5, 3f, Tiers, out _);
            Assert.That(total, Is.EqualTo(10), "11 s: two rewards of 5");
        }

        [Test]
        public void ComboPaysItsHighestTierOnceAfterTheTimeout()
        {
            var score = new ComboScore();
            for (int i = 0; i < 7; i++) Assert.That(score.Hit(i * 0.5, 3f, Tiers), Is.EqualTo(0));
            Assert.That(score.Combo, Is.EqualTo(7));
            Assert.That(score.Remaining(4, 3f), Is.EqualTo(2).Within(1e-9));
            score.Tick(1, 5.9, 5f, 0, 3f, Tiers, out int reward);
            Assert.That(reward, Is.EqualTo(0), "still inside the timeout");
            score.Tick(0.1, 6.0, 5f, 0, 3f, Tiers, out reward);
            Assert.That(reward, Is.EqualTo(50), "7 hits reached the 5-hit tier");
            Assert.That(score.Combo, Is.EqualTo(0));
            score.Tick(1, 7, 5f, 0, 3f, Tiers, out reward);
            Assert.That(reward, Is.EqualTo(0), "paid once");
        }

        [Test]
        public void FinishPaysTheRunningCombo()
        {
            var score = new ComboScore();
            for (int i = 0; i < 12; i++) score.Hit(0, 3f, Tiers);
            Assert.That(score.Finish(Tiers), Is.EqualTo(100));
            Assert.That(score.Finish(Tiers), Is.EqualTo(0));
            Assert.That(ComboScore.Check(new List<ComboTier> { new ComboTier(5, 1), new ComboTier(5, 2) }), Is.Not.Null);
        }
    }
}
