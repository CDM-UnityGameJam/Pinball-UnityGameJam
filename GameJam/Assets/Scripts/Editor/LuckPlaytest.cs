using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Fixed-hit comparison isolates single versus combined dice decisions. It is not human playtesting.
public static class LuckPlaytest
{
    [MenuItem("LuckyPinball/Luck Number/Compare Single vs Combined (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var rows=new List<RunTelemetry>();
        try {
            Hud.Instance=null;
            foreach(bool combined in new[]{false,true})for(int seed=1;seed<=5;seed++) {
                UnityEngine.Random.InitState(5050+seed);
                using(var f=new LuckPrototypeCheck.Fixture()) {
                    var gm=f.gm;gm.Metrics.policy=combined?"Combine same-number pairs":"Split into single casts";
                    gm.difficultyConfig=original.difficultyConfig;
                    var ball=f.Ball();float nextSpawn=0,nextHit=.6f,nextDecision=0;int spawn=0;
                    while(gm.IsPlaying && gm.ActiveTime<180) {
                        float now=gm.ActiveTime;
                        if(now>=nextSpawn) {
                            float x=(spawn%3-1)*2.4f;var element=(Card.Equipment)((spawn+seed)%3);
                            bool clear=EquipmentResolver.Targets(gm).All(e=>Vector2.Distance(new Vector2(x,gm.spawning.normalSpawnZ),new Vector2(e.LocalPosition.x,e.LocalPosition.z))>=e.Radius+gm.normalEnemy.radius+.1f);
                            if(clear)f.Enemy(element,x,gm.spawning.normalSpawnZ,40);spawn++;nextSpawn+=gm.CurrentSpawnInterval;
                        }
                        if(now>=nextHit){var enemies=EquipmentResolver.Targets(gm);if(enemies.Count>0)enemies[0].TryBallHit(ball);nextHit+=1.2f;}
                        if(now>=nextDecision && gm.CanSubmit) {
                            var enemies=EquipmentResolver.Targets(gm);
                            bool pressure=enemies.Count>=8 || (enemies.Count>0 && enemies[0].LocalPosition.z<=2);
                            if(gm.CardCount>0 && pressure) {
                                Card first=null;
                                foreach(var c in gm.HandSnapshot)if(c!=null && (first==null || gm.NumberCount(c.number)>gm.NumberCount(first.number)))first=c;
                                var chosen=new List<Card>{first};
                                if(combined)foreach(var c in gm.HandSnapshot)if(c!=null && !ReferenceEquals(c,first) && c.number==first.number){chosen.Add(c);break;}
                                gm.UseCards(chosen);
                            }
                            nextDecision=now+.5f;
                        }
                        gm.TickGameplay(.05f);f.ApplyMoves(.05f);
                    }
                    rows.Add(gm.Metrics);
                }
            }
            var report=new StringBuilder("# v0.6 single versus combined scripted comparison\n\nFixed 1.2-second hit attempts, five seeds per policy, up to 180 effective seconds. Both start with the same five cards and choose the largest same-number group under the same pressure condition. One policy submits one card per batch; the other combines up to two. Actual charge, dice, immunity, waves, statuses and queue logic run. These are synthetic contacts, not real pinball or human playtest results.\n\n|Policy|Seed|Batches|Cards spent|Base/Hit/Full/Jackpot|Coverage|Batch kills|Breaches|Low gear (0-1) seconds|End|\n|---|---:|---:|---:|---|---:|---:|---:|---:|---|\n");
            int i=0;foreach(var m in rows)report.Append($"|{m.policy}|{i++%5+1}|{m.batches}|{m.plays}|{m.baseBatches}/{m.standardBatches}/{m.fullFieldBatches}/{m.jackpotBatches}|{m.coveredTargets}|{m.batchKills}|{m.breaches}|{m.lowEquipmentSeconds:F1}|{m.deathCause}|\n");
            Directory.CreateDirectory("Docs");File.WriteAllText("Docs/LuckNumberV06ScriptedBalance.md",report.ToString());
            Debug.Log("v0.5 scripted comparison saved to Docs/LuckNumberV06ScriptedBalance.md");
        }finally{GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}

