using System;
using UnityEngine;

// Each field owns one gameplay meaning. Equal default values are deliberately independent.
[Serializable]
public class EnemyTuning
{
    [Min(1)] public int health = 40;
    [Min(0.01f)] public float radius = 0.75f;
    [Min(0.01f)] public float halfHeight = 0.25f;
    public float centerHeight = 0.25f;
    public Vector3 capPosition = new Vector3(0f, 1.01f, 0f);
    public Vector3 capScale = new Vector3(0.8f, 0.02f, 0.8f);
    [Min(0f)] public float hitCooldown = 0.2f;
    [Min(0f)] public float revealDuration = 20f;
    [Min(0.01f)] public float lifetime = 30f;
    [Min(0f)] public float flashDuration = 0.12f;
    [Min(0)] public int hitEnergy = 25;
    [Min(0)] public int hitDamage = 1;
    [Min(0)] public int pulseDamage = 1;
    [Min(0f)] public float kickSpeed = 20f;
    [Min(0f)] public float unstickSpeed = 2f;
    [Min(0f)] public float unstickAcceleration = 3f;
    public float unstickForwardBias = 0.35f;
    public float unstickSideBias = 0.15f;
    [Min(0f)] public float horizontalGap = 0.45f;
    [Min(0f)] public float verticalGap = 0.35f;
    [Min(0f)] public float ballClearance = 0.45f;
    [Min(0f)] public float wallClearance = 0.3f;
    public Color ringColor = new Color32(201, 71, 90, 255);
    public Color fillColor = new Color32(90, 38, 50, 255);
    public Color matchColor = new Color32(71, 214, 163, 255);
    public Color previewColor = new Color32(91, 141, 240, 255);
    public Color flashColor = Color.white;
    public Color hitPopupColor = Color.white;
    public Color killPopupColor = new Color32(71, 214, 163, 255);
}

[Serializable]
public class SpecialTargetTuning
{
    public int target;
    [Min(0f)] public float weight;
    [Min(1)] public int scoreMultiplier;
    public SpecialTargetTuning(int target, float weight, int multiplier)
    { this.target = target; this.weight = weight; scoreMultiplier = multiplier; }
}

[Serializable]
public class SpawnTuning
{
    [Range(0f, 100f)] public float normalChancePercent = 96f;
    [Range(0f, 100f)] public float bumperChancePercent = 4f;
    public float normalSpawnZ = 9.5f;
    public float bumperSpawnZ = 9.5f;
    public float dangerZ = -5f;
    [Min(0)] public int openingNormalCount = 3;
    [Min(0f)] public float openingColumnSpacing = 2f;
    [Min(1)] public int normalAttempts = 30;
    [Min(1)] public int bumperAttempts = 30;
    [Min(1)] public int specialAttempts = 30;
    [Min(0f)] public float specialStartDelay = 0f;
    [Range(0f, 100f)] public float specialInitialChancePercent = 5f;
    [Range(0f, 100f)] public float specialChanceStepPercent = 2f;
    [Min(0.01f)] public float specialChanceStepSeconds = 60f;
    [Range(0f, 100f)] public float specialMaxChancePercent = 15f;
    public float specialMinZ = -3.5f;
    public float specialMaxZ = 8.5f;
    [Min(0f)] public float specialPairExtraGap = 0.6f;
    [Min(0f)] public float specialPairVerticalExtraGap = 0.6f;
    [Min(0.01f)] public float boardHalfWidth = 5f;
    public float arcCenterZ = 7f;
    [Min(0.01f)] public float arcRadius = 5f;
    [Min(0f)] public float arcHorizontalRadius; // Zero keeps the original circular boundary.
    [Min(0f)] public float ballRadius = 0.25f;
    public Vector3 dangerLineScale = new Vector3(10f, 0.02f, 0.08f);
    public Vector2 dangerLineOffset = new Vector2(0f, 0.01f);
    public Color dangerLineColor = new Color32(224, 80, 94, 255);
    public int[] normalTargets = { 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 74, 148, 185, 259, 296 };
    public SpecialTargetTuning[] specialTargets = {
        new SpecialTargetTuning(111, 10f, 10),
        new SpecialTargetTuning(222, 45f, 2),
        new SpecialTargetTuning(333, 45f, 3)
    };

