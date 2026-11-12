using System.Collections.Generic;
using UnityEngine;

namespace Pinball.Core
{
    // Rolls a card's quality Q from a band table. Port of the old LuckTuning.Roll / SelectBand.
    public static class QualityRoller
    {
        public static CastOutcome Classify(int q) =>
            q < 0 ? CastOutcome.ShortCircuit : q == 0 ? CastOutcome.Misfire : CastOutcome.Cast;

        public static int Roll(IReadOnlyList<QualityBand> bands, bool lucky, IRandom random)
        {
            var band = bands[SelectBand(bands, lucky, random.Value())];
            return random.Range(band.minQ, band.maxQ + 1);
        }

        // Picks a band index from a 0..1 sample. Zero-weight bands are skipped. If every weight is zero
        // (invalid authoring) it falls back to the band that contains Q = 0.
        public static int SelectBand(IReadOnlyList<QualityBand> bands, bool lucky, float sample)
        {
            float total = TotalWeight(bands, lucky);
            if (total <= 0f) return FallbackBand(bands);
            float remaining = Mathf.Clamp01(sample) * total;
            int last = FallbackBand(bands);
            for (int i = 0; i < bands.Count; i++)
            {
                float weight = bands[i].Weight(lucky);
                if (weight <= 0f) continue;
                last = i;
                if (remaining < weight) return i;
                remaining -= weight;
            }
            return last;
        }

        public static float TotalWeight(IReadOnlyList<QualityBand> bands, bool lucky)
        {
            float total = 0f;
            foreach (var band in bands) total += band.Weight(lucky);
            return total;
        }

        // Probability of rolling into a band, as shown to designers.
        public static float Chance(IReadOnlyList<QualityBand> bands, int index, bool lucky)
        {
            float total = TotalWeight(bands, lucky);
            if (total <= 0f) return index == FallbackBand(bands) ? 1f : 0f;
            return bands[index].Weight(lucky) / total;
        }

        // Index of the band whose range contains q, or -1.
        public static int BandOf(IReadOnlyList<QualityBand> bands, int q)
        {
            for (int i = 0; i < bands.Count; i++) if (bands[i].Contains(q)) return i;
            return -1;
        }

        static int FallbackBand(IReadOnlyList<QualityBand> bands)
        {
            int zero = BandOf(bands, 0);
            return zero >= 0 ? zero : 0;
        }

        // How long a short circuit lasts: shortest at the mildest negative Q, longest at the worst.
        // The scale comes from the band holding q, so by default -1 gives minSeconds and -10 gives maxSeconds.
        public static float ShortCircuitSeconds(IReadOnlyList<QualityBand> bands, int q, float minSeconds, float maxSeconds)
        {
            int index = BandOf(bands, q);
            if (index < 0) return q < 0 ? maxSeconds : minSeconds;
            var band = bands[index];
            return Mathf.Lerp(minSeconds, maxSeconds, Mathf.InverseLerp(band.maxQ, band.minQ, q));
        }
    }
}
