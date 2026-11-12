using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CardBalanceChecks
{
    static void Check(bool v,string s){if(!v)throw new Exception("Card balance integration: "+s);}
    static void Near(double a,double b,string s){Check(Math.Abs(a-b)<.0001,s+" "+a+" / "+b);}
    static Card C(Card.Equipment e)=>new Card(6,e);
    [MenuItem("LuckyPinball/Card Balance/Check Rules")]
    public static void Rules()=>Debug.Log(CardBalanceRuleChecks.Run());
    [MenuItem("LuckyPinball/Card Balance/Run Integration Checks (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var config=ScriptableObject.CreateInstance<CardBalanceConfig>();
        try{
            Hud.Instance=null;Rules();
            using(var f=new LuckPrototypeCheck.Fixture()){
                f.gm.cardBalanceConfig=config;f.Hand(C(Card.Equipment.Arc));var direct=f.Enemy(Card.Equipment.Spark);var other=f.Enemy(Card.Equipment.Spark,.5f);var ball=f.Ball();direct.TryBallHit(ball);Check(direct.hp==92&&other.hp==96,"direct collision plus Arc; nearby Arc damage");
                config.settings.passiveArcDamage=9;f.gm.TickGameplay(.2f);direct.TryBallHit(ball);Check(other.hp==87&&f.gm.GetCard(0).Description(f.gm.luck).Contains("9 dmg"),"new hit and description read live damage");
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(C(Card.Equipment.Arc));var target=f.Enemy(Card.Equipment.Spark,hp:500);gm.DebugSetDice(new[]{6});gm.UseCard(0);
                config.settings.activeArcDamage=99;config.settings.arcWaveInterval=2;config.settings.arcTiers[1].waves=8;config.settings.diceRevealDelay=9;
                gm.TickGameplay(.45f);Check(target.hp==488,"reveal and first-wave damage captured");gm.TickGameplay(.25f);Check(target.hp==476&&gm.PendingCasts==0,"remaining waves and interval captured");
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(C(Card.Equipment.Spark));var target=f.Enemy(Card.Equipment.Arc);gm.DebugSetDice(new[]{1});gm.UseCard(0);config.settings.activeBurnDps=20;config.settings.burnTickInterval=.1f;
                gm.TickGameplay(.45f);gm.TickGameplay(1);Check(target.hp==94,"submitted fire keeps 6 DPS / 0.5s ticks");
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand();var target=f.Enemy(Card.Equipment.Arc);target.Ignite(false,3,new AttackRecord("old burn"));gm.TickGameplay(.5f);Check(target.hp==98,"old burn first tick");config.settings.passiveBurnDps=20;config.settings.burnTickInterval=.25f;gm.TickGameplay(.5f);Check(target.hp==96,"existing burn parameters retained");
                target.Ignite(false,3,new AttackRecord("refreshed burn"));gm.TickGameplay(.5f);Check(target.hp==91,"refresh reads new parameters without resetting pending tick");gm.TickGameplay(.25f);Check(target.hp==86,"new cadence after refresh");Check(gm.Metrics.burnChargeEvents==0,"burn and refresh never grant charge");
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;var target=f.Enemy(Card.Equipment.Arc);target.Stun(.35f,false);config.settings.passiveStunImmunity=0;gm.TickGameplay(.35f);Near(target.PassiveImmunityRemaining,2,"existing immunity retained");
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(C(Card.Equipment.Arc),C(Card.Equipment.Spark),C(Card.Equipment.Shockwave),C(Card.Equipment.Arc),C(Card.Equipment.Spark));gm.DebugSetDice(new[]{1});gm.UseCard(0);gm.TickGameplay(7.4f);Near(gm.RechargeProgress,74,"old recharge progress");
                config.settings.rechargePerSecond=20;config.settings.rechargeBaseCost=200;config.settings.rechargeCastCost=80;gm.TickGameplay(1);Near(gm.RechargeProgress,94,"natural rate updates without clearing progress");Near(gm.RechargeRequired,150,"current requirement retained");gm.DebugSetDice(new[]{1});gm.UseCard(0);Near(gm.RechargeRequired,230,"new cast cost appended");Check(gm.DebugFillRecharge()&&gm.CardCount==4,"debug uses normal one-card refill");Near(gm.RechargeRequired,200,"next requirement uses new base");
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;var a=C(Card.Equipment.Shockwave);var b=C(Card.Equipment.Shockwave);f.Hand(a,b);var target=f.Enemy(Card.Equipment.Spark);config.settings.shockTiers[3].waves=99;gm.DebugSetDice(new[]{6,6});gm.UseCards(new[]{a,b});gm.TickGameplay(.45f);Check(target.hp==92,"combined Jackpot Shockwave hits once");Near(target.StunRemaining,3,"Jackpot stun duration retained");gm.TickGameplay(2);Check(target.hp==92&&gm.PendingCasts==0,"no delayed Shockwave hits");
            }
            config.settings=new LuckTuning();config.settings.empoweredHitsPerCard=3;
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(C(Card.Equipment.Shockwave));gm.DebugSetDice(new[]{6,6});var randomBefore=UnityEngine.Random.state;Check(!gm.UseCard(0)&&gm.CardCount==1&&gm.FixedDice=="6, 6"&&UnityEngine.Random.state.Equals(randomBefore),"fixed-dice count mismatch is atomic");gm.DebugSetDice(new[]{1});gm.UseCard(0);Check(gm.EmpoweredHits==3&&gm.FixedDice=="Random","configured grants and one-shot dice");gm.DebugClearEmpowered();Check(gm.EmpoweredHits==0,"clear power");Check(gm.DebugAddCard(4,Card.Equipment.Spark),"specified card inserted");
            }
            config.settings=new LuckTuning();config.settings.passiveArcRadius=float.NaN;Check(config.Effective.Valid()&&float.IsNaN(config.settings.passiveArcRadius),"invalid config falls back without modifying asset values");
            Debug.Log("CARD BALANCE INTEGRATION PASSED: passive live edit, batch/status snapshots, recharge preservation, debug inputs and safe defaults.");
        }finally{Object.DestroyImmediate(config);GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
        LuckPrototypeCheck.Run();
    }
    static double started;
    public static void RunBatch(){started=EditorApplication.timeSinceStartup;EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");EditorApplication.update+=Wait;EditorApplication.isPlaying=true;}
    static void Wait(){
        if(EditorApplication.timeSinceStartup-started>120){Finish(false,"Play startup timeout");return;}
        if(!Application.isPlaying||!GameManager.Instance||GameManager.Instance.Lives==0||!Hud.Instance)return;
        EditorApplication.update-=Wait;
        try{Check(GameManager.Instance.cardBalanceConfig&&GameManager.Instance.cardBalanceConfig.settings.Valid(),"scene references valid migrated config");Run();Finish(true,"Card balance integration and existing combat checks passed.");}catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void Finish(bool ok,string detail){EditorApplication.update-=Wait;Directory.CreateDirectory("tmp");File.WriteAllText("tmp/card-balance-result.txt",(ok?"PASS":"FAIL")+"\n"+detail);EditorApplication.Exit(ok?0:1);}
}

