using TMPro;

namespace Pinball.Presentation
{
    // Score, lives, active time and the running combo.
    public sealed class TopBarView : HudPresenter
    {
        public TMP_Text score, lives, time;
        public TMP_Text combo;

        void Update()
        {
            if (!Ready) return;
            var s = Context.Score;
            score.text = s.Score.ToString("N0");
            lives.text = $"{Context.Run.Lives} / {Context.config.lives}";
            time.text = HudRoot.FormatTime(Context.Clock.Now);
            if (combo) combo.text = s.Combo > 0 ? $"COMBO {s.Combo} · {s.ComboRemaining:0.0}s · Pending +{s.ComboPending}" : "";
        }
    }
}
