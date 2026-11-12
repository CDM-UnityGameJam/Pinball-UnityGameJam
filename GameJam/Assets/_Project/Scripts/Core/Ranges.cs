using System;
using UnityEngine;

namespace Pinball.Core
{
    // Rounding shared by every rule, matching the old prototype: halves round up.
    public static class RuleMath
    {
        // Two times closer than this count as the same moment on the combat clock.
        public const double TimeEpsilon = 0.000001;

        public static int RoundHalfUp(float value) => Mathf.FloorToInt(value + 0.5f);

        public static float Snap(float value, float step) =>
            step > 0f ? RoundHalfUp(value / step) * step : value;
    }

    // An integer range picked by a 0..1 position, rounded half up.
    [Serializable]
    public struct IntRange
    {
        public int min, max;
        public IntRange(int min, int max) { this.min = min; this.max = max; }
        public int Evaluate(float t) => RuleMath.RoundHalfUp(Mathf.Lerp(min, max, t));
    }

    // A float range picked by a 0..1 position, then snapped to a step (0 = no snapping).
    [Serializable]
    public struct SteppedRange
    {
        public float min, max;
        [Min(0f)] public float step;
        public SteppedRange(float min, float max, float step) { this.min = min; this.max = max; this.step = step; }
        public float Evaluate(float t) => RuleMath.Snap(Mathf.Lerp(min, max, t), step);
    }
}
