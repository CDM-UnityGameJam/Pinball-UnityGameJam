using System.Collections.Generic;
using UnityEngine;

// Numeric cards use the supplied, Inspector-authored rules; functional rolls are independent.
public readonly struct Card
{
    public enum Kind { Number, MatchBoost, ExtraBall }
    public readonly Kind kind;
    public bool IsNumber => kind == Kind.Number;

    public readonly char op; // '+', '-' or 'x'
    public readonly int n;   // 1..9

    public Card(char op, int n) { kind = Kind.Number; this.op = op; this.n = n; }
    public Card(Kind kind) { this.kind = kind; op = '\0'; n = 0; }

    public static Card? RollFunctional(bool opening, float matchRoll, float ballRoll, float tieRoll,
        float matchChance = 0.03f, float ballChance = 0.03f, CardTuning rules = null)
    {
        bool match = !opening && Succeeds(matchRoll, matchChance);
        bool ball = Succeeds(ballRoll, ballChance);
        if (match && ball) return new Card(tieRoll < (rules ?? new CardTuning()).matchWinsTieChance ? Kind.MatchBoost : Kind.ExtraBall);
        if (match) return new Card(Kind.MatchBoost);
        if (ball) return new Card(Kind.ExtraBall);
        return null;
    }

    static bool Succeeds(float roll, float chance) => chance >= 1f || roll < Mathf.Clamp01(chance);
    public static Card? RandomFunctional(bool opening, float matchChance, float ballChance, CardTuning rules = null) =>
        RollFunctional(opening, UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value, matchChance, ballChance, rules);
    public static Card Opening(float ballChance = 0.03f) => RandomFunctional(true, 0f, ballChance) ?? Random();
    public static Card Reward(int current, Card?[] hand, List<int> targets,
        float matchChance = 0.03f, float ballChance = 0.03f, CardTuning rules = null) =>
        RandomFunctional(false, matchChance, ballChance, rules) ?? Draw(current, hand, targets, rules);

    static readonly char[] Ops = { '+', '-', 'x' };

    // Uniform over configured operators and operands; duplicates allowed.
    public static Card Random(CardTuning rules = null)
    {
        rules = rules ?? new CardTuning();
        int min = Mathf.Max(1, rules.operandMin), max = Mathf.Max(min, rules.operandMax);
        return new Card(Ops[UnityEngine.Random.Range(0, Ops.Length)], UnityEngine.Random.Range(min, max + 1));
    }

    public int Apply(int lucky) => !IsNumber ? throw new System.InvalidOperationException("Functional cards have no numeric result")
        : op == '+' ? lucky + n : op == '-' ? lucky - n : lucky * n;

    // Find a card that reaches a target under the configured base and operand range.
    public static bool TryFor(int target, CardTuning rules, out Card card)
    {
        int min = Mathf.Max(1, rules.operandMin), max = Mathf.Max(min, rules.operandMax);
        long difference = (long)target - rules.baseNumber;
        if (difference >= min && difference <= max) { card = new Card('+', (int)difference); return true; }
        if (-difference >= min && -difference <= max) { card = new Card('-', (int)-difference); return true; }
        if (rules.baseNumber != 0 && (long)target % rules.baseNumber == 0)
        {
            long factor = (long)target / rules.baseNumber;
            if (factor >= min && factor <= max) { card = new Card('x', (int)factor); return true; }
        }
        if (rules.baseNumber == 0 && target == 0) { card = new Card('x', min); return true; }
        card = default;
        return false;
    }

    public static Card For(int target, CardTuning rules = null)
    {
        if (TryFor(target, rules ?? new CardTuning(), out var card)) return card;
        throw new System.ArgumentException("No configured card makes " + target);
    }

    // Energy draw: grant a card for a random visible target that neither the current number nor any
    // card in hand already reaches. Unreachable targets are skipped safely.
    public static Card Draw(int current, Card?[] hand, List<int> visibleTargets, CardTuning rules = null)
    {
        rules = rules ?? new CardTuning();
        var uncovered = new List<int>();
        foreach (var t in visibleTargets)
        {
            if (t == current || uncovered.Contains(t) || !TryFor(t, rules, out _)) continue;
            bool covered = false;
            foreach (var c in hand) if (c != null && c.Value.IsNumber && c.Value.Apply(rules.baseNumber) == t) covered = true;
            if (!covered) uncovered.Add(t);
        }
        return uncovered.Count > 0 ? For(uncovered[UnityEngine.Random.Range(0, uncovered.Count)], rules) : Random(rules);
    }

    public override string ToString() => kind == Kind.MatchBoost ? "MATCH" : kind == Kind.ExtraBall ? "BALL +1" : op.ToString() + n;
}
