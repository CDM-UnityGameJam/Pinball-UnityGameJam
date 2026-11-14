using System.Collections.Generic;
using TMPro;

namespace Pinball.Presentation
{
    // Balls in play, the lucky number, and cards held per element.
    public sealed class ElementStatusView : HudPresenter
    {
        public TMP_Text text;
        readonly List<string> parts = new List<string>();

        void Update()
        {
            if (!Ready) return;
            parts.Clear();
            parts.Add($"BALLS {Context.Balls.Count}");
            parts.Add(Context.Hand.LuckyNumber != 0 ? $"LUCKY #{Context.Hand.LuckyNumber}" : "NO LUCKY");
            foreach (var element in Context.config.elements)
                if (element) parts.Add($"{element.displayName} ×{Context.Hand.HeldCount(element)}");
            text.text = string.Join(" · ", parts);
        }
    }
}
