using System;

[Serializable]
public class RunTelemetry
{
    public string policy = "Manual play";
    public int batches,baseBatches,standardBatches,fullFieldBatches,jackpotBatches,coveredTargets,batchKills,activatedCards;
    public int[] groupSizes=new int[6];
    public int normalBatches,luckyBatches;
    public void RecordBatch(int count){batches++;plays+=count;groupSizes[count]++;}
    public void RecordReveal(DiceBatch batch){
        if(batch.Tier==CastTier.Normal)normalBatches++;else if(batch.Tier==CastTier.Lucky)luckyBatches++;else if(batch.Tier==CastTier.Base)baseBatches++;else if(batch.Tier==CastTier.Standard)standardBatches++;else if(batch.Tier==CastTier.FullField)fullFieldBatches++;else jackpotBatches++;
        coveredTargets+=batch.CoverageCount;activatedCards+=batch.Cards.Count;skillReleases+=batch.Skills.Count;diceMatches+=batch.Matches;
    }
    public int skillReleases,diceMatches,physicalDamageHits,cooldownContacts,collisionChargeEvents,burnChargeEvents;
    public double collisionCharge,burnCharge;
    public int plays, validBallHits, kills, drains, breaches, lowQuality, mediumQuality, highQuality, shortCircuits, misfires;
    public float elapsed, handTimeIntegral, lowEquipmentSeconds;
    public int missingPhysicalHits,  eligibleRechargeHits,  refills;
    public double rechargeEarned, naturalCharge, hitCharge, chargeDiscarded, missingSeconds, enemyCountIntegral, refillIntervalTotal;
    double lastRefillAt;
    public double RechargeRate => missingSeconds>0 ? rechargeEarned/missingSeconds : 0;
    public double MissingHitRate => missingSeconds>0 ? missingPhysicalHits/missingSeconds : 0;
    public double EligibleHitRate => missingSeconds>0 ? eligibleRechargeHits/missingSeconds : 0;
    public double GroupHitRatio => standardBatches+fullFieldBatches+jackpotBatches+baseBatches>0 ? (standardBatches+fullFieldBatches+jackpotBatches)/(double)(standardBatches+fullFieldBatches+jackpotBatches+baseBatches):0;
    public double AverageEnemyCount => missingSeconds>0 ? enemyCountIntegral/missingSeconds : 0;
    public double AverageRefillInterval => refills>0 ? refillIntervalTotal/refills : 0;
    public void TickRecharge(float dt,int enemies) { missingSeconds+=dt;enemyCountIntegral+=dt*enemies; }
    public void RecordRechargeHit(bool eligible) {missingPhysicalHits++;if(eligible)eligibleRechargeHits++;}
    public void RecordRecharge(double requested,double actual,bool natural) {
        rechargeEarned+=actual;chargeDiscarded+=Math.Max(0,requested-actual);
        if(natural)naturalCharge+=actual;else hitCharge+=actual;
    }
    public void StartRechargeInterval(double now) { lastRefillAt=now; }
    public void RecordRefill(double now) { refills++;refillIntervalTotal+=now-lastRefillAt;lastRefillAt=now; }
    public string RechargeSummary() => $"Missing-time={missingSeconds:0.0}s, Physical hits/s={MissingHitRate:0.00}, Eligible hits/s={EligibleHitRate:0.00}, Charge/s={RechargeRate:0.00}, Mean enemies={AverageEnemyCount:0.00}, Refills={refills}, Mean refill interval={AverageRefillInterval:0.00}s";
    public string deathCause = "Alive";
    public float AverageHand => elapsed>0 ? handTimeIntegral/elapsed : 0;
    public void Tick(float delta,int cards) {
        elapsed+=delta; handTimeIntegral+=delta*cards;
        if(cards<=1) lowEquipmentSeconds+=delta;
    }
    public string Summary() => $"Policy={policy}, Time={elapsed:0.0}s, Batches={batches}, Skills={skillReleases}, Collision/Burn charge={collisionCharge:0.##}/{burnCharge:0.##}, Normal/Lucky batches={normalBatches}/{luckyBatches}, Cards={plays}, Groups[1-5]={groupSizes[1]}/{groupSizes[2]}/{groupSizes[3]}/{groupSizes[4]}/{groupSizes[5]}, Tiers={baseBatches}/{standardBatches}/{fullFieldBatches}/{jackpotBatches}, Activated cards={activatedCards}, Coverage={coveredTargets}, Batch kills={batchKills}, Average hand={AverageHand:0.00}, Low gear (0-1)={lowEquipmentSeconds:0.0}s, Kills={kills}, Death cause={deathCause}, Breaches={breaches}, Drains={drains}, {RechargeSummary()}";
}
