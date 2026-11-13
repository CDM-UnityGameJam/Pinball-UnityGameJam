using System;
using UnityEditor;
using UnityEngine;

public static class ScoreChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception("Scoring: "+message);}
    [MenuItem("LuckyPinball/Run/Check Time and Combo Scoring (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var config=ScriptableObject.CreateInstance<CardBalanceConfig>();
        try{
            Hud.Instance=null;config.settings.rechargeHit=0;config.settings.rechargePerSecond=0;
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand();var target=f.Enemy(Card.Equipment.Arc,hp:10000);
                for(int i=0;i<10;i++)gm.RegisterBallHit(target,target.transform.position,true);
                Check(gm.ComboHits==10&&gm.Score==0,"threshold does not pay immediately");gm.RegisterBallHit(target,target.transform.position,false);Check(gm.ComboHits==10,"damage cooldown contact does not count");
                gm.LoseLife();Check(gm.ComboHits==10&&gm.Score==0&&gm.Lives==2,"nonfatal life loss preserves combo without payout");
                gm.SetPaused(true);gm.TickGameplay(10);Check(gm.ComboHits==10&&gm.Score==0&&gm.CombatTime==0,"pause freezes scoring and timeout");gm.SetPaused(false);
                gm.TickGameplay(3);Check(gm.ComboHits==0&&gm.Score==5,"timeout pays after life loss");gm.TickGameplay(7);Check(gm.Score==6,"time score independent of combo");
                target=f.Enemy(Card.Equipment.Arc,hp:10000);for(int i=0;i<50;i++)gm.RegisterBallHit(target,target.transform.position,true);
                Check(gm.Score==6&&gm.ComboHits==50,"fifty hits do not pay early");gm.breachLifeCost=2;gm.LoseLife();Check(gm.IsGameOver&&gm.Score==16&&gm.ComboHits==0,"game over pays only highest tier once");gm.TickGameplay(20);Check(gm.Score==16,"game over stops scoring");
            }
            Debug.Log("SCORING INTEGRATION PASSED: cooldown filtering, life-loss preservation, pause, timeout, timed points and game-over settlement.");
        }finally{UnityEngine.Object.DestroyImmediate(config);GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
