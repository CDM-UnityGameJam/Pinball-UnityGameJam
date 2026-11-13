using System;
using System.Collections.Generic;

[Serializable]
public sealed class ComboRewardTier
{
    public int hits,points;
    public ComboRewardTier(int hits,int points){this.hits=hits;this.points=points;}
}
[Serializable]
public sealed class ScoreRules
{
    public float timeInterval=10,comboTimeout=3;
    public int timePoints=1;
    public ComboRewardTier[] comboTiers={new ComboRewardTier(10,5),new ComboRewardTier(50,10)};
    public List<string> Errors()
    {
        var e=new List<string>();
        if(!LuckTuning.NonNegative(timeInterval)||timeInterval<=0)e.Add("Scoring time interval must be finite and positive.");
        if(!LuckTuning.NonNegative(comboTimeout)||comboTimeout<=0)e.Add("Combo timeout must be finite and positive.");
        if(timePoints<0)e.Add("Time points must be non-negative.");
        if(comboTiers==null||comboTiers.Length==0)e.Add("At least one combo reward tier is required.");
        else {int previous=0;foreach(var t in comboTiers){if(t==null||t.hits<=previous||t.points<0){e.Add("Combo thresholds must be positive, strictly increasing; points must be non-negative.");break;}previous=t.hits;}}
        return e;
    }
    public int Reward(int hits){int points=0;foreach(var t in comboTiers){if(hits<t.hits)break;points=t.points;}return points;}
}
// Only gameplay time advances this account. Each completed combo pays its highest tier once.
public sealed class ScoreAccount
{
    double timeProgress,lastHit;
    public int Combo {get;private set;}
    public double Remaining(double now,ScoreRules rules)=>Combo==0?0:Math.Max(0,rules.comboTimeout-(now-lastHit));
    public long Tick(double elapsed,double now,ScoreRules rules,out int comboReward)
    {
        comboReward=0;if(Combo>0&&now-lastHit>=rules.comboTimeout-1e-7)comboReward=Finish(rules);
        timeProgress+=elapsed;double count=Math.Floor((timeProgress+1e-7)/rules.timeInterval);
        timeProgress=Math.Max(0,timeProgress-count*rules.timeInterval);
        if(rules.timePoints==0)return 0;
        return count>=long.MaxValue/(double)rules.timePoints?long.MaxValue:(long)count*rules.timePoints;
    }
    public int Hit(double now,ScoreRules rules)
    {
        int reward=Combo>0&&now-lastHit>=rules.comboTimeout-1e-7?Finish(rules):0;
        if(Combo<int.MaxValue)Combo++;lastHit=now;return reward;
    }
    public int Finish(ScoreRules rules){int reward=rules.Reward(Combo);Combo=0;return reward;}
}
