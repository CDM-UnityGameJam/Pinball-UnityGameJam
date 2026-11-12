using System;

public static class DifficultyRuleChecks
{
    static void Check(bool ok,string message){if(!ok)throw new Exception("Difficulty: "+message);}
    static void Near(double a,double b,string message){Check(Math.Abs(a-b)<.00001,message+" ("+a+" / "+b+")");}
    public static string Run()
    {
        var stages=DifficultyRules.Defaults();
        var hpTimes=new[]{0d,30,60,90,180,300,480,900};var expectedHp=new[]{40,44,47,50,60,80,100,100};
        for(int i=0;i<hpTimes.Length;i++)Check(DifficultyRules.Evaluate(hpTimes[i],2,40,1,1,stages,14.5).spawnHealth==expectedHp[i],"spawn HP at "+hpTimes[i]);
        Check(DifficultyRules.Health(3,1.5)==5&&DifficultyRules.Health(40,.001)==1,"HP rounding and minimum");
        var badHealth=DifficultyRules.Defaults();badHealth[1].healthMultiplier=0;Check(!DifficultyRules.Valid(2,40,1,1,badHealth),"zero HP multiplier rejected");Check(!DifficultyRules.Valid(2,40,1,1,stages,0),"zero base HP rejected");
        var times=new[]{0d,60,300,480,900};var intervals=new[]{2d,2/1.125,1,.8,.8};var moves=new[]{1d,1.025,1.25,1.4,1.4};
        for(int i=0;i<times.Length;i++){
            var d=DifficultyRules.Evaluate(times[i],2,40,1,1,stages,14.5);
            Near(d.interval,intervals[i],"interval at "+times[i]);Near(d.moveMultiplier,moves[i],"move multiplier at "+times[i]);Near(d.speed,14.5/40*moves[i],"speed");Near(d.traversalSeconds,40/moves[i],"fixed traversal");
        }
        var lateStart=new[]{new DifficultyStage(10,2,3),new DifficultyStage(20,4,5)};
        Near(DifficultyRules.Evaluate(0,2,40,1,1,lateStart,14.5).moveMultiplier,3,"before first row");
        Check(DifficultyRules.Errors(2,40,1,1,new DifficultyStage[0]).Count>0,"empty stages rejected");
        var bad=new[]{new DifficultyStage(0,1,1),new DifficultyStage(0,1,1)};
        Check(DifficultyRules.Errors(2,40,1,1,bad).Count>0,"duplicate time rejected");
        bad[1]=new DifficultyStage(-1,float.NaN,0);Check(DifficultyRules.Errors(double.NaN,0,-1,double.PositiveInfinity,bad).Count>=7,"specific invalid field errors");
        Near(DifficultyRules.Evaluate(300,0,40,1,1,bad,14.5).interval,1,"invalid config uses safe defaults");
        Check(!DifficultyRules.Valid(2,40,1,1,null),"null list rejected");
        var clock=new DifficultyClock();Near(clock.Time(60),60,"automatic");clock.Specify(300);Near(clock.Time(1),300,"specified independent of combat");clock.Freeze(900);Near(clock.Time(1000),300,"freeze specified time");clock.Automatic();Near(clock.Time(1000),1000,"restore immediately");clock.Freeze(120);Near(clock.Time(200),120,"freeze automatic time");
        clock.SetMultipliers(3,2);var overridden=DifficultyRules.Evaluate(0,2,40,7,8,stages,14.5,clock.SpawnOverride,clock.MoveOverride);Near(overridden.interval,2d/3,"temporary override replaces config total");Near(overridden.moveMultiplier,2,"move override replaces total");
        clock.Reset();Near(clock.Time(0),0,"new run automatic");Near(clock.SpawnOverride,0,"clear temporary override");
        var p=new SpawnProgress();int attempts=0;p.Advance(1,2,()=>attempts++);Near(p.Value,.5,"half complete");p.Advance(.25,1,()=>attempts++);Near(p.Value,.75,"speed change preserves fraction");p.Advance(.25,1,()=>attempts++);Check(attempts==1,"one attempt");Near(p.Value,0,"failed attempt consumes progress");p.Advance(5,2,()=>attempts++);Check(attempts==3,"multiple elapsed attempts");Near(p.Value,.5,"fraction remainder");p.Advance(0,1,()=>attempts++);Near(p.Value,.5,"no combat time no progress");
        var stats=new DifficultyTelemetry();Check(!stats.MeanRefill(0).HasValue,"no sample placeholder");stats.BeginRefill(1);stats.RefillCompleted(13);stats.RefillCompleted(21);Near(stats.MeanRefill(21).Value,10,"completed intervals averaged");stats.Spawn(0);stats.Kill(1);Check(stats.Spawns(29)==1&&stats.Kills(29)==1,"events retained");Check(stats.Spawns(30)==0&&stats.Kills(31)==0&&!stats.MeanRefill(51).HasValue,"30-second rolling expiry");
        return "DIFFICULTY RULES PASSED: interpolation, boundaries, validation/fallback, independent debug clock, total overrides/reset, normalized progress and rolling statistics.";
    }
}
