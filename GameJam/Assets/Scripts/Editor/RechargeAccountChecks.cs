using System;

public static class RechargeAccountChecks
{
    static void Check(bool ok,string name){if(!ok)throw new Exception("Recharge: "+name);}
    static void Near(double a,double b,string name){Check(Math.Abs(a-b)<.000001,name);}
    public static string Run()
    {
        var a=new RechargeAccount();Near(a.EarnNatural(10),0,"full hand gains nothing");a.Play(true,100,50);
        Near(a.EarnCombat(0,2.5,6,.25),2.5,"combat one");Near(a.EarnCombat(0,2.5,6,.25),2.5,"combat two");Near(a.EarnCombat(0,2.5,6,.25),1,"combat budget remainder");Near(a.EarnCombat(0,2.5,6,.25),.25,"floor above budget");Near(a.WindowUsed(0),6.25,"example total 6.25");
        Near(a.EarnNatural(10),10,"natural is independent");Near(a.WindowUsed(0),6.25,"natural never enters combat window");
        a.Play(false,100,50);Near(a.Required,200,"extra cast cost");Near(a.Progress,16.25,"cast preserves progress");
        a.FullHand();a.Play(true,100,50);Near(a.EarnCombat(.5,2.5,6,.25),.25,"hand and cast never reset combat window");Near(a.EarnCombat(1,2.5,6,.25),2.5,"old grants expire after one second");
        var rng=new Random(606);var stress=new RechargeAccount();stress.StartMissing(1000000);
        for(int i=0;i<30000;i++){double now=i*.001;double before=stress.WindowUsed(now);double expected=Math.Min(2.5,Math.Max(.25,6-before));Near(stress.EarnCombat(now,2.5,6,.25),expected,"all repeated events follow formula");if(rng.Next(3)==0)stress.EarnNatural(.1);}
        var batch=new RechargeAccount();batch.PlayBatch(true,3,100,50);Near(batch.Required,250,"three-card batch");batch.EarnNatural(450);Check(batch.Consume(100),"consume 250");Near(batch.Progress,200,"overflow");Check(batch.Consume(100)&&batch.Consume(100),"consume next two hundred");batch.FullHand();Near(batch.Progress,0,"full clears surplus");
        Cadence(false,1,new[]{15.0});Cadence(false,3,new[]{25.0,35,45});Cadence(true,1,new[]{12.0});Cadence(true,3,new[]{20.0,28,36});
        return "Recharge v0.6 PASSED: 2.5/2.5/1/0.25, separate natural recovery, 30000 event formula checks, window persistence and reference cadence.";
    }
    static void Cadence(bool hits,int spent,double[] times)
    {
        var a=new RechargeAccount();a.PlayBatch(true,spent,100,50);int filled=0;
        for(int step=1;step<=10000 && filled<spent;step++){
            double now=step*.01;a.EarnNatural(.1);if(hits && step%100==0)a.EarnCombat(now,2.5,6,.25);
            while(a.Consume(100)){Check(Math.Abs(now-times[filled])<=.011,"cadence");filled++;if(filled==spent){a.FullHand();break;}}
        }
        Check(filled==spent,"refills complete");
    }
}
