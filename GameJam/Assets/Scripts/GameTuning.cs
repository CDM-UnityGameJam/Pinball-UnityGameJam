using System;
using UnityEngine;

// Each field owns one gameplay meaning. Equal default values are deliberately independent.
[Serializable]
public class EnemyTuning
{
    [Min(1)] public int health = 5;
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
