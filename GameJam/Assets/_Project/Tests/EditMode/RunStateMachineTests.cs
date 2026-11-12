using NUnit.Framework;
using Pinball.Gameplay;

namespace Pinball.Tests.EditMode
{
    public class RunStateMachineTests
    {
        RunStateMachine run;
        int launches;

        [SetUp]
        public void Create()
        {
            run = new RunStateMachine(3, 3f);
            launches = 0;
            run.LaunchDue += () => launches++;
            run.BeginLaunch();
        }

        void StartPlaying() => run.Tick(3f);

        [Test]
        public void CountdownLaunchesOnceAfterItsDuration()
        {
            run.Tick(2.9f);
            Assert.That(run.State, Is.EqualTo(RunState.Launching));
            Assert.That(launches, Is.Zero);
            run.Tick(0.1f);
            Assert.That(run.State, Is.EqualTo(RunState.Playing));
            Assert.That(launches, Is.EqualTo(1));
            run.Tick(5f);
            Assert.That(launches, Is.EqualTo(1));
        }

        [Test]
        public void PauseFreezesTheCountdown()
        {
            run.SetPaused(true);
            run.Tick(10f);
            Assert.That(run.State, Is.EqualTo(RunState.Launching));
            Assert.That(run.Countdown, Is.EqualTo(3f));
            run.SetPaused(false);
            run.Tick(3f);
            Assert.That(run.State, Is.EqualTo(RunState.Playing));
        }

        [Test]
        public void LivesAreOnlyLostWhilePlaying()
        {
            run.LoseLives(1, "test");
            Assert.That(run.Lives, Is.EqualTo(3), "not during the countdown");
            StartPlaying();
            run.SetPaused(true);
            run.LoseLives(1, "test");
            Assert.That(run.Lives, Is.EqualTo(3), "not while paused");
            run.SetPaused(false);
            run.LoseLives(1, "test");
            Assert.That(run.Lives, Is.EqualTo(2));
            Assert.That(run.IsPlaying);
        }

        [Test]
        public void ZeroLivesEndsTheRunForGood()
        {
            StartPlaying();
            run.LoseLives(5, "Enemy breach");
            Assert.That(run.State, Is.EqualTo(RunState.GameOver));
            Assert.That(run.EndCause, Is.EqualTo("Enemy breach"));
            Assert.That(run.IsPlaying, Is.False);

            run.SetPaused(true);
            Assert.That(run.Paused, Is.False, "can't pause after game over");
            run.BeginLaunch();
            Assert.That(run.State, Is.EqualTo(RunState.GameOver), "can't relaunch after game over");
        }

        [Test]
        public void NegativeAmountsNeverAddLives()
        {
            StartPlaying();
            run.LoseLives(-4, "test");
            Assert.That(run.Lives, Is.EqualTo(3));
        }

        [Test]
        public void IsPlayingNeedsPlayingAndNotPaused()
        {
            Assert.That(run.IsPlaying, Is.False);
            StartPlaying();
            Assert.That(run.IsPlaying);
            run.TogglePause();
            Assert.That(run.IsPlaying, Is.False);
            run.TogglePause();
            Assert.That(run.IsPlaying);
        }
    }
}
