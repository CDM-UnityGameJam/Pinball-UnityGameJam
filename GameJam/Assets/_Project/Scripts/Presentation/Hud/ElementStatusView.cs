using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // One line per element: which numbers are held and whether it is short-circuited.
    public sealed class ElementStatusView : HudPresenter
    {
        public TMP_Text text;
        readonly List<string> parts = new List<string>();
        readonly List<string> groups = new List<string>();

        void Update()
        {
            if (!Ready) return;
            var luck = Context.config.luck;
            parts.Clear();
            foreach (var element in Context.config.elements)
            {
                groups.Clear();
                for (int n = luck.minNumber; n <= luck.maxNumber; n++)
                {
                    int held = Context.Hand.HeldCount(element, n);
                    if (held > 0) groups.Add($"#{n}×{held}");
                }
                float off = Context.Casts.ShortCircuitRemaining(element);
                parts.Add($"{element.displayName} [{(groups.Count == 0 ? "none" : string.Join(", ", groups))}]" + (off > 0 ? $" [OFF {off:0.0}s]" : ""));
            }
            text.text = string.Join(" · ", parts);
        }
    }
}