    public float SpecialChance(float activeTime) => activeTime < specialStartDelay ? 0f :
        Mathf.Clamp01(Mathf.Min(specialInitialChancePercent + specialChanceStepPercent *
            Mathf.Floor((activeTime - specialStartDelay) / Mathf.Max(0.01f, specialChanceStepSeconds)), specialMaxChancePercent) / 100f);

    public SpecialTargetTuning PickSpecial(float roll)
    {
        if (specialTargets == null) return null;
        float total = 0f;
        foreach (var entry in specialTargets) if (entry != null) total += Mathf.Max(0f, entry.weight);
        if (total <= 0f) return null;
        float remaining = Mathf.Clamp01(roll) * total;
        SpecialTargetTuning last = null;
        foreach (var entry in specialTargets)
        {
            if (entry == null || entry.weight <= 0f) continue;
            last = entry;
            remaining -= entry.weight;
            if (remaining < 0f) return entry;
        }
        return last;
    }
}

[Serializable]
public class CardTuning
{
    public int baseNumber = 37;
    public int initialNumber = 37;
    [Min(1)] public int operandMin = 1;
    [Min(1)] public int operandMax = 9;
    [Range(0f, 1f)] public float matchWinsTieChance = 0.5f;
}

[Serializable]
public class OutcomeWeights
{
    [UnityEngine.Serialization.FormerlySerializedAs("healing"), Min(0)] public float backlash;
    [Min(0)] public float low, medium, high;
    public OutcomeWeights(float healing, float low, float medium, float high)
    { this.backlash = healing; this.low = low; this.medium = medium; this.high = high; }
    public float Weight(int band) => Mathf.Max(0, band == 0 ? backlash : band == 1 ? low : band == 2 ? medium : high);
    public float Total => Weight(0) + Weight(1) + Weight(2) + Weight(3);
    public float Chance(int band) => Total > 0 ? Weight(band) / Total : (band == 1 ? 1f : 0f);
}

[Serializable]
public class StrengthBand
{
    public int targetsMin, targetsMax, arcWavesMin, arcWavesMax, shockWavesMin, shockWavesMax;
    public float burnMin, burnMax, stunMin, stunMax;
    public StrengthBand(int t0, int t1, int a0, int a1, float b0, float b1, int s0, int s1, float u0, float u1)
    { targetsMin=t0; targetsMax=t1; arcWavesMin=a0; arcWavesMax=a1; burnMin=b0; burnMax=b1; shockWavesMin=s0; shockWavesMax=s1; stunMin=u0; stunMax=u1; }
}

public struct SkillPlan
{
    public bool allTargets;
    public int targetCount, waves;
    public float burnDuration, stunDuration, waveInterval;
    public string Summary(Card.Equipment type) => type == Card.Equipment.Spark ? $"Burn {burnDuration:0.#}s" : $"{waves} waves" + (type == Card.Equipment.Shockwave ? $" · Stun {stunDuration:0.#}s" : "");
}

