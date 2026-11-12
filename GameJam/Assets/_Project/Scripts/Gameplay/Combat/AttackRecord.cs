namespace Pinball.Gameplay
{
    // One source of damage that can span several hits over time (a ball hit's enchantments, every wave and
    // burn tick of one cast). Kills are counted per record, for multi-kill feedback and stats.
    public class AttackRecord
    {
        public readonly string label;
        public int Kills { get; internal set; }

        public AttackRecord(string label) => this.label = label;
    }
}
