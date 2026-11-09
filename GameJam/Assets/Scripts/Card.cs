using System.Collections.Generic;
using UnityEngine;

// 27 numeric cards (always applied to 37) plus independent rare functional-card rolls.
public readonly struct Card
{
    public enum Kind { Number, MatchBoost, ExtraBall }
    public readonly Kind kind;
    public bool IsNumber => kind == Kind.Number;
    public const int Lucky = 37;

    public readonly char op; // '+', '-' or 'x'
    public readonly int n;   // 1..9

    public Card(char op, int n) { kind = Kind.Number; this.op = op; this.n = n; }
    public Card(Kind kind) { this.kind = kind; op = '\0'; n = 0; }

    public static Card? RollFunctional(bool opening, float matchRoll, float ballRoll, float tieRoll,
        float matchChance = 0.03f, float ballChance = 0.03f)
    {
        bool match = !opening && Succeeds(matchRoll, matchChance);
        bool ball = Succeeds(ballRoll, ballChance);
        if (match && ball) return new Card(tieRoll < 0.5f ? Kind.MatchBoost : Kind.ExtraBall);
        if (match) return new Card(Kind.MatchBoost);
        if (ball) return new Card(Kind.ExtraBall);
        return null;
    }

    static bool Succeeds(float roll, float chance) => chance >= 1f || roll < Mathf.Clamp01(chance);
    public static Card? RandomFunctional(bool opening, float matchChance, float ballChance) =>
        RollFunctional(opening, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, matchChance, ballChance);
    public static Card Opening(float ballChance = 0.03f) => RandomFunctional(true, 0f, ballChance) ?? Random();
    public static Card Reward(int current, Card?[] hand, List<int> targets,
        float matchChance = 0.03f, float ballChance = 0.03f) =>
        RandomFunctional(false, matchChance, ballChance) ?? Draw(current, hand, targets);

    static readonly char[] Ops = { '+', '-', 'x' };

    // Uniform over all 27 cards; duplicates allowed.
    public static Card Random() => new Card(Ops[UnityEngine.Random.Range(0, 3)], UnityEngine.Random.Range(1, 10));

    public int Apply(int lucky) => !IsNumber ? throw new System.InvalidOperationException("Functional cards have no numeric result")
        : op == '+' ? lucky + n : op == '-' ? lucky - n : lucky * n;

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
            foreach (var c in hand) if (c != null && c.Value.IsNumber && c.Value.Apply(Lucky) == t) covered = true;
            if (!covered) uncovered.Add(t);
        }
        return uncovered.Count > 0 ? For(uncovered[UnityEngine.Random.Range(0, uncovered.Count)]) : Random();
    }

    public override string ToString() => kind == Kind.MatchBoost ? "MATCH" : kind == Kind.ExtraBall ? "BALL +1" : op.ToString() + n;
}
