using UnityEngine;

namespace Pinball.Core
{
    // Every random decision in the rules goes through this, so tests can be deterministic.
    public interface IRandom
    {
        // Uniform in [0, 1], like UnityEngine.Random.value.
        float Value();

        // Integer in [minInclusive, maxExclusive), like UnityEngine.Random.Range(int, int).
        int Range(int minInclusive, int maxExclusive);
    }

    // The game's default source. With the same UnityEngine.Random seed and call order it reproduces
    // the old prototype's rolls exactly.
    public sealed class UnityRandom : IRandom
    {
        public static readonly UnityRandom Shared = new UnityRandom();
        public float Value() => Random.value;
        public int Range(int minInclusive, int maxExclusive) => Random.Range(minInclusive, maxExclusive);
    }

    // Self-contained seeded source that doesn't touch Unity's global random state.
    public sealed class SeededRandom : IRandom
    {
        readonly System.Random random;
        public SeededRandom(int seed) => random = new System.Random(seed);
        public float Value() => (float)random.NextDouble();
        public int Range(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : random.Next(minInclusive, maxExclusive);
    }
}
