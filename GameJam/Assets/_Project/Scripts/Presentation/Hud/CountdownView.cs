using Pinball.Gameplay;
using TMPro;
using UnityEngine;

namespace Pinball.Presentation
{
    // The big launch countdown, and the replacement-ball countdown after a drain ("RELAUNCH").
    // Every new second slams in: it starts large and bright and springs down to size, so the tick is hard to miss.
    public sealed class CountdownView : HudPresenter
    {
        public TMP_Text text;
        public int relaunchCaptionSize = 44;
        [Tooltip("Number colour.")] public Color numberColor = new Color32(0xF3, 0xD0, 0x36, 0xFF);
        [Tooltip("RELAUNCH caption colour.")] public Color captionColor = new Color32(0xCA, 0x50, 0xAC, 0xFF);
        [Tooltip("Colour a new second flashes from.")] public Color flashColor = Color.white;

        [Header("Tick pop")]
        [Min(1f)] public float popScale = 1.6f;
        [Min(0.01f), Tooltip("Seconds (unscaled) for a tick to settle.")] public float popTime = 0.3f;

        int shownSecond = -1;
        bool shownRelaunch;
        float popAge = float.MaxValue;

        public float CurrentScale => text ? text.transform.localScale.x : 1f;

        void Update()
        {
            if (!Ready) return;
            var run = Context.Run;
            bool launching = run.State == RunState.Launching;
            bool relaunch = run.State == RunState.Playing && Context.Relaunch.Pending;
            bool show = (launching || relaunch) && !run.Paused;
            if (text.gameObject.activeSelf != show) text.gameObject.SetActive(show);
            if (!show) { shownSecond = -1; return; }
            int seconds = Mathf.CeilToInt(launching ? run.Countdown : Context.Relaunch.Remaining);
            if (seconds != shownSecond || relaunch != shownRelaunch)
            {
                shownSecond = seconds;
                shownRelaunch = relaunch;
                popAge = 0f;
            }
            Animate(Mathf.Min(Time.unscaledDeltaTime, 0.05f), seconds, relaunch);
        }

        void Animate(float dt, int seconds, bool relaunch)
        {
            popAge += dt;
            float t = Mathf.Clamp01(popAge / popTime);
            float ease = 1f - (1f - t) * (1f - t) * (1f - t); // fast then settling
            text.transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, ease);
            var number = Color.Lerp(flashColor, numberColor, ease);
            string hex = ColorUtility.ToHtmlStringRGB(number);
            text.color = Color.white; // colours come from rich text so the caption and number can differ
            text.text = relaunch
                ? $"<size={relaunchCaptionSize}><color=#{ColorUtility.ToHtmlStringRGB(captionColor)}>RELAUNCH</color></size>\n<color=#{hex}>{seconds}</color>"
                : $"<color=#{hex}>{seconds}</color>";
        }
    }
}
