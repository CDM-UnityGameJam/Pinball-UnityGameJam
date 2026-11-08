using System.Collections.Generic;
using UnityEngine;

// One of 27 cards: +1..+9, -1..-9, x1..x9. Always applied to the lucky number, never to the current result.
public readonly struct Card
{
    public const int Lucky = 37;

    public readonly char op; // '+', '-' or 'x'
    public readonly int n;   // 1..9

    public Card(char op, int n) { this.op = op; this.n = n; }

    static readonly char[] Ops = { '+', '-', 'x' };

    // Uniform over all 27 cards; duplicates allowed.
    public static Card Random() => new Card(Ops[UnityEngine.Random.Range(0, 3)], UnityEngine.Random.Range(1, 10));

    public int Apply(int lucky) => op == '+' ? lucky + n : op == '-' ? lucky - n : lucky * n;

    // The one card whose result is this target. Every normal and special target has exactly one.
    public static Card For(int target)
    {
        int d = target - Lucky;
        if (d == 0) return new Card('x', 1);
        if (d >= -9 && d <= 9) return new Card(d > 0 ? '+' : '-', Mathf.Abs(d));
        if (target % Lucky == 0 && target / Lucky >= 2 && target / Lucky <= 9) return new Card('x', target / Lucky);
        throw new System.ArgumentException("No card makes " + target);
    }

    // Energy draw: grant a card for a random visible target that neither the current number nor any
    // card in hand (including slot 5, even though it may be replaced) already reaches. Otherwise uniform.
    public static Card Draw(int current, Card?[] hand, List<int> visibleTargets)
    {
        var uncovered = new List<int>();
        foreach (var t in visibleTargets)
        {
            if (t == current || uncovered.Contains(t)) continue;
            bool covered = false;
            foreach (var c in hand) if (c != null && c.Value.Apply(Lucky) == t) covered = true;
            if (!covered) uncovered.Add(t);
        }
        return uncovered.Count > 0 ? For(uncovered[UnityEngine.Random.Range(0, uncovered.Count)]) : Random();
    }

    public override string ToString() => op.ToString() + n;
}
