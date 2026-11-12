using System.Collections.Generic;
using UnityEngine;

public class AttackRecord
{
    public int Kills {get;internal set;}
    public readonly string Label;
    public AttackRecord(string label) {Label=label;}
}

public sealed class SkillCast
{
    public readonly DiceBatch Batch;
    public readonly Card.Equipment Element;
    public readonly int Copies;
    public readonly SkillPlan Plan;
    public readonly List<Enemy> Targets;
    public int WavesFired {get;private set;}
    public double NextWaveAt {get;private set;}=double.PositiveInfinity;
    public bool Complete => WavesFired>=(Element==Card.Equipment.Shockwave?1:Plan.waves);
    public SkillCast(DiceBatch batch,Card.Equipment element,int copies,SkillPlan plan,List<Enemy> targets)
    {Batch=batch;Element=element;Copies=copies;Plan=plan;Targets=targets;}
    public void FireWave(GameManager gm)
    {
        if(Complete)return;
        bool first=WavesFired==0;
        foreach(var e in Targets) {
            if(!e || !e.CanReceive(Element))continue;
            Batch.Cover(e);
            if(Element==Card.Equipment.Spark)e.Ignite(true,Plan.burnDuration,Batch,Plan.burnDps,Plan.burnTickInterval);
            else {
                if(Element==Card.Equipment.Shockwave && first)e.Stun(Plan.stunDuration,true,Plan.stunImmunity);
                if(gm.Vfx)gm.Vfx.ActiveHit(Element,e.transform.position,Batch.Tier);
                e.TakeElementDamage(Plan.damage,Element,Batch);
            }
        }
        WavesFired++;
        NextWaveAt=Complete?double.PositiveInfinity:gm.CombatTime+System.Math.Max(.000001,Plan.waveInterval);
    }
}
