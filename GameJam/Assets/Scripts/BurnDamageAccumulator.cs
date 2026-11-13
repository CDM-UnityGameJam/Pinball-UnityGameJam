using System;

// One accumulator per enemy's continuous burn, shared by passive/active burn sources.
public sealed class BurnDamageAccumulator
{
    public double Remainder {get;private set;}
    public void Reset(){Remainder=0;}
    public static int WholeDamage(double amount)=>(int)Math.Max(0,Math.Min(int.MaxValue,Math.Floor(amount+1e-7)));
    public int Add(double amount)
    {
        if(double.IsNaN(amount)||double.IsInfinity(amount)||amount<=0)return 0;
        double total=Remainder+amount;int damage=WholeDamage(total);
        Remainder=Math.Max(0,total-damage);return damage;
    }
}
