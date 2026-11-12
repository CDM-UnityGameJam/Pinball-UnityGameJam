using System.Collections.Generic;

public sealed class DifficultyTelemetry
{
    struct Refill {public double time,interval;public Refill(double t,double i){time=t;interval=i;}}
    readonly Queue<double> spawns=new Queue<double>(),kills=new Queue<double>();
    readonly Queue<Refill> refills=new Queue<Refill>();
    double lastRefill;
    public void Spawn(double now){spawns.Enqueue(now);}
    public void Kill(double now){kills.Enqueue(now);}
    public void BeginRefill(double now){lastRefill=now;}
    public void RefillCompleted(double now){refills.Enqueue(new Refill(now,now-lastRefill));lastRefill=now;}
    public void Trim(double now){while(spawns.Count>0&&spawns.Peek()<=now-30)spawns.Dequeue();while(kills.Count>0&&kills.Peek()<=now-30)kills.Dequeue();while(refills.Count>0&&refills.Peek().time<=now-30)refills.Dequeue();}
    public int Spawns(double now){Trim(now);return spawns.Count;}
    public int Kills(double now){Trim(now);return kills.Count;}
    public double? MeanRefill(double now){Trim(now);if(refills.Count==0)return null;double sum=0;foreach(var r in refills)sum+=r.interval;return sum/refills.Count;}
}
