using System;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // Score for the run, on combat time (port of the v0.6 scoring):
    // - Each kill pays the enemy's score value and is added to the AttackRecord that caused it.
    // - Every ScoringRules.timeInterval combat seconds pay timePoints.
    // - Ball hits that deal damage build a combo; after comboTimeout without one, the highest tier reached pays
    //   once. Game over pays the running combo. Losing a life keeps the combo.
    public sealed class ScoreSystem
    {
        readonly GameContext context;
        readonly ComboScore combo = new ComboScore();

        public long Score { get; private set; }
        public int Kills { get; private set; }
        public int Combo => combo.Combo;
        public double ComboRemaining => combo.Remaining(context.Clock.Now, Rules.comboTimeout);
        public int ComboPending => ComboScore.Reward(Rules.comboTiers, combo.Combo);

        // (enemy, points awarded, source)
        public event Action<Enemy, long, AttackRecord> Killed;
        public event Action<long> ScoreChanged;
        // A source has now killed two or more enemies (raised on every kill from the second on).
        public event Action<AttackRecord> MultiKill;
        // A finished combo paid this many points.
        public event Action<int> ComboPaid;

        ScoringRules Rules => context.config.scoring;

        public ScoreSystem(GameContext context)
        {
            this.context = context;
            context.Clock.Stepped += OnStep;
        }

        void OnStep(double step)
        {
            if (!context.Run.IsPlaying) return;
            var rules = Rules;
            long time = combo.Tick(step, context.Clock.Now, rules.timeInterval, rules.timePoints, rules.comboTimeout, rules.comboTiers, out int reward);
            Add(time);
            PayCombo(reward);
        }

        internal void CountComboHit()
        {
            if (!context.Run.IsPlaying) return;
            PayCombo(combo.Hit(context.Clock.Now, Rules.comboTimeout, Rules.comboTiers));
        }

        // Game over: pay the running combo once (called while the run is still playing).
        internal void FinishCombo() => PayCombo(combo.Finish(Rules.comboTiers));

        internal void AddKill(Enemy enemy, AttackRecord source)
        {
            if (!context.Run.IsPlaying) return;
            long reward = Math.Max(0, enemy.Definition.scoreValue);
            Kills++;
            context.Stats.kills++;
            Add(reward);
            Killed?.Invoke(enemy, reward, source);
            if (source == null) return;
            source.Kills++;
            if (source is CastBatch) context.Stats.batchKills++;
            if (source.Kills >= 2) MultiKill?.Invoke(source);
        }

        void PayCombo(int points)
        {
            if (points <= 0) return;
            Add(points);
            ComboPaid?.Invoke(points);
        }

        void Add(long points)
        {
            if (points <= 0) return;
            Score = Score > long.MaxValue - points ? long.MaxValue : Score + points;
            ScoreChanged?.Invoke(Score);
        }
    }
}
