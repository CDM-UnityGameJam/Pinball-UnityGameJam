using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // Score banner and lives. Lives show as hearts when `hearts` is set (one Image per life), else as text.
    // `time` is optional.
    public sealed class TopBarView : HudPresenter
    {
        public TMP_Text score, lives, time;
        public UnityEngine.UI.Image[] hearts = new UnityEngine.UI.Image[0];
        public Color heart = new Color(1f, .3f, .55f);
        [Tooltip("Tint of a lost life's heart.")]
        public Color lostHeart = new Color(1f, 1f, 1f, .25f);

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

        void Update()
        {
            if (!Ready) return;
            score.text = Context.Score.Score.ToString("N0");
            if (lives) lives.text = $"{Context.Run.Lives} / {Context.config.lives}";
            if (time) time.text = HudRoot.FormatTime(Context.Clock.Now);
            for (int i = 0; i < hearts.Length; i++)
                if (hearts[i]) hearts[i].color = i < Context.Run.Lives ? heart : lostHeart;
        }
    }
}
