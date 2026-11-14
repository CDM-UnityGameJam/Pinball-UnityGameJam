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

        Enemy Enemy(ElementDefinition element, float x, float z = 4) => ctx.Spawner.Spawn(grunt, element, new Vector3(x, 0, z), health: 100);

        void Hit(Enemy e)
        {
            ball.Body.isKinematic = false;
            e.Contact.TryContact(ball, e.transform.position);
            ball.Body.isKinematic = true;
        }

        bool PlayNumber(int number) => ctx.Hand.TryPlay(ctx.Hand.Group(number));

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
            ctx.Hand.SetHand(new CardSpec(Arc, 3));
            var direct = Enemy(Shock, 0); var a = Enemy(Shock, .8f); var b = Enemy(Spark, 1.6f); var c = Enemy(Shock, 3.5f);
            Enemy(Arc, 1.2f); var d = Enemy(Spark, 3.9f);
            int active = Arc.passiveHitVfx.ActiveCount;
            Hit(direct);
            var hits = Of(FeedbackKind.PassiveHit);
            Assert.That(hits.Select(h => h.target), Is.EquivalentTo(new[] { direct, a, b, c, d }),
                "one Arc card: the hit foe plus up to five nearby that Arc can affect (Arc foes are immune)");
            Assert.That(hits.All(h => h.element == Arc && h.from.HasValue), "bolts start at the ball contact");
            Assert.That(Arc.passiveHitVfx.ActiveCount, Is.EqualTo(active + 5));
            yield break;
        }

        [UnityTest]
        public IEnumerator ShockPulseOnlyWhenSomethingWasStunned()
        {
            ctx.Hand.SetHand(new CardSpec(Shock, 3));
            var direct = Enemy(Shock, 0);
            Hit(direct);
            Assert.That(Of(FeedbackKind.PassivePulse), Is.Empty, "only Shockwave foes nearby (immune): no pulse");
            var arc = Enemy(Arc, .6f, 8);
            Hit(arc);
            var pulse = Of(FeedbackKind.PassivePulse).Single();
            Assert.That(pulse.radius, Is.EqualTo(4f).Within(1e-5f), "one Shockwave card: 4 m");
            Assert.That(pulse.element, Is.EqualTo(Shock));
            yield break;
        }

        [UnityTest]
        public IEnumerator ActiveWavesRaiseHitsStrongOnLuckyCasts()
        {
            var a = Enemy(Shock, 0, 0); var b = Enemy(Spark, 0, 1); Enemy(Arc, 0, 2);
            ctx.Hand.SetHand(new CardSpec(Arc, 2), new CardSpec(Arc, 3), new CardSpec(Arc, 3));
            ctx.Hand.SetLuckyNumber(3);
            Assert.That(PlayNumber(2));
            var low = Of(FeedbackKind.ActiveHit);
            Assert.That(low.Select(e => e.target), Is.EquivalentTo(new[] { a, b }), "a Normal cast fires at once; the Arc foe is immune");
            Assert.That(low.All(e => !e.strong && e.element == Arc));
            ctx.Clock.Advance(1f);
            events.Clear();
            Assert.That(PlayNumber(3));
            Assert.That(Of(FeedbackKind.ActiveHit), Is.Not.Empty);
            Assert.That(Of(FeedbackKind.ActiveHit).All(e => e.strong), "Lucky casts play strong");
            yield break;
        }

        [UnityTest]
        public IEnumerator IgniteAndBurnTicksRaiseStatusEvents()
        {
            var e = Enemy(Arc, 0);
            e.Status.Ignite(Spark, false, 3, 4, null);
            Assert.That(Of(FeedbackKind.Ignite).Single().strong, Is.False);
            ctx.Clock.Advance(.5f);
            Assert.That(Of(FeedbackKind.BurnTick).Count, Is.EqualTo(1));
            e.Status.Ignite(Spark, true, 1, 6, null);
            ctx.Clock.Advance(.5f);
            Assert.That(Of(FeedbackKind.BurnTick).Last().strong, "the tick used the active burn");
            yield break;
        }

        [UnityTest]
        public IEnumerator BallAurasFollowHeldElements()
        {
            var auras = ball.GetComponent<BallAuraView>();
            ctx.Hand.SetHand(new CardSpec(Arc, 3), new CardSpec(Spark, 2));
            yield return null;
            Assert.That(auras.IsShowing(Arc));
            Assert.That(auras.IsShowing(Spark));
            Assert.That(auras.IsShowing(Shock), Is.False);
            Assert.That(PlayNumber(2));
            yield return null;
            Assert.That(auras.IsShowing(Arc));
            Assert.That(auras.IsShowing(Spark), Is.False, "the Spark card was played");
        }

        // Regression: rolling on the playfield used to raise On Bounce ~23 times a second.
        [Test]
        public void OnlyHitsIntoWallsCountAsBounces()
        {
            var board = ctx.Board;
            Assert.That(ball.IsBounce(board.Up, -board.Up * 10f + board.Forward * 20f), Is.False, "the playfield floor never counts");
            Assert.That(ball.IsBounce(board.transform.right, board.transform.right * 5f), "a hit into a wall counts");
            Assert.That(ball.IsBounce(board.transform.right, board.Forward * 20f), Is.False, "sliding along a wall doesn't");
            Assert.That(ball.IsBounce(board.transform.right, board.transform.right * 0.5f), Is.False, "too soft");
            Assert.That(ball.IsBounce(-board.Up, board.Up * 5f), Is.False, "the underside of the glass never counts");
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
        public IEnumerator EnemyBallContactFiresItsSoundHook()
        {
            var e = Enemy(Arc, 0);
            var view = e.GetComponent<EnemyView>();
            int calls = 0;
            view.onBallHit.DynamicCalls += _ => calls++;
            // Both creature sounds play on a ball hit: the pinball impact and the creature's "get hit" voice.
            Assert.That(view.onBallHit.PersistentCallsList.Count, Is.EqualTo(2), "wired to both SFX players on the prefab");
            Hit(e);
            Hit(e);
            Assert.That(calls, Is.EqualTo(2), "every new contact, also inside the damage cooldown");
            yield break;
        }
    }
}
