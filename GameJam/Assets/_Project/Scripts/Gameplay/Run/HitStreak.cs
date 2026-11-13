using System;

namespace Pinball.Gameplay
{
    // Consecutive valid ball hits on enemies (the HUD's COMBO). Counts what RunStats.validBallHits counts;
    // resets only when the last ball drains, so losing an extra multiball ball keeps the streak.
    public sealed class HitStreak
    {
        public int Count { get; private set; }

        public event Action<int> Changed;

        public HitStreak(GameContext context)
        {
            context.Combat.HitResolved += (enemy, point, source) => Set(Count + 1);
            context.LastBallDrained += () => Set(0);
        }

        void Set(int value)
        {
            Count = value;
            Changed?.Invoke(Count);
        }
    }
}
