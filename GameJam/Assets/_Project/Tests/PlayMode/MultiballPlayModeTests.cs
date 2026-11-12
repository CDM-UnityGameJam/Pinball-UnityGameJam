using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Pinball.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // Port of the multiball-side-kickers branch: extra balls from lucky kills and multi-kills,
    // side-wall kickers, and the enemies' pop-bumper bounce.
    public class MultiballPlayModeTests
    {
        GameContext context;
        EnemyDefinition grunt;
        BoardRoot Board => context.Board;
        MultiballRules Rules => context.config.multiball;

        [UnitySetUp]
        public IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;
            context = Object.FindAnyObjectByType<GameContext>();
            grunt = context.config.spawnProfile.enemies[0].enemy;
            float end = Time.realtimeSinceStartup + 6f;
            while (context.Run.State != RunState.Playing)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "timed out waiting for the launch");
                yield return null;
            }
            foreach (var ball in context.Balls.Active) ball.Park(Board.launchPoint.Position);
            foreach (var enemy in context.Enemies.FrontFirst(new List<Enemy>())) Object.Destroy(enemy.gameObject);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        static IEnumerator FixedSteps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        int Lucky => context.Luck.Current;
        int NotLucky => Lucky == context.config.luck.minNumber ? Lucky + 1 : Lucky - 1;

        Enemy Spawn(int number, float x, float z) => context.Spawner.Spawn(grunt, number, new Vector3(x, 0f, z));

        Vector3 OnSurface(float x, float z) => Board.ToWorld(new Vector3(x, Board.SurfaceHeightAt(x, z) + 0.375f + 0.01f, z));

        [UnityTest]
        public IEnumerator LuckyKillEarnsABallThatIsHeldThenDropsDownTheTable()
        {
            Spawn(NotLucky, -4f, 4f).Health.TakeDamage(10000);
            Assert.That(context.Multiball.Pending, Is.Zero, "an unlucky kill earns nothing");

            Spawn(Lucky, 4f, 4f).Health.TakeDamage(10000);
            Assert.That(context.Multiball.Pending, Is.EqualTo(1));
            yield return null;

            var held = context.Multiball.Held;
            Assert.That(held, Is.Not.Null);
            Assert.That(context.Balls.Count, Is.EqualTo(2));
            Assert.That(held.Body.isKinematic, "held at the entry");
            Assert.That(Vector3.Distance(held.transform.position, Board.extraBallEntry.Position), Is.LessThan(0.01f));

            float end = Time.time + Rules.entryHold + 0.5f;
            while (context.Multiball.Held && Time.time < end) yield return null;
            Assert.That(context.Multiball.Held, Is.Null, "dropped after the hold");
            Assert.That(held.Body.isKinematic, Is.False);
            yield return FixedSteps(2);
            Assert.That(Board.transform.InverseTransformDirection(held.Body.linearVelocity).z, Is.LessThan(0f), "moving down the table");
        }

        [Test]
        public void MultiKillEarnsOneBallPerAttack()
        {
            var attack = new AttackRecord("Test");
            for (int i = 0; i < 3; i++) Spawn(NotLucky, -4f + 4f * i, 4f).Health.TakeDamage(10000, attack);
            Assert.That(attack.Kills, Is.EqualTo(3));
            Assert.That(context.Multiball.Pending, Is.EqualTo(1));
        }

        [Test]
        public void TheCapCountsBallsWaitingToEnter()
        {
            int free = Rules.maxBalls - context.Balls.Count;
            for (int i = 0; i < free; i++) Assert.That(context.Multiball.AddBall(), Is.True);
            Assert.That(context.Multiball.AddBall(), Is.False);
            Assert.That(context.Multiball.Pending, Is.EqualTo(free));
        }

        [UnityTest]
        public IEnumerator DrainingAnExtraBallCostsNoLife()
        {
            int lives = context.Run.Lives;
            var main = context.Balls.Active[0];
            context.Multiball.AddBall();
            yield return null;
            float end = Time.time + Rules.entryHold + 0.5f;
            while (context.Multiball.Held && Time.time < end) yield return null;

            main.Launch(Board.drain.transform.position, Vector3.zero);
            yield return FixedSteps(3);
            Assert.That(context.Balls.Count, Is.EqualTo(1));
            Assert.That(context.Run.Lives, Is.EqualTo(lives));
            Assert.That(context.Run.State, Is.EqualTo(RunState.Playing));
        }

        [UnityTest]
        public IEnumerator SideWallsKickTheBallUpTheTable()
        {
            var ball = context.Balls.Active[0];
            ball.Launch(OnSurface(-7.3f, 2f), Board.transform.TransformDirection(Vector3.left) * 6f);
            float best = 0f, bestZ = 0f;
            for (int i = 0; i < 40; i++)
            {
                yield return new WaitForFixedUpdate();
                var v = Board.transform.InverseTransformDirection(ball.Body.linearVelocity);
                float planar = new Vector2(v.x, v.z).magnitude;
                if (planar > best) { best = planar; bestZ = v.z; }
            }
            Assert.That(best, Is.GreaterThan(25f), "kicked at least near kickSpeed");
            Assert.That(bestZ, Is.GreaterThan(0f), "toward the top of the board");
        }

        [UnityTest]
        public IEnumerator EnemiesPushASlowBallAwayLikeAPopBumper()
        {
            var enemy = Spawn(NotLucky, 4f, 2f);
            enemy.Health.SetCurrent(10000);
            var ball = context.Balls.Active[0];
            ball.Launch(OnSurface(4f, 2f + grunt.radius + 0.375f + 0.3f), Vector3.zero);
            float best = 0f;
            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
                var v = Board.transform.InverseTransformDirection(ball.Body.linearVelocity);
                best = Mathf.Max(best, new Vector2(v.x, v.z).magnitude);
            }
            Assert.That(best, Is.GreaterThanOrEqualTo(grunt.hitBounceSpeed - 0.5f));
        }
    }
}
