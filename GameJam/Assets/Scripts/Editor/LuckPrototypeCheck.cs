using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class LuckPrototypeCheck
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string text){if(!ok)throw new Exception("v0.6: "+text);}
    static void Near(double a,double b,string text)=>Check(Math.Abs(a-b)<.0002,text+$" ({a} != {b})");
    internal static void State(GameManager gm,string value){var f=typeof(GameManager).GetField("state",Private);f.SetValue(gm,Enum.Parse(f.FieldType,value));}
    internal sealed class Fixture:IDisposable
    {
        public readonly GameObject root,board;public readonly GameManager gm;
        public Fixture(){root=new GameObject("Dice test fixture");gm=root.AddComponent<GameManager>();gm.enabled=false;gm.ballOnMultiKill=false;board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(root.transform);board.transform.position=new Vector3(1000,1000,1000);board.GetComponent<Collider>().enabled=false;gm.board=board.transform;typeof(GameManager).GetField("lives",Private).SetValue(gm,3);State(gm,"Playing");}
        public Enemy Enemy(Card.Equipment element=Card.Equipment.Shockwave,float x=0,float z=4,int hp=100){var e=gm.SpawnEnemy(element,new Vector3(x,0,z));e.maxHp=Math.Max(hp,40);e.hp=hp;return e;}
        public Rigidbody Ball(){var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.transform.SetParent(root.transform);go.tag="Player";go.transform.localScale=Vector3.one*.5f;var b=go.AddComponent<Rigidbody>();b.useGravity=false;return b;}
        public void Hand(params Card[] cards){var h=new Card[5];Array.Copy(cards,h,cards.Length);typeof(GameManager).GetField("hand",Private).SetValue(gm,h);var account=Account(gm);account.FullHand();if(cards.Length<5)account.StartMissing(100);}
        public void ApplyMoves(float dt){foreach(var m in EnemyMovement.Plan(gm,dt)){if(!gm.IsPlaying)break;if(m.local.z<=gm.spawning.dangerZ)m.enemy.Breach();else{var pos=board.transform.TransformPoint(m.local);m.enemy.GetComponent<Rigidbody>().position=pos;m.enemy.transform.position=pos;}}}
        public void Dispose(){foreach(var b in gm.Balls)if(b && !b.transform.IsChildOf(root.transform))Object.DestroyImmediate(b.gameObject);Object.DestroyImmediate(root);}
    }
    static RechargeAccount Account(GameManager gm)=>(RechargeAccount)typeof(GameManager).GetField("recharge",Private).GetValue(gm);
    static Card C(Card.Equipment e,int n=6)=>new Card(n,e);
    [MenuItem("LuckyPinball/Luck Number/Check Card Rules")]
    public static void CheckCards(){Debug.Log(DiceRuleChecks.Run());Debug.Log(RechargeAccountChecks.Run());}
    [MenuItem("LuckyPinball/Luck Number/Run Acceptance Checks (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        try {
            Hud.Instance=null;CheckCards();
            foreach(int face in new[]{3,6})using(var f=new Fixture()){
                var gm=f.gm;var card=C(Card.Equipment.Arc);f.Hand(card);var target=f.Enemy();gm.DebugSetDice(new[]{face});Check(gm.UseCard(0),"single accepted");
                Check(target.hp==100 && gm.PendingBatch!=null,"no damage before reveal");gm.TickGameplay(.449f);Check(target.hp==100,"reveal waits combat time");gm.TickGameplay(.001f);
                Check(gm.LastBatch.Tier==(face==6?CastTier.Standard:CastTier.Base),"one die determines Base or Hit");
                Check(target.hp==88,"first wave always applies");gm.TickGameplay(.25f);Check(target.hp==(face==6?76:88),"standard is two timed 12-damage waves");
            }
            foreach(bool jackpot in new[]{false,true})using(var f=new Fixture()){
                var gm=f.gm;var arc=C(Card.Equipment.Arc);var fire=C(Card.Equipment.Spark);var held=C(Card.Equipment.Shockwave);f.Hand(arc,fire,held);
                var blue=f.Enemy(Card.Equipment.Arc,-2);var red=f.Enemy(Card.Equipment.Spark,0);var purple=f.Enemy(Card.Equipment.Shockwave,2);
                gm.DebugSetDice(jackpot?new[]{6,6}:new[]{1,6});Check(gm.UseCards(new[]{arc,fire}),"same-number mixed elements accepted");Check(gm.CardCount==1 && ReferenceEquals(gm.GetCard(0),held),"held same-number card not included");
                var batch=gm.LastBatch;Check(!gm.UseCard(0),"repeat submission locked before reveal");Near(gm.RechargeRequired,200,"two cards cost 200 even from a partially empty test hand");
                gm.TickGameplay(.45f);Check(batch.Tier==(jackpot?CastTier.Jackpot:CastTier.FullField),"shared tier");
                Check(batch.Skills.First(x=>x.Element==Card.Equipment.Arc).Plan.waves==(jackpot?4:3),"tier arc waves");Near(batch.Skills.First(x=>x.Element==Card.Equipment.Spark).Plan.burnDuration,jackpot?8:6,"tier burn duration");
                Check(blue.hp==100 && blue.IsBurning && red.hp==88 && !red.IsBurning && purple.hp==88 && purple.IsBurning,"element immunity affects both damage and statuses");
                Check(!blue.IsStunned && !red.IsStunned && !purple.IsStunned,"held shock card never casts");
                gm.DebugSetDice(new[]{6});Check(gm.UseCard(0),"next batch allowed once previous revealed while waves continue");
            }
            using(var f=new Fixture()){
                var gm=f.gm;var fire2=C(Card.Equipment.Spark,2);var arc6=C(Card.Equipment.Arc);var fire6=C(Card.Equipment.Spark);f.Hand(fire2,arc6,fire6);
                Account(gm).EarnNatural(7);gm.DebugSetDice(new[]{1,6});var randomBefore=UnityEngine.Random.state;double p=gm.RechargeProgress,r=gm.RechargeRequired,w=gm.RechargeWindowUsed;
                Check(!gm.UseCards(new[]{fire2,arc6}) && !gm.UseCards(new[]{arc6,arc6}) && !gm.UseCards(new[]{C(Card.Equipment.Arc)}),"mixed, duplicate and foreign instances rejected");
                Check(gm.CardCount==3 && gm.PendingBatch==null && gm.PendingCasts==0 && gm.FixedDice=="1, 6" && UnityEngine.Random.state.Equals(randomBefore),"rejected batch changes neither hand nor dice state");
                Near(gm.RechargeProgress,p,"invalid progress unchanged");Near(gm.RechargeRequired,r,"invalid cost unchanged");Near(gm.RechargeWindowUsed,w,"invalid limiter history unchanged");
            }
            using(var f=new Fixture()){
                var gm=f.gm;var a=C(Card.Equipment.Spark);var b=C(Card.Equipment.Spark);f.Hand(a,b);var blue=f.Enemy(Card.Equipment.Arc);var fire=f.Enemy(Card.Equipment.Spark,2);
                gm.DebugSetDice(new[]{6,2});gm.UseCards(new[]{a,b});gm.TickGameplay(.45f);Near(blue.ActiveBurnRemaining,8,"duplicate full-field fire is 6+2 seconds");Check(!fire.IsBurning,"fire enemy immune");
                gm.TickGameplay(8);Check(blue.hp==52 && fire.hp==100,"merged burn is 6 DPS, not stacked twice");
            }
            using(var f=new Fixture()){
                var gm=f.gm;var a=C(Card.Equipment.Arc);var b=C(Card.Equipment.Spark);f.Hand(a,b);
                f.Enemy(Card.Equipment.Arc,-2,hp:12);f.Enemy(Card.Equipment.Spark,0,hp:12);f.Enemy(Card.Equipment.Shockwave,2,hp:12);
                gm.DebugSetDice(new[]{6,2});gm.UseCards(new[]{a,b});gm.TickGameplay(2.45f);
                Check(gm.LastBatch.Kills==3 && gm.Score==150 && gm.Metrics.batchKills==3,"one kill record spans elements, waves and burn ticks");
            }
            using(var f=new Fixture()){
                var gm=f.gm;var arc=C(Card.Equipment.Arc);f.Hand(arc);
                var immune=f.Enemy(Card.Equipment.Arc,z:-4);var targets=Enumerable.Range(0,5).Select(i=>f.Enemy(Card.Equipment.Spark,z:i)).ToArray();
                gm.DebugSetDice(new[]{6});gm.UseCard(0);var snapshot=gm.LastBatch;targets[4].GetComponent<Rigidbody>().position=f.board.transform.TransformPoint(new Vector3(0,.25f,-4.5f));var newcomer=f.Enemy(Card.Equipment.Spark,z:-4.8f);
                gm.TickGameplay(.7f);Check(immune.hp==100 && targets.Take(4).All(e=>e.hp==76) && targets[4].hp==100 && newcomer.hp==100,"standard filters immunity before cap and locks submission positions/order");
            }
            using(var f=new Fixture()){
                var gm=f.gm;var a=C(Card.Equipment.Arc);var b=C(Card.Equipment.Shockwave);f.Hand(a,b);var fire=f.Enemy(Card.Equipment.Spark);
                var faces=new[]{6,6};gm.DebugSetDice(faces);faces[0]=1;gm.UseCards(new[]{a,b});var batch=gm.LastBatch;var rng=UnityEngine.Random.state;
                gm.SetPaused(true);gm.TickGameplay(10);Check(!batch.Revealed && fire.hp==100 && gm.CardCount==0,"pause freezes reveal without refund");gm.SetPaused(false);State(gm,"Waiting");gm.TickGameplay(10);Check(!batch.Revealed,"launch wait freezes reveal");State(gm,"Playing");gm.TickGameplay(.45f);
                Check(batch.Revealed && batch.Tier==CastTier.Jackpot && batch.Dice.SequenceEqual(new[]{6,6}) && UnityEngine.Random.state.Equals(rng),"resume uses identical locked dice, no second roll");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;f.Hand(C(Card.Equipment.Arc));var immune=f.Enemy(Card.Equipment.Arc);
                gm.DebugSetDice(new[]{6});gm.UseCard(0);gm.TickGameplay(.45f);
                Check(gm.LastBatch.Tier==CastTier.Standard && gm.LastBatch.CoverageCount==0 && gm.CardCount==0 && immune.hp==100,"a hit with no eligible targets still consumes cards and shows zero coverage");
            }
            CheckPassives();CheckRecharge();V06Checks.Run();
            Debug.Log("v0.6 ACCEPTANCE PASSED: single/combined dice, immunity, merged elements, atomic rejection, locked targets, shared kills, pause, charge and passive rules.");
        }finally{GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
    static void CheckPassives()
    {
        using(var f=new Fixture()){
            var gm=f.gm;f.Hand(C(Card.Equipment.Arc,6),C(Card.Equipment.Arc,2),C(Card.Equipment.Spark,4),C(Card.Equipment.Spark,5),C(Card.Equipment.Shockwave,3));
            var direct=f.Enemy(Card.Equipment.Arc);var nearRed=f.Enemy(Card.Equipment.Spark,.5f);var nearBlue=f.Enemy(Card.Equipment.Arc,-.4f);var nearPurple=f.Enemy(Card.Equipment.Shockwave,.9f);var ball=f.Ball();
            direct.TryBallHit(ball);Check(direct.hp==96 && nearBlue.hp==100 && nearRed.hp==96 && nearPurple.hp==96,"base is neutral; arcs filter own element before cap");
            Check(direct.IsBurning && !nearRed.IsBurning && nearBlue.IsBurning,"fire immunity and type-only N=2 spread");
            Check(direct.IsStunned && nearRed.IsStunned && !nearPurple.IsStunned,"passive shock immune by enemy element");
            Check(gm.Metrics.validBallHits==1,"damage/status cannot recurse physical hit");
        }
    }
    public static void CheckRecharge()
    {
        foreach(bool hit in new[]{false,true})using(var f=new Fixture()){
            var gm=f.gm;var a=C(Card.Equipment.Arc);var b=C(Card.Equipment.Spark);var c=C(Card.Equipment.Shockwave);f.Hand(a,b,c,C(Card.Equipment.Arc,2),C(Card.Equipment.Spark,5));
            gm.DebugSetDice(hit?new[]{6,2,1}:new[]{1,2,3});gm.UseCards(new[]{a,b,c});Near(gm.RechargeRequired,250,"three cards, same batch cost on miss or hit");gm.TickGameplay(25);Check(gm.CardCount==3,"250 total refills first card");gm.TickGameplay(10);Check(gm.CardCount==4,"350 refills second");gm.TickGameplay(10);Check(gm.CardCount==5 && gm.RechargeProgress==0,"450 refills third, full clears excess");
        }

    }
}

