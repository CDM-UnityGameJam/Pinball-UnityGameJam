using System.Collections.Generic;
using NUnit.Framework;
using Pinball.Core;

namespace Pinball.Tests.EditMode
{
    public class CombatClockTests
    {
        // Fires at a fixed interval and records when.
        sealed class Repeater : ICombatTimer
        {
            readonly double interval;
            readonly string name;
            readonly List<string> log;
            public readonly List<double> times = new List<double>();
            public int remaining;

            public Repeater(double first, double interval, int count, string name = "", List<string> log = null)
            { NextEventAt = first; this.interval = interval; remaining = count; this.name = name; this.log = log; }

            public double NextEventAt { get; private set; }

            public void OnTime(double now)
            {
                times.Add(now);
                log?.Add(name);
                NextEventAt = --remaining > 0 ? now + interval : double.PositiveInfinity;
            }
        }

        CombatClock clock;

        [SetUp] public void Create() => clock = new CombatClock { Running = true };

        [Test]
        public void DoesNotAdvanceWhileStopped()
        {
            clock.Running = false;
            clock.Advance(5);
            Assert.That(clock.Now, Is.Zero);
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void IgnoresInvalidSteps(float elapsed)
        {
            clock.Advance(elapsed);
            Assert.That(clock.Now, Is.Zero);
        }

        // Port of acceptance item 4: "wave waits .25s".
        [Test]
        public void EventsLandOnTheirExactDeadline()
        {
            var wave = new Repeater(0.25, 0.25, 3);
            clock.Register(wave);
            clock.Advance(0.249f);
            Assert.That(wave.times, Is.Empty);
            clock.Advance(0.001f);
            Assert.That(wave.times.Count, Is.EqualTo(1));
        }

        [Test]
        public void OneLargeStepFiresEveryDeadlineInOrderAtItsOwnTime()
        {
            var wave = new Repeater(0.25, 0.25, 4);
            clock.Register(wave);
            clock.Advance(10);
            Assert.That(wave.times, Is.EqualTo(new[] { 0.25, 0.5, 0.75, 1.0 }).Within(1e-9));
            Assert.That(clock.Now, Is.EqualTo(10).Within(1e-6));
        }

        [Test]
        public void SimultaneousTimersFireInRegistrationOrder()
        {
            var log = new List<string>();
            clock.Register(new Repeater(1, 1, 1, "status", log));
            clock.Register(new Repeater(1, 1, 1, "cast", log));
            clock.Register(new Repeater(1, 1, 1, "refill", log));
            clock.Advance(1);
            Assert.That(log, Is.EqualTo(new[] { "status", "cast", "refill" }));
        }

        [Test]
        public void SteppedReportsEverySubStep()
        {
            var steps = new List<double>();
            clock.Stepped += steps.Add;
            clock.Register(new Repeater(0.3, 0.3, 2));
            clock.Advance(1);
            Assert.That(steps, Is.EqualTo(new[] { 0.3, 0.3, 0.4 }).Within(1e-6));
        }

        [Test]
        public void StoppingMidAdvanceHaltsTheClock()
        {
            var breach = new Repeater(0.5, 1, 1);
            clock.Register(breach);
            clock.Register(new StopOnTime(clock, 0.5));
            var later = new Repeater(2, 1, 1);
            clock.Register(later);
            clock.Advance(5);
            Assert.That(clock.Now, Is.EqualTo(0.5).Within(1e-9), "game over stops time at the breach");
            Assert.That(later.times, Is.Empty);
        }

        sealed class StopOnTime : ICombatTimer
        {
            readonly CombatClock clock;
            public StopOnTime(CombatClock clock, double at) { this.clock = clock; NextEventAt = at; }
            public double NextEventAt { get; private set; }
            public void OnTime(double now) { clock.Running = false; NextEventAt = double.PositiveInfinity; }
        }

        [Test]
        public void ReentrantAdvanceIsIgnored()
        {
            clock.Register(new AdvanceOnTime(clock));
            clock.Advance(1);
            Assert.That(clock.Now, Is.EqualTo(1).Within(1e-6));
        }

        sealed class AdvanceOnTime : ICombatTimer
        {
            readonly CombatClock clock;
            public AdvanceOnTime(CombatClock clock) => this.clock = clock;
            public double NextEventAt { get; private set; } = 0.5;
            public void OnTime(double now) { NextEventAt = double.PositiveInfinity; clock.Advance(100); }
        }

        [Test]
        public void ScheduledCallbacksFireOnceAndCanBeCancelled()
        {
            int fired = 0, cancelled = 0;
            clock.ScheduleIn(0.5, () => fired++);
            long id = clock.Schedule(0.7, () => cancelled++);
            Assert.That(clock.Cancel(id));
            clock.Advance(0.49f);
            Assert.That(fired, Is.Zero);
            clock.Advance(2);
            Assert.That(fired, Is.EqualTo(1));
            Assert.That(cancelled, Is.Zero);
        }

        [Test]
        public void UnregisteredTimersStopFiring()
        {
            var timer = new Repeater(0.5, 0.5, 10);
            clock.Register(timer);
            clock.Advance(1);
            clock.Unregister(timer);
            clock.Advance(5);
            Assert.That(timer.times.Count, Is.EqualTo(2));
        }

        // Port of acceptance item 7: pausing / launch wait preserves pending deadlines.
        [Test]
        public void PauseKeepsPendingDeadlines()
        {
            var wave = new Repeater(0.25, 0.25, 2);
            clock.Register(wave);
            clock.Running = false;
            clock.Advance(100);
            clock.Running = true;
            clock.Advance(0.25f);
            Assert.That(wave.times, Is.EqualTo(new[] { 0.25 }).Within(1e-9));
        }
    }
}
