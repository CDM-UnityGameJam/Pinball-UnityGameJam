using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // Score, lives and active time.
    public sealed class TopBarView : HudPresenter
    {
        public TMP_Text score, lives, time;

        void Update()
        {
            if (!Ready) return;
            score.text = Context.Score.Score.ToString("N0");
            lives.text = $"{Context.Run.Lives} / {Context.config.lives}";
            time.text = HudRoot.FormatTime(Context.Clock.Now);
        }
    }
}
