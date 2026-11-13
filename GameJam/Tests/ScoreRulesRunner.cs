using System;
public sealed class Card {public enum Equipment {Arc,Spark,Shockwave}}
class ScoreRulesRunner
{
    static void Check(bool v,string s){if(!v)throw new Exception(s);}
    static void Main(){
        var rules=new ScoreRules();var a=new ScoreAccount();int reward;
        Check(rules.Errors().Count==0,"defaults valid");
        Check(a.Tick(9.99,9.99,rules,out reward)==0,"no early time reward");Check(a.Tick(.01,10,rules,out reward)==1,"ten seconds awards one");Check(a.Tick(20,30,rules,out reward)==2,"large timestep pays each interval");
        for(int i=0;i<50;i++)Check(a.Hit(30+i*.1,rules)==0,"thresholds never pay immediately");
        Check(a.Combo==50,"fifty hits accumulated");Check(a.Tick(0,37.89,rules,out reward)==0&&reward==0,"before expiry no reward");a.Tick(0,37.9,rules,out reward);Check(reward==10&&a.Combo==0,"highest tier only");a.Tick(0,40,rules,out reward);Check(reward==0,"no duplicate settlement");
        for(int i=0;i<10;i++)a.Hit(41+i*.1,rules);Check(a.Finish(rules)==5&&a.Finish(rules)==0,"game-over finish settles once");
        for(int i=0;i<9;i++)a.Hit(50,rules);Check(a.Finish(rules)==0,"below first tier awards zero");
        a=new ScoreAccount();for(int i=0;i<10;i++)a.Hit(0,rules);Check(a.Hit(3,rules)==5&&a.Combo==1,"contact at timeout finishes old and begins new combo");
        rules.comboTiers=new[]{new ComboRewardTier(2,7),new ComboRewardTier(4,20)};Check(rules.Reward(3)==7&&rules.Reward(99)==20,"custom tiers");rules.comboTimeout=0;Check(rules.Errors().Count>0,"invalid timeout rejected");rules=new ScoreRules();rules.comboTiers[1].hits=10;Check(rules.Errors().Count>0,"duplicate threshold rejected");
        Console.WriteLine("SCORE RULES PASSED: timed points, deferred highest-tier rewards, exact timeout, single settlement, game over, custom tiers and validation.");
    }
}
