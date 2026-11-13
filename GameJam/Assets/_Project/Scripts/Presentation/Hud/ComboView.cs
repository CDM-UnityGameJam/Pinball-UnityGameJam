using System;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // Hit-streak counter (Context.Streak). Hidden at zero; pops in scale on every hit.
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

        HitStreak streak;
        float pop;

        // GameContext (execution order -100) has created its systems before the HUD binds.
        protected override void OnBind()
        {
            if (Context == null || Context.Streak == null) return;
            streak = Context.Streak;
            streak.Changed += OnChanged;
            Show(streak.Count);
        }

        // Text only changes with the streak; the pop settles every frame.
        void Update()
        {
            if (pop <= 0f) return;
            pop = Mathf.Max(0f, pop - Time.unscaledDeltaTime / popTime);
            count.transform.localScale = Vector3.one * Mathf.Lerp(1f, popScale, pop);
        }

        void OnChanged(int value)
        {
            Show(value);
            if (value > 0) pop = 1f;
        }

        void Show(int value)
        {
            bool show = value > 0;
            if (group) group.SetActive(show); else count.enabled = show;
            count.text = value.ToString();
            if (!callout) return;
            string line = "";
            foreach (var h in hype) if (value >= h.streak) line = h.text;
            callout.text = line;
        }

        void OnDestroy() { if (streak != null) streak.Changed -= OnChanged; }
    }
}
