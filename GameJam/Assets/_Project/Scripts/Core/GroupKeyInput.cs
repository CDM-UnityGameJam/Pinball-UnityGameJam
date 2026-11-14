namespace Pinball.Core
{
    public enum GroupKeyAction { None, Select, AwaitSecondTap, Cast, Cancel }

    // Number keys for groups (port of the v0.7 GroupKeyInput). The HUD owns the selection; this only decides
    // what a key press means:
    // - A number that isn't selected selects its group (that press never counts as a first tap).
    // - On the selected number, a tap waits DoubleTapSeconds for a second tap: the second tap casts, otherwise
    //   the selection is cancelled when the window runs out (Advance).
    // - Selecting another number, or the selection changing under it, drops a pending tap.
    public sealed class GroupKeyInput
    {
        public readonly double doubleTapSeconds;
        int pending;
        double deadline;

        public GroupKeyInput(double doubleTapSeconds = 0.3) => this.doubleTapSeconds = doubleTapSeconds;

        public bool HasPendingTap => pending != 0;

        public void Reset()
        {
            pending = 0;
            deadline = 0;
        }

        // Call every frame with the current selection (null = none).
        public GroupKeyAction Advance(double now, int? selected)
        {
            if (pending == 0) return GroupKeyAction.None;
            if (selected != pending) { Reset(); return GroupKeyAction.None; }
            if (now <= deadline) return GroupKeyAction.None;
            Reset();
            return GroupKeyAction.Cancel;
        }

        public GroupKeyAction Press(int number, int? selected, double now)
        {
            if (number < 1) return GroupKeyAction.None;
            if (selected != number) { Reset(); return GroupKeyAction.Select; }
            if (pending == number && now <= deadline) { Reset(); return GroupKeyAction.Cast; }
            pending = number;
            deadline = now + doubleTapSeconds;
            return GroupKeyAction.AwaitSecondTap;
        }
    }
}
