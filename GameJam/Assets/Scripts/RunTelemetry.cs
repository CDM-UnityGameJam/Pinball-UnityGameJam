using System;

[Serializable]
public class RunTelemetry
{
    public string policy = "Manual play";
    public int plays, validBallHits, kills, drains, breaches, lowQuality, mediumQuality, highQuality, shortCircuits, misfires;
    public float elapsed, handTimeIntegral, lowEquipmentSeconds;
    public int missingPhysicalHits, matchingMissingHits, eligibleRechargeHits, matchingEligibleHits, refills;
    public double rechargeEarned, naturalCharge, hitCharge, chargeDiscarded, missingSeconds, enemyCountIntegral, refillIntervalTotal;
    double lastRefillAt;
    public double RechargeRate => missingSeconds>0 ? rechargeEarned/missingSeconds : 0;
    public double MissingHitRate => missingSeconds>0 ? missingPhysicalHits/missingSeconds : 0;
    public double EligibleHitRate => missingSeconds>0 ? eligibleRechargeHits/missingSeconds : 0;
    public double MatchRatio => eligibleRechargeHits>0 ? matchingEligibleHits/(double)eligibleRechargeHits : 0;
    public double AverageEnemyCount => missingSeconds>0 ? enemyCountIntegral/missingSeconds : 0;
    public double AverageRefillInterval => refills>0 ? refillIntervalTotal/refills : 0;
    public void TickRecharge(float dt,int enemies) { missingSeconds+=dt;enemyCountIntegral+=dt*enemies; }
    public void RecordRechargeHit(bool match,bool eligible) {
        missingPhysicalHits++;if(match)matchingMissingHits++;
        if(eligible) {eligibleRechargeHits++;if(match)matchingEligibleHits++;}
    }
    public void RecordRecharge(double requested,double actual,bool natural) {
        rechargeEarned+=actual;chargeDiscarded+=Math.Max(0,requested-actual);
        if(natural)naturalCharge+=actual;else hitCharge+=actual;
    }
    public void StartRechargeInterval(double now) { lastRefillAt=now; }
    public void RecordRefill(double now) { refills++;refillIntervalTotal+=now-lastRefillAt;lastRefillAt=now; }
    public string RechargeSummary() => $"Missing-time={missingSeconds:0.0}s, Physical hits/s={MissingHitRate:0.00}, Eligible hits/s={EligibleHitRate:0.00}, Match={MatchRatio:P1}, Charge/s={RechargeRate:0.00}, Mean enemies={AverageEnemyCount:0.00}, Refills={refills}, Mean refill interval={AverageRefillInterval:0.00}s";
    public string deathCause = "Alive";
    public float AverageHand => elapsed>0 ? handTimeIntegral/elapsed : 5;
    public void Tick(float delta,int cards) {
        elapsed+=delta; handTimeIntegral+=delta*cards;
        if(cards<=1) lowEquipmentSeconds+=delta;
    }
    public string Summary() => $"Policy={policy}, Time={elapsed:0.0}s, Plays={plays}, Average hand={AverageHand:0.00}, Low gear (0-1)={lowEquipmentSeconds:0.0}s, Kills={kills}, Death cause={deathCause}, Breaches={breaches}, Drains={drains}, {RechargeSummary()}";
}
