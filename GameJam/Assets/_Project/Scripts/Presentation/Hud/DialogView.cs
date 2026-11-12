using Pinball.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Pinball.Presentation
{
    // Pause ("Restart this run?") and game-over dialog.
    public sealed class DialogView : HudPresenter
    {
        public GameObject panel;
        public TMP_Text title;
        public Button confirm;
        public TMP_Text confirmLabel;
        public Button cancel;
        [Tooltip("Confirm button X when it shares the row with Cancel (pause).")]
        public float confirmXWithCancel = -110f;
        public int detailsFontSize = 28;

        protected override void OnBind()
        {
            confirm.onClick.AddListener(() => Context.Restart());
            cancel.onClick.AddListener(() => Context.Run.SetPaused(false));
        }

        void Update()
        {
            if (!Ready) return;
            var run = Context.Run;
            bool over = run.State == RunState.GameOver, pause = run.Paused && !over;
            if (panel.activeSelf != (over || pause)) panel.SetActive(over || pause);
            if (!over && !pause) return;

            string dim = UiTheme.Hex(Theme.dim);
            title.text = over
                ? $"GAME OVER\n<size={detailsFontSize}><color=#{dim}>Score </color>{Context.Score.Score:N0}<color=#{dim}>     Active time </color>{HudRoot.FormatTime(Context.Clock.Now)}</size>"
                : "PAUSED\nRestart this run?";
            cancel.gameObject.SetActive(pause);
            confirmLabel.text = over ? "Restart" : "Yes";
            var rect = (RectTransform)confirm.transform;
            rect.anchoredPosition = new Vector2(over ? 0f : confirmXWithCancel, rect.anchoredPosition.y);
        }
    }
}
