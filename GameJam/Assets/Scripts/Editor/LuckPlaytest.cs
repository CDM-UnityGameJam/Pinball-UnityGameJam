using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// A reproducible encounter simulation, not a substitute for human flipper/ball playtesting.
public static class LuckPlaytest
{
    [MenuItem("LuckyPinball/Luck Number/Compare Play Policies (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;
        float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var records=new List<RunTelemetry>();
        try {
            Hud.Instance=null;
            foreach(bool greedy in new[]{true,false}) for(int seed=1;seed<=5;seed++) {
                UnityEngine.Random.InitState(3000+seed);
                using(var f=new LuckPrototypeCheck.Fixture()) {
                    var gm=f.gm;
                    gm.Metrics.policy=greedy?"Cast immediately":"Keep enchantments, cast selectively";
                    gm.spawnInterval=original.spawnInterval;gm.enemyDescendTime=original.enemyDescendTime;
                    var ball=f.Ball();ball.position=f.board.transform.position+Vector3.up*10;
                    float nextSpawn=0,nextHit=.6f,nextDecision=0;int spawn=0;
                    while(gm.IsPlaying && gm.CombatTime<180) {
                        float now=gm.ActiveTime;
                        if(now>=nextSpawn) {
                            float x=((spawn%3)-1)*2.4f;int number=(spawn*5+seed)%6+1;
                            bool clear=EquipmentResolver.Targets(gm).All(e=>Vector2.Distance(new Vector2(x,gm.spawning.normalSpawnZ),new Vector2(e.LocalPosition.x,e.LocalPosition.z))>=e.Radius+gm.normalEnemy.radius+.1f);
                            if(clear)f.Enemy(number,x,gm.spawning.normalSpawnZ);
                            spawn++;nextSpawn+=gm.spawnInterval;
                        }
                        if(now>=nextHit) {
                            var enemies=EquipmentResolver.Targets(gm);
                            if(enemies.Count>0) enemies[0].TryBallHit(ball,enemies[0].transform.position);
                            nextHit+=1.2f;
                        }
                        if(now>=nextDecision) {
                            if(greedy) {
                                while(gm.CardCount>0 && gm.IsPlaying)gm.UseCard(0);
                            } else {
                                var enemies=EquipmentResolver.Targets(gm);
                                bool danger=enemies.Count>=8 || (enemies.Count>0 && enemies[0].LocalPosition.z<=2);
                                if(gm.CardCount>=4 && danger) {
                                    int slot=0;
                                    for(int i=0;i<5;i++) if(gm.GetCard(i)!=null && gm.GetCard(i).number==gm.CurrentNumber){slot=i;break;}
                                    gm.UseCard(slot);
                                }
                            }
                            nextDecision+=.5f;
                        }
                        gm.TickGameplay(.05f);f.ApplyMoves(.05f);
                    }
                    records.Add(gm.Metrics);
                }
            }
            var report=new StringBuilder("# Luck Number v0.3 号码限定附魔：出牌成本对照\n\n");
            report.Append("以下为脚本化遭遇模拟，并非真实挡板／弹球试玩。每种策略使用5个固定随机种子，最长180秒；沿用场景生成间隔和下降时间，按固定横向列生成，拥挤时跳过生成，每1.2秒向最前排提交一次有效球命中。运行真实附魔、状态、技能波次、队列、概率和补牌代码；不模拟挡板操作和落球。因此死亡原因只能为越线，结论不能直接外推到真实玩家。\n\n");
            report.Append("策略：见牌就打每0.5秒消耗全部可用牌；保留策略保持至少3张装备，仅敌群≥8或最前怪进入z≤2时考虑出牌，优先幸运同号。低装备严格按用户确认的0～1张计算。概率及补牌追加延迟保持设计值，未自动调参。\n\n");
            report.Append("|策略|样本|出牌次数|平均手牌|0～1张持续秒数|运行秒数|击杀|死亡原因|\n|---|---:|---:|---:|---:|---:|---:|---|\n");
            int index=0;
            foreach(var r in records) {
                report.Append($"|{r.policy}|{index++%5+1}|{r.plays}|{r.AverageHand:F2}|{r.lowEquipmentSeconds:F1}|{r.elapsed:F1}|{r.kills}|{r.deathCause}|\n");
                Debug.Log("Luck v0.3 scripted policy: "+r.Summary());
            }
            report.Append("\n|策略|平均出牌|平均手牌|平均低装备秒数|平均运行秒数|存活至180秒|\n|---|---:|---:|---:|---:|---:|\n");
            foreach(var group in records.GroupBy(r=>r.policy))
                report.Append($"|{group.Key}|{group.Average(r=>r.plays):F1}|{group.Average(r=>r.AverageHand):F2}|{group.Average(r=>r.lowEquipmentSeconds):F1}|{group.Average(r=>r.elapsed):F1}|{group.Count(r=>r.deathCause=="Alive")}/5|\n");
            report.Append("\n人工试玩可直接观察左侧本局记录；结束或重开时 Console 自动输出统计，或在 GameManager Inspector 点击记录按钮。应补充两种真实玩家策略的落球、越线和通关压力，再决定追加延迟改为3秒或提高。\n");
            Directory.CreateDirectory("Docs");File.WriteAllText(Path.GetFullPath("Docs/LuckNumberV03Balance.md"),report.ToString());
            Debug.Log("Luck v0.3 policy comparison saved to Docs/LuckNumberV03Balance.md");
        }finally {GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
    }
}
