using System;
using UnityEngine;

namespace Pinball.Core
{
    // What playing a card does, decided by the sign of its rolled quality Q.
    public enum CastOutcome
    {
        ShortCircuit, // Q < 0: the card's element is disabled for a while.
        Misfire,      // Q = 0: nothing happens.
        Cast,         // Q > 0: the active skill fires, scaled by Q.
    }

    // One row of the Q roll table. A roll first picks a band by weight, then a Q uniformly in [minQ, maxQ].
    // Defaults (v0.3): Backlash -10..-1, Low 0..19, Medium 20..59, High 60..100.
    // Q = 0 (misfire) is part of the Low roll range, exactly as in the prototype.
    [Serializable]
    public class QualityBand
    {
        public string id;
        public int minQ, maxQ;
        [Min(0f)] public float normalWeight;
        [Tooltip("Weight used when the played card's number matches the lucky number.")]
        [Min(0f)] public float luckyWeight;

        public QualityBand() { }

        public QualityBand(string id, int minQ, int maxQ, float normalWeight, float luckyWeight)
        {
            this.id = id; this.minQ = minQ; this.maxQ = maxQ;
            this.normalWeight = normalWeight; this.luckyWeight = luckyWeight;
        }

        public float Weight(bool lucky) => Mathf.Max(0f, lucky ? luckyWeight : normalWeight);
        public bool Contains(int q) => q >= minQ && q <= maxQ;

        // Casts never use Q <= 0, so a band's scaling runs from max(minQ, 1) to maxQ.
        public float CastPosition(int q) => Mathf.InverseLerp(Mathf.Max(minQ, 1), maxQ, q);
    }
}
