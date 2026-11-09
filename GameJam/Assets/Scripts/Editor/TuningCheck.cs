using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class TuningCheck
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Tuning check failed: " + message);
    }

    [MenuItem("LuckyPinball/Check Configurable Rules")]
    public static void RunRules()
    {
        var spawn = new SpawnTuning {
            specialStartDelay = 12f, specialInitialChancePercent = 20f,
            specialChanceStepSeconds = 10f, specialChanceStepPercent = 15f,
            specialMaxChancePercent = 45f,
            specialTargets = new[] { new SpecialTargetTuning(90, 0f, 8), new SpecialTargetTuning(120, 1f, 4) }
        };
        Check(spawn.SpecialChance(11f) == 0f, "special opening delay");
        Check(Mathf.Approximately(spawn.SpecialChance(12f), 0.2f), "custom initial probability");
        Check(Mathf.Approximately(spawn.SpecialChance(22f), 0.35f), "custom probability step");
        Check(Mathf.Approximately(spawn.SpecialChance(100f), 0.45f), "custom probability cap");
        Check(spawn.PickSpecial(0f).target == 120 && spawn.PickSpecial(1f).scoreMultiplier == 4, "zero weights and custom target/reward");
        spawn.specialTargets[1].weight = 0f;
        Check(spawn.PickSpecial(0.5f) == null, "all zero weights disable special spawning");

        var cards = new CardTuning { baseNumber = 40, operandMin = 2, operandMax = 4, matchWinsTieChance = 0f };
        Check(Card.For(120, cards).Apply(cards.baseNumber) == 120, "custom card base");
        Check(!Card.TryFor(41, cards, out _), "custom minimum operand");
        Check(Card.Draw(40, new Card?[2], new List<int> { 41, 120 }, cards).Apply(40) == 120, "skip targets unreachable by configured cards");
        Check(Card.RollFunctional(false, 0f, 0f, 0f, 1f, 1f, cards).Value.kind == Card.Kind.ExtraBall, "custom functional tie rule");
        for (int i = 0; i < 200; i++)
        {
            var card = Card.Random(cards);
            Check(card.n >= 2 && card.n <= 4, "custom random operand range");
        }
        Check(GameManager.SpecialKillReward(100, 1, true, 20) == 0, "multiplier one is valid");
        Debug.Log("Configurable rules passed");
    }

    // Disposable Play Mode verification: never saves the modified runtime configuration.
    [MenuItem("LuckyPinball/Check Independent Tuning (Play Mode)")]
    public static void RunGameplay()
    {
        Check(Application.isPlaying, "enter Play Mode first");
        var gm = GameManager.Instance;
        Check(gm.HandSize == gm.handSize, "hand size was applied before HUD construction");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var gmType = typeof(GameManager);
        var state = gmType.GetField("state", flags);
        state.SetValue(gm, Enum.Parse(state.FieldType, "Playing"));
        gm.SetPaused(false);
        gm.normalEnemy.health = 9;
        gm.normalEnemy.radius = 0.6f;
        gm.normalEnemy.hitDamage = 3;
        gm.specialEnemy.health = 7;
        gm.specialEnemy.radius = 0.9f;
        gm.normalKillPoints = 13;
        gm.matchKillMultiplier = 2;
        gm.specialMatchKillMultiplier = 5;
        var normal = gm.SpawnEnemy(Enemy.Kind.Normal, 40, new Vector3(-3f, 0f, 4f));
        var special = gm.SpawnEnemy(Enemy.Kind.Special, 111, new Vector3(3f, 0f, 4f));
        Check(normal.hp == 9 && special.hp == 7 && normal.Radius == 0.6f && special.Radius == 0.9f, "independent per-kind health and radius");
        typeof(Enemy).GetMethod("ApplyHit", flags).Invoke(normal, new object[] { false });
        Check(normal.hp == 6 && special.hp == 7, "custom hit damage does not affect special health");
        Check(gm.OnNormalKill(true) == 26, "normal match reward");
        long before = gm.Score;
        gm.OnSpecialKill(2, true);
        Check(gm.Score == before * 6, "special match reward uses independent multiplier");

        var spawner = UnityEngine.Object.FindFirstObjectByType<Spawner>();
        var type = typeof(Spawner);
        var update = type.GetMethod("Update", flags);
        gm.spawning.normalChancePercent = 0f;
        gm.spawning.bumperChancePercent = 0f;
        gm.spawning.specialInitialChancePercent = 0f;
        gm.spawning.specialChanceStepPercent = 0f;
        gm.spawning.specialStartDelay = 0f;
        gm.spawnInterval = 2f;
        gm.specialSpawnInterval = 100f;
        gm.bumperSpawnInterval = 100f;
        type.GetField("normalTimer", flags).SetValue(spawner, 3.5f);
        type.GetField("specialTimer", flags).SetValue(spawner, 7f);
        type.GetField("bumperTimer", flags).SetValue(spawner, 9f);
        update.Invoke(spawner, null);
        Check((float)type.GetField("normalTimer", flags).GetValue(spawner) < 2f &&
            (float)type.GetField("specialTimer", flags).GetValue(spawner) >= 7f &&
            (float)type.GetField("bumperTimer", flags).GetValue(spawner) >= 9f, "normal timer does not consume special or bumper timers");
        gm.spawnInterval = 100f;
        gm.specialSpawnInterval = 2f;
        type.GetField("normalTimer", flags).SetValue(spawner, 7f);
        type.GetField("specialTimer", flags).SetValue(spawner, 3.5f);
        update.Invoke(spawner, null);
        Check((float)type.GetField("specialTimer", flags).GetValue(spawner) < 2f &&
            (float)type.GetField("normalTimer", flags).GetValue(spawner) >= 7f, "special timer does not consume normal timer");
        Debug.Log("Independent tuning gameplay passed");
    }
}
