using UnityEditor;
using UnityEngine;

// Checklist C4 self-check for the score arithmetic. No test framework in the project, so this runs from a menu item.
public static class ScoringCheck
{
    [MenuItem("LuckyPinball/Check Scoring")]
    public static void Run()
    {
        Expect(GameManager.AddClamped(0, 50), 50, "kill from 0");
        Expect(GameManager.MultiplyClamped(0, 10), 0, "x10 on 0 stays 0");
        Expect(GameManager.MultiplyClamped(125, 10), 1250, "x10");
        Expect(GameManager.MultiplyClamped(125, 3), 375, "x3");
        Expect(GameManager.MultiplyClamped(long.MaxValue / 2, 10), long.MaxValue, "x10 saturates");
        Expect(GameManager.AddClamped(long.MaxValue - 1, 50), long.MaxValue, "add saturates");
        Expect(GameManager.SpecialKillReward(100, 2, false), 100, "222 ordinary kill gain");
        Expect(GameManager.SpecialKillReward(100, 2, true), 1000, "222 match multiplies gain only");
        Expect(GameManager.SpecialKillReward(100, 2, true, 5), 500, "custom match multiplier");
        Expect(GameManager.SpecialKillReward(100, 2, true, 0), 100, "match multiplier minimum is one");
        Expect(GameManager.SpecialKillReward(100, 3, true), 2000, "333 match gain");
        Expect(GameManager.SpecialKillReward(100, 10, true), 9000, "111 match gain");
        Expect(GameManager.SpecialKillReward(0, 10, true), 0, "special preserves zero-score rule");
        Expect(GameManager.SpecialKillReward(long.MaxValue, 10, true), long.MaxValue, "match gain saturates");
        Debug.Log("Scoring check passed");
    }

    static void Expect(long actual, long expected, string what)
    {
        if (actual != expected) throw new System.Exception("Scoring check failed: " + what + " gave " + actual + ", expected " + expected);
    }
}
