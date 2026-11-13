using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The last batch: its cards, its dice ("?" until revealed, matching faces in gold) and the result.
    // Shown while rolling and for a while after the reveal.
    public sealed class DiceRevealView : HudPresenter
    {
        public GameObject panel;
        public TMP_Text cards, faces, result;
        [MinValue(0), SuffixLabel("s"), Tooltip("Combat seconds the result stays up after the reveal.")]
        public float holdSeconds = 2.5f;

        readonly List<string> parts = new List<string>();

        void Update()
        {
            if (!Ready) return;
            var batch = Context.Casts.LastBatch;
            bool show = batch != null && (!batch.Revealed || Context.Clock.Now - batch.revealAt < holdSeconds);
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            parts.Clear();
            foreach (var c in batch.Cards) parts.Add(c.ToString());
            cards.text = string.Join("   |   ", parts);
            cards.color = batch.Revealed && batch.tier != CastTier.Base ? Theme.gold : Theme.text;

            parts.Clear();
            string gold = UiTheme.Hex(Theme.gold);
            foreach (int face in batch.Dice)
                parts.Add(!batch.Revealed ? "[ ? ]" : face == batch.number ? $"<color=#{gold}>[ {face} ]</color>" : $"[ {face} ]");
            faces.text = string.Join("   ", parts);

            result.text = !batch.Revealed ? $"Rolling to match #{batch.number}"
                : $"{DiceRules.Label(batch.tier)} · {batch.Coverage} targets · {batch.Kills} kills";
            result.color = batch.Revealed && batch.tier == CastTier.Jackpot ? Theme.gold : Theme.text;
        }
    }
}
