using System;
using UnityEditor;
using UnityEngine;

public static class CollisionDamageChecks
{
    static void Check(bool value,string message){if(!value)throw new Exception("Collision damage: "+message);}
    [MenuItem("LuckyPinball/Run/Check Casting and Collision Damage (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var config=ScriptableObject.CreateInstance<CardBalanceConfig>();config.settings.rechargeHit=0;config.settings.rechargePerSecond=0;
        try{
            Hud.Instance=null;
            foreach(bool lucky in new[]{false,true})using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(new Card(1,Card.Equipment.Shockwave));gm.DebugSetLucky(lucky?1:0);
                Check(gm.UseCard(0)&&gm.CardCount==0&&gm.LastBatch.Revealed,"empty-field cast still consumes card and resolves skill");
                var first=f.Enemy(Card.Equipment.Shockwave,hp:1000);var second=f.Enemy(Card.Equipment.Spark,2,hp:1000);var a=f.Ball();var b=f.Ball();
                first.TryBallHit(a);second.TryBallHit(b);Check(first.hp==996&&second.hp==996,"both balls use ordinary damage after casting");
                gm.TickGameplay(.1f);first.TryBallHit(b);Check(first.hp==996,"existing damage cooldown retained");
                gm.TickGameplay(.1f);first.TryBallHit(a);Check(first.hp==992,"later collisions still use ordinary damage");
                config.settings.collisionDamage=7;gm.TickGameplay(.2f);first.TryBallHit(a);Check(first.hp==985,"live ordinary collision configuration still applies");config.settings.collisionDamage=4;
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.cardBalanceConfig=config;f.Hand(new Card(1,Card.Equipment.Shockwave),new Card(1,Card.Equipment.Shockwave));gm.DebugSetLucky(1);
                var immune=f.Enemy(Card.Equipment.Shockwave,hp:1000);Check(gm.UseCard(0)&&immune.hp==1000,"immune skill cast causes no damage");immune.TryBallHit(f.Ball());Check(immune.hp==996,"immune-field cast leaves collision at ordinary damage");
            }
            Debug.Log("COLLISION DAMAGE CHECKS PASSED: normal/lucky casts, empty/immune fields, multiball, cooldown and live ordinary damage.");
        }finally{UnityEngine.Object.DestroyImmediate(config);GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
