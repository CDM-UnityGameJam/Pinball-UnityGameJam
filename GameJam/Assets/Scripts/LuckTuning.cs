using System;
using System.Collections.Generic;

public enum SkillTargetMode { Limited, AllEligible }
[Serializable]
public sealed class ActiveTier
{
    public SkillTargetMode targetMode;
    public int targetCount=1,waves=1;
    public float burnDuration,stunDuration;
    public int damage=12;
    public float burnDps=6;
}
public struct SkillPlan
{
    public bool allTargets;
    public int targetCount,waves,damage;
    public float burnDuration,stunDuration,waveInterval,burnDps,burnTickInterval,stunImmunity;
    public int BurnTicks=>LuckTuning.BurnTicks(burnDuration,burnTickInterval);
    public double BurnTickDamage=>LuckTuning.BurnDamage(burnDps,burnTickInterval);
    public double Duration(Card.Equipment e)=>e==Card.Equipment.Spark?burnDuration:Math.Max(e==Card.Equipment.Shockwave?stunDuration:0,Math.Max(0,waves-1)*(double)waveInterval);
    public double TheoreticalDamage(Card.Equipment e)=>e==Card.Equipment.Spark?BurnDamageAccumulator.WholeDamage(BurnTicks*BurnTickDamage):(double)waves*damage;
    public string Targets=>allTargets?"All eligible foes":"Up to "+targetCount+" eligible foe(s)";
    public string Summary(Card.Equipment e)=>Targets+" · "+(e==Card.Equipment.Spark?$"{burnDuration:0.###}s burn · {BurnTicks} ticks × {BurnTickDamage:0.###} accumulated":$"{waves} waves × {damage} · {Duration(e):0.###}s"+(e==Card.Equipment.Shockwave?$" · stun {stunDuration:0.###}s":""));
}

