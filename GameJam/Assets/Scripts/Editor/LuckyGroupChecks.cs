using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class LuckyGroupChecks
{
    static void Check(bool v,string s){if(!v)throw new Exception("Lucky groups: "+s);}
    static Card C(int n,Card.Equipment e)=>new Card(n,e);
    [MenuItem("LuckyPinball/Run/Check Lucky Groups (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var config=ScriptableObject.CreateInstance<CardBalanceConfig>();
        try{
            Hud.Instance=null;
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;Check(gm.CardCount==5&&gm.IsHandFull&&gm.RechargeProgress==0&&gm.RechargeRequired==0&&gm.LuckyNumber==0,"opening draw is full, free and never lucky");foreach(var c in gm.HandSnapshot)Check(c.number>=1&&c.number<=3&&!gm.IsLucky(c),"opening numbers 1–3, no lucky cards");
                for(int i=1;i<gm.HandSize;i++)Check(gm.GetCard(i-1).number<=gm.GetCard(i).number,"opening hand is sorted by number");
                gm.cardBalanceConfig=config;var arc=C(1,Card.Equipment.Arc);var fire=C(1,Card.Equipment.Spark);var other=C(2,Card.Equipment.Shockwave);f.Hand(arc,fire,other,C(3,Card.Equipment.Arc),C(2,Card.Equipment.Spark));gm.DebugSetLucky(1);
                var enemy=f.Enemy(Card.Equipment.Shockwave,hp:1000);var rng=UnityEngine.Random.state;
                Check(!gm.UseCards(new[]{arc})&&gm.CardCount==5&&gm.RechargeRequired==0&&UnityEngine.Random.state.Equals(rng),"partial group rejected atomically");
                Check(gm.UseCards(new[]{arc,fire})&&gm.CardCount==3&&gm.LastBatch.Tier==CastTier.Lucky&&gm.LastBatch.Revealed&&gm.PendingBatch==null&&gm.LuckyNumber==0,"complete lucky group casts immediately then clears lucky number");Check(gm.RechargeRequired==150,"whole group missing-slot cost");Check(enemy.hp==988,"first wave occurs at submission");
                config.settings.luckySkills[0].damage=99;gm.DebugSetLucky(2);gm.TickGameplay(.25f);Check(enemy.hp==976,"later wave keeps submitted damage and lucky tier");
                var account=(RechargeAccount)typeof(GameManager).GetField("recharge",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(gm);account.EarnNatural(80);
                gm.UseCard(0);Check(gm.CardCount==1&&gm.RechargeRequired==250&&gm.RechargeProgress>=80,"another full group recalculates cost and preserves progress");
                config.settings.draws.luckyProbability=0;Check(gm.DebugFillRecharge()&&gm.CardCount==5&&gm.LuckyNumber==0&&gm.RechargeProgress==0,"batch refill all spaces, replaces luck and clears charge");
                for(int i=1;i<gm.HandSize;i++)Check(gm.GetCard(i-1).number<=gm.GetCard(i).number,"refill keeps hand sorted by number");
                gm.SetPaused(true);var cards=gm.HandSnapshot;Check(!gm.UseCard(0),"pause blocks submission");double time=gm.CombatTime;gm.TickGameplay(10);Check(gm.CombatTime==time&&gm.RechargeProgress==0,"pause freezes gameplay");gm.SetPaused(false);
            }
            config.settings=new LuckTuning();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(C(1,Card.Equipment.Arc),C(1,Card.Equipment.Arc),C(2,Card.Equipment.Arc));gm.DebugSetLucky(1);var enemy=f.Enemy(Card.Equipment.Shockwave);gm.RegisterBallHit(enemy,enemy.transform.position,true);Check(enemy.hp==88,"one merged Arc uses total count plus two lucky bonuses");
            }
            config.settings=new LuckTuning();config.settings.rechargeHit=1;config.settings.draws.refillCosts=new float[]{1,1,1,1,1};config.settings.draws.luckyProbability=1;config.settings.draws.numberWeights=new float[]{0,1,0};config.settings.draws.elementWeights=new float[]{1,0,0};
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(C(1,Card.Equipment.Shockwave));gm.DebugSetLucky(1);var enemy=f.Enemy(Card.Equipment.Spark);gm.RegisterBallHit(enemy,enemy.transform.position,true);
                Check(enemy.hp==96&&gm.CardCount==5,"newly refilled Arc does not join triggering hit");Check(gm.LuckyNumber==1&&gm.LuckyHeldCount(Card.Equipment.Shockwave)==1&&gm.LuckyHeldCount(Card.Equipment.Arc)==0,"refill preserves luck while original lucky card remains, even at candidate probability one");
                gm.RegisterBallHit(enemy,enemy.transform.position,true);Check(enemy.hp==88,"next hit uses new ordinary Arc");
                Check(gm.UseCard(0)&&gm.LastBatch.Tier==CastTier.Lucky&&gm.LuckyNumber==0,"spending last lucky group clears number but submitted skill stays lucky");
                Check(gm.DebugFillRecharge()&&gm.LuckyNumber==2&&gm.LuckyHeldCount(Card.Equipment.Arc)==5,"next refill chooses luck again and buffs matching old cards");
            }
            Debug.Log("LUCKY GROUP INTEGRATION PASSED: full opening hand, atomic whole groups, immediate casts/snapshots, full recharge, lucky held effects and collision-before-refill ordering.");
        }finally{UnityEngine.Object.DestroyImmediate(config);GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
