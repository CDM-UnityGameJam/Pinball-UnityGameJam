using System;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The lucky number and quality (Q) rolls. Port of the old GameManager lucky-number and NextOutcome code:
    // - The lucky number changes every luckyRefreshInterval seconds of combat time, never to the same number.
    // - A card whose number matches the lucky number (locked when it's played) rolls with the lucky weights.
    public sealed class LuckSystem : ICombatTimer
    {
        readonly GameContext context;
        double nextChangeAt;

        public int Current { get; private set; }
        public int? ForcedQuality { get; private set; }
        public float Remaining => (float)Math.Max(0, nextChangeAt - context.Clock.Now);

        public event Action<int> LuckyNumberChanged;

        LuckConfig Config => context.config.luck;

        public LuckSystem(GameContext context)
        {
            this.context = context;
            Current = LuckyNumberRule.First(Config.minNumber, Config.maxNumber, context.Random);
            nextChangeAt = context.Clock.Now + Mathf.Max(0.01f, Config.luckyRefreshInterval);
        }

        public int RollQuality(bool lucky)
        {
            if (ForcedQuality.HasValue)
            {
                int forced = ForcedQuality.Value;
                ForcedQuality = null;
                return forced;
            }
            return QualityRoller.Roll(Config.bands, lucky, context.Random);
        }

        // Debug: the next roll returns q (once).
        public bool ForceNextQuality(int q)
        {
            int min = int.MaxValue, max = int.MinValue;
            foreach (var band in Config.bands) { min = Math.Min(min, band.minQ); max = Math.Max(max, band.maxQ); }
            if (q < min || q > max) return false;
            ForcedQuality = q;
            return true;
        }

        public void ClearForcedQuality() => ForcedQuality = null;

        // Debug: change the lucky number without resetting its timer.
        public bool SetLuckyNumber(int number)
        {
            if (number < Config.minNumber || number > Config.maxNumber || context.Run.State == RunState.GameOver) return false;
            Current = number;
            LuckyNumberChanged?.Invoke(Current);
            return true;
        }

        public double NextEventAt => nextChangeAt;

        public void OnTime(double now)
        {
            if (nextChangeAt > now + RuleMath.TimeEpsilon) return;
            Current = LuckyNumberRule.Next(Current, Config.minNumber, Config.maxNumber, context.Random);
            nextChangeAt = now + Mathf.Max(0.01f, Config.luckyRefreshInterval);
            LuckyNumberChanged?.Invoke(Current);
        }
    }
}
