using System;
using UnityEditor;
using UnityEngine;

public static class EmptyHandArcChecks
{
    static void Check(bool v,string s){if(!v)throw new Exception("Empty hand / direct Arc: "+s);}
    [MenuItem("LuckyPinball/Card Balance/Check Empty Start and Direct Arc (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        try{
            Hud.Instance=null;
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;Check(gm.CardCount==0&&gm.HandSize==5&&gm.RechargeProgress==0&&gm.RechargeRequired==gm.luck.rechargeBaseCost,"new run starts empty with base recharge cost only");
                gm.TickGameplay(10);Check(gm.CardCount==1&&gm.RechargeRequired==100&&gm.EmpoweredHits==0&&gm.Metrics.plays==0,"natural recharge grants first card without cast cost or power");
            }
            foreach(int hp in new[]{8,4})using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;f.Hand(new Card(6,Card.Equipment.Arc));var direct=f.Enemy(Card.Equipment.Spark,hp:hp);var a=f.Enemy(Card.Equipment.Spark,.5f,hp:4);var b=f.Enemy(Card.Equipment.Spark,-.5f,hp:4);
                direct.TryBallHit(f.Ball());Check(!direct.Alive&&!a.Alive&&!b.Alive&&gm.Score==150,"direct plus two nearby targets, one kill per foe even when collision kills direct");Check(gm.LastEventKills==3&&gm.Metrics.validBallHits==1&&gm.Metrics.collisionChargeEvents==1,"one shared attack/charge event for collision and Arc");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                f.Hand(new Card(6,Card.Equipment.Arc));var direct=f.Enemy(Card.Equipment.Arc);var other=f.Enemy(Card.Equipment.Spark,.5f);direct.TryBallHit(f.Ball());Check(direct.hp==96&&other.hp==96,"direct Arc immunity retained; nearby Arc still triggers");
            }
            Debug.Log("EMPTY START / DIRECT ARC CHECKS PASSED: base-cost recharge, direct and nearby damage, immunity, shared kills and single charge event.");
        }finally{GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
