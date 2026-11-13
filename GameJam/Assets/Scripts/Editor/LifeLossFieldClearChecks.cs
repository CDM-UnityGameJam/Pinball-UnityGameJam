using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class LifeLossFieldClearChecks
{
    static void Check(bool v,string message){if(!v)throw new Exception("Life-loss clear: "+message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.0001,message+" "+a+" / "+b);}
    [MenuItem("LuckyPinball/Run/Check Life Loss Field Clear (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var rng=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        try{
            Hud.Instance=null;
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.SetPaused(false);gm.spawning.openingNormalCount=0;gm.spawning.normalChancePercent=100;
                var spawner=gm.gameObject.AddComponent<Spawner>();spawner.Initialize(gm);typeof(GameManager).GetField("difficultySpawner",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(gm,spawner);
                f.Hand(new Card(6,Card.Equipment.Arc));gm.TickGameplay(1);Near(spawner.GenerationProgress,.5,"half spawn progress");
                var first=f.Enemy(Card.Equipment.Spark,0,gm.spawning.dangerZ+.001f,1000);var second=f.Enemy(Card.Equipment.Arc,3,gm.spawning.dangerZ+.001f,1000);var burning=f.Enemy(Card.Equipment.Shockwave,-3,4,1000);burning.Ignite(false,3,new AttackRecord("cleared burn"));
                gm.DebugSetDice(new[]{6});gm.UseCard(0);var batch=gm.LastBatch;double progress=gm.RechargeProgress,required=gm.RechargeRequired,time=gm.CombatTime;int power=gm.EmpoweredHits;
                EnemyMovement.Step(gm,.1f);
                Check(gm.Lives==2&&gm.IsPlaying,"same-step breaches cost one life before clear");Check(!first.Alive&&!second.Alive&&!burning.Alive&&EquipmentResolver.Targets(gm).Count==0,"all field enemies immediately leave combat");
                Check(gm.Score==0&&gm.Metrics.kills==0&&batch.Kills==0&&gm.BallCount==0,"clear grants no score, kills, multikill or balls");Near(gm.RechargeProgress,progress,"clear grants no energy");Near(gm.RechargeRequired,required,"cost retained");Check(gm.EmpoweredHits==power&&gm.CardCount==0,"power and hand retained");Near(gm.CombatTime,time,"time retained");Near(spawner.GenerationProgress,.5,"spawn progress retained");Near(Time.timeScale,1,"no pause on clear");
                gm.TickGameplay(.5f);Check(batch.Revealed&&gm.Score==0&&EquipmentResolver.Targets(gm).Count==0,"pending skill skips cleared targets without retargeting");Near(spawner.GenerationProgress,.75,"normal spawn cadence continues");
                gm.TickGameplay(.49f);Check(EquipmentResolver.Targets(gm).Count==0,"no instant replacement wave");gm.TickGameplay(.01f);Check(EquipmentResolver.Targets(gm).Count==1&&spawner.PeriodicAttempts==1,"next scheduled spawn succeeds normally");
                gm.breachLifeCost=0;var newborn=EquipmentResolver.Targets(gm)[0];var survivor=f.Enemy(Card.Equipment.Spark,0,4);newborn.Breach();Check(gm.Lives==2&&survivor.Alive,"no actual life loss does not clear other enemies");gm.breachLifeCost=2;survivor.Breach();Check(gm.IsGameOver&&gm.Lives==0&&EquipmentResolver.Targets(gm).Count==0,"final life loss clears field too");
            }
            Debug.Log("LIFE-LOSS FIELD CLEAR CHECKS PASSED: immediate clear, one same-step loss, no rewards, preserved state/progress, future spawning and game over.");
        }finally{GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=rng;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
