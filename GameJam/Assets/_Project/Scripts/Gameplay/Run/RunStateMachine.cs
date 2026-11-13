using System;
using UnityEngine;

namespace Pinball.Gameplay
{
    public enum RunState
    {
        Launching, // Countdown before the first ball launches. (After a drain the run keeps playing; see GameContext.)
        Playing,
        GameOver,
    }

    // Run flow and lives. Port of the old GameManager state handling:
    // - Pause is a separate flag: you can pause during the launch countdown too, and it freezes it.
    // - Lives are only lost while playing (not paused, not counting down). LivesLost runs before the run can end,
    //   so its handlers (clear the field, pay the combo) still see a playing run.
    // - Reaching zero lives ends the run; nothing restarts it except a scene reload.
    public sealed class RunStateMachine
    {
        readonly float countdownSeconds;

        public RunState State { get; private set; } = RunState.Launching;
        public bool Paused { get; private set; }
        public int Lives { get; private set; }
        public float Countdown { get; private set; }
        public string EndCause { get; private set; }

        // Gameplay time runs (combat clock, physics) only in this state.
        public bool IsPlaying => State == RunState.Playing && !Paused;

        public event Action<RunState> StateChanged;
        public event Action<bool> PauseChanged;
        public event Action<int> LivesChanged;
        // (amount) after lives went down, before a game over is decided.
        public event Action<int> LivesLost;
        // The countdown finished: launch the waiting ball.
        public event Action LaunchDue;

        public RunStateMachine(int lives, float countdownSeconds)
        {
            Lives = Mathf.Max(1, lives);
            this.countdownSeconds = Mathf.Max(0f, countdownSeconds);
            Countdown = this.countdownSeconds;
        }

        // Start (or restart after a drain) the launch countdown.
        public void BeginLaunch()
        {
            if (State == RunState.GameOver) return;
            Countdown = countdownSeconds;
            Set(RunState.Launching);
        }

        // Advance the countdown with real (unscaled) time.
        public void Tick(float unscaledDeltaTime)
        {
            if (Paused || State != RunState.Launching) return;
            Countdown -= Mathf.Max(0f, unscaledDeltaTime);
            if (Countdown > 0f) return;
            Countdown = 0f;
            Set(RunState.Playing);
            LaunchDue?.Invoke();
        }

        public void SetPaused(bool paused)
        {
            if (State == RunState.GameOver || paused == Paused) return;
            Paused = paused;
            PauseChanged?.Invoke(paused);
        }

        public void TogglePause() => SetPaused(!Paused);

        public void LoseLives(int amount, string cause)
        {
            if (!IsPlaying) return;
            amount = Mathf.Max(0, amount);
            if (amount == 0) return;
            Lives = Mathf.Max(0, Lives - amount);
            LivesChanged?.Invoke(Lives);
            LivesLost?.Invoke(amount);
            if (Lives > 0) return;
            EndCause = cause;
            Set(RunState.GameOver);
        }

        void Set(RunState state)
        {
            if (State == state) return;
            State = state;
            StateChanged?.Invoke(state);
        }
    }
}