// Shared battle and preview calculations; persistent values live only in CardBalanceConfig.
[Serializable]
public class LuckTuning
{
    public ScoreRules scoring=new ScoreRules();
    public HandRules draws=new HandRules();
    public ActiveTier[] normalSkills=NewSkills(false),luckySkills=NewSkills(true);
    public static ActiveTier[] NewSkills(bool lucky)=>new[]{
        new ActiveTier{targetMode=lucky?SkillTargetMode.AllEligible:SkillTargetMode.Limited,targetCount=4,waves=lucky?3:2,damage=12},
        new ActiveTier{targetMode=lucky?SkillTargetMode.AllEligible:SkillTargetMode.Limited,targetCount=4,burnDuration=lucky?6:4,burnDps=6},
        new ActiveTier{targetMode=lucky?SkillTargetMode.AllEligible:SkillTargetMode.Limited,targetCount=4,waves=1,damage=8,stunDuration=lucky?2:1.5f}};
    public float rechargeBaseCost=100,rechargeCastCost=50,rechargePerSecond=10,rechargeHit=2.5f;
    public int collisionDamage=4;
    public int passiveArcDamage=4;
    public float passiveArcRadius=3,passiveBurnDps=4,passiveBurnDuration=3,passiveSparkRadius=2.5f,passiveStunDuration=.35f,passiveStunImmunity=2,burnTickInterval=.5f;
    public int[] arcTargetCounts={2,3,4,5,5},sparkTargetCounts={0,1,2,3,3};
    public float[] shockRadii={1.2f,1.45f,1.7f,1.95f,2.2f};
    public float[] sparkDpsMultipliers={1,2,3,4,5};
    public int activeArcDamage=12,activeShockDamage=8;
    public float activeBurnDps=6,arcWaveInterval=.25f;
    public ActiveTier[] arcTiers=DefaultTiers(Card.Equipment.Arc),sparkTiers=DefaultTiers(Card.Equipment.Spark),shockTiers=DefaultTiers(Card.Equipment.Shockwave);
    public int extraArcWaves=1;
    public float extraBurnPerCard=2,diceRevealDelay=.45f,enemyQueueGap=.1f;
    public static ActiveTier[] DefaultTiers(Card.Equipment e)
    {
        var r=new ActiveTier[4];int[] a={1,2,3,4};float[] b={1,4,6,8},st={.5f,1.5f,2,3};
        for(int i=0;i<4;i++)r[i]=new ActiveTier{targetMode=i>=2?SkillTargetMode.AllEligible:SkillTargetMode.Limited,targetCount=i==0?1:4,waves=e==Card.Equipment.Arc?a[i]:1,burnDuration=e==Card.Equipment.Spark?b[i]:0,stunDuration=e==Card.Equipment.Shockwave?st[i]:0};return r;
    }
    public static bool NonNegative(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v)&&v>=0;
    public static double BurnDamage(float dps,float interval)=>(double)dps*interval;
    public static int BurnTicks(float duration,float interval)=>interval<=0?0:(int)Math.Min(int.MaxValue,Math.Floor((double)duration/interval+.000001));
    public int ArcTargets(int n)=>n<=0?0:arcTargetCounts[Math.Min(n,5)-1];
    public int ExtraSparkTargets(int n)=>n<=0?0:sparkTargetCounts[Math.Min(n,5)-1];
    public float HeldSparkDps(int n)=>n<=0?0:(float)Math.Min(float.MaxValue,(double)passiveBurnDps*sparkDpsMultipliers[Math.Min(n,5)-1]);
    public float ShockRadius(int n)=>n<=0?0:shockRadii[Math.Min(n,5)-1];
    public ActiveTier[] Tiers(Card.Equipment e)=>e==Card.Equipment.Arc?arcTiers:e==Card.Equipment.Spark?sparkTiers:shockTiers;
    public SkillPlan Plan(Card.Equipment e,CastTier tier,int copies)
    {
        if(copies<1)return default(SkillPlan);bool current=tier==CastTier.Normal||tier==CastTier.Lucky;var row=current?(tier==CastTier.Lucky?luckySkills:normalSkills)[(int)e]:Tiers(e)[(int)tier];int extra=copies-1;
        return new SkillPlan{allTargets=row.targetMode==SkillTargetMode.AllEligible,targetCount=row.targetCount,
            waves=e==Card.Equipment.Arc?(int)Math.Min(int.MaxValue,(long)row.waves+(long)extra*extraArcWaves):1,
            damage=current?row.damage:e==Card.Equipment.Arc?activeArcDamage:activeShockDamage,burnDuration=(float)Math.Min(float.MaxValue,(double)row.burnDuration+extra*(double)extraBurnPerCard),stunDuration=row.stunDuration,
            waveInterval=e==Card.Equipment.Arc?arcWaveInterval:0,burnDps=current?row.burnDps:activeBurnDps,burnTickInterval=burnTickInterval,stunImmunity=passiveStunImmunity};
    }
    public int HeldArcDamage(int lucky)=>(int)Math.Min(int.MaxValue,Math.Floor(passiveArcDamage+(double)lucky*draws.luckyArcDamage+1e-7));
    public float HeldFireDps(int n,int lucky)=>(float)Math.Min(float.MaxValue,HeldSparkDps(n)+(double)lucky*draws.luckyBurnDps);
    public float HeldStun(int lucky)=>(float)Math.Min(float.MaxValue,passiveStunDuration+(double)lucky*draws.luckyStunDuration);
    public string HeldSummary(Card.Equipment e,int n,int lucky=0)
    {
        if(n<=0)return "Enchantment lost";
        if(e==Card.Equipment.Arc)return $"Hit foe + {ArcTargets(n)} nearby · {passiveArcRadius:0.###}m · {HeldArcDamage(lucky)} Arc damage";
        if(e==Card.Equipment.Spark)return $"Hit foe + {ExtraSparkTargets(n)} nearby · {passiveSparkRadius:0.###}m · {HeldFireDps(n,lucky):0.###} DPS · {passiveBurnDuration:0.###}s";
        return $"{ShockRadius(n):0.###}m · stun {HeldStun(lucky):0.###}s · immunity {passiveStunImmunity:0.###}s · no damage";
    }
    public bool Valid()
    {
        if(scoring==null||scoring.Errors().Count>0)return false;
        if(draws==null||draws.Errors().Count>0||!ValidSkills(normalSkills)||!ValidSkills(luckySkills))return false;
        if(!NonNegative(rechargeBaseCost)||!NonNegative(rechargeCastCost)||!NonNegative(rechargePerSecond)||!NonNegative(rechargeHit))return false;
        if(collisionDamage<0||passiveArcDamage<0||activeArcDamage<0||activeShockDamage<0||extraArcWaves<0)return false;
        if(!NonNegative(passiveArcRadius)||!NonNegative(passiveBurnDps)||!NonNegative(passiveBurnDuration)||!NonNegative(passiveSparkRadius)||!NonNegative(passiveStunDuration)||!NonNegative(passiveStunImmunity)||!NonNegative(activeBurnDps)||!NonNegative(extraBurnPerCard)||!NonNegative(diceRevealDelay)||!NonNegative(enemyQueueGap))return false;
        if(!NonNegative(burnTickInterval)||burnTickInterval<=0||!NonNegative(arcWaveInterval)||arcWaveInterval<=0)return false;
        if(sparkDpsMultipliers==null||sparkDpsMultipliers.Length!=5)return false;
        for(int i=0;i<5;i++)if(!NonNegative(sparkDpsMultipliers[i]))return false;
        if(arcTargetCounts==null||arcTargetCounts.Length!=5||sparkTargetCounts==null||sparkTargetCounts.Length!=5||shockRadii==null||shockRadii.Length!=5)return false;
        for(int i=0;i<5;i++)if(arcTargetCounts[i]<0||sparkTargetCounts[i]<0||!NonNegative(shockRadii[i]))return false;
        return ValidTiers(arcTiers)&&ValidTiers(sparkTiers)&&ValidTiers(shockTiers);
    }
    static bool ValidTiers(ActiveTier[] tiers){if(tiers==null||tiers.Length!=4)return false;foreach(var t in tiers)if(t==null||(int)t.targetMode<0||(int)t.targetMode>1||t.targetCount<1||t.waves<1||!NonNegative(t.burnDuration)||!NonNegative(t.stunDuration))return false;return true;}
    static bool ValidSkills(ActiveTier[] rows){if(rows==null||rows.Length!=3)return false;foreach(var t in rows)if(t==null||!Enum.IsDefined(typeof(SkillTargetMode),t.targetMode)||t.targetCount<1||t.waves<1||t.damage<0||!NonNegative(t.burnDps)||!NonNegative(t.burnDuration)||!NonNegative(t.stunDuration))return false;return rows[2].waves==1;}
    public List<string> Errors()
    {
        var r=new List<string>();if(draws==null)r.Add("Draw settings missing.");else r.AddRange(draws.Errors());if(!ValidSkills(normalSkills))r.Add("Normal skills: three valid element rows required; Shockwave must have one wave.");if(!ValidSkills(luckySkills))r.Add("Lucky skills: three valid element rows required; Shockwave must have one wave.");if(scoring==null)r.Add("Scoring settings missing.");else r.AddRange(scoring.Errors());foreach(var f in typeof(LuckTuning).GetFields()){
            if(f.FieldType==typeof(float)&&!NonNegative((float)f.GetValue(this)))r.Add(f.Name+": must be finite and non-negative.");
            if(f.FieldType==typeof(int)&&(int)f.GetValue(this)<0)r.Add(f.Name+": must not be negative.");}
        if(burnTickInterval<=0)r.Add("burnTickInterval: must be greater than zero.");if(arcWaveInterval<=0)r.Add("arcWaveInterval: must be greater than zero.");
        CheckTable(r,"arcTargetCounts",arcTargetCounts);CheckTable(r,"sparkTargetCounts",sparkTargetCounts);CheckTable(r,"shockRadii",shockRadii);CheckTable(r,"sparkDpsMultipliers",sparkDpsMultipliers);
        CheckTiers(r,"Arc",arcTiers);CheckTiers(r,"Spark",sparkTiers);CheckTiers(r,"Shockwave",shockTiers);return r;
    }
    static void CheckTable(List<string> e,string n,Array a){if(a==null||a.Length!=5){e.Add(n+": exactly five rows (held 1–5) required.");return;}for(int i=0;i<5;i++)if(!NonNegative(Convert.ToDouble(a.GetValue(i))))e.Add(n+" row "+(i+1)+": must be finite and non-negative.");}
    static void CheckTiers(List<string> e,string n,ActiveTier[] a){if(a==null||a.Length!=4){e.Add(n+": four tiers required.");return;}for(int i=0;i<4;i++){var t=a[i];string s=n+" / "+DiceRules.Label((CastTier)i)+": ";if(t==null){e.Add(s+"missing row.");continue;}if(!Enum.IsDefined(typeof(SkillTargetMode),t.targetMode))e.Add(s+"invalid target mode.");if(t.targetCount<1)e.Add(s+"target count must be at least 1.");if(t.waves<1)e.Add(s+"waves must be at least 1.");if(!NonNegative(t.burnDuration)||!NonNegative(t.stunDuration))e.Add(s+"durations must be finite and non-negative.");}}
}
