using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class DifficultyChecks
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string message){if(!ok)throw new Exception("Difficulty integration: "+message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.0001,message+" ("+a+" / "+b+")");}
    [MenuItem("LuckyPinball/Difficulty/Check Rules")]
    public static void Rules(){Debug.Log(DifficultyRuleChecks.Run());}
    [MenuItem("LuckyPinball/Difficulty/Run Integration Checks (Play Mode)")]
    public static void Run()
    {
        if(!Application.isPlaying)throw new Exception("Enter Play Mode first.");
        var original=GameManager.Instance;var hud=Hud.Instance;var random=UnityEngine.Random.state;var gravity=Physics.gravity;float scale=Time.timeScale,step=Time.fixedDeltaTime;
        var config=ScriptableObject.CreateInstance<DifficultyConfig>();
        try{
            Hud.Instance=null;Rules();
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.spawning.openingNormalCount=1;
                var ball=f.Ball();gm.ball=ball;
                ball.position=ball.transform.position=f.board.transform.TransformPoint(new Vector3(0,.5f,gm.spawning.normalSpawnZ));
                ((System.Collections.Generic.List<Rigidbody>)typeof(GameManager).GetField("balls",Private).GetValue(gm)).Add(ball);
                typeof(GameManager).GetMethod("BeginWaiting",Private).Invoke(gm,null);
                var spawner=gm.gameObject.AddComponent<Spawner>();spawner.Initialize(gm);
                Check(EquipmentResolver.Targets(gm).Count==1,"opening singleton spawns after ball is moved to launch position");
                Near(spawner.GenerationProgress,0,"opening singleton does not consume periodic progress");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.difficultyConfig=config;gm.spawning.openingNormalCount=3;
                var spawner=gm.gameObject.AddComponent<Spawner>();spawner.SendMessage("Start");typeof(GameManager).GetField("difficultySpawner",Private).SetValue(gm,spawner);
                Check(EquipmentResolver.Targets(gm).Count==3,"three opening monsters");Near(spawner.GenerationProgress,0,"opening progress zero");
                gm.spawning.normalChancePercent=0;gm.TickGameplay(1);Near(spawner.GenerationProgress,.5,"combat tick advances phase");
                gm.SetPaused(true);gm.TickGameplay(10);Near(gm.CombatTime,1,"pause freezes combat");Near(spawner.GenerationProgress,.5,"pause freezes phase");gm.SetPaused(false);
                LuckPrototypeCheck.State(gm,"Waiting");gm.TickGameplay(10);Near(gm.DifficultyTime,1,"waiting freezes difficulty");Near(spawner.GenerationProgress,.5,"waiting freezes phase");LuckPrototypeCheck.State(gm,"Playing");
                config.spawnSpeedMultiplier=2;gm.TickGameplay(.5f);Check(spawner.PeriodicAttempts==1,"live config change preserves half progress");Near(spawner.GenerationProgress,0,"attempt consumed with zero chance");config.spawnSpeedMultiplier=1;
                gm.DifficultyDebug.Specify(300);Near(gm.CurrentSpawnInterval,1,"specified minute five");double combat=gm.CombatTime;gm.TickGameplay(.5f);Near(gm.CombatTime,combat+.5,"combat still runs");Near(gm.DifficultyTime,300,"specified sample fixed");
                gm.DifficultyDebug.Freeze(gm.CombatTime);gm.TickGameplay(.5f);Near(gm.DifficultyTime,300,"frozen sample");gm.DifficultyDebug.Automatic();Near(gm.DifficultyTime,gm.CombatTime,"automatic restores actual time");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.difficultyConfig=config;gm.spawning.openingNormalCount=0;gm.spawning.normalChancePercent=100;
                var spawner=gm.gameObject.AddComponent<Spawner>();spawner.SendMessage("Start");typeof(GameManager).GetField("difficultySpawner",Private).SetValue(gm,spawner);
                for(float x=-9;x<=9;x+=.5f)f.Enemy(Card.Equipment.Arc,x,gm.spawning.normalSpawnZ);
                int count=EquipmentResolver.Targets(gm).Count;gm.TickGameplay(6);Check(EquipmentResolver.Targets(gm).Count==count&&spawner.PeriodicAttempts==3,"blocked spawn consumes every attempt");
                foreach(var enemy in EquipmentResolver.Targets(gm))Object.DestroyImmediate(enemy.gameObject);
                gm.TickGameplay(1);Check(EquipmentResolver.Targets(gm).Count==0,"unblocked no backlog burst");gm.TickGameplay(1);Check(EquipmentResolver.Targets(gm).Count==1,"unblocked resumes normal cadence");
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.difficultyConfig=config;gm.DifficultyDebug.Specify(300);var front=f.Enemy(Card.Equipment.Spark,0,0);var back=f.Enemy(Card.Equipment.Arc,0,2);var side=f.Enemy(Card.Equipment.Spark,4,2);
                var backPosition=f.board.transform.TransformPoint(new Vector3(0,back.LocalPosition.y,front.LocalPosition.z+front.Radius+back.Radius+gm.luck.enemyQueueGap));back.GetComponent<Rigidbody>().position=backPosition;back.transform.position=backPosition;front.Stun(1,true);
                var moves=EnemyMovement.Plan(gm,.5f);Near(moves.First(m=>m.enemy==front).local.z,front.LocalPosition.z,"stunned front stationary");Near(moves.First(m=>m.enemy==back).local.z,back.LocalPosition.z,"back queues without overlap");Near(moves.First(m=>m.enemy==side).local.z,side.LocalPosition.z-gm.CurrentMoveSpeed*.5,"unrelated lane descends at current speed");
                back.Ignite(false,3,new AttackRecord("difficulty burn clock check"));
                f.Hand(new Card(6,Card.Equipment.Arc));gm.DebugSetDice(new[]{1});gm.UseCard(0);gm.TickGameplay(.45f);Check(gm.LastBatch.Revealed,"reveal uses combat time with difficulty at 300");Near(front.StunRemaining,.55,"stun clock unchanged");Near(gm.DifficultyTime,300,"difficulty remains fixed");
                gm.TickGameplay(.05f);Near(back.PassiveBurnRemaining,2.5,"burn uses combat time, not difficulty time");Check(back.hp==98,"first burn tick after half a combat second");
                double speed=gm.CurrentMoveSpeed;config.moveSpeedMultiplier=2;Near(gm.CurrentMoveSpeed,speed*2,"existing enemies immediately use edited speed");var edited=EnemyMovement.Plan(gm,.1f);Near(edited.First(m=>m.enemy==side).local.z,side.LocalPosition.z-speed*2*.1,"live edited speed used in movement plan");config.moveSpeedMultiplier=1;
            }
            using(var f=new LuckPrototypeCheck.Fixture()){
                var gm=f.gm;gm.difficultyConfig=config;gm.DifficultyDebug.Specify(300);var older=gm.SpawnEnemy(Card.Equipment.Spark,new Vector3(0,0,4));Check(older.maxHp==80&&older.hp==80,"minute-five spawn HP");older.TakeDamage(10,new AttackRecord("HP retention"));gm.DifficultyDebug.Specify(480);var newer=gm.SpawnEnemy(Card.Equipment.Arc,new Vector3(3,0,4));Check(newer.hp==100&&newer.maxHp==100,"minute-eight spawn HP");Check(older.hp==70&&older.maxHp==80,"existing HP and maximum never rescale");config.baseEnemyHealth=20;var edited=gm.SpawnEnemy(Card.Equipment.Spark,new Vector3(-3,0,4));Check(edited.hp==50&&older.hp==70&&newer.hp==100,"live base HP change affects new spawns only");config.baseEnemyHealth=40;
            }
            Debug.Log("DIFFICULTY INTEGRATION PASSED: opening count, phase, pause/wait, blocked attempts, live edits, independent sample, queue, reveal timing and spawn-only HP growth.");
        }finally{Object.DestroyImmediate(config);GameManager.Instance=original;Hud.Instance=hud;UnityEngine.Random.state=random;Physics.gravity=gravity;Time.timeScale=scale;Time.fixedDeltaTime=step;}
        LuckPrototypeCheck.Run();
    }
    static double started;
    public static void RunBatch(){started=EditorApplication.timeSinceStartup;EditorSceneManager.OpenScene("Assets/Scenes/Pinball.unity");EditorApplication.update+=Wait;EditorApplication.isPlaying=true;}
    static void Wait(){
        if(EditorApplication.timeSinceStartup-started>120){Finish(false,"Play startup timeout");return;}
        if(!Application.isPlaying||!GameManager.Instance||GameManager.Instance.Lives==0||!Hud.Instance)return;
        EditorApplication.update-=Wait;
        try{var gm=GameManager.Instance;Check(gm.difficultyConfig&&gm.difficultyConfig.Errors().Count==0,"scene references valid default asset");Near(gm.CurrentSpawnInterval,2,"scene default interval");Run();Finish(true,"Difficulty integration and existing v0.6 regressions passed.");}catch(Exception e){Debug.LogException(e);Finish(false,e.ToString());}
    }
    static void Finish(bool ok,string detail){EditorApplication.update-=Wait;Directory.CreateDirectory("tmp");File.WriteAllText("tmp/difficulty-result.txt",(ok?"PASS":"FAIL")+"\n"+detail);EditorApplication.Exit(ok?0:1);}
}
