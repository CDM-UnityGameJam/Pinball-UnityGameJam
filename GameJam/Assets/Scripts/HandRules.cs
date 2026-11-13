using System;
using System.Collections.Generic;

[Serializable]
public sealed class HandRules
{
    public int handSize=5;
    public int luckyArcDamage=2;
    public float luckyProbability=.2f,luckyBurnDps=1,luckyStunDuration=.25f;
    public float[] numberWeights={1,1,1},elementWeights={1,1,1},refillCosts={100,150,200,250,300};
    public double LuckyChance(int draws)=>1-Math.Pow(1-luckyProbability,Math.Max(0,draws));
    public double RefillCost(int missing)=>missing<=0?0:refillCosts[Math.Min(5,missing)-1];
    public static int Weighted(float[] weights,double roll)
    {
        double total=0;foreach(float w in weights)total+=w;double sample=roll*total;
        for(int i=0;i<weights.Length;i++){sample-=weights[i];if(sample<0)return i;}
        for(int i=weights.Length-1;i>=0;i--)if(weights[i]>0)return i;return 0;
    }
    // Reservoir sampling treats each successful card as one candidate, including duplicate numbers.
    public int PickLucky(IList<int> numbers,Func<double> random)
    {
        int selected=0,successes=0;
        foreach(int n in numbers){double roll=random();if(luckyProbability==1||roll<luckyProbability){successes++;if(successes==1||random()<1.0/successes)selected=n;}}
        return selected;
    }
    public List<string> Errors()
    {
        var e=new List<string>();if(handSize<1||handSize>5)e.Add("Hand size must be 1–5.");
        if(!LuckTuning.NonNegative(luckyProbability)||luckyProbability>1)e.Add("Lucky candidate probability must be 0–1.");
        foreach(float v in new[]{luckyArcDamage,luckyBurnDps,luckyStunDuration})if(!LuckTuning.NonNegative(v))e.Add("Lucky held bonuses must be finite and non-negative.");
        Check(e,numberWeights,3,"Number weights",true);Check(e,elementWeights,3,"Element weights",true);Check(e,refillCosts,5,"Refill costs",false);return e;
    }
    static void Check(List<string> e,float[] a,int count,string label,bool positiveSum)
    {if(a==null||a.Length!=count){e.Add(label+": requires "+count+" entries.");return;}double sum=0;foreach(float v in a){if(!LuckTuning.NonNegative(v))e.Add(label+": entries must be finite and non-negative.");sum+=v;}if(positiveSum&&sum<=0)e.Add(label+": at least one weight must be positive.");}
}
