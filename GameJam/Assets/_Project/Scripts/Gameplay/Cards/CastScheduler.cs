using System;
using System.Collections.Generic;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // Casts played groups and runs their skill waves on the combat clock. Port of the v0.7 RevealBatch / SkillCast:
    // - Casting is immediate: every element in the group casts once, at the group's tier (Normal or Lucky).
    // - Each cast hits the targets it locked when the group was played (minus enemies of its element, which
    //   are immune); enemies that spawn later never join, dead ones are never replaced.
    // - The first wave fires on the cast, later ones every WaveInterval.
    public sealed class CastScheduler : ICombatTimer
    {
        readonly GameContext context;
        readonly List<SkillCast> active = new List<SkillCast>();

        public CastScheduler(GameContext context) => this.context = context;

        public CastBatch LastBatch { get; private set; }
        public int ActiveCasts => active.Count;

        // After a group's first waves fired.
        public event Action<CastBatch> BatchCast;
        public event Action<SkillCast> WaveFired;

        internal void Cast(CastBatch batch)
        {
            LastBatch = batch;
            context.Stats.RecordCast(batch);
            foreach (var cast in batch.Casts)
            {
                Fire(cast);
                if (!cast.Complete) active.Add(cast);
            }
            BatchCast?.Invoke(batch);
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
                double next = double.PositiveInfinity;
                foreach (var cast in active) next = Math.Min(next, cast.NextWaveAt);
                return next;
            }
        }

        // Old order: newest cast first when several waves are due at the same moment.
        public void OnTime(double now)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var cast = active[i];
                if (cast.NextWaveAt <= now + RuleMath.TimeEpsilon) Fire(cast);
                if (cast.Complete) active.RemoveAt(i);
            }
        }
    }
}
