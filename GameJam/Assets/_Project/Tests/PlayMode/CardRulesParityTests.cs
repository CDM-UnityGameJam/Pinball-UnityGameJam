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
    // Phase 5 "done when": full rules parity with Luck Number v0.3. Every scenario of the old
    // LuckPrototypeCheck ("Run Acceptance Checks" and "Number-limited enchantments") replayed in Game.unity
    // with the same hands, enemy numbers, board positions, forced Q values and time steps.
    // Like the old fixture, the clock is stepped by hand and nothing spawns or marches on its own.
    public class CardRulesParityTests
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
            yield return null; // Start: ball and opening enemies exist
            ctx.Run.Tick(60f); // skip the launch countdown
            ball = ctx.Balls.Active[0];
            ball.Park(ctx.Board.launchPoint.Position);
            foreach (var e in ctx.Enemies.FrontFirst(new List<Enemy>())) e.Remove();
            grunt = ctx.config.spawnProfile.enemies[0].enemy;
            Arc = ctx.config.elements.First(e => e.displayName == "Arc");
            Spark = ctx.config.elements.First(e => e.displayName == "Spark");
            Shock = ctx.config.elements.First(e => e.displayName == "Shockwave");
            Assert.That(ctx.Run.IsPlaying);
            Assert.That(ctx.Clock.Now, Is.Zero);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        // ---- the old fixture's helpers ----
        Enemy Enemy(int number = 1, float x = 0, float z = 4, int hp = 40)
        {
            var e = ctx.Spawner.Spawn(grunt, number, new Vector3(x, 0, z));
            e.Health.SetCurrent(hp);
            return e;
        }

        CardSpec C(ElementDefinition element, int number = 6) => new CardSpec(element, number);
        void Hand(params CardSpec[] cards) => ctx.Hand.SetHand(cards);
        void Tick(float seconds) => ctx.Clock.Advance(seconds);
        bool Use(int slot) => ctx.Hand.TryPlay(slot);
        void FixNext(int q) => Assert.That(ctx.Luck.ForceNextQuality(q));

        bool Hit(Enemy e) => Hit(e, e.transform.position);

        bool Hit(Enemy e, Vector3 point)
        {
            ball.Body.isKinematic = false;
            bool ok = e.Contact.TryBallHit(ball, point);
            ball.Body.isKinematic = true;
            return ok;
        }

        static void Near(double actual, double expected, string message) =>
            Assert.That(actual, Is.EqualTo(expected).Within(0.0002), message);

        // ---- Run Acceptance Checks ----

        [UnityTest]
        public IEnumerator HeldEnchantmentsTriggerTogetherOnContactOnly()
        {
            Hand(C(Arc), C(Arc), C(Spark), C(Spark), C(Shock));
            var direct = Enemy(6); var a = Enemy(6, .7f); var b = Enemy(6, 1.4f); var c = Enemy(6, 2.3f); var far = Enemy(6, 3.2f);
            Tick(1);
            Assert.That(direct.Health.Current == 40 && a.Health.Current == 40, "1 no timed attacks without contact");
            Assert.That(Hit(direct), "accepted contact");
            Assert.That(new[] { direct, a, b, c, far }.Select(e => e.Health.Current), Is.EqualTo(new[] { 36, 36, 36, 36, 40 }),
                "1 direct hit plus arc; no double hit on direct target");
            Assert.That(direct.Status.IsBurning && a.Status.IsBurning && !b.Status.IsBurning && direct.Status.IsStunned,
                "all held enchantments trigger together");
            Assert.That(Hit(direct), Is.False, "contact cooldown");
            Tick(.5f);
            Assert.That(direct.Health.Current == 34 && a.Health.Current == 34 && ctx.Stats.validBallHits == 1, "2 burn and arc never recurse");
            direct.Health.TakeDamage(1);
            Assert.That(ctx.Stats.validBallHits, Is.EqualTo(1), "damage API cannot trigger ball enchantments");
            yield break;
        }

        [UnityTest]
        public IEnumerator LethalCollisionStillEmitsNearbyEffects()
        {
            Hand(C(Arc, 2), C(Spark, 2), C(Spark, 2));
            var direct = Enemy(hp: 4); var other = Enemy(2, .9f);
            Hit(direct);
            Assert.That(!direct.Alive && other.Health.Current == 36 && other.Status.IsBurning, "lethal collision still emits nearby effects");
            yield break;
        }

        [UnityTest]
        public IEnumerator LastCardRemovesEnchantmentAndShortCircuitIsTypeLocal()
        {
            Hand(C(Shock));
            FixNext(0); Use(0);
            Assert.That(ctx.Hand.HeldCount(Shock) == 0 && ctx.Hand.EnabledCount(Shock) == 0, "3 last card removes enchantment");
            ctx.Hand.Grant(C(Arc, 6)); ctx.Hand.Grant(C(Arc, 5)); ctx.Hand.Grant(C(Spark, 4));
            FixNext(-10); Use(0);
            Assert.That(ctx.Hand.HeldCount(Arc) == 1 && ctx.Hand.EnabledCount(Arc) == 0 && ctx.Hand.EnabledCount(Spark) == 1, "short circuit is type-local");
            Near(ctx.Casts.ShortCircuitRemaining(Arc), 4, "negative Q does not heal, disables type");
            Tick(4);
            Assert.That(ctx.Hand.EnabledCount(Arc), Is.EqualTo(1), "enchantment recovers after short circuit");
            yield break;
        }

        [UnityTest]
        public IEnumerator ActiveCoverageAndWavesScaleWithQ([Values(5, 40, 90)] int q)
        {
            var targets = Enumerable.Range(0, 12).Select(i => Enemy(1, x: (i % 3) * 2, z: i * .6f)).ToArray();
            FixNext(q); Use(2); // starting hand slot 2 = 2 Arc
            var cast = ctx.Hand.LastPlay;
            Assert.That(cast.card.element, Is.EqualTo(Arc));
            Assert.That(cast.targets.Count, Is.EqualTo(q == 5 ? 2 : q == 40 ? 6 : 12), "4 locked coverage by Q");
            Assert.That(targets[0].Health.Current == 28 && cast.WavesFired == 1, "first wave applies exactly twelve, never all damage upfront");
            var newcomer = Enemy(2, 4, 8);
            Tick(.249f);
            Assert.That(cast.WavesFired, Is.EqualTo(1), "wave waits .25s");
            Tick(.001f);
            Assert.That(cast.WavesFired, Is.EqualTo(q == 5 ? 1 : 2), "second wave only for medium/high Q");
            Tick(.5f);
            Assert.That(cast.WavesFired == cast.plan.waves && newcomer.Health.Current == 40, "subsequent spawn never joins locked targets");
            if (q == 90) Assert.That(cast.Kills == 12 && ctx.Score.Score == 600, "kills aggregate over all waves at flat fifty points");
            yield break;
        }

        [UnityTest]
        public IEnumerator BurnStackingRules()
        {
            int passiveDps = ((IgnitePassive)Spark.passive).burnDps, activeDps = ((BurnSkill)Spark.active).burnDps;
            var e = Enemy(hp: 100);
            e.Status.Ignite(false, 3, passiveDps, null); Tick(.25f); e.Status.Ignite(false, 3, passiveDps, null); Tick(.25f);
            Assert.That(e.Health.Current, Is.EqualTo(98), "refresh does not reset burn tick phase");
            e.Status.Ignite(true, 1, activeDps, null); Tick(1);
            Assert.That(e.Health.Current, Is.EqualTo(92), "passive and active burning use max dps, not sum");
            e.Status.Ignite(false, 3, passiveDps, null);
            Near(e.Status.ActiveBurnRemaining, 0, "passive cannot extend active high-dps expiry");
            Tick(.5f);
            Assert.That(e.Health.Current, Is.EqualTo(90), "weak burn resumes after active expires");
            var odd = Enemy(1, 4, hp: 100);
            odd.Status.Ignite(true, .75f, activeDps, null); Tick(.6f); odd.Status.Ignite(true, 1, activeDps, null); Tick(.4f);
            Assert.That(odd.Health.Current, Is.EqualTo(94), "refresh between non-aligned expiry and tick preserves phase");
            yield break;
        }

        [UnityTest]
        public IEnumerator StunAndImmunityRules()
        {
            var e = Enemy(hp: 100);
            Assert.That(e.Status.Stun(.35f, false), "passive stun starts");
            Tick(.2f);
            Assert.That(e.Status.Stun(.35f, false), Is.False, "5 passive cannot refresh active stun");
            Tick(.15f);
            Assert.That(!e.Status.IsStunned && !e.Status.Stun(.35f, false), "5 two-second immunity begins after stun");
            Assert.That(e.Status.Stun(1.2f, true), "active stun bypasses passive immunity");
            Tick(.4f); e.Status.Stun(.3f, true);
            Near(e.Status.StunRemaining, .8, "repeated active stun uses max, not addition");
            Tick(.8f);
            Near(e.Status.PassiveImmunityRemaining, 2, "active end also grants passive immunity");
            Tick(2);
            Assert.That(e.Status.Stun(.35f, false), "passive stun works after immunity");
            yield break;
        }

        [UnityTest]
        public IEnumerator PauseAndLaunchWaitFreezeEverything()
        {
            var e = Enemy(hp: 1000);
            FixNext(90); Use(2);  // 2 Arc, high: 4 waves
            FixNext(-10); Use(0); // 6 Spark: short circuit
            e.Status.Ignite(false, 3, 4, null); e.Status.Stun(1, true);
            float refill = ctx.Hand.RefillRemaining, stun = e.Status.StunRemaining, burn = e.Status.PassiveBurnRemaining;
            float shorted = ctx.Casts.ShortCircuitRemaining(Spark), luck = ctx.Luck.Remaining;
            int hp = e.Health.Current;
            ctx.Run.SetPaused(true); Tick(100);
            Assert.That(!Use(0) && e.Health.Current == hp, "7 paused play and burn frozen");
            Near(ctx.Hand.RefillRemaining, refill, "refill frozen");
            Near(ctx.Luck.Remaining, luck, "luck frozen");
            Near(e.Status.StunRemaining, stun, "stun frozen");
            Near(e.Status.PassiveBurnRemaining, burn, "burn duration frozen");
            Near(ctx.Casts.ShortCircuitRemaining(Spark), shorted, "short circuit frozen");
            ctx.Run.SetPaused(false);
            ctx.Run.BeginLaunch(); Tick(100);
            Near(ctx.Hand.RefillRemaining, refill, "launch wait preserves debt");
            Assert.That(e.Health.Current, Is.EqualTo(hp), "waiting freezes pending waves");
            ctx.Run.Tick(60f); ball.Park(ctx.Board.launchPoint.Position);
            Tick(.25f);
            Assert.That(e.Health.Current, Is.EqualTo(hp - 12), "pending arc wave resumes after wait");
            yield break;
        }

        [UnityTest]
        public IEnumerator EveryPlayPaysTheRefillDelay([Values(1, 2, 3, 5)] int count)
        {
            for (int i = 0; i < count; i++) { FixNext(i % 3 == 0 ? 5 : i % 3 == 1 ? -6 : 0); Use(0); }
            double first = 8 + 4 * count;
            Near(ctx.Hand.RefillRemaining, first, "6 every success/failure pays additive delay");
            Tick((float)first - .001f);
            Assert.That(ctx.Hand.Count, Is.EqualTo(5 - count), "no early refill");
            Tick(.001f);
            Assert.That(ctx.Hand.Count, Is.EqualTo(6 - count), "first card at delayed deadline");
            for (int i = 1; i < count; i++) { Tick(8); Assert.That(ctx.Hand.Count, Is.EqualTo(6 - count + i), "remaining cards refill every eight seconds"); }
            Assert.That(ctx.Hand.IsFull, "full recovery at expected deadline");
            if (count == 5) Near(ctx.Stats.lowEquipmentSeconds, 36, "metric uses agreed zero-to-one-card threshold");
            yield break;
        }

        [UnityTest]
        public IEnumerator RefillDebtIsAddedAndSurvivesLifeLoss()
        {
            FixNext(0); Use(0); Tick(3); FixNext(0); Use(0);
            Near(ctx.Hand.RefillRemaining, 13, "in-progress countdown adds four instead of restarting");
            float debt = ctx.Hand.RefillRemaining;
            ctx.Run.LoseLives(ctx.config.breachLifeCost, "Enemy breach");
            Near(ctx.Hand.RefillRemaining, debt, "life loss never clears debt");
            yield break;
        }

        // ---- Number-limited enchantments ----

        [UnityTest]
        public IEnumerator EachEnchantmentOnlyTouchesItsNumber()
        {
            Hand(C(Arc, 6), C(Spark, 2), C(Shock, 4));
            var direct = Enemy(2); var arc = Enemy(6, .8f); var shock = Enemy(4, -.8f); var wrong = Enemy(3, .2f);
            Hit(direct);
            Assert.That(direct.Health.Current == 36 && direct.Status.IsBurning && !direct.Status.IsStunned, "direct #2 gets base damage and #2 burn only");
            Assert.That(arc.Health.Current == 36 && !arc.Status.IsBurning && !arc.Status.IsStunned, "#6 arc activates from a #2 hit");
            Assert.That(shock.Health.Current == 40 && shock.Status.IsStunned && !shock.Status.IsBurning, "#4 stun only");
            Assert.That(wrong.Health.Current == 40 && !wrong.Status.IsBurning && !wrong.Status.IsStunned, "wrong numbers never receive passive effects");
            Assert.That(ctx.Stats.validBallHits, Is.EqualTo(1), "filtered effects cannot recurse");
            yield break;
        }

        [UnityTest]
        public IEnumerator ArcStacksPerNumberAndShortCircuitsTypeWide()
        {
            Hand(C(Arc, 6), C(Arc, 2));
            var direct = Enemy(3);
            var six = new[] { Enemy(6, .5f), Enemy(6, 1f), Enemy(6, 1.5f) };
            var two = new[] { Enemy(2, -.5f), Enemy(2, -1f), Enemy(2, -1.5f) };
            Hit(direct);
            Assert.That(six.Select(e => e.Health.Current).Concat(two.Select(e => e.Health.Current)), Is.EqualTo(new[] { 36, 36, 40, 36, 36, 40 }),
                "split arc numbers each use N=1 and select two qualified targets");
            FixNext(0); Use(0); Tick(.2f); Hit(direct);
            Assert.That(six[0].Health.Current == 36 && two[0].Health.Current == 32 && ctx.Hand.HeldCount(Arc, 6) == 0 && ctx.Hand.HeldCount(Arc, 2) == 1,
                "consuming last #6 arc leaves #2 arc working");
            ctx.Hand.Grant(C(Arc, 6)); FixNext(-10); Use(1);
            Assert.That(ctx.Hand.EnabledCount(Arc, 2), Is.Zero, "short circuit remains type-wide across number groups");
            yield break;
        }

        [UnityTest]
        public IEnumerator SameNumberArcsStackAndWrongNumbersNeverTakeASlot()
        {
            Hand(C(Arc, 6), C(Arc, 6));
            var direct = Enemy(1); var wrong = Enemy(2, .1f);
            var same = new[] { Enemy(6, .5f), Enemy(6, 1), Enemy(6, 1.5f), Enemy(6, 2) };
            Hit(direct);
            Assert.That(same.Take(3).All(e => e.Health.Current == 36) && same[3].Health.Current == 40 && wrong.Health.Current == 40,
                "two same-number arcs use N=2; wrong nearer target cannot consume a slot or relay");
            yield break;
        }

        [UnityTest]
        public IEnumerator SparkTargetsAreCountedPerElementNumberPair()
        {
            Hand(C(Spark, 6), C(Spark, 2));
            var direct = Enemy(2); var nearSix = Enemy(6, .5f); var nearTwo = Enemy(2, -.5f);
            Hit(direct);
            Assert.That(direct.Status.IsBurning && !nearSix.Status.IsBurning && !nearTwo.Status.IsBurning,
                "split sparks each N=1: only matching directly hit target burns");
            Hand(C(Spark, 6), C(Spark, 6), C(Spark, 2));
            Tick(.2f);
            var wrong = Enemy(3, 0, 0); var a = Enemy(6, .7f, 0); var b = Enemy(6, 1.4f, 0); var foreign = Enemy(2, .2f, 0);
            Hit(wrong);
            Assert.That(!wrong.Status.IsBurning && a.Status.IsBurning && !b.Status.IsBurning && !foreign.Status.IsBurning,
                "spark extra targets are counted per element-number pair and filtered before the cap");
            yield break;
        }

        [UnityTest]
        public IEnumerator ShockRadiusGrowsPerNumber()
        {
            Hand(C(Shock, 6), C(Shock, 2));
            var direct = Enemy(3); var six = Enemy(6, 1.3f); var two = Enemy(2, -1.3f);
            Hit(direct);
            Assert.That(!six.Status.IsStunned && !two.Status.IsStunned && !direct.Status.IsStunned, "split shockwaves each use 1.2m radius, not combined 1.45m");
            Hand(C(Shock, 6), C(Shock, 6), C(Shock, 2));
            Tick(.2f); Hit(direct);
            Assert.That(six.Status.IsStunned && !two.Status.IsStunned && !direct.Status.IsStunned, "only #6 radius expands with a second #6 card");
            yield break;
        }

        [UnityTest]
        public IEnumerator ForeignEnemyNearAShockwaveIsNotStunnedOrImmune()
        {
            Hand(C(Shock, 6), C(Shock, 6), C(Shock, 6));
            var front = Enemy(6, 0, 0); var back = Enemy(2, 0, 1.6f);
            Hit(front);
            Assert.That(front.Status.IsStunned && !back.Status.IsStunned && back.Status.PassiveImmunityRemaining == 0,
                "foreign rear in shockwave radius is not stunned or immune");
            Tick(.35f);
            Assert.That(!back.Status.IsStunned && back.Status.PassiveImmunityRemaining == 0, "queue waiting never grants passive stun immunity");
            yield break;
        }

        [UnityTest]
        public IEnumerator ActiveSkillsIgnoreNumbersAndUseTheFrontOfTheQueue()
        {
            foreach (var element in new[] { Arc, Spark, Shock })
            {
                foreach (var old in ctx.Enemies.FrontFirst(new List<Enemy>())) old.Remove();
                Hand(C(element, 6));
                ctx.Luck.SetLuckyNumber(6);
                var a = Enemy(1, 0, 0); var b = Enemy(2, 0, 1); var farMatch = Enemy(6, 0, 8);
                var plan = element.Plan(ctx.config.luck, 5);
                var preview = ctx.Enemies.FrontFirst(new List<Enemy>()).Take(plan.targetCount).ToList();
                FixNext(5); Use(0);
                var cast = ctx.Hand.LastPlay;
                Assert.That(cast.lucky && cast.targets.Count == 2 && cast.targets.SequenceEqual(preview) && !cast.targets.Contains(farMatch),
                    element.name + ": active low strength ignores numbers and never adds distant matching foes");
                if (element == Arc) Assert.That(a.Health.Current == 28 && b.Health.Current == 28 && farMatch.Health.Current == 40, "arc actively damages any number");
                if (element == Spark) Assert.That(a.Status.IsBurning && b.Status.IsBurning && !farMatch.Status.IsBurning, "active fire burns any number");
                if (element == Shock) Assert.That(a.Status.IsStunned && b.Status.IsStunned && !farMatch.Status.IsStunned, "active shock stuns any number");
            }
            yield break;
        }

        [UnityTest]
        public IEnumerator MediumAndHighSkillsSelectByQueueOrderOnly()
        {
            var enemies = Enumerable.Range(0, 12).Select(i => Enemy(i % 6 + 1, 0, i)).ToArray();
            FixNext(40); Use(0);
            Assert.That(ctx.Hand.LastPlay.targets.Count == 6 && ctx.Hand.LastPlay.targets.SequenceEqual(enemies.Take(6)),
                "medium active skill selects nearest six without same-number expansion");
            FixNext(90); Use(0);
            Assert.That(ctx.Hand.LastPlay.targets.Count, Is.EqualTo(12), "high active skill still covers every number");
            yield break;
        }

        // ---- CheckRealCollision: a real physics contact, lethal, triggers the arc exactly once ----
        [UnityTest]
        public IEnumerator RealContactKeepsItsPointAfterALethalHit()
        {
            Hand(C(Arc, 2));
            var direct = Enemy(hp: 4); var other = Enemy(2, 0, 6);
            yield return new WaitForFixedUpdate();
            var board = ctx.Board;
            var start = direct.BoardPosition + new Vector3(-1.15f, 0f, 0f);
            start.y = board.SurfaceHeightAt(start.x, start.z) + 0.375f;
            ball.Launch(board.ToWorld(start), board.transform.right * 8f);
            for (int i = 0; i < 20; i++) { Tick(.01f); yield return new WaitForFixedUpdate(); }
            Assert.That(ctx.Stats.validBallHits == 1 && !direct.Alive && other.Health.Current == 36,
                "actual collision retains point after lethal hit and triggers arc exactly once");
        }
    }
}
