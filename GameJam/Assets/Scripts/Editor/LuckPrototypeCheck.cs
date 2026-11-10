using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class LuckPrototypeCheck
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message) { if(!ok) throw new Exception("Luck v0.3: "+message); }
    static void Near(double a,double b,string message) => Check(Math.Abs(a-b)<.0002,message+$" ({a} != {b})");
    internal static void State(GameManager gm,string value) {
        var f=typeof(GameManager).GetField("state",Private);f.SetValue(gm,Enum.Parse(f.FieldType,value));
    }
    internal sealed class Fixture:IDisposable
    {
        public readonly GameObject root,board; public readonly GameManager gm;
        public Fixture() {
            root=new GameObject("Luck v0.3 fixture");gm=root.AddComponent<GameManager>();gm.enabled=false;
            board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(root.transform);board.transform.position=new Vector3(1000,1000,1000);board.GetComponent<Collider>().enabled=false;
            gm.board=board.transform;typeof(GameManager).GetField("lives",Private).SetValue(gm,3);State(gm,"Playing");
        }
        public Enemy Enemy(int number=1,float x=0,float z=4,int hp=40) {
            var e=gm.SpawnEnemy(global::Enemy.Kind.Normal,number,new Vector3(x,0,z));e.maxHp=Math.Max(40,hp);e.hp=hp;return e;
        }
        public Rigidbody Ball() {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.transform.SetParent(root.transform);go.tag="Player";go.transform.localScale=Vector3.one*.5f;
            var b=go.AddComponent<Rigidbody>();b.useGravity=false;return b;
        }
        public void Hand(params Card[] cards) {
            var values=new Card[5];Array.Copy(cards,values,cards.Length);typeof(GameManager).GetField("hand",Private).SetValue(gm,values);
            typeof(GameManager).GetField("nextRefillAt",Private).SetValue(gm,cards.Length==5?double.PositiveInfinity:gm.CombatTime+8);
        }
        public void ApplyMoves(float dt) {
            foreach(var m in EnemyMovement.Plan(gm,dt)) {
                if(!gm.IsPlaying) break;
                if(m.local.z<=gm.spawning.dangerZ)m.enemy.Breach();
                else {var pos=board.transform.TransformPoint(m.local);m.enemy.GetComponent<Rigidbody>().position=pos;m.enemy.transform.position=pos;}
            }
        }
        public void Dispose() {
            foreach(var b in gm.Balls) if(b && !b.transform.IsChildOf(root.transform))Object.DestroyImmediate(b.gameObject);
            Object.DestroyImmediate(root);
        }
    }
    static Card C(Card.Equipment type,int n=6)=>new Card(n,type);
    [MenuItem("LuckyPinball/Luck Number/Check Card Rules")]
    public static void CheckCards() {
        var saved=UnityEngine.Random.state;
        try {
            UnityEngine.Random.InitState(303);var t=new LuckTuning();
            foreach(bool lucky in new[]{false,true}) {
                int[] bands=new int[4];bool min=false,max=false;
                for(int i=0;i<160000;i++) {int q=t.Roll(lucky);bands[q<0?0:q<20?1:q<60?2:3]++;min|=q==-10;max|=q==100;}
                Check(min&&max,"both endpoints reachable");
                for(int i=0;i<4;i++)Check(Mathf.Abs(bands[i]/160000f-t.Weights(lucky).Chance(i))<.006,"unchanged probability tables");
            }
            foreach(Card.Equipment e in Enum.GetValues(typeof(Card.Equipment))) {
                var low=t.Plan(e,5);var mid=t.Plan(e,40);var high=t.Plan(e,90);
                Check(low.targetCount==2 && mid.targetCount==6 && high.allTargets,"Q5/40/90 coverage");
                if(e==Card.Equipment.Arc)Check(low.waves==1 && mid.waves==2 && high.waves==4,"arc scales waves");
                if(e==Card.Equipment.Spark)Check(low.burnDuration==1 && mid.burnDuration==4 && high.burnDuration==7.5f,"flame scales duration");
                if(e==Card.Equipment.Shockwave)Check(low.waves==1 && mid.waves==2 && high.waves==3 && Math.Abs(high.stunDuration-2.8f)<.001,"shock scales waves and stun");
            }
            Near(t.ShortCircuitDuration(-1),2,"mild short circuit");Near(t.ShortCircuitDuration(-10),4,"strong short circuit");
            Check(t.ArcTargets(5)==5 && t.ExtraSparkTargets(5)==3,"passive target caps");
            Debug.Log("Luck v0.3 rules PASSED: 320000 probability samples; Q5/40/90 plans; rounding; short circuits and caps.");
        }finally {UnityEngine.Random.state=saved;}
    }
    [MenuItem("LuckyPinball/Luck Number/Run Acceptance Checks (Play Mode)")]
    public static void Run() {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        try {
            Hud.Instance=null;
            using(var f=new Fixture()) {
                var gm=f.gm;f.Hand(C(Card.Equipment.Arc),C(Card.Equipment.Arc),C(Card.Equipment.Spark),C(Card.Equipment.Spark),C(Card.Equipment.Shockwave));var direct=f.Enemy(6);var a=f.Enemy(6,.7f);var b=f.Enemy(6,1.4f);var c=f.Enemy(6,2.3f);var far=f.Enemy(6,3.2f);var ball=f.Ball();
                gm.TickGameplay(1);Check(direct.hp==40&&a.hp==40,"1 no timed attacks without contact");
                Check(direct.TryBallHit(ball,direct.transform.position),"accepted contact");
                Check(direct.hp==36 && a.hp==36 && b.hp==36 && c.hp==36 && far.hp==40,"1 direct hit plus arc; no double hit on direct target");
                Check(direct.IsBurning && a.IsBurning && !b.IsBurning && direct.IsStunned,"all held enchantments trigger together");
                Check(!direct.TryBallHit(ball),"contact cooldown");
                gm.TickGameplay(.5f);Check(direct.hp==34&&a.hp==34&&gm.Metrics.validBallHits==1,"2 burn and arc never recurse");
                direct.TakeDamage(1);Check(gm.Metrics.validBallHits==1,"damage API cannot trigger ball enchantments");
            }
            using(var f=new Fixture()) {
                f.Hand(C(Card.Equipment.Arc,2),C(Card.Equipment.Spark,2),C(Card.Equipment.Spark,2));var direct=f.Enemy(hp:4);var other=f.Enemy(2,.9f);var ball=f.Ball();var point=direct.transform.position;
                direct.TryBallHit(ball,point);Check(!direct.Alive&&other.hp==36&&other.IsBurning,"lethal collision still emits nearby effects");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;f.Hand(C(Card.Equipment.Shockwave));gm.DebugFixNextOutcome(0);gm.UseCard(0);
                Check(gm.HeldCount(Card.Equipment.Shockwave)==0&&gm.EnabledCount(Card.Equipment.Shockwave)==0,"3 last card removes enchantment");
                gm.DebugAddCard(6,Card.Equipment.Arc);gm.DebugAddCard(5,Card.Equipment.Arc);gm.DebugAddCard(4,Card.Equipment.Spark);
                gm.DebugFixNextOutcome(-10);gm.UseCard(0);
                Check(gm.HeldCount(Card.Equipment.Arc)==1&&gm.EnabledCount(Card.Equipment.Arc)==0&&gm.EnabledCount(Card.Equipment.Spark)==1,"short circuit is type-local");
                Near(gm.ShortCircuitRemaining(Card.Equipment.Arc),4,"negative Q does not heal, disables type");
                gm.TickGameplay(4);Check(gm.EnabledCount(Card.Equipment.Arc)==1,"enchantment recovers after short circuit");
            }
            foreach(int q in new[]{5,40,90}) using(var f=new Fixture()) {
                var gm=f.gm;var targets=Enumerable.Range(0,12).Select(i=>f.Enemy(1,x:(i%3)*2,z:i*.6f)).ToArray();
                gm.DebugFixNextOutcome(q);gm.UseCard(2);var cast=gm.LastCast;
                Check(cast.Targets.Count==(q==5?2:q==40?6:12),"4 locked coverage by Q");
                Check(targets[0].hp==28&&cast.WavesFired==1,"first wave applies exactly twelve, never all damage upfront");
                var newcomer=f.Enemy(2,4,8);
                gm.TickGameplay(.249f);Check(cast.WavesFired==1,"wave waits .25s");gm.TickGameplay(.001f);
                Check(cast.WavesFired==(q==5?1:2),"second wave only for medium/high Q");
                gm.TickGameplay(.5f);Check(cast.WavesFired==cast.Plan.waves&&newcomer.hp==40,"subsequent spawn never joins locked targets");
                if(q==90)Check(cast.Kills==12&&gm.Score==600,"kills aggregate over all waves at flat fifty points");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;var e=f.Enemy(hp:100);e.Ignite(false,3,null);gm.TickGameplay(.25f);e.Ignite(false,3,null);gm.TickGameplay(.25f);
                Check(e.hp==98,"refresh does not reset burn tick phase");
                e.Ignite(true,1,null);gm.TickGameplay(1);Check(e.hp==92,"passive and active burning use max dps, not sum");
                e.Ignite(false,3,null);Near(e.ActiveBurnRemaining,0,"passive cannot extend active high-dps expiry");gm.TickGameplay(.5f);Check(e.hp==90,"weak burn resumes after active expires");
                var odd=f.Enemy(1,4,hp:100);odd.Ignite(true,.75f,null);gm.TickGameplay(.6f);odd.Ignite(true,1,null);gm.TickGameplay(.4f);Check(odd.hp==94,"refresh between non-aligned expiry and tick preserves phase");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;var e=f.Enemy(hp:100);
                Check(e.Stun(.35f,false),"passive stun starts");gm.TickGameplay(.2f);Check(!e.Stun(.35f,false),"5 passive cannot refresh active stun");
                gm.TickGameplay(.15f);Check(!e.IsStunned&&!e.Stun(.35f,false),"5 two-second immunity begins after stun");
                Check(e.Stun(1.2f,true),"active stun bypasses passive immunity");gm.TickGameplay(.4f);e.Stun(.3f,true);Near(e.StunRemaining,.8,"repeated active stun uses max, not addition");
                gm.TickGameplay(.8f);Near(e.PassiveImmunityRemaining,2,"active end also grants passive immunity");gm.TickGameplay(2);Check(e.Stun(.35f,false),"passive stun works after immunity");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;var front=f.Enemy(1,0,0);var back=f.Enemy(2,0,1.6f);var third=f.Enemy(3,0,3.2f);var other=f.Enemy(4,3,1.6f);
                front.Stun(3,true);var plan=EnemyMovement.Plan(gm,.1f);
                Near(plan.Find(m=>m.enemy==front).local.z,0,"stunned front stays put");Near(plan.Find(m=>m.enemy==back).local.z,1.6,"rear waits at safe gap");
                Check(plan.Find(m=>m.enemy==other).local.z<1.6,"independent lateral path keeps moving");
                for(int i=0;i<400;i++) {
                    gm.TickGameplay(.01f);var before=EquipmentResolver.Targets(gm).ToDictionary(e=>e,e=>e.LocalPosition.z);f.ApplyMoves(.01f);
                    foreach(var e in EquipmentResolver.Targets(gm))Check(e.LocalPosition.z<=before[e]+.0001,"queue never teleports backwards");
                    Near(back.LocalPosition.z-front.LocalPosition.z,1.6,"queue spacing stays safe");Near(third.LocalPosition.z-back.LocalPosition.z,1.6,"third row stays safe");
                }
                float z=back.LocalPosition.z;front.TakeDamage(100);f.ApplyMoves(.01f);Near(z-back.LocalPosition.z,(gm.spawning.normalSpawnZ-gm.spawning.dangerZ)/gm.enemyDescendTime*.01,"front death resumes normal speed without catch-up");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;var e=f.Enemy(hp:1000);gm.DebugFixNextOutcome(90);gm.UseCard(2);gm.DebugFixNextOutcome(-10);gm.UseCard(0);
                e.Ignite(false,3,null);e.Stun(1,true);
                float refill=gm.RefillRemaining,stun=e.StunRemaining,burn=e.PassiveBurnRemaining,shorted=gm.ShortCircuitRemaining(Card.Equipment.Spark),luck=gm.LuckyRemaining;int hp=e.hp,waves=gm.LastCast.WavesFired;
                gm.SetPaused(true);gm.TickGameplay(100);
                Check(!gm.UseCard(0)&&e.hp==hp,"7 paused play and burn frozen");Near(gm.RefillRemaining,refill,"refill frozen");Near(gm.LuckyRemaining,luck,"luck frozen");Near(e.StunRemaining,stun,"stun frozen");Near(e.PassiveBurnRemaining,burn,"burn duration frozen");Near(gm.ShortCircuitRemaining(Card.Equipment.Spark),shorted,"short circuit frozen");
                gm.SetPaused(false);State(gm,"Waiting");gm.TickGameplay(100);Near(gm.RefillRemaining,refill,"launch wait preserves debt");Check(e.hp==hp,"waiting freezes pending waves");
                State(gm,"Playing");gm.TickGameplay(.25f);Check(e.hp==hp-12,"pending arc wave resumes after wait");
            }
            foreach(int count in new[]{1,2,3,5}) using(var f=new Fixture()) {
                var gm=f.gm;
                for(int i=0;i<count;i++){gm.DebugFixNextOutcome(i%3==0?5:i%3==1?-6:0);gm.UseCard(0);}
                double first=8+4*count;Near(gm.RefillRemaining,first,"6 every success/failure pays additive delay");
                gm.TickGameplay((float)first-.001f);Check(gm.CardCount==5-count,"no early refill");gm.TickGameplay(.001f);Check(gm.CardCount==6-count,"first card at delayed deadline");
                for(int i=1;i<count;i++){gm.TickGameplay(8);Check(gm.CardCount==6-count+i,"remaining cards refill every eight seconds");}
                Check(gm.IsHandFull,"full recovery at expected deadline");
                if(count==5)Near(gm.Metrics.lowEquipmentSeconds,36,"metric uses agreed zero-to-one-card threshold");
            }
            using(var f=new Fixture()) {
                var gm=f.gm;gm.DebugFixNextOutcome(0);gm.UseCard(0);gm.TickGameplay(3);gm.DebugFixNextOutcome(0);gm.UseCard(0);Near(gm.RefillRemaining,13,"in-progress countdown adds four instead of restarting");
                float debt=gm.RefillRemaining;gm.LoseLife();Near(gm.RefillRemaining,debt,"life loss never clears debt");
            }
            CheckNumberRestrictedEnchantments();
            CheckRealCollision();
            Debug.Log("Luck v0.3 acceptance PASSED: contact-only enchants; real physics contact; statuses; Q scaling and timed waves; queue; 12/24/36/60-second recovery; pause and 0-1-card metrics.");
        }finally {GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
    static void CheckNumberRestrictedEnchantments()
    {
        using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(Card.Equipment.Arc,6),C(Card.Equipment.Spark,2),C(Card.Equipment.Shockwave,4));
            var direct=f.Enemy(2);var arc=f.Enemy(6,.8f);var shock=f.Enemy(4,-.8f);var wrong=f.Enemy(3,.2f);var ball=f.Ball();
            direct.TryBallHit(ball,direct.transform.position);
            Check(direct.hp==36 && direct.IsBurning && !direct.IsStunned,"number example: direct #2 gets base damage and #2 burn only");
            Check(arc.hp==36 && !arc.IsBurning && !arc.IsStunned,"number example: #6 arc activates from a #2 hit");
            Check(shock.hp==40 && shock.IsStunned && !shock.IsBurning,"number example: #4 stun only");
            Check(wrong.hp==40 && !wrong.IsBurning && !wrong.IsStunned,"wrong numbers never receive passive effects");
            Check(gm.Metrics.validBallHits==1,"filtered effects cannot recurse");
        }
        using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(Card.Equipment.Arc,6),C(Card.Equipment.Arc,2));
            var direct=f.Enemy(3);var ball=f.Ball();
            var six=new[]{f.Enemy(6,.5f),f.Enemy(6,1f),f.Enemy(6,1.5f)};
            var two=new[]{f.Enemy(2,-.5f),f.Enemy(2,-1f),f.Enemy(2,-1.5f)};
            direct.TryBallHit(ball,direct.transform.position);
            Check(six[0].hp==36 && six[1].hp==36 && six[2].hp==40 && two[0].hp==36 && two[1].hp==36 && two[2].hp==40,"split arc numbers each use N=1 and select two qualified targets");
            gm.DebugFixNextOutcome(0);gm.UseCard(0);gm.TickGameplay(.2f);direct.TryBallHit(ball,direct.transform.position);
            Check(six[0].hp==36 && two[0].hp==32 && gm.HeldCount(Card.Equipment.Arc,6)==0 && gm.HeldCount(Card.Equipment.Arc,2)==1,"consuming last #6 arc leaves #2 arc working");
            gm.DebugAddCard(6,Card.Equipment.Arc);gm.DebugFixNextOutcome(-10);gm.UseCard(1);
            Check(gm.EnabledCount(Card.Equipment.Arc,2)==0,"short circuit remains type-wide across number groups");
        }
        using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(Card.Equipment.Arc,6),C(Card.Equipment.Arc,6));
            var direct=f.Enemy(1);var wrong=f.Enemy(2,.1f);var same=new[]{f.Enemy(6,.5f),f.Enemy(6,1),f.Enemy(6,1.5f),f.Enemy(6,2)};
            direct.TryBallHit(f.Ball(),direct.transform.position);
            Check(same.Take(3).All(e=>e.hp==36) && same[3].hp==40 && wrong.hp==40,"two same-number arcs use N=2; wrong nearer target cannot consume a slot or relay");
        }
        using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(Card.Equipment.Spark,6),C(Card.Equipment.Spark,2));
            var direct=f.Enemy(2);var nearSix=f.Enemy(6,.5f);var nearTwo=f.Enemy(2,-.5f);var ball=f.Ball();direct.TryBallHit(ball);
            Check(direct.IsBurning && !nearSix.IsBurning && !nearTwo.IsBurning,"split sparks each N=1: only matching directly hit target burns");
            f.Hand(C(Card.Equipment.Spark,6),C(Card.Equipment.Spark,6),C(Card.Equipment.Spark,2));
            gm.TickGameplay(.2f);var wrong=f.Enemy(3,0,0);var a=f.Enemy(6,.7f,0);var b=f.Enemy(6,1.4f,0);var foreign=f.Enemy(2,.2f,0);wrong.TryBallHit(ball);
            Check(!wrong.IsBurning && a.IsBurning && !b.IsBurning && !foreign.IsBurning,"spark extra targets are counted per element-number pair and filtered before the cap");
        }
        using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(Card.Equipment.Shockwave,6),C(Card.Equipment.Shockwave,2));
            var direct=f.Enemy(3);var six=f.Enemy(6,1.3f);var two=f.Enemy(2,-1.3f);var ball=f.Ball();direct.TryBallHit(ball);
            Check(!six.IsStunned && !two.IsStunned && !direct.IsStunned,"split shockwaves each use 1.2m radius, not combined 1.45m");
            f.Hand(C(Card.Equipment.Shockwave,6),C(Card.Equipment.Shockwave,6),C(Card.Equipment.Shockwave,2));gm.TickGameplay(.2f);direct.TryBallHit(ball);
            Check(six.IsStunned && !two.IsStunned && !direct.IsStunned,"only #6 radius expands with a second #6 card");
        }
        using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(Card.Equipment.Shockwave,6),C(Card.Equipment.Shockwave,6),C(Card.Equipment.Shockwave,6));
            var front=f.Enemy(6,0,0);var back=f.Enemy(2,0,1.6f);var free=f.Enemy(3,3,1.6f);
            front.TryBallHit(f.Ball(),front.transform.position);
            Check(front.IsStunned && !back.IsStunned && back.PassiveImmunityRemaining==0,"foreign rear in shockwave radius is not stunned or immune");
            var plan=EnemyMovement.Plan(gm,.1f);Near(plan.Find(m=>m.enemy==back).local.z,1.6,"foreign rear waits due to queue only");
            Check(plan.Find(m=>m.enemy==free).local.z<1.6,"unrelated lateral path stays free");
            gm.TickGameplay(.35f);Check(!back.IsStunned && back.PassiveImmunityRemaining==0,"queue waiting never grants passive stun immunity");
        }
        foreach(Card.Equipment type in Enum.GetValues(typeof(Card.Equipment))) using(var f=new Fixture()) {
            var gm=f.gm;f.Hand(C(type,6));gm.DebugSetLuckyNumber(6);
            var a=f.Enemy(1,0,0);var b=f.Enemy(2,0,1);var farMatch=f.Enemy(6,0,8);
            var plan=gm.luck.Plan(type,5);var preview=EquipmentResolver.ActiveTargets(gm,plan);
            gm.DebugFixNextOutcome(5);gm.UseCard(0);
            Check(gm.LastPlayWasLucky && gm.LastTargetCount==2 && gm.LastCast.Targets.SequenceEqual(preview) && !gm.LastCast.Targets.Contains(farMatch),"active low strength ignores numbers and never adds distant matching foes");
            if(type==Card.Equipment.Arc)Check(a.hp==28 && b.hp==28 && farMatch.hp==40,"arc actively damages any number");
            if(type==Card.Equipment.Spark)Check(a.IsBurning && b.IsBurning && !farMatch.IsBurning,"active fire burns any number");
            if(type==Card.Equipment.Shockwave)Check(a.IsStunned && b.IsStunned && !farMatch.IsStunned,"active shock stuns any number");
        }
        using(var f=new Fixture()) {
            var gm=f.gm;var enemies=Enumerable.Range(0,12).Select(i=>f.Enemy(i%6+1,0,i)).ToArray();
            gm.DebugFixNextOutcome(40);gm.UseCard(0);
            Check(gm.LastTargetCount==6 && gm.LastCast.Targets.SequenceEqual(enemies.Take(6)),"medium active skill selects nearest six without same-number expansion");
            gm.DebugFixNextOutcome(90);gm.UseCard(0);Check(gm.LastTargetCount==12,"high active skill still covers every number");
        }
        Debug.Log("Number-limited enchantments PASSED: mixed hits, per-number N, removal, unchanged type-wide short circuit, active targeting and queue-only blocking.");
    }
    static void CheckRealCollision() {
        var scene=SceneManager.CreateScene("Luck local physics test",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try {using(var f=new Fixture()) {
            SceneManager.MoveGameObjectToScene(f.root,scene);
            f.Hand(C(Card.Equipment.Arc,2));var direct=f.Enemy(hp:4);var other=f.Enemy(2,0,6);var ball=f.Ball();
            foreach(var collider in direct.GetComponentsInChildren<Collider>()) if(collider!=direct.GetComponent<MeshCollider>())collider.enabled=false;
            foreach(var collider in other.GetComponentsInChildren<Collider>()) if(collider!=other.GetComponent<MeshCollider>())collider.enabled=false;
            ball.position=direct.transform.position+Vector3.left*1.15f;ball.linearVelocity=Vector3.right*8;
            for(int i=0;i<20;i++){f.gm.TickGameplay(.01f);scene.GetPhysicsScene().Simulate(.01f);}
            Check(f.gm.Metrics.validBallHits==1 && !direct.Alive && other.hp==36,"actual OnCollisionEnter retains point after lethal hit and triggers arc exactly once");
        }}finally {SceneManager.UnloadSceneAsync(scene);}
    }
}
