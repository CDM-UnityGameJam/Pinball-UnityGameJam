using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // Flashes a coloured vignette around the screen edges when a life is lost, then fades it out.
    // A last life (game over) flashes harder and holds a faint red edge behind the dialog.
    // Runs on unscaled time: the game freezes time at game over.
    public sealed class LifeLostVignetteView : HudPresenter
    {
        [Required, Tooltip("Full-screen Graphic (the vignette texture); its alpha is driven here.")]
        public Graphic vignette;
        [Tooltip("Defaults to the theme's danger colour when alpha is 0.")]
        public Color color = new Color(0, 0, 0, 0);

        [Title("Flash")]
        [MinValue(0), SuffixLabel("s")] public float rise = 0.06f;
        [MinValue(0.01), SuffixLabel("s")] public float fade = 1.1f;
        [Range(0, 1)] public float peak = 0.85f;
        [Range(0, 1), Tooltip("Peak on the life that ends the run.")] public float gameOverPeak = 1f;
        [Range(0, 1), Tooltip("Alpha the vignette keeps after the run ended.")] public float gameOverHold = 0.35f;

        int lives = -1;
        float age = float.MaxValue, flashPeak, hold;

        public bool Showing => vignette && vignette.enabled;

        protected override void OnBind()
        {
            if (!Context) return;
            lives = Context.Run.Lives;
            Context.Run.LivesChanged += OnLivesChanged;
            Apply(0f);
        }

        void OnDestroy()
        {
            if (Ready) Context.Run.LivesChanged -= OnLivesChanged;
        }

        void OnLivesChanged(int now)
        {
            if (now < lives) Flash(now <= 0);
            lives = now;
        }

        // Public so tests and tools can trigger it.
        public void Flash(bool gameOver = false)
        {
            age = 0f;
            flashPeak = gameOver ? gameOverPeak : peak;
            hold = gameOver ? gameOverHold : 0f;
        }

        void Update()
        {
            if (age == float.MaxValue) return;
            age += Time.unscaledDeltaTime;
            float a;
            if (age < rise) a = flashPeak * (rise <= 0f ? 1f : age / rise);
            else
            {
                float t = Mathf.Clamp01((age - rise) / fade);
                a = Mathf.Lerp(flashPeak, hold, 1f - (1f - t) * (1f - t)); // ease out
                if (t >= 1f && hold <= 0f) age = float.MaxValue;
            }
            Apply(a);
        }

        void Apply(float alpha)
        {
            if (!vignette) return;
            var c = color.a > 0f ? color : (Theme ? Theme.danger : Color.red);
            c.a = alpha;
            vignette.color = c;
            vignette.enabled = alpha > 0.001f;
        }
    }
}
