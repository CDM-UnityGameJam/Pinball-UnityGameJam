using System;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The running scoring combo (ScoreSystem.Combo: counted ball hits, ends after the combo timeout and pays
    // its tier). Hidden at zero. Every hit pops the count and kicks a little wobble; reaching a new hype
    // threshold bursts bigger and springs the callout in.
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

        [Header("Wobble and burst")]
        [Min(0), Tooltip("Degrees of tilt each hit kicks, alternating sides.")] public float wobbleDegrees = 9f;
        [Min(1)] public float wobbleStiffness = 260f;
        [Min(0)] public float wobbleDamping = 9f;
        [Min(1f), Tooltip("Pop size when a new hype threshold is reached.")] public float burstScale = 1.6f;
        [Min(0.01f), Tooltip("Seconds for the callout to spring in.")] public float calloutPopTime = 0.25f;

        [Serializable]
        public struct Hype
        {
            [Min(1)] public int streak;
            public string text;
        }

        float pop, popPeak = 1.25f;
        float tilt, tiltVel, side = 1f;
        float calloutAge = float.MaxValue;
        int shown = -1, shownTier = -1;

        protected override void OnBind() => Show(0);

        void Update()
        {
            if (!Ready) return;
            int value = Context.Score.Combo;
            if (value != shown)
            {
                if (value > shown && value > 0) Hit(TierOf(value) > shownTier);
                Show(value);
            }
            Animate(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        }

        void Hit(bool newTier)
        {
            pop = 1f;
            popPeak = newTier ? burstScale : popScale;
            side = -side;
            tiltVel += side * wobbleDegrees * Mathf.Sqrt(wobbleStiffness) * (newTier ? 2f : 1.2f);
            if (newTier) calloutAge = 0f;
        }

        void Animate(float dt)
        {
            pop = Mathf.Max(0f, pop - dt / popTime);
            count.transform.localScale = Vector3.one * Mathf.Lerp(1f, popPeak, pop * pop);
            tiltVel += (-tilt * wobbleStiffness - tiltVel * wobbleDamping) * dt;
            tilt += tiltVel * dt;
            count.transform.localEulerAngles = new Vector3(0f, 0f, tilt);

            if (!callout) return;
            if (calloutAge < calloutPopTime)
            {
                calloutAge += dt;
                float t = Mathf.Clamp01(calloutAge / calloutPopTime);
                // Back-out ease: overshoots to about 1.2, then settles at 1.
                const float s = 2.2f;
                float u = t - 1f;
                callout.transform.localScale = Vector3.one * (1f + (s + 1f) * u * u * u + s * u * u);
            }
            else callout.transform.localScale = Vector3.one;
        }

        int TierOf(int value)
        {
            int tier = -1;
            for (int i = 0; i < hype.Length; i++) if (value >= hype[i].streak) tier = i;
            return tier;
        }

        void Show(int value)
        {
            shown = value;
            shownTier = TierOf(value);
            bool show = value > 0;
            if (group) group.SetActive(show); else count.enabled = show;
            count.text = value.ToString();
            if (!show) { tilt = tiltVel = 0f; pop = 0f; }
            if (!callout) return;
            callout.text = shownTier >= 0 ? hype[shownTier].text : "";
        }
    }
}
