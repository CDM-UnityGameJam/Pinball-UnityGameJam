using System;
using System.Linq;
using UnityEngine;
internal static class V06Checks
{
 static void Check(bool v,string s){if(!v)throw new Exception("v0.6: "+s);}
 static void Near(double a,double b,string s){Check(Math.Abs(a-b)<.002,s+" "+a+" != "+b);}
 static Card C(Card.Equipment e)=>new Card(4,e);
 static LuckPrototypeCheck.Fixture F(){var f=new LuckPrototypeCheck.Fixture();f.gm.luck.rechargePerSecond=0;return f;}
 public static void Run(){
  using(var f=F()){
   f.Hand();var e=f.Enemy(Card.Equipment.Spark);var b=f.Ball();var c=b.GetComponent<Collider>();
   Check(e.BeginBallContact(b,c,e.transform.position),"first contact");Check(e.hp==96,"first damage");
   f.gm.TickGameplay(.1f);Check(!e.BeginBallContact(b,c,e.transform.position),"stay ignored");e.EndBallContact(b,c);e.BeginBallContact(b,c,e.transform.position);Check(e.hp==96,"cooldown damage blocked");
   f.gm.TickGameplay(.2f);e.EndBallContact(b,c);e.BeginBallContact(b,c,e.transform.position);Check(e.hp==92,"third damage");Near(f.gm.Metrics.collisionCharge,7.5,"three contact gains");
   f.gm.TickGameplay(1);Check(!e.BeginBallContact(b,c,e.transform.position),"persistent contact ignored");Near(f.gm.Metrics.collisionCharge,7.5,"stay no gain");
  }
  using(var f=F()){
   f.Hand();var e=f.Enemy(Card.Equipment.Spark,hp:1000);
   for(int i=0;i<6;i++){var b=f.Ball();e.BeginBallContact(b,b.GetComponent<Collider>(),e.transform.position);}
   Near(f.gm.Metrics.collisionCharge,15,"multiball full gains without cap");Check(e.hp==996,"shared damage cooldown");
  }
  using(var f=F()){
   f.Hand();var e=f.Enemy(Card.Equipment.Arc);e.Ignite(false,3,new AttackRecord("burn test"));f.gm.TickGameplay(1);e.Ignite(true,2,new AttackRecord("refresh"));f.gm.TickGameplay(2);
   Check(f.gm.Metrics.burnChargeEvents==0,"burn never charges");e.Ignite(false,1,new AttackRecord("new burn"));f.gm.TickGameplay(.5f);Check(f.gm.Metrics.burnChargeEvents==0,"reignited burn never charges");
  }
  using(var f=F()){
   f.Hand(C(Card.Equipment.Arc),C(Card.Equipment.Arc),C(Card.Equipment.Arc),C(Card.Equipment.Arc),C(Card.Equipment.Arc));var e=f.Enemy(Card.Equipment.Shockwave);
   e.Ignite(false,2,new AttackRecord("full hand burn"));f.gm.TickGameplay(.5f);f.gm.DebugSetDice(new[]{1});f.gm.UseCard(0);f.gm.TickGameplay(1);Check(f.gm.Metrics.burnChargeEvents==0,"full hand first tick not deferred");
  }
  using(var f=F()){
   f.Hand();var e=f.Enemy(Card.Equipment.Arc);e.Ignite(false,3,new AttackRecord("six ticks"));f.gm.TickGameplay(3);Check(e.hp==88 && f.gm.Metrics.burnChargeEvents==0,"six burn ticks, no charge");
   var lethal=f.Enemy(Card.Equipment.Arc,hp:2);lethal.Ignite(false,1,new AttackRecord("lethal first tick"));f.gm.TickGameplay(.5f);Check(!lethal.Alive && f.gm.Metrics.burnChargeEvents==0,"lethal burn grants no charge");
  }
  foreach(int outcome in new[]{0,1,2})using(var f=F()){
   var a=C(Card.Equipment.Spark);var b=C(Card.Equipment.Spark);f.Hand(a,b);var target=f.Enemy(Card.Equipment.Arc);var immune=f.Enemy(Card.Equipment.Spark,2);
   f.gm.DebugSetDice(outcome==0?new[]{2,6}:outcome==1?new[]{4,6}:new[]{4,4});Check(f.gm.UseCards(new[]{a,b}),"fire batch accepted");f.gm.TickGameplay(.45f);
   Near(target.ActiveBurnRemaining,outcome==0?3:outcome==1?8:10,"duplicate fire duration");Check(!immune.IsBurning,"fire immunity");Check(f.gm.LastBatch.Skills.Count>0,"Base creates skills");
  }
  using(var f=F()){
   f.Hand(C(Card.Equipment.Shockwave));var a=f.Enemy(Card.Equipment.Spark);var b=f.Enemy(Card.Equipment.Arc,.5f);a.TryBallHit(f.Ball());Check(a.hp==96 && b.hp==100 && b.IsStunned,"passive shock status only");
   f.gm.DebugSetDice(new[]{4});f.gm.UseCard(0);f.gm.TickGameplay(.45f);Check(a.hp==88 && b.hp==92,"active shock eight damage");Near(a.StunRemaining,1.5,"active stun 1.5 seconds");
  }
  Debug.Log("v0.6 SEVEN ACCEPTANCE GROUPS PASSED");
 }
}


