using System.Collections.Generic;
using Pinball.Core;
using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The big launch countdown.
    public sealed class CountdownView : HudPresenter
    {
        public TMP_Text text;

        void Update()
        {
            if (!Ready) return;
            bool show = Context.Run.State == RunState.Launching && !Context.Run.Paused;
            if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
            if (show) text.text = Mathf.CeilToInt(Context.Run.Countdown).ToString();
        }
    }
}
