using System;
using System.Collections.Generic;

namespace Pinball.Core
{
    // Something on the combat clock with its own deadline (a burn tick, a skill wave, a refill...).
    public interface ICombatTimer
    {
        // Next time this timer wants OnTime called; +infinity for none.
        double NextEventAt { get; }

        // Called when the clock reaches NextEventAt. The timer must move its deadline forward.
        void OnTime(double now);
    }

    // Gameplay time, separate from Unity time. Port of the old GameManager.TickGameplay loop:
    // - It only advances while Running (stopped during pause, launch wait and game over).
    // - Advance(elapsed) steps exactly to every deadline inside the span, in order, so effects land on
    //   the right moment regardless of frame rate.
    // - When several are due at the same moment, timers fire in registration order, then one-shot
    //   callbacks in scheduling order.
    public sealed class CombatClock
    {
        readonly List<ICombatTimer> timers = new List<ICombatTimer>();
        readonly List<Scheduled> scheduled = new List<Scheduled>();
        readonly List<ICombatTimer> firing = new List<ICombatTimer>();
        long nextScheduleId;
        bool advancing;

        public double Now { get; private set; }

        // Set by the run state. Checked between steps, so stopping mid-advance (game over) works.
        public bool Running { get; set; }

        // Raised after each step with the step length (for telemetry such as average hand size).
        public event Action<double> Stepped;

        public void Register(ICombatTimer timer)
        {
            if (timer != null && !timers.Contains(timer)) timers.Add(timer);
        }

        public void Unregister(ICombatTimer timer) => timers.Remove(timer);

        // One-shot callback at an absolute combat time. Returns an id for Cancel.
        public long Schedule(double at, Action callback)
        {
            if (callback == null) return -1;
            long id = nextScheduleId++;
            scheduled.Add(new Scheduled { id = id, at = at, callback = callback });
            return id;
        }

        public long ScheduleIn(double delay, Action callback) => Schedule(Now + Math.Max(0, delay), callback);

        public bool Cancel(long id) => scheduled.RemoveAll(s => s.id == id) > 0;

        public void Advance(float elapsed)
        {
            if (!Running || advancing || elapsed <= 0f || float.IsNaN(elapsed) || float.IsInfinity(elapsed)) return;
            advancing = true;
            try
            {
                double end = Now + elapsed;
                while (Now + RuleMath.TimeEpsilon < end && Running)
                {
                    double next = Math.Max(Now, Math.Min(end, NextDeadline()));
                    double step = next - Now;
                    Now = next;
                    Stepped?.Invoke(step);
                    FireDue();
                }
            }
            finally { advancing = false; }
        }

        double NextDeadline()
        {
            double next = double.PositiveInfinity;
            foreach (var timer in timers) next = Math.Min(next, timer.NextEventAt);
            foreach (var s in scheduled) next = Math.Min(next, s.at);
            return next;
        }

        void FireDue()
        {
            firing.Clear();
            firing.AddRange(timers);
            foreach (var timer in firing)
            {
                if (!Running) return;
                if (timers.Contains(timer) && timer.NextEventAt <= Now + RuleMath.TimeEpsilon) timer.OnTime(Now);
            }
            for (int i = 0; i < scheduled.Count && Running;)
            {
                var s = scheduled[i];
                if (s.at <= Now + RuleMath.TimeEpsilon)
                {
                    scheduled.RemoveAt(i);
                    s.callback();
                }
                else i++;
            }
        }

        // Back to time zero for a new run. Registered timers stay; scheduled callbacks are dropped.
        public void Reset()
        {
            Now = 0;
            scheduled.Clear();
        }

        struct Scheduled
        {
            public long id;
            public double at;
            public Action callback;
        }
    }
}
