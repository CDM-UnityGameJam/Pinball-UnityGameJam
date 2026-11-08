using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Checklist B1 self-check. No test framework in the project, so this runs from a menu item.
public static class CardMathCheck
{
    [MenuItem("LuckyPinball/Check Card Math")]
    public static void Run()
    {
        Expect(new Card('+', 3).Apply(37), 40, "+3");
        Expect(new Card('x', 2).Apply(37), 74, "x2 applies to 37, not to 40");
        Expect(new Card('x', 1).Apply(37), 37, "x1 resets to 37");
        Expect(new Card('-', 9).Apply(37), 28, "-9");
        Expect(new Card('x', 8).Apply(37), 296, "x8");

        // 20k draws must hit every one of the 27 cards and nothing else.
        var seen = new HashSet<string>();
        for (int i = 0; i < 20000; i++)
        {
            var c = Card.Random();
            if (c.n < 1 || c.n > 9 || (c.op != '+' && c.op != '-' && c.op != 'x')) throw new System.Exception("Bad card " + c);
            seen.Add(c.ToString());
        }
        Expect(seen.Count, 27, "distinct cards drawn");

        // Every normal and special target has exactly the card that reaches it (B3).
        foreach (var t in Enemy.NormalTargets) Expect(Card.For(t).Apply(37), t, "card for " + t);
        foreach (var t in Enemy.SpecialTargets) Expect(Card.For(t).Apply(37), t, "card for " + t);

        // Only 74 is uncovered: 41 is the current number, 30 is covered by -7 in slot 5.
        var hand = new Card?[] { new Card('+', 1), null, null, null, new Card('-', 7) };
        var targets = new List<int> { 41, 30, 74, 74 };
        for (int i = 0; i < 200; i++) Expect(Card.Draw(41, hand, targets).Apply(37), 74, "guaranteed draw picks the uncovered target");

        // Two uncovered targets: both must come up, nothing else.
        var picks = new HashSet<int>();
        for (int i = 0; i < 500; i++) picks.Add(Card.Draw(37, new Card?[5], new List<int> { 111, 296 }).Apply(37));
        Expect(picks.Count, 2, "uniform over uncovered targets");

        // Everything covered: uniform over all 27.
        seen.Clear();
        for (int i = 0; i < 20000; i++) seen.Add(Card.Draw(41, hand, new List<int> { 41, 38, 30 }).ToString());
        Expect(seen.Count, 27, "no gap falls back to all 27 cards");

        Debug.Log("Card math check passed");
    }

    static void Expect(int actual, int expected, string what)
    {
        if (actual != expected) throw new System.Exception("Card math check failed: " + what + " gave " + actual + ", expected " + expected);
    }
}
