using System;
using System.Collections.Generic;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // Reveals played batches and runs their skill waves on the combat clock. Port of the v0.6 RevealBatch /
    // SkillCast:
    // - A batch reveals its dice `diceRevealDelay` after it was played. Then every element in it casts once,
    //   at the batch's tier (Base when no die matched: a skill always fires).
    // - Each cast hits the targets it locked when the batch was played (minus enemies of its element, which
    //   are immune); enemies that spawn later never join, dead ones are never replaced.
    // - The first wave fires on the reveal, later ones every WaveInterval.
    public sealed class CastScheduler : ICombatTimer
    {
        readonly GameContext context;
        readonly List<SkillCast> active = new List<SkillCast>();

        public CastScheduler(GameContext context) => this.context = context;

        // The batch waiting for its dice (at most one).
        public DiceBatch PendingBatch { get; private set; }
        public DiceBatch LastBatch { get; private set; }
        public int ActiveCasts => active.Count;

        public event Action<DiceBatch> BatchRevealed;
        public event Action<SkillCast> WaveFired;

        internal void Accept(DiceBatch batch)
        {
            PendingBatch = batch;
            LastBatch = batch;
        }

        void Reveal()
        {
            var batch = PendingBatch;
            PendingBatch = null;
            batch.Revealed = true;
            context.Stats.RecordReveal(batch);
            context.Hand.Resolving = true;
            try
            {
                foreach (var cast in batch.Casts)
                {
                    Fire(cast);
                    if (!cast.Complete) active.Add(cast);
                }
            }
            finally { context.Hand.Resolving = false; }
            BatchRevealed?.Invoke(batch);
        }

        void Fire(SkillCast cast)
        {
            if (cast.Complete) return;
            var skill = cast.element.active;
            skill?.FireWave(new SkillWave(context, cast, cast.WavesFired == 0));
            cast.WavesFired++;
            cast.NextWaveAt = cast.Complete ? double.PositiveInfinity
                : context.Clock.Now + Math.Max(0.000001, skill != null ? skill.WaveInterval : 0.01f);
            WaveFired?.Invoke(cast);
        }

        public double NextEventAt
        {
            get
            {
                double next = PendingBatch != null ? PendingBatch.revealAt : double.PositiveInfinity;
                foreach (var cast in active) next = Math.Min(next, cast.NextWaveAt);
                return next;
            }
        }

        // Old order: the reveal first, then waves newest cast first when several are due at the same moment.
        public void OnTime(double now)
        {
            if (PendingBatch != null && PendingBatch.revealAt <= now + RuleMath.TimeEpsilon) Reveal();
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var cast = active[i];
                if (cast.NextWaveAt <= now + RuleMath.TimeEpsilon) Fire(cast);
                if (cast.Complete) active.RemoveAt(i);
            }
        }
    }
}
