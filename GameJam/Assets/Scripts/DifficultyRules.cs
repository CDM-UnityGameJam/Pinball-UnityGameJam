using System;
using System.Collections.Generic;

[Serializable]
public struct DifficultyStage
{
    public float time, spawnMultiplier, moveMultiplier,healthMultiplier;
    public DifficultyStage(float t,float s,float m,float h=1){time=t;spawnMultiplier=s;moveMultiplier=m;healthMultiplier=h;}
}

public struct DifficultySample
{
    public double spawnMultiplier, moveMultiplier,healthMultiplier, interval, speed, traversalSeconds;
    public int spawnHealth;
}

public static class DifficultyRules
{
    static readonly DifficultyStage[] fallback=Defaults();
    public static bool Valid(double interval,double descent,double spawn,double move,IList<DifficultyStage> stages,int baseHealth=40)
    {
        if(baseHealth<1||!Positive(interval)||!Positive(descent)||!Positive(spawn)||!Positive(move)||stages==null||stages.Count==0)return false;
        for(int i=0;i<stages.Count;i++){var s=stages[i];if(!Finite(s.time)||s.time<0||!Positive(s.spawnMultiplier)||!Positive(s.moveMultiplier)||!Positive(s.healthMultiplier)||(i>0&&s.time<=stages[i-1].time))return false;}
        return true;
    }
    public static DifficultyStage[] Defaults(){return new[]{new DifficultyStage(0,1,1,1),new DifficultyStage(30,1,1,1.1f),new DifficultyStage(90,1.25f,1.05f,1.25f),new DifficultyStage(180,1.6f,1.15f,1.5f),new DifficultyStage(300,2,1.25f,2),new DifficultyStage(480,2.5f,1.4f,2.5f)};}
    public static bool Finite(double v){return !double.IsNaN(v)&&!double.IsInfinity(v);}
    public static bool Positive(double v){return Finite(v)&&v>0;}
    public static List<string> Errors(double interval,double descent,double spawn,double move,IList<DifficultyStage> stages,int baseHealth=40)
    {
        var errors=new List<string>();
        if(baseHealth<1)errors.Add("Base enemy health must be a positive whole number.");
        if(!Positive(interval))errors.Add("Base spawn interval must be a finite positive number.");
        if(!Positive(descent))errors.Add("Base descent time must be a finite positive number.");
        if(!Positive(spawn))errors.Add("Spawn speed multiplier must be a finite positive number.");
        if(!Positive(move))errors.Add("Move speed multiplier must be a finite positive number.");
        if(stages==null||stages.Count==0){errors.Add("At least one difficulty stage is required.");return errors;}
        for(int i=0;i<stages.Count;i++){
            var s=stages[i];string row="Stage "+(i+1)+": ";
            if(!Finite(s.time)||s.time<0)errors.Add(row+"time must be finite and non-negative.");
            if(i>0&&s.time<=stages[i-1].time)errors.Add(row+"time must be strictly greater than the previous row.");
            if(!Positive(s.spawnMultiplier))errors.Add(row+"spawn multiplier must be finite and positive.");
            if(!Positive(s.moveMultiplier))errors.Add(row+"move multiplier must be finite and positive.");
            if(!Positive(s.healthMultiplier))errors.Add(row+"health multiplier must be finite and positive.");
        }
        return errors;
    }
    public static int Health(int baseHealth,double multiplier)=>(int)Math.Max(1,Math.Min(int.MaxValue,Math.Floor(Math.Max(1,baseHealth)*(Positive(multiplier)?multiplier:1)+.5)));
    public static DifficultySample Evaluate(double time,double interval,double descent,double spawn,double move,IList<DifficultyStage> stages,double distance,double temporarySpawn=0,double temporaryMove=0,int baseHealth=40)
    {
        if(!Valid(interval,descent,spawn,move,stages,baseHealth)){interval=2;descent=40;spawn=move=1;stages=fallback;baseHealth=40;}
        time=Finite(time)?Math.Max(0,time):0;
        var a=stages[0];var b=a;double t=0;
        for(int i=1;i<stages.Count;i++){
            b=stages[i];
            if(time<=b.time){t=Math.Max(0,Math.Min(1,(time-a.time)/(b.time-a.time)));break;}
            a=b;
        }
        if(time>=stages[stages.Count-1].time){a=b=stages[stages.Count-1];t=0;}
        double sm=(a.spawnMultiplier+(b.spawnMultiplier-a.spawnMultiplier)*t)*(Positive(temporarySpawn)?temporarySpawn:spawn);
        double mm=(a.moveMultiplier+(b.moveMultiplier-a.moveMultiplier)*t)*(Positive(temporaryMove)?temporaryMove:move);
        double hm=a.healthMultiplier+(b.healthMultiplier-a.healthMultiplier)*t;
        // Keep calculated values representable in Unity float positions, even for extreme valid inputs.
        sm=Math.Max(1e-6,Math.Min(1e6,sm));mm=Math.Max(1e-6,Math.Min(1e6,mm));
        double duration=Math.Max(1e-30,Math.Min(1e30,descent/mm));
        return new DifficultySample{spawnMultiplier=sm,moveMultiplier=mm,healthMultiplier=hm,spawnHealth=Health(baseHealth,hm),interval=Math.Max(1e-6,Math.Min(1e30,interval/sm)),speed=Math.Min(1e30,Math.Max(0,Finite(distance)?Math.Abs(distance):0)/duration),traversalSeconds=duration};
    }
}

public enum DifficultyTimeMode { Automatic, Specified, Frozen }
public sealed class DifficultyClock
{
    public DifficultyTimeMode Mode {get;private set;}
    double fixedTime;
    public double SpawnOverride {get;private set;}
    public double MoveOverride {get;private set;}
    public double Time(double combatTime){return Mode==DifficultyTimeMode.Automatic?combatTime:fixedTime;}
    public void Specify(double time){if(!DifficultyRules.Finite(time)||time<0)return;fixedTime=time;Mode=DifficultyTimeMode.Specified;}
    public void Freeze(double combatTime){fixedTime=Time(combatTime);Mode=DifficultyTimeMode.Frozen;}
    public void Automatic(){Mode=DifficultyTimeMode.Automatic;}
    public void SetMultipliers(double spawn,double move){if(DifficultyRules.Positive(spawn))SpawnOverride=spawn;if(DifficultyRules.Positive(move))MoveOverride=move;}
    public void ClearMultipliers(){SpawnOverride=MoveOverride=0;}
    public void Reset(){Mode=DifficultyTimeMode.Automatic;fixedTime=0;ClearMultipliers();}
}

// A failed/blocked attempt consumes progress just like a successful attempt.
public sealed class SpawnProgress
{
    public double Value {get;private set;}
    public int Advance(double elapsed,double interval,Action attempt)
    {
        if(!DifficultyRules.Positive(elapsed)||!DifficultyRules.Positive(interval))return 0;
        Value+=elapsed/interval;int count=0;
        while(Value>=1-1e-10){Value=Math.Max(0,Value-1);count++;attempt();}
        return count;
    }
}
