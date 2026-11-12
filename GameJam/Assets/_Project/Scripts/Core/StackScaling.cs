using System;
using UnityEngine;

namespace Pinball.Core
{
    // A value that grows with how many cards of the same element and number are held.
    // value(stack) = baseValue + perExtraCard * (stack - 1), clamped to [min, max]. Zero cards gives 0.
    [Serializable]
    public struct StackedInt
    {
        public int baseValue, perExtraCard, min, max;

        public StackedInt(int baseValue, int perExtraCard, int min, int max)
        { this.baseValue = baseValue; this.perExtraCard = perExtraCard; this.min = min; this.max = max; }

        public int Evaluate(int stack) =>
            stack <= 0 ? 0 : Mathf.Clamp(baseValue + perExtraCard * (stack - 1), min, max);
    }

    // Same as StackedInt, unclamped (for radii).
    [Serializable]
    public struct StackedFloat
    {
        public float baseValue, perExtraCard;

        public StackedFloat(float baseValue, float perExtraCard)
        { this.baseValue = baseValue; this.perExtraCard = perExtraCard; }

        public float Evaluate(int stack) => stack <= 0 ? 0f : baseValue + perExtraCard * (stack - 1);
    }
}
