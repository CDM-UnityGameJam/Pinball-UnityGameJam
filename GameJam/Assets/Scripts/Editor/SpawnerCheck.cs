using UnityEditor;
using UnityEngine;

// Checklist D1/D3 self-check for spawn odds and timing. No test framework in the project, so this runs from a menu item.
public static class SpawnerCheck
{
    [MenuItem("LuckyPinball/Check Spawner")]
    public static void Run()
    {
        Expect(Spawner.SpecialChance(0f), 0.05f, "special chance at start");
        Expect(Spawner.SpecialChance(59.9f), 0.05f, "still 5% before the first full minute");
        Expect(Spawner.SpecialChance(60f), 0.07f, "+2 points after one minute");
        Expect(Spawner.SpecialChance(299f), 0.13f, "4 minutes");
        Expect(Spawner.SpecialChance(300f), 0.15f, "cap reached at 5 minutes");
        Expect(Spawner.SpecialChance(3600f), 0.15f, "capped");
        Expect(Spawner.DescendSpeed * Spawner.DescendTime, Spawner.SpawnZ - Spawner.LineZ, "top to red line in 40 s");

        int a = 0, b = 0, c = 0;
        for (int i = 0; i < 100000; i++)
        {
            int t = Spawner.SpecialTarget(Random.value);
            if (t == 111) a++; else if (t == 222) b++; else if (t == 333) c++; else throw new System.Exception("bad special " + t);
        }
        Expect(a / 100000f, 0.10f, "111 weight", 0.01f);
        Expect(b / 100000f, 0.45f, "222 weight", 0.01f);
        Expect(c / 100000f, 0.45f, "333 weight", 0.01f);
        Debug.Log("Spawner check passed");
    }

    static void Expect(float actual, float expected, string what, float tolerance = 0.0001f)
    {
        if (Mathf.Abs(actual - expected) > tolerance) throw new System.Exception("Spawner check failed: " + what + " gave " + actual + ", expected " + expected);
    }
}
