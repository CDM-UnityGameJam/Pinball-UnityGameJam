using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Pinball.Presentation
{
    // Bottom bar: what is being cast, the last batch's result, and the charge toward the next card.
    public sealed class CastPanelView : HudPresenter
    {
        [Required] public HandView hand;
        [FormerlySerializedAs("lucky")] public TMP_Text status;
        public TMP_Text lastCast;
        [FormerlySerializedAs("refill")] public TMP_Text charge;
        [Tooltip("Second charge line (cost per card).")] public TMP_Text chargeHint;
        [FormerlySerializedAs("refillFill"), Tooltip("Fill whose right anchor tracks charge progress.")]
        public RectTransform chargeFill;

        void Update()
        {
            if (!Ready) return;
            var casts = Context.Casts;
            var cards = Context.Hand;
            status.text = casts.PendingBatch != null ? $"Rolling {casts.PendingBatch.Dice.Count} dice"
                : hand.Selection.Count == 0 ? "Select cards"
                : $"Match #{hand.SelectedNumber} · {hand.Selection.Count} selected";

            var batch = casts.LastBatch;
            lastCast.text = batch == null ? "No cast yet"
                : !batch.Revealed ? "Revealing dice..."
                : $"{DiceRules.Label(batch.tier)} · {batch.Coverage} targets · {batch.Kills} kills";
            lastCast.color = batch != null && batch.Revealed && batch.tier == CastTier.Jackpot ? Theme.gold : Theme.blue;

            charge.text = cards.IsFull ? "Equipment full" : $"Recharge: {cards.ChargeProgress:0.#} / {cards.ChargeRequired:0.#}";
            if (chargeHint) chargeHint.text = $"Per card cast: required charge +{Context.config.deck.perCardCost:0.#}";
            float progress = cards.IsFull || cards.ChargeRequired <= 0 ? 0f : Mathf.Clamp01((float)(cards.ChargeProgress / cards.ChargeRequired));
            chargeFill.anchorMax = new Vector2(progress, chargeFill.anchorMax.y);
        }
    }
}
