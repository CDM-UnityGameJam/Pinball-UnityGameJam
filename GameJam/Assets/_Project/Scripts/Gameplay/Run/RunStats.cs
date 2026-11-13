using Pinball.Core;

namespace Pinball.Gameplay
{
    // Run telemetry for balancing (port of the v0.6 RunTelemetry). Time is combat time; "low gear" is time
    // spent holding 0-1 cards.
    public sealed class RunStats
    {
        public int batches, cardsPlayed, skillCasts, matchingDice;
        public int baseBatches, hitBatches, fullFieldBatches, jackpotBatches;
        public int ballContacts, validBallHits, cooldownContacts, empoweredUsed;
        public int kills, batchKills, spawns, drains, breaches;
        public double elapsed, handTimeIntegral, lowEquipmentSeconds;
        public string deathCause = "Alive";

        public double AverageHand => elapsed > 0 ? handTimeIntegral / elapsed : 0;

        public void Tick(double delta, int cards)
        {
            elapsed += delta;
            handTimeIntegral += delta * cards;
            if (cards <= 1) lowEquipmentSeconds += delta;
        }

        public void RecordBatch(int cards)
        {
            batches++;
            cardsPlayed += cards;
        }

        public void RecordReveal(DiceBatch batch)
        {
            skillCasts += batch.Casts.Count;
            matchingDice += batch.matches;
            switch (batch.tier)
            {
                case CastTier.Base: baseBatches++; break;
                case CastTier.Hit: hitBatches++; break;
                case CastTier.FullField: fullFieldBatches++; break;
                default: jackpotBatches++; break;
            }
        }

        public string Summary() =>
            $"Time={elapsed:0.0}s, Batches={batches}, Cards={cardsPlayed}, Skills={skillCasts}, Matches={matchingDice}, " +
            $"Base/Hit/Full/Jackpot={baseBatches}/{hitBatches}/{fullFieldBatches}/{jackpotBatches}, Average hand={AverageHand:0.00}, " +
            $"Low gear (0-1)={lowEquipmentSeconds:0.0}s, Kills={kills}, Death cause={deathCause}, Breaches={breaches}, Drains={drains}";
    }
}
