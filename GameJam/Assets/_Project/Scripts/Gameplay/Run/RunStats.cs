using Pinball.Core;

namespace Pinball.Gameplay
{
    // Run telemetry for balancing. Port of the old RunTelemetry: time is combat time, and "low gear" is
    // time spent holding 0-1 cards.
    public sealed class RunStats
    {
        public int plays, validBallHits, kills, drains, breaches;
        public int lowQuality, mediumQuality, highQuality, shortCircuits, misfires;
        public double elapsed, handTimeIntegral, lowEquipmentSeconds;
        public string deathCause = "Alive";

        public double AverageHand => elapsed > 0 ? handTimeIntegral / elapsed : 5;

        public void Tick(double delta, int cards)
        {
            elapsed += delta;
            handTimeIntegral += delta * cards;
            if (cards <= 1) lowEquipmentSeconds += delta;
        }

        // Q bands for stats follow the v0.3 split: under 20 low, under 60 medium, otherwise high.
        public void RecordPlay(CastResult play)
        {
            plays++;
            switch (play.outcome)
            {
                case CastOutcome.ShortCircuit: shortCircuits++; break;
                case CastOutcome.Misfire: misfires++; break;
                default:
                    if (play.quality < 20) lowQuality++;
                    else if (play.quality < 60) mediumQuality++;
                    else highQuality++;
                    break;
            }
        }

        public string Summary() =>
            $"Time={elapsed:0.0}s, Plays={plays}, Average hand={AverageHand:0.00}, Low gear (0-1)={lowEquipmentSeconds:0.0}s, " +
            $"Kills={kills}, Death cause={deathCause}, Breaches={breaches}, Drains={drains}";
    }
}
