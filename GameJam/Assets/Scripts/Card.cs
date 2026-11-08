using UnityEngine;

// One of 27 cards: +1..+9, -1..-9, x1..x9. Always applied to the lucky number, never to the current result.
public readonly struct Card
{
    public readonly char op; // '+', '-' or 'x'
    public readonly int n;   // 1..9

    public Card(char op, int n) { this.op = op; this.n = n; }

    static readonly char[] Ops = { '+', '-', 'x' };

    // Uniform over all 27 cards; duplicates allowed.
    public static Card Random() => new Card(Ops[UnityEngine.Random.Range(0, 3)], UnityEngine.Random.Range(1, 10));

    public int Apply(int lucky) => op == '+' ? lucky + n : op == '-' ? lucky - n : lucky * n;

    public override string ToString() => op.ToString() + n;
}
