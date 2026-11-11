using System;
using System.Collections.Generic;

// Pure accounting logic; the journal is shared by all charge sources and all balls.
public sealed class RechargeAccount
{
    struct Grant { public double time, amount; public Grant(double time,double amount) { this.time=time;this.amount=amount; } }
    readonly Queue<Grant> journal=new Queue<Grant>();
    double windowUsed;
    public double Progress { get; private set; }
    public double Required { get; private set; }
    public void StartMissing(double baseCost) { if(Required<=0) Required=Math.Max(.001,baseCost); }
    public void Play(bool wasFull,double baseCost,double extraCost)
    {
        if(wasFull) { Progress=0;Required=Math.Max(.001,baseCost); }
        else StartMissing(baseCost);
        Required+=Math.Max(0,extraCost);
    }
    public void FullHand() { Progress=0;Required=0; } // Deliberately keeps the rolling journal.
    public double WindowUsed(double now)
    {
        // Standard sliding window (now-1, now]. No fixed-second buckets.
        while(journal.Count>0 && journal.Peek().time<=now-1.0) windowUsed-=journal.Dequeue().amount;
        windowUsed=Math.Max(0,windowUsed);
        return windowUsed;
    }
    public double Earn(double now,double requested,double cap)
    {
        double used=WindowUsed(now);
        if(Required<=0 || requested<=0) return 0;
        double actual=Math.Min(requested,Math.Max(0,cap-used));
        if(actual<=1e-10) return 0;
        journal.Enqueue(new Grant(now,actual));windowUsed+=actual;Progress+=actual;
        return actual;
    }
    public bool Consume(double nextBaseCost)
    {
        if(Required<=0 || Progress+1e-8<Required) return false;
        Progress=Math.Max(0,Progress-Required);Required=Math.Max(.001,nextBaseCost);
        return true;
    }
}
