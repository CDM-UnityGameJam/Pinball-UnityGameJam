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
        [Tooltip("The lucky number alone (\"-\" when there is none), e.g. on a tile.")]
        public TMP_Text luckyNumber;
        [Min(0.01f), Tooltip("Seconds (unscaled) of the card-flip when the lucky number changes.")]
        public float flipTime = 0.35f;
        [Tooltip("Status line: lucky number and selection (optional).")]
        [FormerlySerializedAs("lucky")] public TMP_Text status;
        public TMP_Text lastCast;
        [FormerlySerializedAs("refill")] public TMP_Text charge;
        [Tooltip("Second charge line (how many cards the refill draws).")] public TMP_Text chargeHint;
        [FormerlySerializedAs("refillFill"), Tooltip("Fill whose right anchor tracks charge progress.")]
        public RectTransform chargeFill;

        string shownLucky;
        float flipAge = float.MaxValue;

        void Update()
        {
            if (!Ready) return;
            var cards = Context.Hand;
            AnimateLucky(cards.LuckyNumber != 0 ? cards.LuckyNumber.ToString() : "-");
            string lucky = cards.LuckyNumber != 0 ? $"Lucky #{cards.LuckyNumber}" : "No lucky number";
            if (status) status.text = hand.Selection.Count == 0 ? $"{lucky} · Drag a group out to cast"
                : $"{lucky} · Group #{hand.SelectedNumber} ({hand.Selection.Count})";

            var batch = Context.Casts.LastBatch;
            if (lastCast)
            {
                lastCast.text = batch == null ? "No cast yet"
                    : $"{GroupRules.Label(batch.tier)} #{batch.number} · {batch.Coverage} targets · {batch.Kills} kills";
                lastCast.color = batch != null && batch.Lucky ? Theme.gold : Theme.blue;
            }

            if (charge) charge.text = cards.IsFull ? "Equipment full" : $"Recharge: {cards.ChargeProgress:0.#} / {cards.ChargeRequired:0.#}";
            if (chargeHint) chargeHint.text = cards.IsFull ? "" : $"Refill draws {cards.Missing} card{(cards.Missing == 1 ? "" : "s")}";
            float progress = cards.IsFull || cards.ChargeRequired <= 0 ? 0f : Mathf.Clamp01((float)(cards.ChargeProgress / cards.ChargeRequired));
            if (chargeFill) chargeFill.anchorMax = new Vector2(progress, chargeFill.anchorMax.y);
        }

        // A new lucky number turns in like a card: squeezed flat, it opens wide past full size and settles.
        void AnimateLucky(string text)
        {
            if (!luckyNumber) return;
            if (text != shownLucky)
            {
                if (shownLucky != null) flipAge = 0f;
                shownLucky = text;
                luckyNumber.text = text;
            }
            if (flipAge >= flipTime) { luckyNumber.transform.localScale = Vector3.one; return; }
            flipAge += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float t = Mathf.Clamp01(flipAge / flipTime);
            const float s = 2.4f; // back-out ease: overshoots, then settles at 1
            float u = t - 1f;
            float open = 1f + (s + 1f) * u * u * u + s * u * u;
            luckyNumber.transform.localScale = new Vector3(open, 1f + 0.25f * Mathf.Sin(t * Mathf.PI), 1f);
        }
    }
}
