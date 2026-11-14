using Pinball.Core;
using Pinball.Gameplay;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace Pinball.Presentation
{
    // Bottom bar: the lucky number and what is selected, the last group's result, and the charge toward the refill.
    public sealed class CastPanelView : HudPresenter
    {
        [Required] public HandView hand;
        [FormerlySerializedAs("lucky")] public TMP_Text status;
        public TMP_Text lastCast;
        [FormerlySerializedAs("refill")] public TMP_Text charge;
        [Tooltip("Second charge line (how many cards the refill draws).")] public TMP_Text chargeHint;
        [FormerlySerializedAs("refillFill"), Tooltip("Fill whose right anchor tracks charge progress.")]
        public RectTransform chargeFill;

        void Update()
        {
            if (!Ready) return;
            var cards = Context.Hand;
            string lucky = cards.LuckyNumber != 0 ? $"Lucky #{cards.LuckyNumber}" : "No lucky number";
            status.text = hand.Selection.Count == 0 ? $"{lucky} · Drag a group out to cast"
                : $"{lucky} · Group #{hand.SelectedNumber} ({hand.Selection.Count})";

            var batch = Context.Casts.LastBatch;
            if (lastCast)
            {
                lastCast.text = batch == null ? "No cast yet"
                    : $"{GroupRules.Label(batch.tier)} #{batch.number} · {batch.Coverage} targets · {batch.Kills} kills";
                lastCast.color = batch != null && batch.Lucky ? Theme.gold : Theme.blue;
            }

            charge.text = cards.IsFull ? "Equipment full" : $"Recharge: {cards.ChargeProgress:0.#} / {cards.ChargeRequired:0.#}";
            if (chargeHint) chargeHint.text = cards.IsFull ? "" : $"Refill draws {cards.Missing} card{(cards.Missing == 1 ? "" : "s")}";
            float progress = cards.IsFull || cards.ChargeRequired <= 0 ? 0f : Mathf.Clamp01((float)(cards.ChargeProgress / cards.ChargeRequired));
            if (chargeFill) chargeFill.anchorMax = new Vector2(progress, chargeFill.anchorMax.y);
        }
    }
}
