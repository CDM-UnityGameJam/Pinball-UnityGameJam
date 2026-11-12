using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pinball.Feedback;
using Pinball.Gameplay;
using Pinball.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // Phase 7: gameplay raises feedback events, presentation plays the cues set on assets, VFX are pooled,
    // and ball auras follow the hand.
    public class FeedbackPlayModeTests
    {
        GameContext ctx;
        Ball ball;
        EnemyDefinition grunt;
        ElementDefinition Arc, Spark, Shock;
        readonly List<CombatFeedbackEvent> events = new List<CombatFeedbackEvent>();

        [UnitySetUp]
        public IEnumerator Fixture()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            ctx = Object.FindAnyObjectByType<GameContext>();
            ctx.DriveTimeManually = true;
            yield return null;
            ctx.Run.Tick(60f);
            ball = ctx.Balls.Active[0];
            ball.Park(ctx.Board.launchPoint.Position);
            foreach (var e in ctx.Enemies.FrontFirst(new List<Enemy>())) e.Remove();
            grunt = ctx.config.spawnProfile.enemies[0].enemy;
            Arc = ctx.config.elements.First(e => e.displayName == "Arc");
            Spark = ctx.config.elements.First(e => e.displayName == "Spark");
            Shock = ctx.config.elements.First(e => e.displayName == "Shockwave");
            events.Clear();
            ctx.Feedback.Raised += events.Add;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        Enemy Enemy(int number, float x, float z = 4) => ctx.Spawner.Spawn(grunt, number, new Vector3(x, 0, z));

        void Hit(Enemy e)
        {
            ball.Body.isKinematic = false;
            e.Contact.TryBallHit(ball, e.transform.position);
            ball.Body.isKinematic = true;
        }

        List<CombatFeedbackEvent> Of(FeedbackKind kind) => events.Where(e => e.kind == kind).ToList();

        [UnityTest]
        public IEnumerator VfxCueReusesFinishedInstances()
        {
            var cue = Arc.passiveHitVfx;
            int pooledBefore = cue.PooledCount;
            cue.Play(VfxRequest.At(Vector3.zero, Quaternion.identity));
            Assert.That(cue.ActiveCount, Is.EqualTo(1));
            yield return new WaitForSeconds(0.3f); // longer than the 0.12 s bolt (scaled time; the game is playing)
            Assert.That(cue.ActiveCount, Is.Zero, "finished effect released");
            Assert.That(cue.PooledCount, Is.EqualTo(pooledBefore + 1), "back in the pool");
            cue.Play(VfxRequest.At(Vector3.zero, Quaternion.identity));
            Assert.That(cue.PooledCount, Is.EqualTo(pooledBefore), "reused, not instantiated");
        }

        [UnityTest]
        public IEnumerator ArcJumpsRaiseOneHitPerTouchedEnemyAndPlayBolts()
        {
            ctx.Hand.SetHand(new CardSpec(Arc, 6));
            var direct = Enemy(1, 0); var a = Enemy(6, .8f); var b = Enemy(6, 1.6f); Enemy(6, 3.5f);
            int active = Arc.passiveHitVfx.ActiveCount;
            Hit(direct);
            var hits = Of(FeedbackKind.PassiveHit);
            Assert.That(hits.Select(h => h.target), Is.EquivalentTo(new[] { a, b }), "N=1 arc: two nearest #6 in range");
            Assert.That(hits.All(h => h.element == Arc && h.from.HasValue), "bolts start at the ball contact");
            Assert.That(Arc.passiveHitVfx.ActiveCount, Is.EqualTo(active + 2));
            yield break;
        }

        [UnityTest]
        public IEnumerator ShockPulseOnlyWhenSomethingWasStunned()
        {
            ctx.Hand.SetHand(new CardSpec(Shock, 6));
            var direct = Enemy(3, 0);
            Hit(direct);
            Assert.That(Of(FeedbackKind.PassivePulse), Is.Empty, "no #6 nearby: no pulse");
            var six = Enemy(6, .6f, 8);
            ctx.Clock.Advance(.2f);
            Hit(six);
            var pulse = Of(FeedbackKind.PassivePulse).Single();
            Assert.That(pulse.radius, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(pulse.element, Is.EqualTo(Shock));
            yield break;
        }

        [UnityTest]
        public IEnumerator ActiveWavesRaiseHitsStrongAtHighQ()
        {
            var a = Enemy(1, 0, 0); var b = Enemy(2, 0, 1);
            ctx.Hand.SetHand(new CardSpec(Arc, 6), new CardSpec(Arc, 5));
            ctx.Luck.ForceNextQuality(5); ctx.Hand.TryPlay(0);
            var low = Of(FeedbackKind.ActiveHit);
            Assert.That(low.Select(e => e.target), Is.EquivalentTo(new[] { a, b }));
            Assert.That(low.All(e => !e.strong && e.element == Arc));
            events.Clear();
            ctx.Luck.ForceNextQuality(90); ctx.Hand.TryPlay(0);
            Assert.That(Of(FeedbackKind.ActiveHit).All(e => e.strong), "all-targets plans are strong");
            yield break;
        }

        [UnityTest]
        public IEnumerator IgniteAndBurnTicksRaiseStatusEvents()
        {
            var e = Enemy(1, 0);
            e.Health.SetCurrent(100);
            e.Status.Ignite(false, 3, 4, null);
            Assert.That(Of(FeedbackKind.Ignite).Single().strong, Is.False);
            ctx.Clock.Advance(.5f);
            Assert.That(Of(FeedbackKind.BurnTick).Count, Is.EqualTo(1));
            e.Status.Ignite(true, 1, 6, null);
            ctx.Clock.Advance(.5f);
            Assert.That(Of(FeedbackKind.BurnTick).Last().strong, "the tick used the active burn");
            yield break;
        }

        [UnityTest]
        public IEnumerator BallAurasFollowEnabledElements()
        {
            var auras = ball.GetComponent<BallAuraView>();
            ctx.Hand.SetHand(new CardSpec(Arc, 6), new CardSpec(Spark, 2));
            yield return null;
            Assert.That(auras.IsShowing(Arc));
            Assert.That(auras.IsShowing(Spark));
            Assert.That(auras.IsShowing(Shock), Is.False);
            ctx.Luck.ForceNextQuality(-10);
            ctx.Hand.TryPlay(1); // Spark short circuit: the Spark card is gone anyway
            ctx.Luck.ForceNextQuality(-10);
            ctx.Hand.Grant(new CardSpec(Arc, 3));
            ctx.Hand.TryPlay(1); // Arc short-circuited while #6 Arc is still held
            yield return null;
            Assert.That(ctx.Hand.HeldCount(Arc), Is.EqualTo(1));
            Assert.That(auras.IsShowing(Arc), Is.False, "short-circuited: no aura");
            Assert.That(auras.IsShowing(Spark), Is.False);
        }

        // Regression: rolling on the playfield used to raise On Bounce ~23 times a second.
        [Test]
        public void OnlyHitsIntoWallsCountAsBounces()
        {
            var board = ctx.Board;
            Assert.That(ball.IsBounce(board.Up, -board.Up * 10f + board.Forward * 20f), Is.False, "the playfield floor never counts");
            Assert.That(ball.IsBounce(board.transform.right, board.transform.right * 5f), "a hit into a wall counts");
            Assert.That(ball.IsBounce(board.transform.right, board.Forward * 20f), Is.False, "sliding along a wall doesn't");
            Assert.That(ball.IsBounce(board.transform.right, board.transform.right * 2f), Is.False, "too soft");
        }

        [UnityTest]
        public IEnumerator RollingOnThePlayfieldMakesNoBounceSound()
        {
            ctx.DriveTimeManually = false;
            int bounces = 0;
            ball.onBounce.DynamicCalls += () => bounces++;
            var board = ctx.Board;
            var start = new Vector3(0f, 0f, 6f);
            start.y = board.SurfaceHeightAt(start.x, start.z) + 0.375f;
            ball.Launch(board.ToWorld(start), board.Forward * 2f);
            for (int i = 0; i < 50; i++) yield return new WaitForFixedUpdate(); // 0.5 s of free rolling, no walls nearby
            Assert.That(bounces, Is.Zero);
        }

        [UnityTest]
        public IEnumerator EnemyBallHitFiresItsSoundHook()
        {
            var e = Enemy(1, 0);
            var view = e.GetComponent<EnemyView>();
            int calls = 0;
            view.onBallHit.DynamicCalls += _ => calls++;
            Assert.That(view.onBallHit.PersistentCallsList.Count, Is.EqualTo(1), "wired to the SFX player on the prefab");
            Hit(e);
            Assert.That(calls, Is.EqualTo(1));
            yield break;
        }
    }
}
