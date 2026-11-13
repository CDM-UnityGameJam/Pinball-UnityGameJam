using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinball.Core;
using Pinball.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // Port of the v0.6 acceptance checks from integration/v05-multiball-side-kickers (LuckPrototypeCheck,
    // V06Checks, MultiballChecks, LifeAndRelaunchChecks, LifeLossFieldClearChecks, ScoreChecks,
    // EmptyHandArcChecks), run in Game.unity with the combat clock driven by the test.
    public class CombatRulesPlayModeTests
    {
        GameContext ctx;
        Ball ball;
        EnemyDefinition grunt;
        ElementDefinition Arc, Spark, Shock;

        [UnitySetUp]
        public IEnumerator Fixture()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            ctx = Object.FindAnyObjectByType<GameContext>();
            ctx.DriveTimeManually = true;
            yield return null;
            Arc = ctx.config.elements[0];
            Spark = ctx.config.elements[1];
            Shock = ctx.config.elements[2];
            grunt = ctx.config.spawnProfile.enemies[0].enemy;
            Assert.That(ctx.Hand.Count, Is.Zero, "a run starts with an empty hand");
            ctx.Run.Tick(60f);
            ball = ctx.Balls.Active[0];
            ball.Park(ctx.Board.launchPoint.Position);
            foreach (var e in ctx.Enemies.FrontFirst(new List<Enemy>())) e.Remove();
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        Enemy Foe(ElementDefinition element, float x, float z, int health = 100) =>
            ctx.Spawner.Spawn(grunt, element, new Vector3(x, 0f, z), health);

        void Hit(Enemy e)
        {
            ball.Body.isKinematic = false;
            e.Contact.TryContact(ball, e.transform.position);
            ball.Body.isKinematic = true;
        }

        bool Play(int[] dice, params int[] slots) => ctx.Hand.TryPlay(slots.Select(i => ctx.Hand[i]).ToList(), dice);

        void Reveal() => ctx.Clock.Advance(ctx.config.diceRevealDelay + 0.001f);

        [Test]
        public void EmptyOpeningHandChargesFromNaturalRecoveryAndContacts()
        {
            Assert.That(ctx.Hand.ChargeRequired, Is.EqualTo(100));
            ctx.Clock.Advance(10f);
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(20).Within(1e-6), "natural 2/s");
            var e = Foe(Arc, 0, 4, 1000);
            Hit(e);
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(33).Within(1e-6), "+13 per contact");
            Hit(e);
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(46).Within(1e-6), "contacts inside the damage cooldown still charge");
            ctx.Clock.Advance(27f);
            Assert.That(ctx.Hand.Count, Is.EqualTo(1), "100 charge draws a card");
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(0).Within(0.05), "overflow carries");
        }

        [Test]
        public void FullHandEarnsNoChargeAndPlayingRestartsAtBaseCost()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 4), new CardSpec(Spark, 4), new CardSpec(Shock, 2), new CardSpec(Arc, 5), new CardSpec(Arc, 6));
            ctx.Clock.Advance(5f);
            Assert.That(ctx.Hand.ChargeProgress, Is.Zero);
            Assert.That(Play(new[] { 1, 1 }, 0, 1));
            Assert.That(ctx.Hand.Count, Is.EqualTo(3));
            Assert.That(ctx.Hand.ChargeRequired, Is.EqualTo(100), "per-card cost is 0 in v0.6");
        }

        [Test]
        public void InvalidBatchesChangeNothing()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 4), new CardSpec(Spark, 6));
            Assert.That(Play(new[] { 1, 1 }, 0, 1), Is.False, "mixed numbers");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard> { ctx.Hand[0], ctx.Hand[0] }), Is.False, "same card twice");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard> { new HandCard(new CardSpec(Arc, 4)) }), Is.False, "not in hand");
            Assert.That(ctx.Hand.Count, Is.EqualTo(2));
            Assert.That(ctx.Hand.EmpoweredHits, Is.Zero);
            Assert.That(ctx.Casts.PendingBatch, Is.Null);
        }

        [Test]
        public void ZeroMatchingDiceStillCastTheBaseSkill()
        {
            var a = Foe(Shock, 0, 0); var b = Foe(Spark, 1.5f, 1); var c = Foe(Arc, -1.5f, 2);
            ctx.Hand.SetHand(new CardSpec(Arc, 4), new CardSpec(Spark, 4));
            Assert.That(Play(new[] { 2, 6 }, 0, 1));
            Assert.That(ctx.Casts.PendingBatch, Is.Not.Null);
            Assert.That(a.Health.Current, Is.EqualTo(100), "nothing before the reveal");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard>()), Is.False);
            Reveal();
            var batch = ctx.Casts.LastBatch;
            Assert.That(batch.tier, Is.EqualTo(CastTier.Base));
            Assert.That(batch.Casts.Count, Is.EqualTo(2), "each element casts once");
            Assert.That(a.Health.Current, Is.EqualTo(80), "Base Arc: one wave of 20");
            Assert.That(b.Health.Current, Is.EqualTo(80));
            Assert.That(c.Health.Current, Is.EqualTo(100), "Arc foes are immune to Arc");
            Assert.That(c.Status.IsBurning, "but the Spark cast burns them");
            Assert.That(b.Status.IsBurning, Is.False, "Spark foes are immune to Spark");
            Assert.That(ctx.Hand.EmpoweredHits, Is.EqualTo(2), "one empowered hit per card, whatever the dice");
        }

        [Test]
        public void TargetsAreLockedWhenTheBatchIsPlayed()
        {
            var a = Foe(Shock, 0, 0);
            ctx.Hand.SetHand(new CardSpec(Arc, 3));
            Assert.That(Play(new[] { 3 }, 0));
            var late = Foe(Shock, 2, 0);
            Reveal();
            Assert.That(ctx.Casts.LastBatch.tier, Is.EqualTo(CastTier.Hit));
            ctx.Clock.Advance(1f);
            Assert.That(a.Health.Current, Is.EqualTo(60), "Hit Arc: two waves of 20");
            Assert.That(late.Health.Current, Is.EqualTo(100), "enemies spawned after the play never join");
        }

        [Test]
        public void JackpotFullFieldAndExtraCopies()
        {
            var foes = Enumerable.Range(0, 6).Select(i => Foe(Shock, -5 + 2 * i, 4)).ToList();
            ctx.Hand.SetHand(new CardSpec(Spark, 5), new CardSpec(Spark, 5));
            Assert.That(Play(new[] { 5, 5 }, 0, 1));
            Reveal();
            Assert.That(ctx.Casts.LastBatch.tier, Is.EqualTo(CastTier.Jackpot));
            var plan = ctx.Casts.LastBatch.Casts[0].plan;
            Assert.That(plan.allTargets);
            Assert.That(plan.burnSeconds, Is.EqualTo(8f), "6 s + 2 s for the second Spark card");
            Assert.That(foes.All(f => f.Status.IsBurning), "every eligible foe");
            Assert.That(ctx.Casts.LastBatch.Coverage, Is.EqualTo(6));
        }

        [Test]
        public void ShockwaveCastIsOneImpactWithTierStun()
        {
            var a = Foe(Arc, 0, 4);
            ctx.Hand.SetHand(new CardSpec(Shock, 2), new CardSpec(Shock, 2), new CardSpec(Shock, 2));
            Assert.That(Play(new[] { 2, 1, 1 }, 0, 1, 2));
            Reveal();
            Assert.That(ctx.Casts.LastBatch.tier, Is.EqualTo(CastTier.FullField));
            ctx.Clock.Advance(2f);
            Assert.That(a.Health.Current, Is.EqualTo(85), "one impact of 15, however many cards");
            Assert.That(a.Status.StunRemaining, Is.EqualTo(13f).Within(0.01f), "Full Field stun 15 s");
        }

        [Test]
        public void EmpoweredHitDoublesBallDamageOnce()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1));
            Assert.That(Play(new[] { 2 }, 0));
            Reveal();
            var e = Foe(Arc, 0, 4);
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(92), "empowered: 8");
            Assert.That(ctx.Hand.EmpoweredHits, Is.Zero);
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(92), "inside the cooldown: no damage, no stock used");
            ctx.Clock.Advance(0.25f);
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(88), "normal: 4");
        }

        [Test]
        public void HeldArcHitsTheDirectFoeAndNearbyOnes()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1), new CardSpec(Arc, 2));
            var direct = Foe(Spark, 0, 4);
            var near = Enumerable.Range(1, 5).Select(i => Foe(Shock, i * 0.9f - 2.5f, 6)).ToList();
            var immune = Foe(Arc, 0, 5.5f);
            Hit(direct);
            Assert.That(direct.Health.Current, Is.EqualTo(100 - 4 - 4), "ball 4 + Arc 4");
            Assert.That(near.Count(f => f.Health.Current == 96), Is.EqualTo(4), "two Arc cards: four nearby");
            Assert.That(immune.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void HeldSparkBurnsFractionallyAndShockOnlyStuns()
        {
            ctx.Hand.SetHand(new CardSpec(Spark, 1), new CardSpec(Shock, 2));
            var e = Foe(Arc, 0, 4);
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(96));
            Assert.That(e.Status.IsStunned, "held Shockwave stuns the hit foe");
            ctx.Clock.Advance(6f);
            Assert.That(e.Health.Current, Is.EqualTo(93), "0.5 DPS for 6 s: 3 damage");
        }

        [Test]
        public void TwoKillsInOneBatchEarnOneBall()
        {
            var foes = Enumerable.Range(0, 5).Select(i => Foe(Shock, -4 + 2 * i, 4, 10)).ToList();
            ctx.Hand.SetHand(new CardSpec(Arc, 6), new CardSpec(Arc, 6));
            Assert.That(Play(new[] { 6, 1 }, 0, 1));
            Reveal();
            Assert.That(ctx.Casts.LastBatch.Kills, Is.EqualTo(5));
            Assert.That(ctx.Multiball.Pending, Is.EqualTo(1), "one ball per attack");
            Assert.That(ctx.Score.Kills, Is.EqualTo(5));
        }

        [Test]
        public void BallHitAndItsEnchantmentsShareOneAttack()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 6));
            var direct = Foe(Spark, 0, 4, 8);
            Foe(Shock, 1, 4, 4);
            Hit(direct);
            Assert.That(ctx.Score.Kills, Is.EqualTo(2));
            Assert.That(ctx.Multiball.Pending, Is.EqualTo(1), "collision kill + Arc kill = a multi-kill");
        }

        [Test]
        public void LosingALifeClearsTheFieldWithoutRewards()
        {
            var a = Foe(Arc, -3, 4); var b = Foe(Spark, 3, 4);
            long score = ctx.Score.Score;
            ctx.Run.LoseLives(1, "test");
            Assert.That(ctx.Enemies.Count, Is.Zero);
            Assert.That(ctx.Score.Kills, Is.Zero);
            Assert.That(ctx.Score.Score, Is.EqualTo(score));
            Assert.That(ctx.Multiball.Pending, Is.Zero);
            Assert.That(ctx.Run.State, Is.EqualTo(RunState.Playing));
        }

        [Test]
        public void ComboPaysAfterTheTimeoutAndAtGameOver()
        {
            var e = Foe(Arc, 0, 4, 10000);
            for (int i = 0; i < 10; i++) { Hit(e); ctx.Clock.Advance(0.25f); }
            Assert.That(ctx.Score.Combo, Is.EqualTo(10));
            long before = ctx.Score.Score;
            ctx.Clock.Advance(3f);
            Assert.That(ctx.Score.Combo, Is.Zero);
            Assert.That(ctx.Score.Score - before, Is.EqualTo(100 + 5), "10-hit tier, plus one time reward at 5 s");

            for (int i = 0; i < 5; i++) { Hit(e); ctx.Clock.Advance(0.25f); }
            before = ctx.Score.Score;
            ctx.Run.LoseLives(3, "Enemy breach");
            Assert.That(ctx.Run.State, Is.EqualTo(RunState.GameOver));
            Assert.That(ctx.Score.Score - before, Is.EqualTo(50), "game over pays the running combo");
        }

        [Test]
        public void DifficultyScalesNewEnemiesSpawnRateAndSpeed()
        {
            Assert.That(ctx.Spawner.HealthFor(grunt), Is.EqualTo(9));
            Assert.That(ctx.Spawner.CurrentInterval, Is.EqualTo(5).Within(1e-6));
            var early = Foe(Arc, 0, 4, ctx.Spawner.HealthFor(grunt));
            ctx.Clock.Advance(300f);
            Assert.That(ctx.Spawner.HealthFor(grunt), Is.EqualTo(72), "9 × 8 at 300 s");
            Assert.That(ctx.Spawner.CurrentInterval, Is.EqualTo(1).Within(1e-6), "5 / 5");
            Assert.That(ctx.March.SpeedOf(grunt), Is.EqualTo(14.5f / 35f * 1.7f).Within(1e-4f));
            Assert.That(early.Health.Max, Is.EqualTo(9), "living enemies keep their health");
        }
    }
}
