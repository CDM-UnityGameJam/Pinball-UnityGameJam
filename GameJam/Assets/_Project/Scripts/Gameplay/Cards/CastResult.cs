using System.Collections.Generic;
using Pinball.Core;

namespace Pinball.Gameplay
{
    // One played card: what was rolled and, for a cast, its locked targets and wave progress.
    // It is also the AttackRecord for every wave and burn tick it causes, so kills add up per cast.
    public sealed class CastResult : AttackRecord
    {
        public readonly CardSpec card;
        public readonly int quality;
        public readonly CastOutcome outcome;
        public readonly bool lucky;
        public readonly int luckyNumber;
        public readonly SkillPlan plan;
        public readonly IReadOnlyList<Enemy> targets;

        public int WavesFired { get; internal set; }
        public double NextWaveAt { get; internal set; } = double.PositiveInfinity;
        public bool Complete => WavesFired >= plan.waves;

        public CastResult(CardSpec card, int quality, CastOutcome outcome, bool lucky, int luckyNumber, SkillPlan plan, IReadOnlyList<Enemy> targets)
            : base(card.element ? card.element.skillName : "Cast")
        {
            this.card = card;
            this.quality = quality;
            this.outcome = outcome;
            this.lucky = lucky;
            this.luckyNumber = luckyNumber;
            this.plan = plan;
            this.targets = targets;
        }
    }
}
