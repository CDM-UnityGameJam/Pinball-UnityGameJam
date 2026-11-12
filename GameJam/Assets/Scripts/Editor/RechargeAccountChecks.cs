using System;
using System.Collections.Generic;

public static class RechargeAccountChecks
{
    struct Sample {public double time,amount;public Sample(double t,double a){time=t;amount=a;}}
    static void Check(bool ok,string name){if(!ok)throw new Exception("Recharge: "+name);}
    static void Near(double a,double b,string name){Check(Math.Abs(a-b)<.000001,name);}
    public static string Run()
    {
        var a=new RechargeAccount();
        Near(a.Earn(0,10,16),0,"full hand gains nothing");
        a.Play(true,100,50);Near(a.Required,150,"first play costs 150");
        a.Earn(0,3,16);a.Play(false,100,50);Near(a.Progress,3,"play keeps progress");Near(a.Required,200,"next play adds fifty");a.Play(false,100,50);Near(a.Required,250,"three plays cost 250");
        a.FullHand();Near(a.Progress,0,"full hand clears progress");a.Play(true,100,50);Near(a.Earn(0,30,16),13,"full hand/play does not reset cap history");
        Near(a.Earn(.9999,2,16),0,"no fixed bucket reset");Near(a.Earn(1,20,16),16,"grants expire after one rolling second");
        var boundary=new RechargeAccount();boundary.StartMissing(10000);boundary.Earn(.99,16,16);Near(boundary.Earn(1.01,16,16),0,"fixed-second boundary cannot double budget");
        var random=new Random(404);var meter=new RechargeAccount();meter.StartMissing(1000000);var grants=new List<Sample>();double now=0;
        for(int i=0;i<30000;i++) {
            now+=random.NextDouble()*.007;
            double request=i%5==0?3:random.NextDouble()*.2;
            double actual=meter.Earn(now,request,16);grants.Add(new Sample(now,actual));
            double sum=0;for(int j=grants.Count-1;j>=0 && grants[j].time>now-1;j--)sum+=grants[j].amount;
            Check(sum<=16.0000001,"every rolling window stays below sixteen");Near(sum,meter.WindowUsed(now),"journal matches independently summed gains");
            if(i%1000==0){meter.FullHand();meter.Play(true,100,50);}
        }
        var refill=new RechargeAccount();refill.Play(true,100,50);refill.Play(false,100,50);refill.Play(false,100,50);
        int cards=2;double total=0;var thresholds=new List<double>();
        for(int i=1;cards<5;i++) {
            total+=refill.Earn(i*1.001,Math.Min(16,450-total),16);
            while(refill.Consume(100)) {cards++;thresholds.Add(total);if(cards==5){refill.FullHand();break;}}
        }
        Check(thresholds.Count==3 && thresholds[0]>=250 && thresholds[1]>=350 && thresholds[2]==450,"three cards require cumulative 250/350/450, overflow continues");Near(refill.Progress,0,"final overflow discarded");
        var exact=new RechargeAccount();exact.Play(true,100,50);exact.Play(false,100,50);exact.Play(false,100,50);
        for(int i=1;i<=45;i++)exact.Earn(i*1.001,10,16);
        Check(exact.Consume(100),"first consumes 250");Near(exact.Progress,200,"overflow retained");Check(exact.Consume(100),"second consumes 100");Near(exact.Progress,100,"third remains");Check(exact.Consume(100),"third consumes 100");Near(exact.Progress,0,"exact totals");
        CheckCadence(false,1,new double[]{15});
        CheckCadence(false,3,new double[]{25,35,45});
        CheckCadence(true,1,new double[]{12});
        CheckCadence(true,3,new double[]{20,28,36});
        return "Recharge account PASSED: 30000 independent rolling-window checks, full-hand history, additive cost, overflow and 250/350/450 totals.";
    }
    static void CheckCadence(bool regular,int spent,double[] expected)
    {
        var a=new RechargeAccount();
        for(int i=0;i<spent;i++)a.Play(i==0,100,50);
        int refills=0;
        for(int step=1;refills<spent && step<=10000;step++) {
            double now=step*.01;
            a.Earn(now,.1,16);
            if(regular && step%100==0)a.Earn(now,2.5,16);
            while(a.Consume(100)) {
                Check(Math.Abs(now-expected[refills])<=.011,"no-hit / unified-hit cadence matches design reference");
                refills++;if(refills==spent){a.FullHand();break;}
            }
        }
        Check(refills==spent,"reference cadence completes");
    }

}
