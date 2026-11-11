using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RechargeCheck
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string name){if(!ok)throw new Exception("Recharge integration: "+name);}
    static void Near(double a,double b,string name){Check(Math.Abs(a-b)<.0002,name+$" ({a} != {b})");}
    static Card C(int n=6,Card.Equipment type=Card.Equipment.Arc)=>new Card(n,type);
    static RechargeAccount Account(GameManager gm)=>(RechargeAccount)typeof(GameManager).GetField("recharge",Private).GetValue(gm);
    static void Progress(GameManager gm,double value)=>typeof(RechargeAccount).GetField("<Progress>k__BackingField",Private).SetValue(Account(gm),value);
    [MenuItem("LuckyPinball/Luck Number/Check Recharge Account")]
    public static void CheckAccount()=>Debug.Log(RechargeAccountChecks.Run());
    public static void RunIntegration()
    {
        CheckAccount();
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;gm.DebugFixNextOutcome(0);gm.UseCard(0);var ball=f.Ball();
            var different=f.Enemy(1,hp:1000);different.TryBallHit(ball);Near(gm.RechargeProgress,2,"different number grants two");
            var same=f.Enemy(6,3,hp:1000);same.TryBallHit(ball);Near(gm.RechargeProgress,5,"same number grants three once regardless of duplicate held numbers");
            double hit=gm.Metrics.hitCharge;gm.TickGameplay(.2f);different.TryBallHit(f.Ball());Near(gm.Metrics.hitCharge,hit,"same enemy cooldown shared by multiple balls");
            gm.TickGameplay(.8f);different.TryBallHit(ball);Near(gm.Metrics.hitCharge,hit+2,"same enemy eligible again after one second");
            Check(gm.Metrics.eligibleRechargeHits==3 && gm.Metrics.missingPhysicalHits==4,"record raw versus recharge-eligible hits separately");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;f.Hand(C(),C(),C(6,Card.Equipment.Spark),C(6,Card.Equipment.Spark),C(6,Card.Equipment.Shockwave));gm.DebugFixNextOutcome(0);gm.UseCard(0);
            var direct=f.Enemy(6,hp:4);f.Enemy(6,.6f,hp:4);f.Enemy(6,1.2f,hp:4);direct.TryBallHit(f.Ball());
            Check(gm.Score==150 && gm.Metrics.validBallHits==1,"one collision can kill three through enchantments");Near(gm.Metrics.hitCharge,3,"lethal physical hit still grants exactly one award");
            gm.TickGameplay(.5f);Near(gm.Metrics.hitCharge,3,"burn ticks and kills grant no charge");
            gm.DebugFixNextOutcome(90);gm.UseCard(0);gm.TickGameplay(1);Near(gm.Metrics.hitCharge,3,"active skills grant no charge");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;gm.DebugFixNextOutcome(-10);gm.UseCard(2);var same=f.Enemy(4,hp:1000);same.TryBallHit(f.Ball());
            Check(gm.ShortCircuitRemaining(Card.Equipment.Arc)>0,"arc shorted");Near(gm.Metrics.hitCharge,3,"short circuit and equipment type do not affect number bonus");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;f.Hand();var e=f.Enemy(3,hp:1000);e.TryBallHit(f.Ball());Near(gm.RechargeProgress,2,"empty hand still earns base hit charge");gm.TickGameplay(1);Near(gm.RechargeProgress,12,"empty hand still recovers naturally");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;f.Hand(C());gm.TickGameplay(.2f);var ball=f.Ball();
            for(int i=0;i<20;i++)f.Enemy(6,i*4,hp:1000).TryBallHit(ball);
            Near(gm.RechargeProgress,16,"natural and all enemy/ball sources share sixteen-point cap");Near(gm.Metrics.naturalCharge+gm.Metrics.hitCharge,16,"telemetry counts only accepted charge");
            gm.TickGameplay(.7f);Near(gm.RechargeProgress,16,"discarded gains are not queued");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;for(int i=0;i<3;i++){gm.DebugFixNextOutcome(i==0?5:i==1?0:-6);gm.UseCard(0);}
            Near(gm.RechargeRequired,250,"three outcomes incur same cost");Progress(gm,249.5);var e=f.Enemy(4,hp:1000);e.TryBallHit(f.Ball());
            Check(gm.CardCount==3,"first card filled after effects");Near(gm.RechargeProgress,2.5,"hit overflow enters next card");Near(gm.RechargeRequired,100,"subsequent card costs one hundred");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;gm.DebugFixNextOutcome(0);gm.UseCard(2);Progress(gm,149);
            // Arrange the next random card to be a #1 arc, without adding a production debug override.
            bool found=false;for(int seed=0;seed<10000;seed++){UnityEngine.Random.InitState(seed);var state=UnityEngine.Random.state;var c=Card.Random();if(c.number==1&&c.equipment==Card.Equipment.Arc){UnityEngine.Random.state=state;found=true;break;}}
            Check(found,"deterministic next card found");var direct=f.Enemy(3,hp:1000);var other=f.Enemy(1,.8f);var ball=f.Ball();direct.TryBallHit(ball);
            Check(gm.IsHandFull && gm.HeldCount(Card.Equipment.Arc,1)==1 && other.hp==40,"new card cannot affect collision that earned it");Near(gm.RechargeProgress,0,"full hand clears surplus");Near(gm.RechargeRequired,0,"full hand stores no demand");double used=gm.RechargeWindowUsed;
            gm.TickGameplay(.2f);direct.TryBallHit(ball);Check(other.hp==36,"new card starts with next collision");Near(gm.RechargeWindowUsed,used,"full hand neither banks charge nor resets history");
            gm.DebugFixNextOutcome(0);gm.UseCard(0);Near(gm.RechargeWindowUsed,used,"new cast does not reset limiter");Near(gm.RechargeRequired,150,"new missing cycle costs one fifty");
        }
        using(var f=new LuckPrototypeCheck.Fixture()) {
            var gm=f.gm;gm.TickGameplay(12);gm.DebugFixNextOutcome(0);gm.UseCard(0);gm.TickGameplay(2);double p=gm.RechargeProgress,r=gm.RechargeRequired,w=gm.RechargeWindowUsed;
            gm.SetPaused(true);gm.TickGameplay(100);Near(gm.RechargeProgress,p,"pause stops charge");Near(gm.RechargeWindowUsed,w,"effective-time limiter also pauses");gm.SetPaused(false);LuckPrototypeCheck.State(gm,"Waiting");gm.TickGameplay(100);Near(gm.RechargeProgress,p,"waiting stops charge");
            LuckPrototypeCheck.State(gm,"Playing");gm.LoseLife();Near(gm.RechargeProgress,p,"life loss keeps progress");Near(gm.RechargeRequired,r,"life loss keeps demand");
            LuckPrototypeCheck.State(gm,"GameOver");gm.TickGameplay(100);Near(gm.RechargeProgress,p,"game over stops charge");
        }
        Debug.Log("Recharge integration PASSED: signed outcomes share cost; matching/empty/shorted hands; lethal hit; per-enemy and shared cap; overflow; collision snapshot ordering; state gates.");
    }
}
