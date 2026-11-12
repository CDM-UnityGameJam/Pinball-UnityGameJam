using System;
using System.Collections.Generic;

// Natural recovery and physical contacts grant their full configured amount, with no rate cap.
public sealed class RechargeAccount
{
    struct Grant {public double time,amount;public Grant(double t,double a){time=t;amount=a;}}
    readonly Queue<Grant> combatJournal=new Queue<Grant>();
    double combatUsed;
    bool pending;
    public double Progress {get;private set;}
    public double Required {get;private set;}
    public void StartMissing(double cost){if(!pending){pending=true;Required=Math.Max(0,cost);}}
    public void Play(bool wasFull,double baseCost,double extraCost){if(wasFull){Progress=0;pending=true;Required=Math.Max(0,baseCost);}else StartMissing(baseCost);Required+=Math.Max(0,extraCost);}
    public void PlayBatch(bool wasFull,int count,double baseCost,double perCardCost){if(count<1||count>5)throw new ArgumentOutOfRangeException("count");Play(wasFull,baseCost,perCardCost*count);}
    public void FullHand(){Progress=0;Required=0;pending=false;}
    public double WindowUsed(double now)
    {
        while(combatJournal.Count>0 && combatJournal.Peek().time<=now-1)combatUsed-=combatJournal.Dequeue().amount;
        return combatUsed=Math.Max(0,combatUsed);
    }
    public double EarnNatural(double amount){if(!pending || amount<=0)return 0;Progress+=amount;return amount;}
    public double EarnCombat(double now,double normal)
    {
        double used=WindowUsed(now);if(!pending || normal<=0)return 0;
        double actual=normal;
        if(actual<=0)return 0;
        combatJournal.Enqueue(new Grant(now,actual));combatUsed+=actual;Progress+=actual;return actual;
    }
    public bool Consume(double baseCost){if(!pending || Progress+1e-8<Required)return false;Progress=Math.Max(0,Progress-Required);Required=Math.Max(0,baseCost);return true;}
}
