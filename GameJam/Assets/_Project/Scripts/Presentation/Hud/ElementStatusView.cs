using System.Collections.Generic;
using TMPro;

namespace Pinball.Presentation
{
    // Balls in play, stocked empowered hits, and cards held per element.
    public sealed class ElementStatusView : HudPresenter
    {
        public TMP_Text text;
        readonly List<string> parts = new List<string>();

        void Update()
        {
            if (!Ready) return;
            parts.Clear();
            parts.Add($"BALLS {Context.Balls.Count}");
            parts.Add($"POWER {Context.Hand.EmpoweredHits}");
            foreach (var element in Context.config.elements)
                if (element) parts.Add($"{element.displayName} ×{Context.Hand.HeldCount(element)}");
            text.text = string.Join(" · ", parts);
        }
    }
}
