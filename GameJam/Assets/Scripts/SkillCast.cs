using System.Collections.Generic;
using UnityEngine;

// One record spans every wave / burn tick caused by the same cast or ball hit.
public class AttackRecord
{
    public int Kills { get; internal set; }
    public readonly string Label;
    public AttackRecord(string label) { Label = label; }
}

public sealed class SkillCast : AttackRecord
{
    public readonly Card Card;
    public readonly int Quality;
    public readonly SkillPlan Plan;
    public readonly List<Enemy> Targets;
    public int WavesFired { get; private set; }
    public double NextWaveAt { get; private set; } = double.PositiveInfinity;
    public bool Complete => WavesFired >= Plan.waves;
    public SkillCast(Card card, int quality, SkillPlan plan, List<Enemy> targets) : base(card.SkillName)
    { Card=card; Quality=quality; Plan=plan; Targets=targets; }
    public void FireWave(GameManager gm)
    {
        if (Complete || Quality <= 0) return;
        bool first = WavesFired == 0;
        foreach (var e in Targets) {
            if (!e || !e.Alive) continue;
            if (Card.equipment == Card.Equipment.Spark) e.Ignite(true, Plan.burnDuration, this);
            else {
                if (Card.equipment == Card.Equipment.Shockwave && first) e.Stun(Plan.stunDuration, true);
                if (gm.Vfx) gm.Vfx.ActiveHit(Card.equipment, e.transform.position, Quality);
                e.TakeDamage(Card.equipment == Card.Equipment.Arc ? gm.luck.activeArcDamage : gm.luck.activeShockDamage, this);
            }
        }
        WavesFired++;
        NextWaveAt = Complete ? double.PositiveInfinity : gm.CombatTime + Mathf.Max(.01f, Plan.waveInterval);
    }
}
