using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class LifeAndRelaunchChecks
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool v,string message){if(!v)throw new Exception("Life/relaunch: "+message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.0001,message+" "+a+" / "+b);}
    static List<Rigidbody> Balls(GameManager gm)=>(List<Rigidbody>)typeof(GameManager).GetField("balls",Private).GetValue(gm);
    static Rigidbody Setup(LuckPrototypeCheck.Fixture f)
    {
        var template=f.Ball();template.gameObject.SetActive(false);typeof(GameManager).GetField("ballTemplate",Private).SetValue(f.gm,template);
        var live=f.Ball();Balls(f.gm).Add(live);f.gm.ball=live;f.gm.SetPaused(false);return live;
    }
    [MenuItem("LuckyPinball/Run/Check Breach Lives and Continuous Relaunch (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        try{
            Hud.Instance=null;
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;var dropped=Setup(f);gm.launchWait=.5f;f.Hand(new Card(6,Card.Equipment.Arc));
                var target=f.Enemy(Card.Equipment.Spark,hp:1000);var burning=f.Enemy(Card.Equipment.Arc,4,hp:1000);var walking=f.Enemy(Card.Equipment.Spark,-4,6);
                target.Stun(1,true);burning.Ignite(false,3,new AttackRecord("relaunch status check"));gm.DebugSetDice(new[]{1});gm.UseCard(0);
                gm.OnDrain(dropped);gm.OnDrain(dropped);
                Check(gm.Lives==3&&gm.IsPlaying&&!gm.IsWaiting&&gm.IsRelaunching&&gm.BallCount==0,"last drain starts one countdown without life loss or waiting state");Near(Time.timeScale,1,"drain never pauses physics");Check(gm.Metrics.drains==1,"duplicate drain ignored");
                gm.TickGameplay(.25f);Near(gm.CombatTime,.25,"combat clock advances without a ball");Check(gm.RechargeProgress>0&&gm.PendingBatch!=null,"charge advances, reveal still scheduled");Near(target.StunRemaining,.75,"stun time advances");
                var moves=EnemyMovement.Plan(gm,.1f);Check(moves.Exists(m=>m.enemy==walking&&m.local.z<walking.LocalPosition.z),"unstunned enemy moves during relaunch");
                double progress=gm.RechargeProgress;float remaining=gm.Countdown;gm.SetPaused(true);gm.TickGameplay(10);Near(gm.CombatTime,.25,"manual pause freezes combat");Near(gm.Countdown,remaining,"manual pause freezes relaunch");Near(gm.RechargeProgress,progress,"manual pause freezes recharge");gm.SetPaused(false);
                gm.TickGameplay(.25f);Check(!gm.IsRelaunching&&gm.IsPlaying&&gm.BallCount==1&&gm.ball&&!gm.ball.isKinematic,"relaunch completes on combat time");Check(gm.LastBatch.Revealed&&target.hp==988&&burning.hp==998,"dice and burn resolve while ball is absent");Near(gm.DifficultyTime,gm.CombatTime,"difficulty clock advances");Near(Vector3.Distance(gm.ball.position,gm.board.TransformPoint(gm.launchLocal)),0,"relaunch uses shared launch point");Check(Vector3.Dot(gm.ball.linearVelocity,gm.board.forward)>0,"relaunch aims into playfield");
                gm.OnDrain(gm.ball);Check(gm.IsRelaunching&&gm.Lives==3,"second drain still free");
                for(int i=0;i<3;i++)f.Enemy(Card.Equipment.Arc,5,gm.spawning.dangerZ).Breach();Check(gm.IsGameOver&&gm.Lives==0&&!gm.IsRelaunching,"breaches alone end run and cancel relaunch");gm.TickGameplay(10);Check(gm.BallCount==0,"game over cannot relaunch");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;var first=Setup(f);var second=f.Ball();Balls(gm).Add(second);gm.OnDrain(first);Check(gm.BallCount==1&&!gm.IsRelaunching&&gm.Lives==3,"remaining multiball prevents replacement");gm.OnDrain(second);Check(gm.BallCount==0&&gm.IsRelaunching&&gm.Lives==3,"only last ball starts replacement");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;var first=Setup(f);gm.launchWait=0;gm.OnDrain(first);Check(gm.BallCount==1&&!gm.IsRelaunching&&gm.Lives==3,"zero delay relaunches once immediately");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;var first=Setup(f);typeof(GameManager).GetField("pendingBalls",Private).SetValue(gm,1);gm.OnDrain(first);Check(!gm.IsRelaunching&&gm.Lives==3,"queued multiball prevents duplicate replacement");
            }
            Debug.Log("LIFE / RELAUNCH CHECKS PASSED: breach-only life loss, continuous combat, pause, status/reveal/charge clocks, multiball, duplicate drains, zero delay and game-over cancellation.");
        }finally{GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
