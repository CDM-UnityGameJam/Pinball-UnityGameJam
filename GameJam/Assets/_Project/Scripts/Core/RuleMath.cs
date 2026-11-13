using UnityEngine;

namespace Pinball.Core
{
    // Rounding and time comparison shared by every rule.
    public static class RuleMath
    {
        // Two times closer than this count as the same moment on the combat clock.
        public const double TimeEpsilon = 0.000001;

        public static int RoundHalfUp(float value) => Mathf.FloorToInt(value + 0.5f);
        public static int RoundHalfUp(double value) => (int)System.Math.Floor(value + 0.5);

        public static bool NonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        public static bool Positive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;
    }
}
