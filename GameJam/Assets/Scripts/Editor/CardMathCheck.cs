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
        Debug.Log("Card math check passed");
    }

    static void Expect(int actual, int expected, string what)
    {
        if (actual != expected) throw new System.Exception("Card math check failed: " + what + " gave " + actual + ", expected " + expected);
    }
}
