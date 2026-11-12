using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Pinball.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // Phase 4 "done when": enemies spawn in the SpawnZone, march, breach, take hits and die.
    public class EnemyPlayModeTests
    {
        GameContext context;
        EnemyDefinition grunt;

        [UnitySetUp]
        public IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null; // let Start run (opening spawn)
            context = Object.FindAnyObjectByType<GameContext>();
            grunt = context.config.spawnProfile.enemies[0].enemy;
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        IEnumerator WaitUntilPlaying()
        {
            float end = Time.realtimeSinceStartup + 6f;
            while (context.Run.State != RunState.Playing)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "timed out waiting for the launch");
                yield return null;
            }
            // Keep the ball out of the way so it can't hit the enemies under test.
            foreach (var ball in context.Balls.Active) ball.Park(context.Board.launchPoint.Position);
        }

        static IEnumerator FixedSteps(int count)
        {
            for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate();
        }

        List<Enemy> All => context.Enemies.FrontFirst(new List<Enemy>());

        [Test]
        public void OpeningEnemiesStandOnTheSpawnRowInsideTheArc()
        {
            var zone = context.Board.spawnZone;
            var enemies = All;
            Assert.That(enemies.Count, Is.EqualTo(context.config.spawnProfile.openingCount));
            var columns = new List<float>();
            foreach (var e in enemies)
            {
                var p = e.BoardPosition;
                Assert.That(p.z, Is.EqualTo(zone.RowZ).Within(0.01f));
                Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(zone.HalfWidthAt(zone.RowZ) - grunt.radius - grunt.wallClearance + 0.001f));
                Assert.That(e.Number, Is.InRange(context.config.luck.minNumber, context.config.luck.maxNumber));
                Assert.That(p.y, Is.EqualTo(context.Board.SurfaceHeightAt(p.x, p.z) + e.centerHeight).Within(0.01f), "stands on the playfield");
                columns.Add(Mathf.Round(p.x * 100f) / 100f);
            }
            Assert.That(columns, Is.EquivalentTo(new[] { -2f, 0f, 2f }), "shuffled columns 2 m apart");
        }

        [UnityTest]
        public IEnumerator EnemiesHoldDuringTheCountdownThenMarchAtTheirSpeed()
        {
            var front = All[0];
            float z0 = front.BoardPosition.z;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(front.BoardPosition.z, Is.EqualTo(z0), "no movement before launch");

            yield return WaitUntilPlaying();
            var lone = context.Spawner.Spawn(grunt, 3, new Vector3(5f, 0f, 2f));
            yield return new WaitForFixedUpdate();
            float start = lone.BoardPosition.z;
            yield return FixedSteps(100);
            float expected = context.March.SpeedOf(grunt) * 1f;
            Assert.That(start - lone.BoardPosition.z, Is.EqualTo(expected).Within(0.02f), "one second of marching");
            Assert.That(expected, Is.EqualTo((9.5f - -5f) / 40f).Within(0.001f), "v0.3 speed");
        }

        [UnityTest]
        public IEnumerator StunnedEnemyStaysPut()
        {
            yield return WaitUntilPlaying();
            var e = context.Spawner.Spawn(grunt, 3, new Vector3(5f, 0f, 2f));
            yield return new WaitForFixedUpdate();
            Assert.That(e.Status.Stun(1f, true));
            float z = e.BoardPosition.z;
            yield return FixedSteps(50);
            Assert.That(e.BoardPosition.z, Is.EqualTo(z).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator ReachingTheDangerLineBreaches()
        {
            yield return WaitUntilPlaying();
            int lives = context.Run.Lives;
            float danger = context.Board.dangerLine.BoardZ;
            var e = context.Spawner.Spawn(grunt, 3, new Vector3(5f, 0f, danger + 0.02f));
            yield return FixedSteps(10);
            Assert.That(context.Run.Lives, Is.EqualTo(lives - context.config.breachLifeCost));
            Assert.That(e.gameObject.activeSelf, Is.False, "back in the pool");
            Assert.That(All, Has.No.Member(e));
            Assert.That(context.Score.Kills, Is.Zero, "a breach is not a kill");
        }

        [UnityTest]
        public IEnumerator BallHitsDamageWithACooldownAndKillsScore()
        {
            yield return WaitUntilPlaying();
            var e = context.Spawner.Spawn(grunt, 3, new Vector3(5f, 0f, 2f));
            var ball = context.Balls.Active[0];
            ball.Body.isKinematic = false;
            int damage = context.config.ballHitDamage;
            int hits = 0;
            context.Enemies.BallHit += (enemy, b, point) => { if (enemy == e) hits++; };

            Assert.That(e.Contact.TryBallHit(ball, e.transform.position));
            Assert.That(e.Health.Current, Is.EqualTo(grunt.health - damage));
            Assert.That(e.Contact.TryBallHit(ball, e.transform.position), Is.False, "cooldown");
            yield return FixedSteps(Mathf.CeilToInt(grunt.hitCooldown / Time.fixedDeltaTime) + 1);
            Assert.That(e.Contact.TryBallHit(ball, e.transform.position), "cooldown passed");
            Assert.That(hits, Is.EqualTo(2), "every valid hit is reported for enchantments");

            e.Health.SetCurrent(damage);
            yield return FixedSteps(Mathf.CeilToInt(grunt.hitCooldown / Time.fixedDeltaTime) + 1);
            long score = context.Score.Score;
            Assert.That(e.Contact.TryBallHit(ball, e.transform.position), "lethal hit still counts");
            Assert.That(hits, Is.EqualTo(3), "a lethal hit still reports, so enchantments can trigger");
            Assert.That(e.gameObject.activeSelf, Is.False, "dead enemies return to the pool");
            Assert.That(context.Score.Score, Is.EqualTo(score + grunt.scoreValue));
            Assert.That(context.Score.Kills, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RealBallCollisionDealsDamage()
        {
            yield return WaitUntilPlaying();
            var board = context.Board;
            var e = context.Spawner.Spawn(grunt, 3, new Vector3(4f, 0f, 0f));
            yield return new WaitForFixedUpdate();
            var ball = context.Balls.Active[0];
            var start = e.BoardPosition + new Vector3(-1.5f, 0f, 0f);
            start.y = board.SurfaceHeightAt(start.x, start.z) + 0.375f;
            ball.Launch(board.ToWorld(start), board.transform.right * 8f);
            for (int i = 0; i < 30 && e.Health.Current == grunt.health; i++) yield return new WaitForFixedUpdate();
            Assert.That(e.Health.Current, Is.EqualTo(grunt.health - context.config.ballHitDamage));
        }

        [UnityTest]
        public IEnumerator BurnTicksOnTheCombatClock()
        {
            yield return WaitUntilPlaying();
            var e = context.Spawner.Spawn(grunt, 3, new Vector3(5f, 0f, 2f));
            e.Health.SetCurrent(100);
            e.Status.Ignite(false, 3f, 4, null);
            Assert.That(e.Status.IsBurning);
            yield return FixedSteps(55); // past the first tick at 0.5 s
            Assert.That(e.Health.Current, Is.EqualTo(98), "4 DPS x 0.5 s");
        }

        [UnityTest]
        public IEnumerator EnemiesKeepSpawningWhilePlaying()
        {
            int added = 0;
            context.Enemies.Added += _ => added++;
            yield return WaitUntilPlaying();
            // The three opening enemies block the whole spawn row until they have marched 1.85 m down
            // (about 5.1 s); after that, attempts every 2 s at 96 % find room.
            yield return FixedSteps(1000);
            Assert.That(added, Is.GreaterThanOrEqualTo(1));
        }

        [Test]
        public void ReturnLaneOutletsBlockSpawnsAboveThem()
        {
            // The return lanes are switched off (side-wall kickers replaced them); the spawner skips inactive
            // blockers, but the outlets keep theirs for when the lanes are switched back on.
            var blockers = context.Board.GetComponentsInChildren<SpawnBlocker>(true);
            Assert.That(blockers.Length, Is.EqualTo(2), "one per return lane outlet");
            float danger = context.Board.dangerLine.BoardZ, row = context.Board.spawnZone.RowZ;
            float reach = grunt.radius + grunt.wallClearance;
            foreach (var b in blockers)
            {
                var outlet = b.BoardPosition;
                Assert.That(b.Blocks(new Vector3(outlet.x, 0f, row), reach, danger), "straight above the outlet");
                Assert.That(b.Blocks(new Vector3(0f, 0f, row), reach, danger), Is.False, "the middle of the row is free");
            }
        }
    }
}
