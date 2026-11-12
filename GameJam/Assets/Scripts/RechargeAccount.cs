using System;
using System.Collections.Generic;

// Natural recovery is independent. Only actual combat gains enter the rolling soft-budget journal.
public sealed class RechargeAccount
{
    struct Grant {public double time,amount;public Grant(double t,double a){time=t;amount=a;}}
    readonly Queue<Grant> combatJournal=new Queue<Grant>();
    double combatUsed;
    public double Progress {get;private set;}
    public double Required {get;private set;}
    public void StartMissing(double cost){if(Required<=0)Required=Math.Max(.001,cost);}
    public void Play(bool wasFull,double baseCost,double extraCost){if(wasFull){Progress=0;Required=Math.Max(.001,baseCost);}else StartMissing(baseCost);Required+=Math.Max(0,extraCost);}
    public void PlayBatch(bool wasFull,int count,double baseCost,double perCardCost){if(count<1||count>5)throw new ArgumentOutOfRangeException("count");Play(wasFull,baseCost,perCardCost*count);}
    public void FullHand(){Progress=0;Required=0;}
    public double WindowUsed(double now)
    {
        while(combatJournal.Count>0 && combatJournal.Peek().time<=now-1)combatUsed-=combatJournal.Dequeue().amount;
        return combatUsed=Math.Max(0,combatUsed);
    }
    public double EarnNatural(double amount){if(Required<=0 || amount<=0)return 0;Progress+=amount;return amount;}
    public double EarnCombat(double now,double normal,double budget,double minimum)
    {
        double used=WindowUsed(now);if(Required<=0 || normal<=0)return 0;
        double actual=Math.Min(normal,Math.Max(Math.Max(0,minimum),budget-used));
        if(actual<=0)return 0;
        combatJournal.Enqueue(new Grant(now,actual));combatUsed+=actual;Progress+=actual;return actual;
    }
    public bool Consume(double baseCost){if(Required<=0 || Progress+1e-8<Required)return false;Progress=Math.Max(0,Progress-Required);Required=Math.Max(.001,baseCost);return true;}
}
