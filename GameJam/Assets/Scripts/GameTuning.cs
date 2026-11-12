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
    [Min(0f), Tooltip("A ball hitting this enemy leaves at least this fast, pushed away like a pop bumper. 0 = plain bounce.")]
    public float hitBounceSpeed = 14f;
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
    [Header("Recharge")]
    [Min(1)] public float rechargeBaseCost = 100f;
    [Min(0)] public float rechargeCastCost = 50f;
    [Min(0)] public float rechargePerSecond = 10f;
    [Min(0)] public float rechargeHit = 2.5f;
    [Min(0)] public float rechargeCombatBudget = 6f;
    [Min(0)] public float rechargeCombatMinimum = 0.25f;
    [Min(0)] public float diceRevealDelay = 0.45f;
    [Min(0)] public int collisionDamage = 4;
    [Min(0)] public int empoweredCollisionDamage = 8;
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
    [Min(1)] public int baseTargets=1,standardTargets=4;
    [Min(1)] public int baseWaves=1;
    [Min(0)] public float baseBurn=1,baseStun=0.5f;
    [Min(1)] public int standardArcWaves=2,fullArcWaves=3,jackpotArcWaves=4;
    [Min(1)] public int standardShockWaves=1,fullShockWaves=2,jackpotShockWaves=3;
    [Min(0)] public float standardBurn=4,fullBurn=6,jackpotBurn=8;
    [Min(0)] public float standardStun=1.5f,fullStun=2,jackpotStun=3;
    [Min(0)] public float extraBurnPerCard=2;
    public int ArcTargets(int count) => count <= 0 ? 0 : Mathf.Min(count + 1, 5);
    public int ExtraSparkTargets(int count) => Mathf.Clamp(count - 1, 0, 3);
    public float ShockRadius(int count) => count <= 0 ? 0 : shockRadius + shockRadiusPerCard * (count - 1);
    public SkillPlan Plan(Card.Equipment type,CastTier tier,int copies)
    {
        if(copies<1)return default;
        int arc=tier==CastTier.Base?baseWaves:tier==CastTier.Standard?standardArcWaves:tier==CastTier.FullField?fullArcWaves:jackpotArcWaves;
        int shock=tier==CastTier.Base?baseWaves:tier==CastTier.Standard?standardShockWaves:tier==CastTier.FullField?fullShockWaves:jackpotShockWaves;
        return new SkillPlan {
            allTargets=tier==CastTier.FullField || tier==CastTier.Jackpot,targetCount=tier==CastTier.Base?baseTargets:standardTargets,
            waves=type==Card.Equipment.Spark?1:(type==Card.Equipment.Arc?arc:shock)+copies-1,
            burnDuration=(tier==CastTier.Base?baseBurn:tier==CastTier.Standard?standardBurn:tier==CastTier.FullField?fullBurn:jackpotBurn)+(copies-1)*extraBurnPerCard,
            stunDuration=tier==CastTier.Base?baseStun:tier==CastTier.Standard?standardStun:tier==CastTier.FullField?fullStun:jackpotStun,
            waveInterval=type==Card.Equipment.Arc?arcWaveInterval:shockWaveInterval
        };
    }
}
