using TMPro;

namespace Pinball.Presentation
{
    // Balancing readout (RunStats).
    public sealed class RunStatsView : HudPresenter
    {
        public TMP_Text text;

        void Update()
        {
            if (!Ready) return;
            var s = Context.Stats;
            text.text = $"RUN STATS\nBatches: {s.batches} · Cards: {s.cardsPlayed} · Skills: {s.skillCasts} · Matches: {s.matchingDice}" +
                        $"\nMean hand: {s.AverageHand:0.00} · Low gear: {s.lowEquipmentSeconds:0.0}s" +
                        $"\nBase / Hit / Full / Jackpot: {s.baseBatches} / {s.hitBatches} / {s.fullFieldBatches} / {s.jackpotBatches}";
        }
    }
}
