using System;

public static class CardBalanceRuleChecks
{
    static void Check(bool v,string s){if(!v)throw new Exception("Card balance: "+s);}
    static void Near(double a,double b,string s){Check(Math.Abs(a-b)<.00001,s+" ("+a+" / "+b+")");}
    public static string Run()
    {
        var t=new LuckTuning();Check(t.Valid()&&t.Errors().Count==0,"default settings valid");
        int[] arc={2,3,4,5,5},spark={0,1,2,3,3};
        for(int n=1;n<=5;n++)Near(t.HeldSparkDps(n),4*n,"linear held fire DPS "+n);
        t.sparkDpsMultipliers[2]=7.5f;Near(t.HeldSparkDps(3),30,"custom held fire multiplier");Near(t.HeldSparkDps(2),8,"independent held fire row");t.sparkDpsMultipliers[2]=3;
        Near(t.HeldSparkDps(0),0,"no held fire has no DPS");
        for(int n=1;n<=5;n++){Check(t.ArcTargets(n)==arc[n-1],"arc table "+n);Check(t.ExtraSparkTargets(n)==spark[n-1],"spark table "+n);Near(t.ShockRadius(n),1.2+.25*(n-1),"shock radius "+n);}
        int[] arcWaves={1,2,3,4};float[] burn={1,4,6,8},stun={.5f,1.5f,2,3};
        for(int tier=0;tier<4;tier++)for(int n=1;n<=5;n++){
            var a=t.Plan(Card.Equipment.Arc,(CastTier)tier,n);var f=t.Plan(Card.Equipment.Spark,(CastTier)tier,n);var s=t.Plan(Card.Equipment.Shockwave,(CastTier)tier,n);
            Check(a.waves==arcWaves[tier]+n-1&&s.waves==1,"Arc stacking / fixed single Shockwave");Near(f.burnDuration,burn[tier]+2*(n-1),"migration burn parity");Near(s.stunDuration,stun[tier],"migration stun parity");Check(a.allTargets==(tier>=2)&&f.allTargets==(tier>=2)&&s.allTargets==(tier>=2),"migration scope parity");Check(a.targetCount==(tier==0?1:4),"migration target count");
            Near(a.TheoreticalDamage(Card.Equipment.Arc),a.waves*12,"arc preview damage");Near(f.TheoreticalDamage(Card.Equipment.Spark),f.BurnTicks*3,"fire preview damage");Near(s.TheoreticalDamage(Card.Equipment.Shockwave),s.waves*8,"shock preview damage");
        }
        t.sparkTiers[0].targetCount=2;Check(t.Plan(Card.Equipment.Spark,CastTier.Base,1).targetCount==2&&t.Plan(Card.Equipment.Arc,CastTier.Base,1).targetCount==1,"independent elemental scopes");
        for(int tier=0;tier<4;tier++){t.shockTiers[tier].waves=99;for(int n=1;n<=5;n++){var single=t.Plan(Card.Equipment.Shockwave,(CastTier)tier,n);Check(single.waves==1&&single.TheoreticalDamage(Card.Equipment.Shockwave)==8,"legacy values cannot add Shockwave hits");Near(single.stunDuration,stun[tier],"Shockwave stun remains tier-based");}}
        var old=t.Plan(Card.Equipment.Arc,CastTier.Standard,2);t.activeArcDamage=31;t.arcWaveInterval=.8f;t.arcTiers[1].waves=9;Check(old.damage==12&&old.waves==3&&old.waveInterval==.25f,"submitted plan is a value snapshot");Check(t.Plan(Card.Equipment.Arc,CastTier.Standard,2).damage==31,"new plan reads live values");
        var fire=t.Plan(Card.Equipment.Spark,CastTier.Base,2);t.activeBurnDps=.1f;t.burnTickInterval=.25f;Check(fire.BurnTickDamage==3&&fire.BurnTicks==6,"submitted burn snapshot");Near(t.Plan(Card.Equipment.Spark,CastTier.Base,2).BurnTickDamage,.025,"small DPS retains fractional damage");
        var fractional=new BurnDamageAccumulator();
        for(int i=1;i<=12;i++)Check(fractional.Add(.25)==(i%4==0?1:0),"whole-point damage only on every fourth quarter tick");
        fractional.Reset();for(int i=0;i<4;i++)Check(fractional.Add(3.5)==(i%2==0?3:4),"fractional high DPS alternates whole damage");
        fractional.Reset();fractional.Add(.25);Check(fractional.Add(.75)==1,"source changes preserve continuous burn remainder");
        fractional.Add(.75);fractional.Reset();Check(fractional.Add(.25)==0,"new burn episode resets remainder");
        fractional.Reset();int total=0;for(int i=0;i<10000;i++)total+=fractional.Add(.1);Check(total==1000,"fractional precision over long burns");
        t.activeBurnDps=.5f;t.burnTickInterval=.5f;t.sparkTiers[0].burnDuration=6;Near(t.Plan(Card.Equipment.Spark,CastTier.Base,1).TheoreticalDamage(Card.Equipment.Spark),3,"preview accumulates before rounding");
        t.sparkDpsMultipliers[0]=-1;Check(!t.Valid()&&t.Errors().Count>0,"invalid held DPS multiplier rejected");
        var unlimited=new RechargeAccount();unlimited.StartMissing(1000);for(int i=0;i<100;i++)Near(unlimited.EarnCombat(0,2.5),2.5,"unlimited collision gain");Near(unlimited.Progress,250,"no rolling cap");
        t=new LuckTuning();t.rechargeHit=-1;Check(!t.Valid()&&t.Errors().Count>0,"negative collision charge invalid");t=new LuckTuning();t.burnTickInterval=0;Check(!t.Valid(),"zero burn interval invalid");t=new LuckTuning();t.arcTiers[0].waves=0;Check(!t.Valid(),"zero waves invalid");t=new LuckTuning();t.sparkTiers[0].targetCount=0;Check(!t.Valid(),"zero target count invalid");t=new LuckTuning();t.shockRadii=new float[4];Check(!t.Valid(),"incomplete held table invalid");t=new LuckTuning();t.passiveArcRadius=float.NaN;Check(!t.Valid(),"non-finite invalid");
        var account=new RechargeAccount();account.Play(true,100,50);account.EarnNatural(74);account.StartMissing(200);Near(account.Progress,74,"live base-cost change preserves charge");Near(account.Required,150,"live base-cost change preserves current requirement");account.EarnNatural(76);Check(account.Consume(200),"old requirement still completes");Near(account.Required,200,"next requirement uses new base");
        account=new RechargeAccount();account.Play(true,0,0);for(int i=0;i<5;i++)Check(account.Consume(0),"zero-cost config supported");account.FullHand();Check(!account.Consume(0),"full-hand free refill stops");
        return "CARD BALANCE RULES PASSED: 60 default plans, independent scopes, captured plans, fractional burn accumulation and preview, charge accounting and invalid-config diagnostics.";
    }
}

