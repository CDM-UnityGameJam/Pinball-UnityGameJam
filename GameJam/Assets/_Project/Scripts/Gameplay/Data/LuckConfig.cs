using System.Collections.Generic;
using Pinball.Core;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Pinball.Gameplay
{
    // The lucky number and the quality (Q) roll for played cards.
    [CreateAssetMenu(fileName = "LuckConfig", menuName = "Pinball/Config/Luck Config", order = 1)]
    public sealed class LuckConfig : ScriptableObject
    {
        [Title("Numbers")]
        [Tooltip("Card and enemy numbers, and the lucky number, are drawn from this range.")]
        [MinValue(0)] public int minNumber = 1;
        [ValidateInput(nameof(MaxAboveMin), "Max must be at least min.")]
        public int maxNumber = 6;
        [MinValue(0.01), SuffixLabel("s"), Tooltip("How often the lucky number changes. It never repeats.")]
        public float luckyRefreshInterval = 10f;

        [Title("Quality roll")]
        [InfoBox("A played card first picks a band by weight (lucky weights when its number matches the lucky number), " +
                 "then a Q evenly inside the band. Q < 0 short-circuits the element, Q = 0 misfires, Q > 0 casts. " +
                 "Element skills scale per band id, so keep ids in sync with each element's Band Scaling.")]
        [ValidateInput(nameof(ValidateBands))]
        [ListDrawerSettings(ShowFoldout = false)]
        public List<QualityBand> bands = new List<QualityBand>();

        [Title("Short circuit")]
        [MinValue(0), SuffixLabel("s"), Tooltip("Duration at the mildest negative Q.")]
        public float shortCircuitMinSeconds = 2f;
        [MinValue(0), SuffixLabel("s"), Tooltip("Duration at the worst negative Q.")]
        public float shortCircuitMaxSeconds = 4f;

        public int NumberCount => maxNumber - minNumber + 1;

        bool MaxAboveMin(int value) => value >= minNumber;

        bool ValidateBands(List<QualityBand> value, ref string message)
        {
            message = LuckConfigValidation.Check(value);
            return message == null;
        }

        [ShowInInspector, ReadOnly, PropertyOrder(10), LabelText("Normal chance per band")]
        string NormalChances => Chances(false);

        [ShowInInspector, ReadOnly, PropertyOrder(11), LabelText("Lucky chance per band")]
        string LuckyChances => Chances(true);

        string Chances(bool lucky)
        {
            if (bands == null || bands.Count == 0) return "-";
            var parts = new string[bands.Count];
            for (int i = 0; i < bands.Count; i++)
                parts[i] = $"{bands[i]?.id}: {QualityRoller.Chance(bands, i, lucky):P0}";
            return string.Join("   ", parts);
        }
    }

    // Kept separate so tests can run the same checks the Inspector shows.
    public static class LuckConfigValidation
    {
        // Returns null when valid, otherwise what's wrong.
        public static string Check(IReadOnlyList<QualityBand> bands)
        {
            if (bands == null || bands.Count == 0) return "Add at least one band.";
            bool anyNormal = false, anyLucky = false, holdsZero = false;
            for (int i = 0; i < bands.Count; i++)
            {
                var a = bands[i];
                if (a == null) return $"Band {i} is empty.";
                if (string.IsNullOrWhiteSpace(a.id)) return $"Band {i} needs an id.";
                if (a.minQ > a.maxQ) return $"{a.id}: min Q is above max Q.";
                anyNormal |= a.normalWeight > 0;
                anyLucky |= a.luckyWeight > 0;
                holdsZero |= a.Contains(0);
                for (int j = i + 1; j < bands.Count; j++)
                {
                    var b = bands[j];
                    if (b == null) continue;
                    if (a.id == b.id) return $"Two bands are called {a.id}.";
                    if (a.minQ <= b.maxQ && b.minQ <= a.maxQ) return $"{a.id} and {b.id} overlap.";
                }
            }
            if (!anyNormal) return "Every normal weight is zero.";
            if (!anyLucky) return "Every lucky weight is zero.";
            if (!holdsZero) return "No band contains Q = 0 (the misfire / fallback band).";
            return null;
        }
    }
}
