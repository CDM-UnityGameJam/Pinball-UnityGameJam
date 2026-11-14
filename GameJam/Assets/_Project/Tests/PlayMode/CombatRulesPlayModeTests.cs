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
    // Port of the v0.7 acceptance checks from integration/v05-multiball-side-kickers (GroupRulesRunner,
    // LuckyGroupChecks, CollisionDamageChecks) and the v0.6 ones that still hold (MultiballChecks,
    // LifeAndRelaunchChecks, LifeLossFieldClearChecks, ScoreChecks, EmptyHandArcChecks), run in Game.unity with
    // the combat clock driven by the test.
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

        bool PlayNumber(int number) => ctx.Hand.TryPlay(ctx.Hand.Group(number));

        [Test]
        public void RunStartsWithAFreeFullHandAndNoLuckyNumber()
        {
            Assert.That(ctx.Hand.Count, Is.EqualTo(ctx.config.deck.handSize));
            Assert.That(ctx.Hand.LuckyNumber, Is.Zero, "the opening hand never rolls luck");
            Assert.That(ctx.Hand.Slots.All(c => c.Number >= 1 && c.Number <= 3), "card numbers are 1-3");
            Assert.That(ctx.Hand.Slots.Select(c => c.Number), Is.Ordered, "the hand is sorted by number");
            Assert.That(ctx.Hand.ChargeRequired, Is.Zero);
            ctx.Clock.Advance(5f);
            Assert.That(ctx.Hand.ChargeProgress, Is.Zero, "a full hand earns no charge");
        }

        [Test]
        public void HandStaysSortedByNumber()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 3), new CardSpec(Spark, 1), new CardSpec(Shock, 3), new CardSpec(Arc, 2));
            Assert.That(ctx.Hand.Slots.Take(4).Select(c => c.Number + " " + c.Element.displayName),
                Is.EqualTo(new[] { "1 Spark", "2 Arc", "3 Arc", "3 Shockwave" }));
            Assert.That(ctx.Hand[4], Is.Null, "empty slots go last");
        }

        [Test]
        public void RefillCostFollowsEmptySlotsAndKeepsProgress()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1), new CardSpec(Spark, 1), new CardSpec(Shock, 2), new CardSpec(Arc, 3), new CardSpec(Arc, 3));
            Assert.That(PlayNumber(2));
            Assert.That(ctx.Hand.ChargeRequired, Is.EqualTo(100), "one empty slot");
            ctx.Clock.Advance(10f);
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(30).Within(1e-6), "natural 3/s");
            var e = Foe(Arc, 0, 4, 1000);
            Hit(e);
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(40).Within(1e-6), "+10 per contact");
            Hit(e);
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(50).Within(1e-6), "contacts inside the damage cooldown still charge");
            Assert.That(PlayNumber(3));
            Assert.That(ctx.Hand.ChargeRequired, Is.EqualTo(450), "three empty slots: 450");
            Assert.That(ctx.Hand.ChargeProgress, Is.EqualTo(50).Within(1e-6), "casting keeps the progress");
            Assert.That(PlayNumber(1));
            Assert.That(ctx.Hand.ChargeRequired, Is.EqualTo(1000), "five empty slots");
        }

        [Test]
        public void FullChargeDrawsEveryEmptySlotAndResets()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1), new CardSpec(Spark, 2), new CardSpec(Shock, 2));
            ctx.Hand.SetLuckyNumber(0);
            Assert.That(ctx.Hand.ChargeRequired, Is.EqualTo(250));
            ctx.Clock.Advance(80f);
            Assert.That(ctx.Hand.Count, Is.EqualTo(3), "240 < 250");
            ctx.Clock.Advance(4f);
            Assert.That(ctx.Hand.IsFull, "both empty slots drawn together");
            Assert.That(ctx.Hand.ChargeProgress, Is.Zero, "overflow is cleared");
            Assert.That(ctx.Stats.refills, Is.EqualTo(1));
            Assert.That(ctx.Hand.LuckyNumber == 0 || ctx.Hand.HeldCountOfNumber(ctx.Hand.LuckyNumber) > 0,
                "a new lucky number comes from the drawn cards");
        }

        [Test]
        public void RefillKeepsALuckyNumberStillHeld()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1), new CardSpec(Spark, 2), new CardSpec(Shock, 3));
            ctx.Hand.SetLuckyNumber(2);
            Assert.That(PlayNumber(1));
            Assert.That(ctx.Casts.LastBatch.tier, Is.EqualTo(CastTier.Normal));
            Assert.That(ctx.Hand.FillCharge());
            Assert.That(ctx.Hand.IsFull);
            Assert.That(ctx.Hand.LuckyNumber, Is.EqualTo(2), "the hand still held a 2, so no new draw for luck");
        }

        [Test]
        public void CastingTheLastLuckyCardsClearsTheLuckyNumber()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 2), new CardSpec(Spark, 2), new CardSpec(Shock, 3));
            ctx.Hand.SetLuckyNumber(2);
            Assert.That(PlayNumber(2));
            Assert.That(ctx.Casts.LastBatch.tier, Is.EqualTo(CastTier.Lucky), "the lucky skill is captured first");
            Assert.That(ctx.Hand.LuckyNumber, Is.Zero);
        }

        [Test]
        public void InvalidGroupsChangeNothing()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 2), new CardSpec(Spark, 2), new CardSpec(Shock, 3));
            Assert.That(ctx.Hand.TryPlay(new List<HandCard> { ctx.Hand[0] }), Is.False, "part of a group");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard> { ctx.Hand[0], ctx.Hand[2] }), Is.False, "mixed numbers");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard> { ctx.Hand[0], ctx.Hand[0] }), Is.False, "same card twice");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard> { new HandCard(new CardSpec(Arc, 3)) }), Is.False, "not in hand");
            Assert.That(ctx.Hand.TryPlay(new List<HandCard>()), Is.False);
            Assert.That(ctx.Hand.Count, Is.EqualTo(3));
            Assert.That(ctx.Casts.LastBatch, Is.Null);
        }

        [Test]
        public void PlayingOneSlotCastsItsWholeGroup()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1), new CardSpec(Spark, 2), new CardSpec(Arc, 2));
            Assert.That(ctx.Hand.TryPlay(1));
            Assert.That(ctx.Casts.LastBatch.Cards.Count, Is.EqualTo(2));
            Assert.That(ctx.Hand.Count, Is.EqualTo(1));
        }

        [Test]
        public void NormalCastIsImmediateAndSkipsImmuneFoes()
        {
            var a = Foe(Shock, 0, 0); var b = Foe(Spark, 1.5f, 1); var c = Foe(Arc, -1.5f, 2);
            ctx.Hand.SetHand(new CardSpec(Arc, 2), new CardSpec(Spark, 2));
            ctx.Hand.SetLuckyNumber(0);
            Assert.That(PlayNumber(2));
            var batch = ctx.Casts.LastBatch;
            Assert.That(batch.tier, Is.EqualTo(CastTier.Normal));
            Assert.That(batch.Casts.Count, Is.EqualTo(2), "each element casts once");
            Assert.That(a.Health.Current, Is.EqualTo(60), "first Normal Arc wave of 40, at once");
            Assert.That(c.Health.Current, Is.EqualTo(100), "Arc foes are immune to Arc");
            Assert.That(c.Status.IsBurning, "but the Spark cast burns them");
            Assert.That(b.Status.IsBurning, Is.False, "Spark foes are immune to Spark");
            ctx.Clock.Advance(0.3f);
            Assert.That(a.Health.Current, Is.EqualTo(20), "second wave");
        }

        [Test]
        public void TargetsAreLockedWhenTheGroupIsPlayed()
        {
            var a = Foe(Shock, 0, 0);
            ctx.Hand.SetHand(new CardSpec(Arc, 3));
            ctx.Hand.SetLuckyNumber(0);
            Assert.That(PlayNumber(3));
            var late = Foe(Shock, 2, 0);
            ctx.Clock.Advance(1f);
            Assert.That(a.Health.Current, Is.EqualTo(20), "two waves of 40");
            Assert.That(late.Health.Current, Is.EqualTo(100), "enemies spawned after the play never join");
        }

        [Test]
        public void LuckyCastAndExtraCopies()
        {
            var foes = Enumerable.Range(0, 6).Select(i => Foe(Shock, -5 + 2 * i, 4)).ToList();
            ctx.Hand.SetHand(new CardSpec(Spark, 1), new CardSpec(Spark, 1));
            ctx.Hand.SetLuckyNumber(1);
            Assert.That(PlayNumber(1));
            Assert.That(ctx.Casts.LastBatch.tier, Is.EqualTo(CastTier.Lucky));
            var plan = ctx.Casts.LastBatch.Casts[0].plan;
            Assert.That(plan.allTargets);
            Assert.That(plan.burnSeconds, Is.EqualTo(17f), "Lucky 15 s + 2 s for the second Spark card");
            Assert.That(plan.burnDps, Is.EqualTo(15f));
            Assert.That(foes.All(f => f.Status.IsBurning), "every eligible foe");
            Assert.That(ctx.Casts.LastBatch.Coverage, Is.EqualTo(6));
        }

        [Test]
        public void ShockwaveCastIsOneImpactWithTierStun()
        {
            var a = Foe(Arc, 0, 4);
            ctx.Hand.SetHand(new CardSpec(Shock, 2), new CardSpec(Shock, 2), new CardSpec(Shock, 2));
            ctx.Hand.SetLuckyNumber(2);
            Assert.That(PlayNumber(2));
            ctx.Clock.Advance(2f);
            Assert.That(a.Health.Current, Is.EqualTo(85), "one impact of 15, however many cards");
            Assert.That(a.Status.StunRemaining, Is.EqualTo(28f).Within(0.01f), "Lucky stun 30 s");
        }

        [Test]
        public void CastsLeaveBallDamageOrdinary()
        {
            ctx.Hand.SetHand(new CardSpec(Shock, 1));
            var e = Foe(Shock, 0, 4, 1000);
            Assert.That(PlayNumber(1));
            Assert.That(e.Health.Current, Is.EqualTo(1000), "immune foe: the cast does nothing");
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(996), "no empowered hits: ball damage stays 4");
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(996), "inside the cooldown: no damage");
            ctx.Clock.Advance(0.25f);
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(992));
        }

        [Test]
        public void HeldArcHitsTheDirectFoeAndNearbyOnes()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 1), new CardSpec(Arc, 2));
            ctx.Hand.SetLuckyNumber(0);
            var direct = Foe(Spark, 0, 4);
            var near = Enumerable.Range(1, 5).Select(i => Foe(Shock, i * 0.9f - 2.5f, 6)).ToList();
            var immune = Foe(Arc, 0, 5.5f);
            Hit(direct);
            Assert.That(direct.Health.Current, Is.EqualTo(100 - 4 - 4), "ball 4 + Arc 4");
            Assert.That(near.Count(f => f.Health.Current == 96), Is.EqualTo(5), "two Arc cards reach up to seven nearby");
            Assert.That(immune.Health.Current, Is.EqualTo(100));
        }

        [Test]
        public void HeldLuckyCardsAddTheirBonus()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 2), new CardSpec(Arc, 2), new CardSpec(Shock, 2), new CardSpec(Arc, 3));
            ctx.Hand.SetLuckyNumber(2);
            Assert.That(ctx.Hand.LuckyHeldCount(Arc), Is.EqualTo(2));
            var direct = Foe(Spark, 0, 4);
            Hit(direct);
            Assert.That(direct.Health.Current, Is.EqualTo(100 - 4 - 8), "Arc 4 + 2 per lucky Arc card");
            Assert.That(direct.Status.StunRemaining, Is.EqualTo(1.25f).Within(0.01f), "stun 1 s + 0.25 s per lucky Shockwave card");
        }

        [Test]
        public void HeldSparkBurnsFractionallyAndShockOnlyStuns()
        {
            ctx.Hand.SetHand(new CardSpec(Spark, 1), new CardSpec(Shock, 2));
            ctx.Hand.SetLuckyNumber(0);
            var e = Foe(Arc, 0, 4);
            Hit(e);
            Assert.That(e.Health.Current, Is.EqualTo(96));
            Assert.That(e.Status.IsStunned, "held Shockwave stuns the hit foe");
            ctx.Clock.Advance(6f);
            Assert.That(e.Health.Current, Is.EqualTo(93), "0.5 DPS for 6 s: 3 damage");
        }

        [Test]
        public void TwoKillsInOneGroupEarnOneBall()
        {
            var foes = Enumerable.Range(0, 5).Select(i => Foe(Shock, -4 + 2 * i, 4, 10)).ToList();
            ctx.Hand.SetHand(new CardSpec(Arc, 3), new CardSpec(Arc, 3));
            Assert.That(PlayNumber(3));
            Assert.That(ctx.Casts.LastBatch.Kills, Is.EqualTo(5));
            Assert.That(ctx.Multiball.Pending, Is.EqualTo(1), "one ball per attack");
            Assert.That(ctx.Score.Kills, Is.EqualTo(5));
        }

        [Test]
        public void BallHitAndItsEnchantmentsShareOneAttack()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 3));
            ctx.Hand.SetLuckyNumber(0);
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
            Assert.That(ctx.Spawner.HealthFor(grunt), Is.EqualTo(20));
            Assert.That(ctx.Spawner.CurrentInterval, Is.EqualTo(3).Within(1e-6));
            var early = Foe(Arc, 0, 4, ctx.Spawner.HealthFor(grunt));
            ctx.Clock.Advance(300f);
            Assert.That(ctx.Spawner.HealthFor(grunt), Is.EqualTo(140), "20 x 7 at 300 s");
            Assert.That(ctx.Spawner.CurrentInterval, Is.EqualTo(1).Within(1e-6), "3 / 3");
            Assert.That(ctx.March.SpeedOf(grunt), Is.EqualTo(14.5f / 50f * 1.7f).Within(1e-4f));
            Assert.That(early.Health.Max, Is.EqualTo(20), "living enemies keep their health");
        }
    }
}
