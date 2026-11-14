using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // Score banner, lives and (optionally) a text combo line. Lives show as hearts when `hearts` is set (one Image per life), else as text.
    // `time` is optional. The score rolls up to its new value and punches (bigger for bigger gains) with a gold flash.
    public sealed class TopBarView : HudPresenter
    {
        public TMP_Text score, lives, time;
        [Tooltip("Optional text combo line (the art HUD uses ComboView instead).")]
        public TMP_Text combo;
        public UnityEngine.UI.Image[] hearts = new UnityEngine.UI.Image[0];
        public Color heart = new Color(1f, .3f, .55f);
        [Tooltip("Tint of a lost life's heart.")]
        public Color lostHeart = new Color(1f, 1f, 1f, .25f);

        [Header("Score roll and punch")]
        [Min(0.01f), Tooltip("Seconds (unscaled) for the shown score to catch up with the real one.")]
        public float rollTime = 0.35f;
        [Range(0, 1), Tooltip("Extra size at the peak of a punch for the largest gains.")] public float punchMax = 0.35f;
        [Range(0, 1), Tooltip("Punch for the smallest gain (a time reward).")] public float punchMin = 0.08f;
        [Min(1), Tooltip("Gain that gives the full Punch Max.")] public float punchFullGain = 500f;
        [Tooltip("Colour the score flashes to on a gain (its own colour otherwise).")] public Color flashColor = Color.white;
        [Min(1)] public float punchStiffness = 380f;
        [Min(0)] public float punchDamping = 16f;

        double shownScore;
        long lastScore;
        float punch, punchVel, flash;
        Color scoreColor;
        bool scoreBound;

        // One heart per life: clone the last heart or hide surplus ones to match GameConfig.lives.
        // The Hearts row's HorizontalLayoutGroup (Hud.prefab) positions and centres them.
        protected override void OnBind()
        {
            if (Context == null || Context.config == null || hearts.Length == 0) return;
            int lives = Context.config.lives;
            var list = new List<UnityEngine.UI.Image>(hearts);
            var last = hearts[hearts.Length - 1];
            while (list.Count < lives) list.Add(Instantiate(last, last.transform.parent));
            for (int i = 0; i < list.Count; i++) list[i].gameObject.SetActive(i < lives);
            hearts = list.ToArray();
        }

        void UpdateScore(long value)
        {
            if (!scoreBound) { scoreBound = true; scoreColor = score.color; shownScore = lastScore = value; }
            if (value > lastScore)
            {
                long gain = value - lastScore;
                float strength = Mathf.Lerp(punchMin, punchMax, Mathf.Clamp01(gain / punchFullGain));
                punchVel = Mathf.Max(punchVel, strength * Mathf.Sqrt(punchStiffness) * 1.6f);
                flash = 1f;
            }
            else if (value < lastScore) shownScore = value; // restart: snap down
            lastScore = value;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            // Exponential catch-up, always at least one point per frame so it lands exactly.
            double step = (value - shownScore) * (1.0 - System.Math.Exp(-dt * 4.0 / rollTime));
            shownScore = value - shownScore <= 1.0 ? value : shownScore + System.Math.Max(1.0, step);
            score.text = ((long)shownScore).ToString("N0");

            punchVel += (-punch * punchStiffness - punchVel * punchDamping) * dt;
            punch += punchVel * dt;
            if (Mathf.Abs(punch) < 1e-4f && Mathf.Abs(punchVel) < 1e-3f) punch = punchVel = 0f;
            score.transform.localScale = Vector3.one * (1f + punch);
            flash = Mathf.Max(0f, flash - dt / 0.25f);
            score.color = Color.Lerp(scoreColor, flashColor, flash * 0.8f);
        }

        void Update()
        {
            if (!Ready) return;
            var s = Context.Score;
            UpdateScore(s.Score);
            if (combo) combo.text = s.Combo > 0 ? $"COMBO {s.Combo} · {s.ComboRemaining:0.0}s · Pending +{s.ComboPending}" : "";
            if (lives) lives.text = $"{Context.Run.Lives} / {Context.config.lives}";
            if (time) time.text = HudRoot.FormatTime(Context.Clock.Now);
            for (int i = 0; i < hearts.Length; i++)
                if (hearts[i]) hearts[i].color = i < Context.Run.Lives ? heart : lostHeart;
        }
    }
}
