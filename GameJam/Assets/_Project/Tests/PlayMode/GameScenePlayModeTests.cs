using System.Collections;
using NUnit.Framework;
using Pinball.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pinball.Tests.PlayMode
{
    // Phase 3 "done when": you can flip, launch, drain and lose lives in Game.unity.
    public class GameScenePlayModeTests
    {
        GameContext context;

        [UnitySetUp]
        public IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            context = Object.FindAnyObjectByType<GameContext>();
            Assert.That(context, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator Restore()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        IEnumerator WaitUntilPlaying(float timeoutSeconds = 6f)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (context.Run.State != RunState.Playing)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "timed out waiting for the launch");
                yield return null;
            }
        }

        Ball OnlyBall
        {
            get
            {
                Assert.That(context.Balls.Count, Is.EqualTo(1));
                return context.Balls.Active[0];
            }
        }

        IEnumerator DrainTheBall()
        {
            var ball = OnlyBall;
            ball.Body.linearVelocity = Vector3.zero;
            ball.Body.position = context.Board.drain.transform.position;
            for (int i = 0; i < 3 && context.Run.State == RunState.Playing; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator BallWaitsThenLaunchesAfterTheCountdown()
        {
            Assert.That(context.Run.State, Is.EqualTo(RunState.Launching));
            var ball = OnlyBall;
            Assert.That(ball.Body.isKinematic, "ball waits still");
            Assert.That(Vector3.Distance(ball.Body.position, context.Board.launchPoint.Position), Is.LessThan(0.01f));
            Assert.That(Time.timeScale, Is.Zero, "physics stopped during the countdown");

            float start = Time.realtimeSinceStartup;
            yield return WaitUntilPlaying();
            Assert.That(Time.realtimeSinceStartup - start, Is.GreaterThan(context.config.launchCountdown - 0.5f));
            Assert.That(ball.Body.isKinematic, Is.False);
            Assert.That(ball.Body.linearVelocity.magnitude, Is.EqualTo(context.config.launchSpeed).Within(0.5f));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator FlipperSwingsUpWhilePressedAndBack()
        {
            yield return WaitUntilPlaying();
            var flipper = context.Board.flippers[0];
            flipper.SetPressedOverride(true);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(flipper.IsPressed);
            Assert.That(flipper.Angle, Is.EqualTo(flipper.upAngle), "50 degrees at 1800 deg/s takes under 3 steps");
            flipper.SetPressedOverride(false);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            Assert.That(flipper.Angle, Is.EqualTo(flipper.restAngle));
        }

        [UnityTest]
        public IEnumerator DrainCostsALifeAndWaitsToRelaunch()
        {
            yield return WaitUntilPlaying();
            yield return DrainTheBall();
            Assert.That(context.Run.Lives, Is.EqualTo(context.config.lives - context.config.drainLifeCost));
            Assert.That(context.Run.State, Is.EqualTo(RunState.Launching));
            var ball = OnlyBall;
            Assert.That(ball.Body.isKinematic);
            Assert.That(Vector3.Distance(ball.Body.position, context.Board.launchPoint.Position), Is.LessThan(0.01f));
            yield return WaitUntilPlaying();
            Assert.That(OnlyBall.Body.isKinematic, Is.False, "relaunched");
        }

        [UnityTest]
        public IEnumerator LosingEveryLifeEndsTheRun()
        {
            for (int life = 0; life < context.config.lives; life++)
            {
                yield return WaitUntilPlaying();
                yield return DrainTheBall();
            }
            Assert.That(context.Run.State, Is.EqualTo(RunState.GameOver));
            Assert.That(context.Run.Lives, Is.Zero);
            Assert.That(Time.timeScale, Is.Zero);
        }

        [UnityTest]
        public IEnumerator PauseFreezesTimeAndTheBall()
        {
            yield return WaitUntilPlaying();
            yield return new WaitForFixedUpdate();
            context.Run.SetPaused(true);
            double clock = context.Clock.Now;
            var position = OnlyBall.Body.position;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(context.Clock.Now, Is.EqualTo(clock));
            Assert.That(OnlyBall.Body.position, Is.EqualTo(position));
            context.Run.SetPaused(false);
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(context.Clock.Now, Is.GreaterThan(clock));
        }

        // Continuous collision: a very fast ball must bounce off a resting flipper, not pass through it.
        [UnityTest]
        public IEnumerator FastBallDoesNotTunnelThroughAFlipper()
        {
            yield return WaitUntilPlaying();
            var board = context.Board;
            var bat = board.flippers[0].transform.Find("Bat");
            var ball = OnlyBall;
            var batBoard = board.ToBoard(bat.position);
            ball.Body.position = board.ToWorld(batBoard + new Vector3(0f, 0f, 2f));
            ball.Body.linearVelocity = -board.Forward * 80f;
            for (int i = 0; i < 6; i++) yield return new WaitForFixedUpdate();
            Assert.That(board.ToBoard(ball.Body.position).z, Is.GreaterThan(batBoard.z), "ball stayed above the bat");
        }
    }
}
