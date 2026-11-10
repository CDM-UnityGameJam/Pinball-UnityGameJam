using System;

[Serializable]
public class RunTelemetry
{
    public string policy = "Manual play";
    public int plays, validBallHits, kills, drains, breaches, lowQuality, mediumQuality, highQuality, shortCircuits, misfires;
    public float elapsed, handTimeIntegral, lowEquipmentSeconds;
    public string deathCause = "Alive";
    public float AverageHand => elapsed>0 ? handTimeIntegral/elapsed : 5;
    public void Tick(float delta,int cards) {
        elapsed+=delta; handTimeIntegral+=delta*cards;
        if(cards<=1) lowEquipmentSeconds+=delta;
    }
    public string Summary() => $"Policy={policy}, Time={elapsed:0.0}s, Plays={plays}, Average hand={AverageHand:0.00}, Low gear (0-1)={lowEquipmentSeconds:0.0}s, Kills={kills}, Death cause={deathCause}, Breaches={breaches}, Drains={drains}";
}
