using UnityEngine;

namespace Pinball.Core
{
    // When the next card is drawn into a hand that isn't full. Port of the old "refill debt" rules:
    // - Playing a card from a full hand starts a fresh countdown of `interval`.
    // - Every play (success or failure) adds `playDelay` to the current deadline; it never restarts it.
    // - Each refill draws one card; the next is due `interval` later while the hand still isn't full.
    // - Losing a life or waiting for launch never clears the debt (the combat clock just stops).
    public sealed class RefillClock
    {
        public float interval;
        public float playDelay;

        public double NextAt { get; private set; } = double.PositiveInfinity;

        public RefillClock(float interval, float playDelay)
        {
            this.interval = interval;
            this.playDelay = playDelay;
        }

        float Interval => Mathf.Max(0.01f, interval);
        float Delay => Mathf.Max(0f, playDelay);

        public double NextEventAt(bool handFull) => handFull ? double.PositiveInfinity : NextAt;
        public bool IsDue(double now, bool handFull) => !handFull && NextAt <= now + RuleMath.TimeEpsilon;
        public float Remaining(double now, bool handFull) => handFull ? 0f : (float)System.Math.Max(0, NextAt - now);

        // What the countdown would read right after playing a card now.
        public float RemainingAfterPlay(double now, bool handFull) => (handFull ? Interval : Remaining(now, false)) + Delay;

        // Call when a card leaves the hand by being played. handWasFull is the state before removal.
        public void OnCardPlayed(double now, bool handWasFull)
        {
            if (handWasFull || double.IsPositiveInfinity(NextAt)) NextAt = now + Interval;
            NextAt += Delay;
        }

        // Call after the refill drew its card.
        public void OnRefilled(double now, bool handNowFull) =>
            NextAt = handNowFull ? double.PositiveInfinity : now + Interval;

        // Call after a card arrived some other way (debug tools, rewards). Keeps a running countdown.
        public void OnCardGranted(double now, bool handNowFull)
        {
            if (handNowFull) NextAt = double.PositiveInfinity;
            else if (double.IsPositiveInfinity(NextAt)) NextAt = now + Interval;
        }

        // Sets the deadline directly, e.g. when a run starts with a partial hand.
        public void SetNext(double at) => NextAt = at;

        public void Reset() => NextAt = double.PositiveInfinity;
    }
}
