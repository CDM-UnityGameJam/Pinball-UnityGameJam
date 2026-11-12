using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

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
            text.text = $"RUN STATS\nPlays: {s.plays} · Average hand: {s.AverageHand:0.00}\nLow gear (0-1 cards): {s.lowEquipmentSeconds:0.0}s\nDeath cause: {s.deathCause}";
        }
    }
}
