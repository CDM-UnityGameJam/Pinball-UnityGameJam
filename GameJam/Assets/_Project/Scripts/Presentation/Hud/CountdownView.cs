using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The big launch countdown, and the replacement-ball countdown after a drain ("RELAUNCH").
    public sealed class CountdownView : HudPresenter
    {
        public TMP_Text text;
        public int relaunchCaptionSize = 24;

        void Update()
        {
            if (!Ready) return;
            var run = Context.Run;
            bool launching = run.State == RunState.Launching;
            bool relaunch = run.State == RunState.Playing && Context.Relaunch.Pending;
            bool show = (launching || relaunch) && !run.Paused;
            if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
            if (!show) return;
            int seconds = Mathf.CeilToInt(launching ? run.Countdown : Context.Relaunch.Remaining);
            text.text = relaunch ? $"<size={relaunchCaptionSize}>RELAUNCH</size>\n{seconds}" : seconds.ToString();
        }
    }
}