[Serializable]
public class LuckTuning
{
    [Min(0.01f)] public float refillInterval = 8f;
    [Min(0f)] public float playRefillDelay = 4f;
    [Min(0.01f)] public float luckyRefreshInterval = 10f;
    [Min(0)] public int collisionDamage = 4;
    [Header("Ball enchantments")]
    [Min(0)] public int passiveArcDamage = 4;
    [Min(0)] public float passiveArcRadius = 3f, passiveSparkRadius = 2.5f;
    [Min(0)] public int passiveBurnDps = 4;
    [Min(0)] public float passiveBurnDuration = 3f;
    [Min(0)] public float passiveStunDuration = 0.35f, shockRadius = 1.2f, shockRadiusPerCard = 0.25f;
    [Min(0)] public float passiveStunImmunity = 2f;
    [Min(0)] public float enemyQueueGap = 0.1f;
    [Min(0.01f)] public float burnTickInterval = 0.5f;
    [Header("Active skills")]
    [Min(0)] public int activeArcDamage = 12, activeBurnDps = 6, activeShockDamage = 8;
    [Min(0.01f)] public float arcWaveInterval = 0.25f, shockWaveInterval = 0.35f;
    [Min(0)] public float shortCircuitMin = 2f, shortCircuitMax = 4f;
    public StrengthBand low = new StrengthBand(2,3,1,1,1,2,1,1,0.4f,0.8f);
    public StrengthBand medium = new StrengthBand(4,8,1,2,3,5,1,2,1,1.8f);
    public StrengthBand high = new StrengthBand(0,0,3,4,6,8,2,3,2,3);
    public OutcomeWeights normalWeights = new OutcomeWeights(25,45,25,5);
    public OutcomeWeights luckyWeights = new OutcomeWeights(10,25,40,25);
    public int ArcTargets(int count) => count <= 0 ? 0 : Mathf.Min(count + 1, 5);
    public int ExtraSparkTargets(int count) => Mathf.Clamp(count - 1, 0, 3);
    public float ShockRadius(int count) => count <= 0 ? 0 : shockRadius + shockRadiusPerCard * (count - 1);
    public float ShortCircuitDuration(int q) => Mathf.Lerp(shortCircuitMin, shortCircuitMax, Mathf.InverseLerp(1,10,Mathf.Abs(q)));
    static int Round(float value) => Mathf.FloorToInt(value + 0.5f);
    static float Snap(float value, float interval) => Round(value / interval) * interval;
    public SkillPlan Plan(Card.Equipment type, int q)
    {
        if(q <= 0) return default;
        bool all = q >= 60;
        var band = all ? high : q >= 20 ? medium : low;
        float t = Mathf.InverseLerp(all ? 60 : q >= 20 ? 20 : 1, all ? 100 : q >= 20 ? 59 : 19, q);
        return new SkillPlan {
            allTargets=all, targetCount=Round(Mathf.Lerp(band.targetsMin,band.targetsMax,t)),
            waves=type==Card.Equipment.Spark ? 1 : Mathf.Max(1, Round(type==Card.Equipment.Arc ? Mathf.Lerp(band.arcWavesMin,band.arcWavesMax,t) : Mathf.Lerp(band.shockWavesMin,band.shockWavesMax,t))),
            burnDuration=Snap(Mathf.Lerp(band.burnMin,band.burnMax,t),0.5f),
            stunDuration=Snap(Mathf.Lerp(band.stunMin,band.stunMax,t),0.1f),
            waveInterval=type==Card.Equipment.Arc ? arcWaveInterval : shockWaveInterval
        };
    }
    public OutcomeWeights Weights(bool lucky) => lucky ? luckyWeights : normalWeights;
    public static int MinResult(int band) => band == 0 ? -10 : band == 1 ? 0 : band == 2 ? 20 : 60;
    public static int MaxResult(int band) => band == 0 ? -1 : band == 1 ? 19 : band == 2 ? 59 : 100;
    public static int SelectBand(OutcomeWeights weights, float sample)
    {
        if (weights.Total <= 0) return 1; // Invalid authoring falls back to the low band.
        float remaining = Mathf.Clamp01(sample) * weights.Total;
        int last = 1;
        for (int band = 0; band < 4; band++) {
            float w = weights.Weight(band);
            if (w <= 0) continue;
            last = band;
            if (remaining < w) return band;
            remaining -= w;
        }
        return last;
    }
    public int Roll(bool lucky)
    {
        int band = SelectBand(Weights(lucky), UnityEngine.Random.value);
        return UnityEngine.Random.Range(MinResult(band), MaxResult(band) + 1);
    }
}
