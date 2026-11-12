using System;
using System.Collections.Generic;
using System.IO;
using Pinball.Core;
using UnityEngine;

namespace Pinball.Tests.EditMode
{
    // The v0.3 tuning from the old prototype (LuckTuning defaults, identical to the values saved in
    // Pinball.unity), expressed in the new Core types. Phase 2's data assets must hold these same values.
    static class LegacyV03
    {
        public const int MinNumber = 1, MaxNumber = 6;
        public const float RefillInterval = 8f, PlayRefillDelay = 4f;
        public const float BurnTickInterval = 0.5f;
        public const int PassiveBurnDps = 4, ActiveBurnDps = 6;
        public const float PassiveStunImmunity = 2f;
        public const float ShortCircuitMin = 2f, ShortCircuitMax = 4f;

        public static readonly StackedInt ArcTargets = new StackedInt(2, 1, 0, 5);
        public static readonly StackedInt ExtraSparkTargets = new StackedInt(0, 1, 0, 3);
        public static readonly StackedFloat ShockRadius = new StackedFloat(1.2f, 0.25f);

        public static List<QualityBand> Bands() => new List<QualityBand>
        {
            new QualityBand("Backlash", -10, -1, 25, 10),
            new QualityBand("Low", 0, 19, 45, 25),
            new QualityBand("Medium", 20, 59, 25, 40),
            new QualityBand("High", 60, 100, 5, 25),
        };

        public enum Element { Arc, Spark, Shockwave }

        // Old StrengthBand values; each element only differs in which wave range it uses.
        public static List<BandScaling> Scaling(Element element)
        {
            IntRange Waves(int arc0, int arc1, int shock0, int shock1) =>
                element == Element.Spark ? new IntRange(1, 1)
                : element == Element.Arc ? new IntRange(arc0, arc1) : new IntRange(shock0, shock1);

            return new List<BandScaling>
            {
                Band("Low", false, 2, 3, Waves(1, 1, 1, 1), 1, 2, 0.4f, 0.8f),
                Band("Medium", false, 4, 8, Waves(1, 2, 1, 2), 3, 5, 1f, 1.8f),
                Band("High", true, 0, 0, Waves(3, 4, 2, 3), 6, 8, 2f, 3f),
            };
        }

        static BandScaling Band(string id, bool all, int t0, int t1, IntRange waves, float b0, float b1, float s0, float s1) =>
            new BandScaling
            {
                bandId = id, allTargets = all, targets = new IntRange(t0, t1), waves = waves,
                burnSeconds = new SteppedRange(b0, b1, 0.5f), stunSeconds = new SteppedRange(s0, s1, 0.1f),
            };
    }

    // Values captured from the old prototype's code (see the file's "source" field).
    [Serializable]
    class Golden
    {
        public RollSet[] rolls;
        public LuckySequence luckySequence;
        public PlanRow[] plans;
        public ShortCircuitRow[] shortCircuit;
        public int[] bandSelection; // 1001 normal samples (0, 0.001 .. 1), then 1001 lucky samples
        public int[] arcTargets, extraSparkTargets; // index = cards held
        public float[] shockRadius;

        [Serializable] public class RollSet { public int seed; public bool lucky; public int[] q; }
        [Serializable] public class LuckySequence { public int seed, start; public int[] values; }
        [Serializable] public class PlanRow { public string element; public int q; public bool all; public int targets, waves; public float burn, stun; }
        [Serializable] public class ShortCircuitRow { public int q; public float seconds; }

        const string Path = "Assets/_Project/Tests/EditMode/Golden/LegacyLuckV03.json";
        static Golden cached;
        public static Golden Load() => cached ??= JsonUtility.FromJson<Golden>(File.ReadAllText(Path));
    }
}
