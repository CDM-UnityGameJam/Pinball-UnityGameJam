namespace Pinball.Gameplay
{
    // Run telemetry for balancing (port of the v0.7 RunTelemetry). Time is combat time; "low gear" is time
    // spent holding 0-1 cards.
    public sealed class RunStats
    {
        public int batches, cardsPlayed, skillCasts, refills;
        public int normalBatches, luckyBatches;
        public int ballContacts, validBallHits, cooldownContacts;
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

        public void RecordCast(CastBatch batch)
        {
            skillCasts += batch.Casts.Count;
            if (batch.Lucky) luckyBatches++; else normalBatches++;
        }

        public string Summary() =>
            $"Time={elapsed:0.0}s, Batches={batches}, Cards={cardsPlayed}, Skills={skillCasts}, Refills={refills}, " +
            $"Normal/Lucky={normalBatches}/{luckyBatches}, Average hand={AverageHand:0.00}, " +
            $"Low gear (0-1)={lowEquipmentSeconds:0.0}s, Kills={kills}, Death cause={deathCause}, Breaches={breaches}, Drains={drains}";
    }
}
