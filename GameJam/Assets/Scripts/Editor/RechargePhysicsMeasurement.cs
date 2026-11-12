using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Uses the actual Pinball scene, native ball collisions and moving flippers. No synthetic enemy hits.
[DefaultExecutionOrder(-500)]
public class RechargePhysicsMeasurement : MonoBehaviour
{
    class Result {public string profile;public int seed;public RunTelemetry stats;}
    readonly List<Result> results=new List<Result>();
    GameManager manager,previous;
    Flipper[] flippers;
    int episode;
    float oldCapture;
    bool configured,oldBackground;
    Action completed;
    const int Seeds=3;
    const float Duration=120;
    [MenuItem("LuckyPinball/Luck Number/Compare Recharge Density (Real Physics, Play Mode)")]
    public static void Run() => StartComparison(null);
    public static void StartComparison(Action onComplete)
    {
        if(!Application.isPlaying || !GameManager.Instance)throw new Exception("Enter Pinball Play Mode first. This comparison resets the current run.");
        if(FindAnyObjectByType<RechargePhysicsMeasurement>())throw new Exception("A measurement is already running.");
        var go=new GameObject("Real physics recharge measurement");
        DontDestroyOnLoad(go);
        var driver=go.AddComponent<RechargePhysicsMeasurement>();
        driver.oldCapture=Time.captureDeltaTime;driver.oldBackground=Application.runInBackground;driver.completed=onComplete;
        Time.captureDeltaTime=.05f;Application.runInBackground=true;
        driver.NextScene();
    }
    void NextScene()
    {
        previous=manager?manager:GameManager.Instance;manager=null;configured=false;
        UnityEngine.Random.InitState(4040+episode%Seeds);
        SceneManager.LoadScene("Assets/Scenes/Pinball.unity");
    }
    void Update()
    {
        if(!configured) {
            var gm=GameManager.Instance;
            if(!gm || gm==previous || gm.Lives==0 || !Hud.Instance || !gm.ball)return;
            manager=gm;
            manager.DifficultyDebug.SetMultipliers((gm.difficultyConfig?gm.difficultyConfig.spawnSpeedMultiplier:1)*(episode<Seeds?1:2),gm.difficultyConfig?gm.difficultyConfig.moveSpeedMultiplier:1);
            manager.Metrics.policy=episode<Seeds?"Regular density / physical controller":"High density / physical controller";
            flippers=gm.board.GetComponentsInChildren<Flipper>();foreach(var f in flippers)f.enabled=false;
            typeof(GameManager).GetMethod("Launch",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(gm,null);
            configured=true;
        }
        if(!manager.IsGameOver && manager.ActiveTime<Duration)return;
        results.Add(new Result {profile=episode<Seeds?"Regular":"High (2x spawn attempts)",seed=episode%Seeds+1,stats=manager.Metrics});
        Debug.Log("V05 REAL PHYSICS: "+manager.Metrics.Summary());
        episode++;
        if(episode<Seeds*2)NextScene();else Finish();
    }
    void FixedUpdate()
    {
        if(!configured || !manager || !manager.IsPlaying)return;
        var ball=manager.ball;
        Vector3 p=ball?manager.board.InverseTransformPoint(ball.position):Vector3.zero;
        foreach(var f in flippers)f.Step(ball && p.z< -6 && (f.isLeft?p.x<1.5f:p.x> -1.5f),Time.fixedDeltaTime);
        var enemies=EquipmentResolver.Targets(manager);
        bool pressure=enemies.Count>=8 || (enemies.Count>0 && enemies[0].LocalPosition.z<=2);
        if(manager.CardCount<4 || !pressure || !manager.CanSubmit)return;
        int slot=0;for(int i=0;i<manager.HandSize;i++)if(manager.GetCard(i)!=null && manager.NumberCount(manager.GetCard(i).number)>1){slot=i;break;}
        manager.UseCard(slot);
    }
    [MenuItem("LuckyPinball/Luck Number/Stop Physical Recharge Measurement")]
    public static void Stop() {var driver=FindAnyObjectByType<RechargePhysicsMeasurement>();if(driver)driver.Finish();}
    void Finish()
    {
        var csv=new StringBuilder("profile,seed,effective_seconds,missing_seconds,raw_hits_per_missing_second,eligible_hits_per_missing_second,batch_hit_ratio,actual_charge_per_missing_second,average_enemies,refills,mean_refill_interval,breaches,drains,death_cause\n");
        var md=new StringBuilder("# v0.5 real-physics density measurement\n\nNative ball collisions in Pinball, original enemy movement/returns/skills, a fixed ball-position-based flipper controller, three paired seeds, up to 120 effective seconds per run. The high profile doubles the starting spawn-attempt frequency only for that run; there is no time-based acceleration. Both profiles keep at least three cards and cast under pressure. This is automated real-physics play, not human playtesting.\n\n|Profile|Seed|Missing hits/s|Eligible hits/s|Batch hit|Actual charge/s|Mean enemies|Refills|Mean refill interval|Breaches|Drains|Death|\n|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|\n");
        foreach(var r in results) {
            var m=r.stats;
            csv.AppendFormat(CultureInfo.InvariantCulture,"{0},{1},{2:F3},{3:F3},{4:F4},{5:F4},{6:F4},{7:F4},{8:F3},{9},{10:F3},{11},{12},{13}\n",r.profile,r.seed,m.elapsed,m.missingSeconds,m.MissingHitRate,m.EligibleHitRate,m.GroupHitRatio,m.RechargeRate,m.AverageEnemyCount,m.refills,m.AverageRefillInterval,m.breaches,m.drains,m.deathCause);
            md.AppendFormat(CultureInfo.InvariantCulture,"|{0}|{1}|{2:F2}|{3:F2}|{4:P1}|{5:F2}|{6:F2}|{7}|{8:F2}s|{9}|{10}|{11}|\n",r.profile,r.seed,m.MissingHitRate,m.EligibleHitRate,m.GroupHitRatio,m.RechargeRate,m.AverageEnemyCount,m.refills,m.AverageRefillInterval,m.breaches,m.drains,m.deathCause);
        }
        var regular=results.Where(r=>r.profile=="Regular").ToArray();
        if(regular.Length>0) {
            double seconds=regular.Sum(r=>r.stats.missingSeconds),charge=regular.Sum(r=>r.stats.rechargeEarned);
            double rate=seconds>0?charge/seconds:0;
            md.AppendFormat(CultureInfo.InvariantCulture,"\nRegular weighted charge rate: {0:F2}/s. Reference: 12.5/s; the equivalent waiting-time tolerance of +/-10% corresponds to approximately 11.36-13.89/s. No gains or caps are automatically retuned from this controller's performance.\n",rate);
        }
        md.Append("\nDo not certify 'high density is slightly harder' from the cap alone. Compare breach pressure, drains, sample length and real human runs. If high density is clearly easier at comparable operation, try 15/s while keeping the 10/s base recovery.\n");
        Directory.CreateDirectory("Data");Directory.CreateDirectory("Docs");File.WriteAllText("Data/v05-real-physics.csv",csv.ToString());File.WriteAllText("Docs/LuckNumberV05Physics.md",md.ToString());
        Time.captureDeltaTime=oldCapture;Application.runInBackground=oldBackground;
        var callback=completed;Destroy(gameObject);SceneManager.LoadScene("Assets/Scenes/Pinball.unity");callback?.Invoke();
    }
}
