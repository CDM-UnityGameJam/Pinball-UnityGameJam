using System;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // Replacement ball after the last one drains (port of the v0.6 continuous relaunch):
    // - The run keeps playing: enemies march and spawn, skills and burns resolve, charge builds, cards can be played.
    // - After GameConfig.launchCountdown seconds of combat time a new ball launches from the launch point.
    //   No ball sits on the table meanwhile.
    // - If another ball appears first (a multiball reward), the replacement is cancelled.
    public sealed class BallRelaunch : ICombatTimer
    {
        readonly GameContext context;
        double dueAt = double.PositiveInfinity;

        public BallRelaunch(GameContext context) => this.context = context;

        public bool Pending => !double.IsPositiveInfinity(dueAt);
        public float Remaining => Pending ? (float)Math.Max(0, dueAt - context.Clock.Now) : 0f;

        public event Action Launched;

        internal void Begin(float delay)
        {
            dueAt = context.Clock.Now + Math.Max(0, delay);
            if (delay <= 0f) OnTime(context.Clock.Now);
        }

        internal void Cancel() => dueAt = double.PositiveInfinity;

        public double NextEventAt => dueAt;

        public void OnTime(double now)
        {
            if (!Pending || dueAt > now + RuleMath.TimeEpsilon) return;
            dueAt = double.PositiveInfinity;
            if (!context.Run.IsPlaying || context.Balls.Count > 0 || context.Multiball.Pending > 0) return;
            context.LaunchNewBall();
            Launched?.Invoke();
        }
    }
}
