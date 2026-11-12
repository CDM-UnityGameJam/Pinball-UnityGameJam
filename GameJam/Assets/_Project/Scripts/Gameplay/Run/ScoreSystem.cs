using System;

namespace Pinball.Gameplay
{
    // Score and kills for the run. Kills only count while the run is playing; each kill is also added to the
    // AttackRecord that caused it (a ball hit's enchantments, or one cast across all its waves).
    public sealed class ScoreSystem
    {
        readonly RunStateMachine run;
        readonly RunStats stats;

        public long Score { get; private set; }
        public int Kills { get; private set; }

        // (enemy, points awarded, source)
        public event Action<Enemy, long, AttackRecord> Killed;
        public event Action<long> ScoreChanged;
        // A source has now killed two or more enemies (raised on every kill from the second on).
        public event Action<AttackRecord> MultiKill;

        public ScoreSystem(RunStateMachine run, RunStats stats)
        {
            this.run = run;
            this.stats = stats;
        }

        internal void AddKill(Enemy enemy, AttackRecord source)
        {
            if (!run.IsPlaying) return;
            long reward = Math.Max(0, enemy.Definition.scoreValue);
            Score = Score > long.MaxValue - reward ? long.MaxValue : Score + reward;
            Kills++;
            stats.kills++;
            Killed?.Invoke(enemy, reward, source);
            ScoreChanged?.Invoke(Score);
            if (source == null) return;
            source.Kills++;
            if (source.Kills >= 2) MultiKill?.Invoke(source);
        }
    }
}
