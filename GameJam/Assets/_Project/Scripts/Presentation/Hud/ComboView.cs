using System;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The running scoring combo (ScoreSystem.Combo: counted ball hits, ends after the combo timeout and pays
    // its tier). Hidden at zero; pops in scale on every hit.
    public sealed class ComboView : HudPresenter
    {
        public TMP_Text count;
        [Tooltip("Shown and hidden together with the count (the COMBO label and any backing art).")]
        public GameObject group;
        [Min(1f)] public float popScale = 1.25f;
        [Min(0.01f), Tooltip("Seconds (unscaled) for a pop to settle.")]
        public float popTime = 0.15f;
        [Tooltip("Line under the count; shows the hype text of the highest reached threshold (rich text allowed).")]
        public TMP_Text callout;
        [Tooltip("Ordered by streak, lowest first.")]
        public Hype[] hype = new Hype[0];

        [Serializable]
        public struct Hype
        {
            [Min(1)] public int streak;
            public string text;
        }

        float pop;
        int shown = -1;

        protected override void OnBind() => Show(0);

        void Update()
        {
            if (!Ready) return;
            int value = Context.Score.Combo;
            if (value != shown)
            {
                if (value > shown && value > 0) pop = 1f;
                Show(value);
            }
            if (pop <= 0f) return;
            pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime / popTime);
            count.transform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, pop);
        }

        void Show(int value)
        {
            shown = value;
            bool show = value > 0;
            if (group) group.SetActive(show); else count.enabled = show;
            count.text = value.ToString();
            if (!callout) return;
            string line = "";
            foreach (var h in hype) if (value >= h.streak) line = h.text;
            callout.text = line;
        }
    }
}
